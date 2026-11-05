using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召回留下的幻影：复制被召回者当时的形态，吸附在格中心，只对另一名玩家生效。
/// 全场最多 <see cref="MaxPhantoms"/> 个，超出时销毁最旧的那个（FIFO）。
/// 破坏/交互细节由玩家侧和幻影自己结算，不销毁来源玩家。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabPhantom : MonoBehaviour
{
    public const int MaxPhantoms = 3;

    /// <summary>Play 时生成的所有幻影，按生成顺序排队（越靠前越旧）。</summary>
    public static readonly List<LabPhantom> All = new List<LabPhantom>();

    public LabPlayer Source { get; private set; }
    public LabPlayer Target { get; private set; }
    public LabForm Form { get; private set; }
    public int SpiritMask { get; private set; }
    public int Pixels { get; private set; }
    public bool Framed { get; private set; }
    public Vector2Int Cell { get; private set; }
    public bool Leaving { get; private set; }

    public bool Loaded => loaded != null;

    TuningLab lab;
    BoxCollider2D body;
    LabVisual visual;
    LabPlayer loaded;
    Vector2 entryVelocity = Vector2.zero;
    Vector2 entryDir = Vector2.zero;
    float converterTimer;
    Collider2D ignoredCollider;
    bool ignoredWasEnabled;

    /// <summary>幻影半高，用来把出膛的玩家放到表面外侧。</summary>
    float HalfHeight => body != null ? body.size.y * 0.5f : Pixels / (2f * TuningLab.CellPixels);

    void Awake() => body = GetComponent<BoxCollider2D>();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    void OnDestroy()
    {
        if (ignoredCollider != null && body != null)
            Physics2D.IgnoreCollision(body, ignoredCollider, false);
    }

    /// <summary>
    /// 幻影只忽略来源玩家：来源可以穿过去，另一名玩家保持实体碰撞。
    /// 来源进膛时 collider 会被关掉，出膛后重建引用，所以每物理步核对一次。
    /// </summary>
    void FixedUpdate()
    {
        Collider2D current = Source != null ? Source.GetComponent<Collider2D>() : null;
        bool enabledNow = current != null && current.enabled;
        if (current == ignoredCollider && enabledNow == ignoredWasEnabled) return;

        if (ignoredCollider != null && body != null)
            Physics2D.IgnoreCollision(body, ignoredCollider, false);

        ignoredCollider = current;
        ignoredWasEnabled = enabledNow;
        if (current != null && body != null)
            Physics2D.IgnoreCollision(body, current, true);
    }

    /// <summary>
    /// 在指定格中心生成一个幻影。生成前先按 FIFO 淘汰最旧的，保证场上永远不超过上限。
    /// </summary>
    public static LabPhantom Spawn(
        TuningLab lab, LabPlayer source, LabPlayer target, Vector2Int cell,
        int spiritMask, int pixels, bool framed)
    {
        All.RemoveAll(phantom => phantom == null);
        while (All.Count >= MaxPhantoms) All[0].DestroyPhantom();

        var go = new GameObject("Phantom");
        if (lab != null) go.transform.SetParent(lab.transform, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<LabPhantom>().Configure(lab, source, target, cell, spiritMask, pixels, framed);
        return All.Count > 0 ? All[All.Count - 1] : null;
    }

    void Configure(
        TuningLab lab, LabPlayer source, LabPlayer target, Vector2Int cell,
        int spiritMask, int pixels, bool framed)
    {
        this.lab = lab;
        Source = source;
        Target = target;
        Cell = cell;
        SpiritMask = spiritMask;
        Form = LabForms.Of(spiritMask);
        Pixels = Mathf.Clamp(pixels, 1, TuningLab.CellPixels);
        Framed = framed;

        if (body == null) body = GetComponent<BoxCollider2D>();
        body.isTrigger = false;
        body.size = Vector2.one * (Pixels / (float)TuningLab.CellPixels);
        body.offset = Vector2.zero;
        if (lab != null) body.sharedMaterial = lab.SlickMaterial;

        Color sourceColor = framed ? TuningLab.ColorA : TuningLab.ColorB;
        var ghostColor = new Color(sourceColor.r, sourceColor.g, sourceColor.b, 0.35f);
        visual = LabVisual.BuildGhost(transform, Pixels, framed, ghostColor);
        for (int i = 0; i < 4; i++)
        {
            var edge = (LabPlayer.Edge)i;
            bool spirit = (spiritMask & (1 << i)) != 0;
            visual.SetEdge(edge, spirit ? LabPlayer.EdgeState.Spirit : LabPlayer.EdgeState.Normal);
        }

        // 第一次也要立刻生效，不能等下一个物理步。
        Collider2D sourceCollider = source != null ? source.GetComponent<Collider2D>() : null;
        if (sourceCollider != null)
        {
            Physics2D.IgnoreCollision(body, sourceCollider, true);
            ignoredCollider = sourceCollider;
            ignoredWasEnabled = sourceCollider.enabled;
        }
    }

    /// <summary>开炮（大炮幻影）。具体弹道在幻影炮实装时结算；这里先保证膛里的人不会卡住。</summary>
    public void Fire()
    {
        if (loaded == null) return;
        EjectLoaded(loaded);
    }

    /// <summary>把膛里的玩家放回幻影外侧，恢复进入时的速度。</summary>
    public void EjectLoaded(LabPlayer player)
    {
        if (loaded == null) return;
        var b = loaded;
        loaded = null;
        converterTimer = 0f;

        Vector2 dir = entryDir.sqrMagnitude > 0.0001f ? -entryDir : Vector2.down;
        Vector2 position = (Vector2)transform.position
            + dir * (HalfHeight + b.HalfHeight + 0.05f);
        b.WakeFromHost(position, entryVelocity, false);
    }

    /// <summary>销毁幻影。立即从静态表移除，避免同一帧的 FIFO 读到已销毁对象。</summary>
    public void DestroyPhantom()
    {
        if (Leaving) return;
        Leaving = true;
        if (loaded != null) EjectLoaded(loaded);
        All.Remove(this);
        Destroy(gameObject);
    }

    /// <summary>地图重置 / 场景销毁时清空全部幻影。</summary>
    public static void ClearAll()
    {
        var copy = new List<LabPhantom>(All);
        All.Clear();
        for (int i = 0; i < copy.Count; i++)
            if (copy[i] != null) copy[i].DestroyPhantom();
    }
}
