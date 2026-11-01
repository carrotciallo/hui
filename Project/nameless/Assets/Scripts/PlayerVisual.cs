using UnityEngine;

/// <summary>
/// 玩家的三层视觉，全部运行时生成（纯白贴图 + 染色），不产出美术资源。
///
///   外圈   = 四个方向的速度等级（比碰撞箱外扩 2px，允许超出）
///   中框   = 碰撞箱本身，四条边独立染色 = 红热态
///   白眼睛 = 朝向 / 冲刺意图
///
/// 由 PlayerMotor 自动挂载，所以所有场景的玩家都会有，不需要改场景。
/// </summary>
public class PlayerVisual : MonoBehaviour
{
    public const int RingGap = 2;
    public const int RingThickness = 2;
    public const int EyePixels = 2;

    SpriteRenderer[] ring = new SpriteRenderer[4];    // 上 下 左 右
    SpriteRenderer[] edges = new SpriteRenderer[4];   // 上 下 左 右
    SpriteRenderer eye;

    Transform eyeT;
    float eyeOffset;
    Color baseColor = Color.white;
    bool hasFrame;

    static readonly System.Collections.Generic.Dictionary<long, Sprite> cache =
        new System.Collections.Generic.Dictionary<long, Sprite>();

    /// <summary>按碰撞箱边长（像素）建一套视觉。</summary>
    public static PlayerVisual Build(Transform root, int pixels, bool hollow, Color color)
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(root, false);
        var visual = go.AddComponent<PlayerVisual>();
        visual.Create(pixels, hollow, color);
        return visual;
    }

    void Create(int pixels, bool hollow, Color color)
    {
        hasFrame = hollow;
        baseColor = color;

        float px = GameConstants.PixelsPerUnit;
        float halfBox = pixels / px * 0.5f;

        // ---- 外圈：四段，各显示一个方向的速度等级 ----
        int ringSize = pixels + RingGap * 2;
        float ringOff = (pixels * 0.5f + RingThickness * 0.5f) / px;

        ring[0] = Part("Ring_Top", Solid(ringSize, RingThickness), new Vector2(0f, ringOff), 19);
        ring[1] = Part("Ring_Bottom", Solid(ringSize, RingThickness), new Vector2(0f, -ringOff), 19);
        ring[2] = Part("Ring_Left", Solid(RingThickness, pixels), new Vector2(-ringOff, 0f), 19);
        ring[3] = Part("Ring_Right", Solid(RingThickness, pixels), new Vector2(ringOff, 0f), 19);
        for (int i = 0; i < 4; i++) ring[i].color = TierColor(0);

        // ---- 中框：碰撞箱 + 红热态 ----
        if (hollow)
        {
            float wall = 2f / px;
            float off = halfBox - wall * 0.5f;
            int innerLen = Mathf.RoundToInt(pixels - wall * px * 2f);

            edges[0] = Part("Edge_Top", Solid(pixels, 2), new Vector2(0f, off), 20);
            edges[1] = Part("Edge_Bottom", Solid(pixels, 2), new Vector2(0f, -off), 20);
            edges[2] = Part("Edge_Left", Solid(2, innerLen), new Vector2(-off, 0f), 20);
            edges[3] = Part("Edge_Right", Solid(2, innerLen), new Vector2(off, 0f), 20);
        }
        else
        {
            edges = null;
            Part("Body", Solid(pixels, pixels), Vector2.zero, 20).color = color;
        }

        // ---- 白眼睛 ----
        eye = Part("Eye", Solid(EyePixels, EyePixels), Vector2.zero, 21);
        eye.color = Color.white;
        eyeT = eye.transform;
        eyeOffset = halfBox - (hollow ? 2f / px : 0f) - EyePixels / px * 0.5f;

        for (int i = 0; i < 4; i++) SetEdge(i, PlayerMotor.EdgeState.Normal);
        SetAim(Vector2.right);
    }

    SpriteRenderer Part(string name, Sprite sprite, Vector2 localPos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // ---- 对外 ----

    /// <summary>外圈四段各自的等级：上 下 左 右。只亮"正在往那个方向去"的那几段。</summary>
    public void SetRingTiers(int up, int down, int left, int right)
    {
        ring[0].color = TierColor(up);
        ring[1].color = TierColor(down);
        ring[2].color = TierColor(left);
        ring[3].color = TierColor(right);
    }

    /// <summary>只给"参与碰撞的那一条边"上色。</summary>
    public void SetEdge(int index, PlayerMotor.EdgeState state)
    {
        if (edges == null || index < 0 || index >= 4) return;
        var sr = edges[index];
        if (sr == null) return;

        switch (state)
        {
            case PlayerMotor.EdgeState.Hot1: sr.color = new Color(1f, 0.55f, 0.15f); break;
            case PlayerMotor.EdgeState.Hot2: sr.color = new Color(1f, 0.12f, 0.08f); break;
            default: sr.color = baseColor * 0.65f; break;
        }
    }

    /// <summary>白眼睛挪到朝向那一侧，支持上下左右。</summary>
    public void SetAim(Vector2 dir)
    {
        if (eyeT == null) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? new Vector2(Mathf.Sign(dir.x), 0f)
            : new Vector2(0f, Mathf.Sign(dir.y));
        eyeT.localPosition = new Vector3(dir.x * eyeOffset, dir.y * eyeOffset, 0f);
    }

    /// <summary>零级不亮，一级黄 → 四级白。</summary>
    public static Color TierColor(int tier)
    {
        switch (tier)
        {
            case 1: return new Color(1f, 0.86f, 0.2f);
            case 2: return new Color(1f, 0.55f, 0.15f);
            case 3: return new Color(0.35f, 0.85f, 1f);
            case 4: return Color.white;
            default: return new Color(1f, 1f, 1f, 0.06f);
        }
    }

    // ---- 运行时贴图 ----

    static Sprite Solid(int w, int h)
    {
        long key = ((long)1 << 40) | ((long)w << 20) | (long)h;
        if (cache.TryGetValue(key, out var s) && s != null) return s;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), GameConstants.PixelsPerUnit);
        cache[key] = s;
        return s;
    }
}
