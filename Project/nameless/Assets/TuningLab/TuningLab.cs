using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 手感调试台。一个 GameObject 建出整个世界：地面、楼梯、两队方块、相机、HUD。
/// 所有可调参数都在这里，运行时改（HUD 滑块）立刻生效。
/// 场景由菜单 GameJam → Build Tuning Lab 生成；也可以把这个组件拖进任意空场景直接 Play。
/// </summary>
[DisallowMultipleComponent]
public class TuningLab : MonoBehaviour
{
    [Header("时间")]
    [Tooltip("时间缩放，仅调试用。也可以在 Play 中直接改 Inspector，HUD 滑块与它是同一个值。")]
    [Range(0.05f, 2f)] public float timeScale = 1f;

    [Header("场地")]
    [Tooltip("地面和墙壁的长度倍数。1 = 原尺寸，4 = 放大四倍。台阶位置不变。")]
    public float roomScale = 1f;
    [Tooltip("打开则改用自由相机（跟随两人中点并按距离缩放）；像素完美相机会关掉。")]
    public bool freeCamera = false;

    [Header("重力（单位：格/秒²）")]
    [Tooltip("改它只改手感，不改任何等级门槛——门槛只认格。")]
    public float gravity = 50f;

    [Header("跳跃")]
    [Tooltip("跳高（格）。必须 >= 台阶高差，同时应当小于第一级门槛，否则站跳就充能。")]
    public float jumpCells = 2.2f;
    [Tooltip("开启后按住跳跃键会落地即跳（连跳）。关闭则必须每次都重新按。")]
    public bool holdToJump = false;
    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.08f;

    [Header("地面移动")]
    public float walkSpeed = 8f;
    public float walkAccel = 140f;
    [Tooltip("松手后的地面制动。调小 = 更滑，等级能保留更久。")]
    public float groundBrake = 30f;

    [Header("空中")]
    [Tooltip("0 = 空中完全保留动量，等级可以一直保持着飞。")]
    public float airBrake = 0f;
    [Tooltip("水平空气制动，大小 = 重力加速度 g（与竖直方向对称）。竖直没有额外制动，由重力负责。")]
    public bool horizontalAirDrag = true;

    [Header("冲刺")]
    [Tooltip("冲刺态的位移（格）。恒定值，与冲刺速度无关：时长 = 位移 / 速度。")]
    public float dashDistanceCells = 4f;
    [Tooltip("Vd1 = 比 v0 高出的落差（格）。\"反向进入\"和\"零级进入\"时用这个速度。")]
    public float dashOverV0Cells = 0.05f;
    [Tooltip("冲刺结束后锁输入的时间（秒）。")]
    public float dashLockTime = 0f;
    [Tooltip("充能从 0 充满所需时间。站在地面或碰到队友时才走，贴墙不算。")]
    public float dashCooldown = 0.8f;
    public bool groundOnlyDash = false;

    [Header("撞击")]
    [Tooltip("红热判定的门槛取哪一级的边界（1 = v1/v2 边界）。Vp ≥ 该边界的 v² 就红热。")]
    [Range(0, 4)] public int redHotTierIndex = 1;
    [Tooltip("红热态 1 持续多久回到常态。红热态 2 永久。")]
    public float redHotDuration = 1.5f;
    public bool verticalHitEnabled = true;

    [Header("等级（等距）")]
    [Tooltip("第一道门槛的落差（格）= 零级的最大速度。必须大于跳高，否则站跳就越级。")]
    public float tierFirstCells = 4f;
    [Tooltip("每级之间的落差（格）。4 = 4/8/12/16 格。")]
    public float tierSpacingCells = 4f;
    [Tooltip("等级数（不含零级）。4 → 零级..四级。")]
    [Range(1, 6)] public int tierCount = 4;

    [Header("上限")]
    [Tooltip("要能掉出最高一级：maxFall ≥ √(2·g·最高门槛)。g=50、门槛 16 格时需要 40。")]
    public float maxFallSpeed = 42f;

