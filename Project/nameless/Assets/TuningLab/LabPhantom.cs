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
    public bool ConverterLoaded => loaded != null && Form == LabForm.Converter;

    TuningLab lab;
    BoxCollider2D body;
    LabVisual visual;
    LabPlayer loaded;
    Vector2 entryVelocity = Vector2.zero;
    Vector2 entryDir = Vector2.zero;
    LabPlayer.DashState entryDash;
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
        RefreshIgnorePair();

        if (loaded != null && Form == LabForm.Converter)
        {
            converterTimer -= Time.fixedDeltaTime;
            if (converterTimer <= 0f) FireConverter();
        }
    }

    void RefreshIgnorePair()
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

    void OnCollisionEnter2D(Collision2D c) => TryAbsorbCollision(c);
    void OnCollisionStay2D(Collision2D c) => TryAbsorbCollision(c);

    void TryAbsorbCollision(Collision2D c)
    {
        if (loaded != null || c.collider == null) return;
        var player = c.collider.GetComponentInParent<LabPlayer>();
        if (player != null) TryAbsorb(player);
    }

    /// <summary>
    /// 从幻影底面进入。大炮要冲刺向上；L 只要求向上速度高于 v0。
    /// 判定不看幻影自己的开口，底面永远可以进 —— 这是幻影相对真人 A 的额外入口。
    /// </summary>
    public bool TryAbsorb(LabPlayer player)
    {
        if (loaded != null || player == null || player != Target) return false;
        if (player.Suspended || lab == null) return false;
        if (Form != LabForm.Cannon && Form != LabForm.Converter) return false;

        Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
        LabPlayer.Edge struck = LabPlayer.EdgeFor(
            Mathf.Abs(to.x) >= Mathf.Abs(to.y)
                ? new Vector2(Mathf.Sign(to.x), 0f)
                : new Vector2(0f, Mathf.Sign(to.y)));
        if (struck != LabPlayer.Edge.Bottom) return false;

        if (Form == LabForm.Cannon)
        {
            if (!player.InDashState) return false;
            if (Vector2.Dot(player.DashDirection, Vector2.up) <= 0.5f) return false;
        }
        else
        {
            if (player.PreImpactVelocity.y <= lab.V0Speed) return false;
            converterTimer = 1f;
        }

        loaded = player;
        entryVelocity = player.PreImpactVelocity;
        entryDir = player.DashDirection;
        entryDash = player.CaptureDash();
        player.SuspendInPhantom(this);
        return true;
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

    /// <summary>
    /// 开炮（大炮幻影）：v' = √(V前² + 一个等级间距的 v²)，方向取幻影原本的炮口。
    /// L 幻影不响应手动开炮，1 秒后自动等速射出。
    /// </summary>
    public void Fire()
    {
        if (loaded == null) return;
        if (Form == LabForm.Converter) return;
        if (Form != LabForm.Cannon || lab == null)
        {
            EjectLoaded(loaded);
            return;
        }

        var b = loaded;
        loaded = null;
        entryDash = default(LabPlayer.DashState);

        float speed = Mathf.Sqrt(Mathf.Max(0f, entryVelocity.sqrMagnitude + lab.CannonBoostV2));
        Vector2 dir = LabForms.EdgeNormal(LabForms.MouthEdge(SpiritMask));
        Vector2 position = (Vector2)transform.position
            + dir * (HalfHeight + b.HalfHeight + 0.05f);
        b.WakeFromHost(position, dir * speed, Mathf.Abs(dir.x) > 0.5f);
    }

    /// <summary>
    /// L 幻影自动出射：从水平那条 Spirit 边（左优先，否则右）等速转出。
    /// 四种相邻灵体组合里水平 Spirit 边恰好只有一条，所以出口是确定的。
    /// </summary>
    void FireConverter()
    {
        if (loaded == null) return;

        var b = loaded;
        loaded = null;
        converterTimer = 0f;

        LabPlayer.Edge exit = (SpiritMask & LabForms.LeftBit) != 0
            ? LabPlayer.Edge.Left
            : LabPlayer.Edge.Right;
        Vector2 dir = LabForms.EdgeNormal(exit);
        Vector2 position = (Vector2)transform.position
            + dir * (HalfHeight + b.HalfHeight + 0.05f);
        b.WakeFromHost(position, dir * entryVelocity.magnitude, Mathf.Abs(dir.x) > 0.5f);
        if (entryDash.active)
        {
            entryDash.dir = dir;
            b.ApplyDash(entryDash);
        }
        entryDash = default(LabPlayer.DashState);
    }

    /// <summary>把膛里的玩家放回幻影外侧，恢复进入时的速度。</summary>
    public void EjectLoaded(LabPlayer player)
    {
        if (loaded == null || player != loaded) return;
        var b = loaded;
        loaded = null;
        converterTimer = 0f;
        entryDash = default(LabPlayer.DashState);

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
