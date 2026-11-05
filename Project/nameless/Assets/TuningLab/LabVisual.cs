using UnityEngine;

/// <summary>
/// 玩家的分层视觉，全部运行时生成（纯白贴图 + 染色），不产出任何美术资源。
///
///   外圈   = 速度等级（比碰撞箱大 2px，允许超出）
///   中间   = 碰撞箱本身，四条边独立染色 = 红热态（只有 A 有边）
///   白眼睛 = 朝向，2×2 像素，纯白，不占用调色板
///
/// 交给美术的分工：颜色/形状都可以整个替换，这里只保证"信息分层"这件事成立。
/// </summary>
public class LabVisual : MonoBehaviour
{
    /// <summary>外圈比碰撞箱每边外扩多少像素。</summary>
    public const int RingGap = 2;
    /// <summary>外圈线宽（像素）。</summary>
    public const int RingThickness = 2;
    /// <summary>眼睛尺寸（像素）。</summary>
    public const int EyePixels = 2;

    SpriteRenderer[] ring;           // 上 下 左 右，各一段
    SpriteRenderer eye;
    SpriteRenderer[] edges;          // 上 下 左 右
    Transform eyeT;
    readonly System.Collections.Generic.List<SpriteRenderer> parts =
        new System.Collections.Generic.List<SpriteRenderer>();

    float eyeOffset;
    Color baseColor = Color.white;
    bool hasFrame;

    static readonly System.Collections.Generic.Dictionary<long, Sprite> cache =
        new System.Collections.Generic.Dictionary<long, Sprite>();