    // 1 格 = 16 px，原生 384×216 = 24 × 13.5 格。改这个等于重做所有关卡尺寸，别乱动。
    public const int CellPixels = 16;
    public const int RefWidth = 384;
    public const int RefHeight = 216;

    /// <summary>A（大框）：14×14，墙厚 2，内腔 10。留 1 像素余量才能进 1 格宽的通道。</summary>
    public const int A_Pixels = 14;
    /// <summary>B（小块）：10×10，正好填满 A 的内腔（14 - 2×2）。</summary>
    public const int B_Pixels = 10;

    [Header("像素")]
    [Tooltip("P 键切换：叠一层 1 像素的棋盘，用来看最终的像素密度。")]
    public bool showPixelGrid = false;

    public LabPlayer PlayerA { get; private set; }
    public LabPlayer PlayerB { get; private set; }
    public Camera Cam { get; private set; }
    public LabHud Hud { get; private set; }

    // ---- 派生量：所有"速度"都由格算出来，改重力自动跟着走 ----
    public float FixedDt => Time.fixedDeltaTime;
    public float JumpVelocity => TierMath.JumpVelocity(jumpCells, gravity, Time.fixedDeltaTime);
    /// <summary>零级的速度上限，也就是 v0。</summary>
    public float V0Speed => TierMath.SpeedForCells(Boundary(0), gravity);
    /// <summary>Vd1：略大于 v0 的冲刺速度（反向/零级进入时用）。</summary>
    public float Vd1 => TierMath.SpeedForCells(Boundary(0) + Mathf.Max(0.01f, dashOverV0Cells), gravity);
    /// <summary>冲刺态的位移（格），恒定。</summary>
    public float DashDistance => Mathf.Max(0f, dashDistanceCells);

    // ---- 等级门槛：等距生成，不再手填数组 ----
    float[] cachedBounds;

