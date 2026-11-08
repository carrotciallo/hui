using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召回留下的幻影：复制被召回者当时的形态，吸附在格中心，只对另一名玩家生效。
/// 全场最多 <see cref="MaxPhantoms"/> 个，超出时销毁最旧的那个（FIFO）。
/// 破坏/交互细节由玩家侧和幻影自己结算，不销毁来源玩家。
///
/// 电梯形（‖ / =）幻影还会沿平行线方向铺出一条光桥，见 <see cref="LabLightBridgeBlock"/>。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabPhantom : MonoBehaviour
{
    public const int MaxPhantoms = 2;

    /// <summary>光桥最多往一侧铺多少格，防止跨过整张地图。</summary>
    public const int MaxBridgeCells = 24;

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

    /// <summary>这条幻影铺出来的光桥方块；非电梯形态为空。</summary>
    public readonly List<LabLightBridgeBlock> Bridge = new List<LabLightBridgeBlock>();

    public bool Loaded => loaded != null;
    public bool ConverterLoaded => loaded != null && Form == LabForm.Converter;
    /// <summary>这条幻影已经被用过（大炮发射过或 L 转过）。用过的幻影会自毁，此标记供召回判定读。</summary>
    public bool Consumed { get; private set; }

    /// <summary>只给规格用：标记为已使用，不真的走一遍物理装填。</summary>
    public void MarkConsumedForTest() => Consumed = true;
    /// <summary>只有电梯形（‖ / =）幻影铺光桥。</summary>
    public bool HasLightBridge => Form == LabForm.Elevator;
    /// <summary>‖ 是竖直光桥，= 是横向光桥。</summary>
    public bool BridgeIsVertical => Form == LabForm.Elevator && LabForms.ElevatorIsVertical(SpiritMask);

    TuningLab lab;
    BoxCollider2D body;
    LabVisual visual;
    LabPlayer loaded;
    Vector2 entryVelocity = Vector2.zero;
    Vector2 entryDir = Vector2.zero;
    LabPlayer.DashState entryDash;
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
    /// 炮和 L 都是瞬时出膛（#76），这里不再有延时计时器。
    /// </summary>
    void FixedUpdate() => RefreshIgnorePair();

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
    /// 幻影炮 / L 幻影的入口（Issue #76 定案）：
    ///   幻影大炮是"玻璃大炮"——炮底和炮口都能进，不要求冲刺态，进膛后立刻发射，同时幻影自毁；
    ///   幻影转向器是瞬时的——进膛立刻从水平那条灵体边射出，不再等 1 s。
    /// </summary>
    public bool TryAbsorb(LabPlayer player)
    {
        if (loaded != null || player == null || player != Target) return false;
        if (player.Suspended || lab == null) return false;
        if (Form != LabForm.Cannon && Form != LabForm.Converter) return false;

        // 炮底和炮口都算入口：只要从这两条边碰到就算进膛。
        Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
        LabPlayer.Edge struck = LabPlayer.EdgeFor(
            Mathf.Abs(to.x) >= Mathf.Abs(to.y)
                ? new Vector2(Mathf.Sign(to.x), 0f)
                : new Vector2(0f, Mathf.Sign(to.y)));
        if (struck != LabPlayer.Edge.Bottom
            && (Form != LabForm.Cannon || struck != LabForms.MouthEdge(SpiritMask)))
            return false;

        if (Form == LabForm.Converter && player.PreImpactVelocity.y <= lab.V0Speed) return false;

        loaded = player;
        entryVelocity = player.PreImpactVelocity;
        entryDir = player.DashDirection;
        entryDash = player.CaptureDash();
        player.SuspendInPhantom(this);

        // 玻璃大炮 / 瞬时转向：进膛的同一步就射出去，幻影随即报废。
        if (Form == LabForm.Cannon) FireGlassCannon();
        else FireConverter();
        return true;
    }

    /// <summary>
    /// 玻璃大炮：进膛立刻沿原炮口射出，速度取进入速度的大小（只换方向，不加能量），
    /// 然后幻影自毁。不需要冲刺态，也不需要再按一次开炮。
    /// </summary>
    void FireGlassCannon()
    {
        if (loaded == null) return;

        var b = loaded;
        loaded = null;
        entryDash = default(LabPlayer.DashState);

        Vector2 dir = LabForms.EdgeNormal(LabForms.MouthEdge(SpiritMask));
        // 从炮口那一侧的外面出来：炮底进的也从炮口出。
        Vector2 position = (Vector2)transform.position
            + dir * (HalfHeight + b.HalfHeight + 0.05f);
        b.WakeFromHost(position, dir * entryVelocity.magnitude, Mathf.Abs(dir.x) > 0.5f);

        // 幻影被这次发射消耗掉。
        Consumed = true;
        DestroyPhantom();
    }

    /// <summary>
    /// 在指定格中心生成一个幻影。默认先按 FIFO 淘汰最旧的，保证场上永远不超过上限；
    /// 召回走 <c>deferEviction</c>：先铺新桥，再 <see cref="EvictOverflow"/>，
    /// 这样旧幻影的光桥还能参与新幻影的生成判定（#76 的时序要求）。
    /// </summary>
    public static LabPhantom Spawn(
        TuningLab lab, LabPlayer source, LabPlayer target, Vector2Int cell,
        int spiritMask, int pixels, bool framed, bool deferEviction = false)
    {
        All.RemoveAll(phantom => phantom == null);
        if (!deferEviction) EvictOverflow();

        var go = new GameObject("Phantom");
        if (lab != null) go.transform.SetParent(lab.transform, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<LabPhantom>().Configure(lab, source, target, cell, spiritMask, pixels, framed);
        return All.Count > 0 ? All[All.Count - 1] : null;
    }

    /// <summary>
    /// 场上有没有幻影已经被用掉了（大炮发射或 L 转向会消耗幻影）。
    /// 召回前先读它：有就整次召回无效（#76）。
    /// </summary>
    public static bool AnyUsed
    {
        get
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Consumed) return true;
            return false;
        }
    }

    /// <summary>超出上限时从最旧的开始销毁（FIFO）。</summary>
    public static void EvictOverflow()
    {
        All.RemoveAll(phantom => phantom == null);
        while (All.Count > MaxPhantoms) All[0].DestroyPhantom();
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

        BuildLightBridge();
    }

    /// <summary>
    /// 电梯形幻影铺光桥：从自己这一格沿平行线方向逐格往前，撞到第一个实体就停。
    /// ‖（上下两条边碎掉，只剩左右两根竖杠）沿竖直方向铺，= 沿水平方向铺。
    /// A/B 站在路上不打断——光桥直接穿过去继续长。
    /// </summary>
    void BuildLightBridge()
    {
        Bridge.Clear();
        if (!HasLightBridge || lab == null) return;

        bool vertical = BridgeIsVertical;
        Transform parent = transform.parent != null ? transform.parent : lab.transform;

        for (int axis = 0; axis < 2; axis++)
        {
            var step = vertical
                ? new Vector2Int(0, axis == 0 ? 1 : -1)
                : new Vector2Int(axis == 0 ? 1 : -1, 0);

            for (int i = 1; i <= MaxBridgeCells; i++)
            {
                var cell = new Vector2Int(Cell.x + step.x * i, Cell.y + step.y * i);
                if (!CanBridgeInto(cell)) break;

                bool passesForbidden = OverlapsPlayer(cell, Target);
                var block = LabLightBridgeBlock.Spawn(
                    this, cell, vertical, passesForbidden, parent, lab.SlickMaterial);
                if (block == null) break;

                // A（来源）永远能穿过自己幻影铺出来的光桥。
                block.IgnorePlayer(Source);
                if (passesForbidden) block.IgnorePlayer(Target);
                Bridge.Add(block);
            }
        }
    }

    /// <summary>
    /// 这一格能不能继续铺。撞到任何实体就停：地形、冰、岩浆、修复方块、门、绳、地刺、
    /// 别的幻影和已有光桥都算（Issue #76）。只有玩家和隐藏方块不挡。
    /// 判定用格子中心 + 四条边中内侧采样，不用 0.98 宽的盒子 —— 那个盒子会溢出到相邻格，
    /// 把"隔壁有墙"误判成"这一格被堵"。
    /// </summary>
    bool CanBridgeInto(Vector2Int cell)
    {
        if (!LabLightBridgeBlock.IsFree(cell)) return false;

        // 采样点：中心 + 四条边往内缩 0.25 格。
        const float inset = 0.25f;
        var center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int p = 0; p < 5; p++)
        {
            Vector2 point;
            switch (p)
            {
                case 0: point = center; break;
                case 1: point = center + new Vector2(0f, 0.5f - inset); break;
                case 2: point = center + new Vector2(0f, -(0.5f - inset)); break;
                case 3: point = center + new Vector2(0.5f - inset, 0f); break;
                default: point = center + new Vector2(-(0.5f - inset), 0f); break;
            }
            if (HasBlockerAt(point)) return false;
        }
        return true;
    }

    readonly Collider2D[] bridgeHits = new Collider2D[8];

    /// <summary>
    /// 这个点上有没有挡桥的东西：只要是有碰撞体的实体就算，地形（冰 / 岩浆 / 修复方块
    /// 都并进同一份合并几何）、别的幻影、已有光桥和各类物件一视同仁。
    /// 不挡的只有两类：玩家（A/B 站在路上，桥穿过去接着长）和隐藏方块
    /// —— 隐藏方块本来就没有碰撞体，这里也就没有特例可写。
    /// </summary>
    bool HasBlockerAt(Vector2 point)
    {
        int count = Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, bridgeHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = bridgeHits[i];
            if (hit == null || hit.isTrigger || hit == body) continue;
            // 幻影自己不是障碍；别的幻影照挡。
            if (hit.GetComponentInParent<LabPhantom>() == this) continue;
            if (hit.GetComponentInParent<LabPlayer>() != null) continue;
            return true;
        }
        return false;
    }

    /// <summary>这一格在生成光桥之前就压在这个玩家身上吗？压着就对他让路。</summary>
    static bool OverlapsPlayer(Vector2Int cell, LabPlayer player)
    {
        if (player == null) return false;
        Bounds cellBounds = new Bounds(
            new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
            new Vector3(1f, 1f, 1f));
        return cellBounds.Intersects(player.BodyBounds);
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
    /// L 幻影出射（#76 起是瞬时的）：从水平那条 Spirit 边（左优先，否则右）等速转出。
    /// 四种相邻灵体组合里水平灵体边恰好只有一条，所以出口是确定的。
    /// </summary>
    void FireConverter()
    {
        if (loaded == null) return;

        var b = loaded;
        loaded = null;

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

        // L 也是一次性机关：转过一次就报废。
        Consumed = true;
        DestroyPhantom();
    }

    /// <summary>把膛里的玩家放回幻影外侧，恢复进入时的速度。</summary>
    public void EjectLoaded(LabPlayer player)
    {
        if (loaded == null || player != loaded) return;
        var b = loaded;
        loaded = null;
        entryDash = default(LabPlayer.DashState);

        Vector2 dir = entryDir.sqrMagnitude > 0.0001f ? -entryDir : Vector2.down;
        Vector2 position = (Vector2)transform.position
            + dir * (HalfHeight + b.HalfHeight + 0.05f);
        b.WakeFromHost(position, entryVelocity, false);
    }

    /// <summary>
    /// 销毁幻影。立即从静态表移除，避免同一帧的 FIFO 读到已销毁对象。
    /// 光桥跟着幻影一起消失：幻影被覆盖（FIFO 挤掉）时桥也要拆。
    /// </summary>
    public void DestroyPhantom()
    {
        if (Leaving) return;
        Leaving = true;
        if (loaded != null) EjectLoaded(loaded);
        ClearLightBridge();
        All.Remove(this);
        Destroy(gameObject);
    }

    /// <summary>拆掉这条幻影铺的光桥。方块立即从静态表移除，同一帧的生成不会读到已销毁对象。</summary>
    public void ClearLightBridge()
    {
        for (int i = 0; i < Bridge.Count; i++)
        {
            var block = Bridge[i];
            if (block == null) continue;
            LabLightBridgeBlock.All.Remove(block);
            Destroy(block.gameObject);
        }
        Bridge.Clear();
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