    public static LabVisual Build(Transform root, int pixels, bool hasFrame, Color baseColor)
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(root, false);
        var v = go.AddComponent<LabVisual>();
        v.Create(pixels, hasFrame, baseColor, false);
        return v;
    }

    /// <summary>
    /// 幻影用：只保留身体/边框，不画速度外圈和眼睛，整体压到玩家下面。
    /// 语义和玩家的 <see cref="Build"/> 共用同一套部件生成，避免第二套视觉。
    /// </summary>
    public static LabVisual BuildGhost(Transform root, int pixels, bool hasFrame, Color baseColor)
    {
        var go = new GameObject("GhostVisual");
        go.transform.SetParent(root, false);
        var v = go.AddComponent<LabVisual>();
        v.Create(pixels, hasFrame, baseColor, true);
        return v;
    }

    void Create(int pixels, bool hasFrame, Color color, bool ghost)
    {
        this.hasFrame = hasFrame;
        baseColor = color;
        int sort = ghost ? -2 : 0;

        float px = TuningLab.CellPixels;                 // 每格像素
        float halfBox = pixels / px * 0.5f;              // 碰撞箱半边长（世界单位）

        // ---- 外圈：四个方向各一段，显示该方向的速度等级 ----
        // 段中心在碰撞箱边缘外 RingThickness/2 处；上下段占满整宽，左右段缩短以避开四角。
        if (!ghost)
        {
            int ringSize = pixels + RingGap * 2;
            float ringOff = (pixels * 0.5f + RingThickness * 0.5f) / px;
            int sideLen = pixels;                 // 左右段高度，避开上下段

            ring = new SpriteRenderer[4];
            ring[0] = Part("Ring_Top", Solid(ringSize, RingThickness), new Vector2(0f, ringOff), 19 + sort);
            ring[1] = Part("Ring_Bottom", Solid(ringSize, RingThickness), new Vector2(0f, -ringOff), 19 + sort);
            ring[2] = Part("Ring_Left", Solid(RingThickness, sideLen), new Vector2(-ringOff, 0f), 19 + sort);
            ring[3] = Part("Ring_Right", Solid(RingThickness, sideLen), new Vector2(ringOff, 0f), 19 + sort);
            for (int i = 0; i < 4; i++) ring[i].color = TierColor(0);
        }

        // ---- 中间：碰撞箱 + 红热态 ----
        if (hasFrame)
        {
            float wall = 2f / px;                        // 墙厚 2px
            float off = halfBox - wall * 0.5f;           // 墙中心到中心
            float innerLen = pixels / px - wall * 2f;    // 去掉两端墙之后的长度

            edges = new SpriteRenderer[4];
            edges[0] = Part("Edge_Top", Solid(pixels, 2), new Vector2(0f, off), 20 + sort);
            edges[1] = Part("Edge_Bottom", Solid(pixels, 2), new Vector2(0f, -off), 20 + sort);
            edges[2] = Part("Edge_Left", Solid(2, Mathf.RoundToInt(innerLen * px)), new Vector2(-off, 0f), 20 + sort);
            edges[3] = Part("Edge_Right", Solid(2, Mathf.RoundToInt(innerLen * px)), new Vector2(off, 0f), 20 + sort);
        }
        else
        {
            // 没有边的角色：整个碰撞箱一块实心
            Part("Body", Solid(pixels, pixels), Vector2.zero, 20 + sort).color = baseColor;
        }

        // ---- 白眼睛：朝向（幻影不画）----
        if (!ghost)
        {
            eye = Part("Eye", Solid(EyePixels, EyePixels), Vector2.zero, 21 + sort);
            eye.color = Color.white;
            eyeT = eye.transform;
            eyeOffset = halfBox - (hasFrame ? 2f / px : 0f) - EyePixels / px * 0.5f;
        }

        SetAllEdges(LabPlayer.EdgeState.Normal);   // 四条边先上常态色，否则是默认白
        if (!ghost) SetAim(Vector2.right);
    }

    SpriteRenderer Part(string name, Sprite sprite, Vector2 localPos, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        parts.Add(sr);
        return sr;
    }

    /// <summary>装填态：整块 B 不可视，但 GameObject 还在，由宿主带着走。</summary>
    public void SetVisible(bool visible)
    {
        for (int i = 0; i < parts.Count; i++)
            if (parts[i] != null) parts[i].enabled = visible;
    }

    // ------------------------------------------------------------------

    /// <summary>外圈四段各自的等级：上 下 左 右。只显示"正在往那个方向去"的那几段。</summary>
    public void SetRingTiers(int up, int down, int left, int right)
    {
        if (ring == null) return;
        ring[0].color = TierColor(up);
        ring[1].color = TierColor(down);
        ring[2].color = TierColor(left);
        ring[3].color = TierColor(right);
    }

    /// <summary>零级不亮，一级黄 → 四级白。</summary>
    public static Color TierColor(int tier)
    {
        switch (tier)
        {
            case 1: return new Color(1f, 0.86f, 0.2f);    // 黄
            case 2: return new Color(1f, 0.55f, 0.15f);   // 橙
            case 3: return new Color(0.35f, 0.85f, 1f);   // 青
            case 4: return Color.white;
            default: return new Color(1f, 1f, 1f, 0.06f); // 零级：几乎不可见
        }
    }

    /// <summary>只给"参与碰撞的那一条边"上色。</summary>
    public void SetEdge(LabPlayer.Edge e, LabPlayer.EdgeState s)
    {
        if (edges == null) return;
        var sr = edges[(int)e];
        if (sr == null) return;

        switch (s)
        {
            case LabPlayer.EdgeState.Hot2: sr.color = new Color(1f, 0.12f, 0.08f); break;
            case LabPlayer.EdgeState.Spirit: sr.color = new Color(1f, 1f, 1f, 0.15f); break;
            default: sr.color = baseColor * 0.65f; break;
        }
    }

    public void SetAllEdges(LabPlayer.EdgeState s)
    {
        if (edges == null) return;
        for (int i = 0; i < 4; i++) SetEdge((LabPlayer.Edge)i, s);
    }

    /// <summary>白眼睛挪到"当前朝向/冲刺意图"那一侧。支持上下左右四个方向。</summary>
    public void SetAim(Vector2 dir)
    {
        if (eyeT == null) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        // 只认单轴：abs 大的那条轴说了算
        dir = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? new Vector2(Mathf.Sign(dir.x), 0f)
            : new Vector2(0f, Mathf.Sign(dir.y));
        eyeT.localPosition = new Vector3(dir.x * eyeOffset, dir.y * eyeOffset, 0f);
    }

    // ------------------------------------------------------------------
    // 运行时贴图
    // ------------------------------------------------------------------

    Sprite Solid(int w, int h)
    {
        long key = Key(1, w, h, 0);
        if (cache.TryGetValue(key, out var s) && s != null) return s;

        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        cache[key] = s;
        return s;
    }

    Sprite Hollow(int size, int thickness)
    {
        long key = Key(2, size, size, thickness);
        if (cache.TryGetValue(key, out var s) && s != null) return s;

        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < thickness || y < thickness || x >= size - thickness || y >= size - thickness;
                px[y * size + x] = edge ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();

        s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        cache[key] = s;
        return s;
    }

    static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

    static long Key(int kind, int a, int b, int c) =>
        ((long)kind << 40) | ((long)a << 24) | ((long)b << 12) | (uint)c;
}