    /// <summary>第 i 项 = 第 i 级与第 i+1 级之间的落差门槛（格）。</summary>
    public float[] Bounds
    {
        get
        {
            float spacing = Mathf.Max(0.01f, tierSpacingCells);
            int n = Mathf.Clamp(tierCount, 1, 8);

            bool stale = cachedBounds == null || cachedBounds.Length != n;
            if (!stale)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!Mathf.Approximately(cachedBounds[i], tierFirstCells + i * spacing))
                    {
                        stale = true;
                        break;
                    }
                }
            }

            if (stale)
            {
                cachedBounds = new float[n];
                for (int i = 0; i < n; i++)
                    cachedBounds[i] = tierFirstCells + i * spacing;
            }
            return cachedBounds;
        }
    }

    public int MaxTier => Bounds.Length;
    public float Boundary(int index) => index >= 0 && index < Bounds.Length ? Bounds[index] : 0f;

    /// <summary>Rigidbody2D.gravityScale 的换算（Physics2D.gravity 固定 -9.81）。</summary>
    public float GravityScale => gravity / 9.81f;

    public float SpeedForCells(float cells) => TierMath.SpeedForCells(cells, gravity);
    public float CellsForSpeed(float speed) => TierMath.CellsForSpeed(speed, gravity);
    public int TierOfSpeed(float speed) => TierMath.TierForCells(CellsForSpeed(speed), Bounds);
    public int TierOfCells(float cells) => TierMath.TierForCells(cells, Bounds);

    // ---- 双人碰撞事件：其它机制挂这里 ----
    public struct HitInfo
    {
        public LabPlayer hitter;
        public LabPlayer victim;
        public Vector2 normal;      // 撞击者 → 被撞者
        public bool horizontal;
        public int tierBefore;
        public int tierAfter;
        public float speed;         // 加速逻辑之后，快的那一方的新速度
        public float vp;            // 红热判定用的 Vp（速度平方的和/差）
        public bool redHot;
    }

    public event System.Action<HitInfo> PlayerHit;
    public void RaisePlayerHit(HitInfo info) { if (PlayerHit != null) PlayerHit(info); }

    // ---- 像素读数 ----
    /// <summary>当前屏幕上竖向可见多少格。</summary>
    public float VisibleCellsY => Cam != null ? Cam.orthographicSize * 2f : 0f;
    public float VisibleCellsX => Cam != null ? VisibleCellsY * Cam.aspect : 0f;
    /// <summary>一个美术像素在屏幕上占几个屏幕像素。是整数才对得齐。</summary>
    public float TexelScale
    {
        get
        {
            float h = VisibleCellsY * CellPixels;
            return h > 0.01f ? Screen.height / h : 0f;
        }
    }

    static Sprite whiteSprite;
    PhysicsMaterial2D slick;
    GameObject pixelGridGo;

    static readonly Color SolidColor = new Color(0.24f, 0.26f, 0.32f);
    static readonly Color GridColor = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color ColorA = new Color(0.36f, 0.62f, 1f);
    static readonly Color ColorB = new Color(1f, 0.45f, 0.4f);

    void Awake()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Time.timeScale = timeScale;
        BuildRoom();
        BuildPlayers();
        BuildCamera();
        BuildPixelGrid();
        Hud = gameObject.AddComponent<LabHud>();
        Hud.Bind(this);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.rKey.wasPressedThisFrame) ResetPlayers();
        if (kb.pKey.wasPressedThisFrame) showPixelGrid = !showPixelGrid;
    }

    void LateUpdate()
    {
        if (pixelGridGo == null || Cam == null) return;
        pixelGridGo.SetActive(showPixelGrid);
        float h = Cam.orthographicSize * 2f;
        float w = h * Cam.aspect;
        pixelGridGo.transform.position = Cam.transform.position;
        pixelGridGo.transform.localScale = new Vector3(w / RefWidth, h / RefHeight, 1f);
    }

    public void ResetPlayers()
    {
        if (PlayerA != null) PlayerA.Teleport(new Vector2(8f, PlayerA.HalfHeight));
        if (PlayerB != null) PlayerB.Teleport(new Vector2(10f, PlayerB.HalfHeight));
    }

    // ------------------------------------------------------------------
    // 世界搭建
    // ------------------------------------------------------------------

    void BuildRoom()
    {
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        // 地板：上表面 y = 0
        AddSolid("Floor", new Vector2(0f, -0.5f), new Vector2(26f * roomScale, 1f));

        // 楼梯：每级高 2 格、宽 3 格，从 y=2 爬到 y=10。往右走每掉一级就是 2 格落差。
        AddSolid("Step_y02", new Vector2(4.5f, 1.5f), new Vector2(3f, 1f));
        AddSolid("Step_y04", new Vector2(1.5f, 3.5f), new Vector2(3f, 1f));
        AddSolid("Step_y06", new Vector2(-1.5f, 5.5f), new Vector2(3f, 1f));
        AddSolid("Step_y08", new Vector2(-4.5f, 7.5f), new Vector2(3f, 1f));
        AddSolid("Step_y10", new Vector2(-7.5f, 9.5f), new Vector2(3f, 1f));

        // 侧墙
        AddSolid("WallL", new Vector2(-13.5f * roomScale, 8f * roomScale), new Vector2(1f, 16f * roomScale));
        AddSolid("WallR", new Vector2(13.5f * roomScale, 8f * roomScale), new Vector2(1f, 16f * roomScale));

        BuildGrid();
    }

    void BuildGrid()
    {
        var root = new GameObject("Grid").transform;
        root.SetParent(transform, false);

        // 1 个美术像素的粗细，这样网格本身就是"像素密度"的尺子
        float thickness = 1f / CellPixels;

        for (int x = -12; x <= 12; x++)
            AddLine(root, "V" + x, new Vector2(x, 5f), new Vector2(thickness, 14f));
        for (int y = -1; y <= 11; y++)
            AddLine(root, "H" + y, new Vector2(0f, y), new Vector2(24f, thickness));
    }

    /// <summary>
    /// 一层 1 像素的棋盘，按原生分辨率生成，贴满视口。
    /// P 键开关：用来确认"一格到底是多大一块像素"。
    /// </summary>
    void BuildPixelGrid()
    {
        var tex = new Texture2D(RefWidth, RefHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[RefWidth * RefHeight];
        for (int y = 0; y < RefHeight; y++)
            for (int x = 0; x < RefWidth; x++)
                px[y * RefWidth + x] = ((x + y) & 1) == 0
                    ? new Color32(255, 255, 255, 26)
                    : new Color32(255, 255, 255, 8);

        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, RefWidth, RefHeight), new Vector2(0.5f, 0.5f), 1f);

        pixelGridGo = new GameObject("PixelGrid");
        pixelGridGo.transform.SetParent(transform, false);
        var sr = pixelGridGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5000;
        pixelGridGo.SetActive(showPixelGrid);
    }

    void AddLine(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = White;
        sr.color = GridColor;
        sr.sortingOrder = -10;
    }

    GameObject AddSolid(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = White;
        sr.color = SolidColor;
        sr.sortingOrder = 0;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        bc.sharedMaterial = slick;
        return go;
    }

    void BuildPlayers()
    {
        PlayerA = MakePlayer("A_Box", new Vector2(8f, A_Pixels / (2f * CellPixels)),
                             ColorA, LabPlayer.Scheme.P1, A_Pixels, 2);
        PlayerB = MakePlayer("B_Chunk", new Vector2(10f, B_Pixels / (2f * CellPixels)),
                             ColorB, LabPlayer.Scheme.P2, B_Pixels, 0);
        PlayerA.canBeRedHot = true;   // 红热只在 A 判定
        PlayerA.other = PlayerB;
        PlayerB.other = PlayerA;
    }

    LabPlayer MakePlayer(string name, Vector2 position, Color color, LabPlayer.Scheme scheme,
                         int pixels, int wall)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        var bc = go.AddComponent<BoxCollider2D>();
        float units = pixels / (float)CellPixels;
        bc.size = new Vector2(units, units);       // 碰撞箱 = 精灵尺寸，一点不让
        bc.sharedMaterial = slick;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = GravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var p = go.AddComponent<LabPlayer>();
        p.lab = this;
        p.scheme = scheme;
        p.pixels = pixels;
        p.visual = LabVisual.Build(go.transform, pixels, wall > 0, color);
        return p;
    }

    static readonly System.Collections.Generic.Dictionary<long, Sprite> squareCache =
        new System.Collections.Generic.Dictionary<long, Sprite>();

    /// <summary>生成 N×N 的方块精灵；wall &gt; 0 时是空心的框（A），wall = 0 时是实心块（B）。</summary>
    static Sprite SquareSprite(int pixels, int wall)
    {
        long key = ((long)pixels << 8) | (uint)wall;
        if (squareCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[pixels * pixels];
        for (int y = 0; y < pixels; y++)
            for (int x = 0; x < pixels; x++)
            {
                bool border = wall > 0 && (x < wall || y < wall || x >= pixels - wall || y >= pixels - wall);
                px[y * pixels + x] = (wall == 0 || border)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

        tex.SetPixels32(px);
        tex.Apply();

        // PPU = 每格像素数 → 精灵的世界尺寸自动就是 pixels/16 格
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), CellPixels);
        squareCache[key] = sprite;
        return sprite;
    }

    void BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = RefHeight / (2f * CellPixels);   // 216 / 32 = 6.75 → 可见 13.5 格
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        cam.transform.position = new Vector3(0f, 5f, -10f);
        go.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData(); // URP 项目里必须有，否则相机不渲染

        if (freeCamera)
        {
            // 自由相机：跟随两人中点、按距离缩放
            var follow = go.AddComponent<LabCamera>();
            follow.minSize = RefHeight / (2f * CellPixels);
            follow.maxSize = follow.minSize * 4f;
            follow.Configure(PlayerA.transform, PlayerB.transform, RoomBounds());
        }
        // 固定视角：正交尺寸已在上方设为 13.5 格，不用再加组件

        Cam = cam;
    }

    /// <summary>房间的世界范围，给自由相机做边界限制。</summary>
    Rect RoomBounds()
    {
        float halfW = 13f * roomScale;
        float height = 16f * roomScale;
        return Rect.MinMaxRect(-halfW, -1f, halfW, height);
    }

    static Sprite White
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return whiteSprite;
        }
    }
}
