using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竖绳。每一格是居中的 6×16 棕色素条。
///
/// 碰撞规则（按玩家当前挡位）：
///   零级 / 一级  →  当墙，挡死
///   二级         →  穿过（临时忽略碰撞，让玩家滑过去）
///   三级及以上   →  弹回，挡位 +1
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

    [Tooltip("二级穿过时忽略碰撞的时长（秒）。")]
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
        public bool passed;        // 穿过去了 / 被弹回 / 被挡住
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

    void OnDisable() => All.Remove(this);

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
        // 穿过窗口到期就把碰撞恢复回来
        for (int i = passThrough.Count - 1; i >= 0; i--)
        {
            if (Time.time < passUntil[i]) continue;
            if (passThrough[i] != null) Physics2D.IgnoreCollision(passThrough[i], body, false);
            passThrough.RemoveAt(i);
            passUntil.RemoveAt(i);
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out LabPlayer player)) return;
        Resolve(player);
    }

    void Resolve(LabPlayer player)
    {
        int tier = player.SpeedTier;

        Vector2 normal = (Vector2)player.transform.position - (Vector2)transform.position;
        normal = Mathf.Abs(normal.x) >= Mathf.Abs(normal.y)
            ? new Vector2(Mathf.Sign(normal.x), 0f)
            : new Vector2(0f, Mathf.Sign(normal.y));

        // 零级 / 一级：当墙，什么都不做
        if (tier < 2)
        {
            Raise(player, tier, normal, false, false);
            return;
        }

        // 二级：放行
        if (tier == 2)
        {
            Physics2D.IgnoreCollision(player.GetComponent<Collider2D>(), body, true);
            passThrough.Add(player.GetComponent<Collider2D>());
            passUntil.Add(Time.time + passWindow);
            Raise(player, tier, normal, true, false);
            return;
        }

        // 三级及以上：弹回 + 挡位 +1
        float speed = BounceSpeed(tier);
        player.ApplyBounce(normal, speed);
        Raise(player, tier, normal, false, true);
    }

    /// <summary>弹回速度：目标挡位（封顶四级）的速度上限再高一点点，保证判定落在新挡位里。</summary>
    float BounceSpeed(int tier)
    {
        int next = Mathf.Clamp(tier + 1, 0, 4);
        if (lab == null) return 30f;

        float cells = next >= 4
            ? lab.Boundary(lab.MaxTier - 1) + 0.05f
            : lab.Boundary(next) + 0.05f;
        return lab.SpeedForCells(cells);
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
