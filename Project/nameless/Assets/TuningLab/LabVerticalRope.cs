using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竖绳。每一格是居中的 6×16 棕色素条。
///
/// 碰撞规则（挡位按碰撞前沿接触法线撞进去的分量算）：
///   零级 / 一级  →  当墙，挡死
///   二级         →  沿法线穿过：只判一次，恢复入射速度，忽略碰撞直到离开绳体
///   三级及以上   →  沿法线弹回，弹回速度 = 入射速度 + 一个等级间距（和横绳一致）
///
/// 绳接触不参与地形锻造；只有弹回这一种情况会按红热撞击结算边态。
///
/// 相邻的竖绳可以用 chainId 归成一个"长绳"，长绳逻辑通过 <see cref="Contacted"/> 事件接入。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LabVerticalRope : MonoBehaviour
{
    public const int CellPixels = 16;      // 一格 16 像素
    public const int StripPixels = 6;      // 素条宽 6 像素

    [Tooltip("绳长（格）。")]
    public int cells = 4;

    [Tooltip("长绳编号：同一个编号的多段绳视为一条长绳。")]
    public int chainId;

    public Color color = new Color(0.55f, 0.38f, 0.22f);

    [Tooltip("二级穿过时至少忽略碰撞的时长（秒）。玩家还没离开绳体时会继续忽略，避免卡在绳里。")]
    public float passWindow = 0.22f;

    /// <summary>所有竖绳，供长绳逻辑统一查询。</summary>
    public static readonly List<LabVerticalRope> All = new List<LabVerticalRope>();

    /// <summary>一次绳接触。长绳 / 红热 / 音效都挂这个，不要各写一套。</summary>
    public struct RopeContact
    {
        public LabVerticalRope rope;
        public LabPlayer player;
        public int tier;
        public Vector2 normal;     // 绳 → 玩家
        public bool passed;        // true = 穿过；false = 被挡住或弹回
        public bool bounced;
    }

    public event System.Action<RopeContact> Contacted;

    BoxCollider2D body;
    TuningLab lab;
    readonly List<Collider2D> passThrough = new List<Collider2D>();
    readonly List<float> passUntil = new List<float>();

    void Awake()
    {
        body = GetComponent<BoxCollider2D>();
    }

    void OnEnable()
    {
        // 静态表只服务 Play 时的长绳逻辑；编辑器预览不登记，避免残留。
        if (Application.isPlaying) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        ReleasePassThrough();
    }

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
        ApplyShape();
    }

    /// <summary>
    /// 把 cells 应用到碰撞箱和视觉。编辑器的关卡预览也会直接调它，
    /// 所以不依赖 Start；重复调用只复用/增删 Strip，不会叠出两层。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        if (body != null)
        {
            body.size = new Vector2(StripPixels / (float)CellPixels, cells);
            body.offset = Vector2.zero;
        }
        BuildVisual();
    }

    void Update()
    {
        // 穿过窗口到期、并且玩家已经离开绳体之后，才把碰撞恢复回来。
        // 忽略期间不会再次触发 OnCollisionEnter2D，所以一次穿过只判一次，也不会被反复弹回/卡住。
        for (int i = passThrough.Count - 1; i >= 0; i--)
        {
            Collider2D other = passThrough[i];
            if (other == null)
            {
                passThrough.RemoveAt(i);
                passUntil.RemoveAt(i);
                continue;
            }

            if (Time.time < passUntil[i]) continue;
            if (Overlaps(other)) continue;

            Physics2D.IgnoreCollision(other, body, false);
            passThrough.RemoveAt(i);
            passUntil.RemoveAt(i);
        }
    }

    /// <summary>玩家是否还压在绳体上。忽略碰撞期间没有 contact，只能靠包围盒判断。</summary>
    bool Overlaps(Collider2D other)
    {
        if (body == null || !other.enabled || !other.gameObject.activeInHierarchy) return false;

        Bounds a = body.bounds;
        Bounds b = other.bounds;
        const float margin = 0.01f;
        return a.min.x < b.max.x - margin && a.max.x > b.min.x + margin
            && a.min.y < b.max.y - margin && a.max.y > b.min.y + margin;
    }

    void ReleasePassThrough()
    {
        if (body != null)
        {
            for (int i = 0; i < passThrough.Count; i++)
                if (passThrough[i] != null) Physics2D.IgnoreCollision(passThrough[i], body, false);
        }

        passThrough.Clear();
        passUntil.Clear();
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out LabPlayer player)) return;
        Resolve(player, c);
    }

    void Resolve(LabPlayer player, Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider == null || body == null) return;

        Vector2 normal = ContactNormal(player, collision);
        float impact = Mathf.Abs(Vector2.Dot(player.PreImpactVelocity, normal));
        int tier = lab != null ? lab.TierOfSpeed(impact) : player.SpeedTier;

        // 零级 / 一级：和普通墙一样，交给物理解算挡住
        if (tier < 2)
        {
            Raise(player, tier, normal, false, false);
            return;
        }

        // 二级：沿法线穿过。只判一次，忽略到玩家离开绳体，避免反复触发卡在绳里。
        if (tier == 2)
        {
            Physics2D.IgnoreCollision(playerCollider, body, true);
            if (!passThrough.Contains(playerCollider))
            {
                passThrough.Add(playerCollider);
                passUntil.Add(Time.time + passWindow);
            }

            // 进入回调时碰撞已经把法线方向的速度解算掉了；把入射分量还回去，
            // 玩家才会真正穿过去，而不是贴着绳面停住。
            player.ApplyAxisVelocity(normal, Vector2.Dot(player.PreImpactVelocity, normal));
            Raise(player, tier, normal, true, false);
            return;
        }

        // 三级及以上：沿法线弹回，弹回速度和横绳共用同一套算法
        float speed = BounceSpeed(player, normal);
        player.ApplyBounce(normal, speed);
        // 绳接触不锻造；只有反弹这一种情况按正常红热撞击结算。
        player.ApplyTerrainImpact(normal, impact);
        Raise(player, tier, normal, false, true);
    }

    /// <summary>
    /// 接触法线，方向统一为“绳 → 玩家”。
    /// 回调挂在绳上，contact.normal 的正负号跟着 this/other 的约定走；
    /// 这里只取轴向，再用玩家中心相对绳中心的方位定朝外，避免依赖回调方向。
    /// </summary>
    Vector2 ContactNormal(LabPlayer player, Collision2D collision)
    {
        Vector2 delta = (Vector2)player.transform.position - (Vector2)transform.position;

        if (collision == null || collision.contactCount == 0)
        {
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
        }

        Vector2 contact = collision.GetContact(0).normal;
        Vector2 axis = Mathf.Abs(contact.x) >= Mathf.Abs(contact.y)
            ? new Vector2(Mathf.Sign(contact.x), 0f)
            : new Vector2(0f, Mathf.Sign(contact.y));

        if (Vector2.Dot(axis, delta) < 0f) axis = -axis;
        return axis;
    }

    /// <summary>弹回速度：保留碰撞前速度，并增加一个等级间距。</summary>
    float BounceSpeed(LabPlayer player, Vector2 normal)
    {
        if (lab == null) return 30f;
        return lab.RopeBounceSpeed(Vector2.Dot(player.PreImpactVelocity, normal));
    }

    void Raise(LabPlayer player, int tier, Vector2 normal, bool passed, bool bounced)
    {
        if (Contacted == null) return;
        Contacted(new RopeContact
        {
            rope = this,
            player = player,
            tier = tier,
            normal = normal,
            passed = passed,
            bounced = bounced
        });
    }

    // ---- 长绳接口（预留） ----

    /// <summary>同一 chainId 的所有段，按 y 从下到上排好。长绳逻辑从这里取整条绳。</summary>
    public static List<LabVerticalRope> ChainOf(int id)
    {
        var list = new List<LabVerticalRope>();
        foreach (var rope in All)
            if (rope != null && rope.chainId == id) list.Add(rope);
        list.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return list;
    }

    /// <summary>这条绳在世界里的上下端，长绳可以拼出连续区间。</summary>
    public float WorldBottom => transform.position.y - cells * 0.5f;
    public float WorldTop => transform.position.y + cells * 0.5f;

    // ---- 表现 ----

    void BuildVisual()
    {
        float cellSize = CellPixels / (float)TuningLab.CellPixels;   // 1 单位
        int needed = Mathf.Max(0, cells);

        var existing = new List<Transform>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("Strip_")) existing.Add(child);
        }

        for (int i = 0; i < needed; i++)
        {
            Transform strip;
            if (i < existing.Count)
            {
                strip = existing[i];
                strip.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Strip_" + i);
                go.transform.SetParent(transform, false);
                strip = go.transform;
                strip.gameObject.AddComponent<SpriteRenderer>();
            }

            strip.localPosition = new Vector3(0f, (i - (needed - 1) * 0.5f) * cellSize, 0f);

            var sr = strip.GetComponent<SpriteRenderer>();
            if (sr == null) sr = strip.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = StripSprite();
            sr.color = color;
            sr.sortingOrder = 5;
        }

        for (int i = needed; i < existing.Count; i++) DestroyNow(existing[i].gameObject);
    }

    static void DestroyNow(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }

    static Sprite stripSprite;

    /// <summary>6×16 的白色素条，PPU = 16，所以世界尺寸是 0.375 × 1 格。</summary>
    static Sprite StripSprite()
    {
        if (stripSprite != null) return stripSprite;

        var tex = new Texture2D(StripPixels, CellPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[StripPixels * CellPixels];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        stripSprite = Sprite.Create(tex, new Rect(0f, 0f, StripPixels, CellPixels),
                                    new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        return stripSprite;
    }
}
