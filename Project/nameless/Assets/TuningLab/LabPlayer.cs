using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 调试用的方块玩家：1 格大小、不旋转、竖直重力。
/// 关键点是"速度不死"：空中制动默认 0，所以 v1/v2 可以一直保持着飞；
/// 等级只是速度的另一种说法，不需要额外的"锁存"系统。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class LabPlayer : MonoBehaviour
{
    public enum Scheme { P1, P2 }

    public TuningLab lab;
    public Scheme scheme;
    public LabPlayer other;
    /// <summary>碰撞箱边长（像素）。精灵和碰撞箱用同一个数，视觉 = 判定。</summary>
    public int pixels = 16;
    /// <summary>只有大框 A 有"边"这个概念，红热只在它身上判定。</summary>
    public bool canBeRedHot = false;

    // ---- 对外读数 ----
    public bool Grounded => grounded;
    public bool Dashing => dashLock > 0f;
    /// <summary>充能接触：站在地形上，或碰到另一名玩家。贴墙、顶天花板不算。</summary>
    public bool Touching => grounded || playerContactTimer > 0f;
    /// <summary>水平冲刺后速度仍处于 v1 区间、重力被关掉的状态。</summary>
    public bool Hovering => dashPhase == DashPhase.Dash;

    /// <summary>
    /// 当前冲刺意图方向 —— 眼睛显示的就是它，冲刺用的也是它。
    /// 优先级：竖直输入 &gt; 水平输入 &gt; 空中的竖直速度 &gt; 持久朝向。
    /// </summary>
    public Vector2 Aim
    {
        get
        {
            if (Mathf.Abs(vertInput) > 0.01f) return new Vector2(0f, Mathf.Sign(vertInput));
            if (Mathf.Abs(moveX) > 0.01f) return new Vector2(Mathf.Sign(moveX), 0f);
            if (!grounded && Mathf.Abs(rb.linearVelocity.y) > 0.5f)
                return new Vector2(0f, Mathf.Sign(rb.linearVelocity.y));
            return new Vector2(Mathf.Sign(facing), 0f);
        }
    }
    /// <summary>水平 / 竖直分量各自的速度与等级。只认轴的游戏里，这两个才是真等级。</summary>
    public float SpeedAlong(Vector2 normal) => Mathf.Abs(Vector2.Dot(rb.linearVelocity, normal));
    public int TierAlong(Vector2 normal) => lab != null ? lab.TierOfSpeed(SpeedAlong(normal)) : 0;

    public float HorizontalSpeed => Mathf.Abs(rb.linearVelocity.x);
    public float VerticalSpeed => Mathf.Abs(rb.linearVelocity.y);
    public int HorizontalTier => lab != null ? lab.TierOfSpeed(HorizontalSpeed) : 0;
    public int VerticalTier => lab != null ? lab.TierOfSpeed(VerticalSpeed) : 0;

    /// <summary>最近一次撞地形（墙/地/天花板）按法线分量算出的结果。</summary>
    public Vector2 LastImpactNormal { get; private set; }
    public float LastImpactSpeed { get; private set; }
    public int LastImpactTier { get; private set; } = -1;
    public float LastImpactTime { get; private set; } = -999f;
    /// <summary>冲刺态：位移恒定、不受外力、屏蔽输入。</summary>
    public enum DashPhase { None, Dash }
    /// <summary>这次充能已经被接触"上弦"，之后即使离地也会继续回复。</summary>
    public bool RefillArmed => refillArmed;
    public float Charge { get; private set; } = 1f;
    public float HalfHeight => col != null ? col.bounds.extents.y : 0.5f;
    public float FootY => rb.position.y - HalfHeight;
    public Vector2 Velocity => rb.linearVelocity;
    public float Speed => rb.linearVelocity.magnitude;

    /// <summary>离地点（跳起或走下台）的脚底高度。</summary>
    public float LaunchFootY { get; private set; }
    /// <summary>本次腾空最高点的脚底高度。</summary>
    public float ApexFootY { get; private set; }

    /// <summary>落地点格：从起跳点算起，站跳永远接近 0（跳高与下落互相抵消）。</summary>
    public float LandedNetDescent { get; private set; }
    /// <summary>落地点格：从最高点算起，站跳 = 跳高。</summary>
    public float LandedApexDescent { get; private set; }

    public float NetDescent => grounded ? LandedNetDescent : Mathf.Max(0f, LaunchFootY - FootY);
    public float ApexDescent => grounded ? LandedApexDescent : Mathf.Max(0f, ApexFootY - FootY);

    public int SpeedTier => lab != null ? lab.TierOfSpeed(Speed) : 0;
    public int LaunchTier => lab != null ? lab.TierOfCells(NetDescent) : 0;
    public int ApexTier => lab != null ? lab.TierOfCells(ApexDescent) : 0;

    public float HitFlash { get; private set; }

    Rigidbody2D rb;
    Collider2D col;
    Collider2D support;
    public LabVisual visual;

    bool grounded;
    float dashLock;
    float playerContactTimer;
    float coyoteTimer;
    float jumpBuffer;
    float nextHitTime;
    float facing = 1f;
    float dashTimer;
    Vector2 dashDir;
    float dashSpeedNow;
    float dashExitSpeed;
    DashPhase dashPhase;
    bool refillArmed;
    Vector2 lastFixedVelocity;

    float moveX;
    float vertInput;
    bool jumpDownThisFrame;
    bool jumpHeld;
    bool dashDownThisFrame;
    bool dashQueued;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // 站立不动时刚体会休眠，OnCollisionStay 不再触发 → 充能会莫名卡住
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        LaunchFootY = FootY;
        ApexFootY = FootY;
    }

    void Update()
    {
        ReadInput();

        if (jumpDownThisFrame && lab != null) jumpBuffer = lab.jumpBufferTime;
        else jumpBuffer = Mathf.Max(0f, jumpBuffer - Time.deltaTime);

        if (dashDownThisFrame) dashQueued = true;

        if (Mathf.Abs(moveX) > 0.01f) facing = Mathf.Sign(moveX);

        if (visual != null)
        {
            // 外圈：四个方向各自显示对应分量的等级（只亮"正在去"的方向）
            Vector2 vel = rb.linearVelocity;
            visual.SetRingTiers(
                vel.y > 0.01f ? lab.TierOfSpeed(vel.y) : 0,
                vel.y < -0.01f ? lab.TierOfSpeed(-vel.y) : 0,
                vel.x < -0.01f ? lab.TierOfSpeed(-vel.x) : 0,
                vel.x > 0.01f ? lab.TierOfSpeed(vel.x) : 0);

            visual.SetAim(Aim);             // 白眼睛 = 冲刺意图方向（四向）
        }
    }

    void FixedUpdate()
    {
        if (lab == null) return;
        float dt = Time.fixedDeltaTime;
        var v = rb.linearVelocity;

        bool wasGrounded = grounded;
        grounded = ProbeGround();

        if (grounded && !wasGrounded) OnLand();
        if (!grounded && wasGrounded) BeginAir();
        if (!grounded) ApexFootY = Mathf.Max(ApexFootY, FootY);

        coyoteTimer = grounded ? lab.coyoteTime : Mathf.Max(0f, coyoteTimer - dt);
        dashLock = Mathf.Max(0f, dashLock - dt);
        playerContactTimer = Mathf.Max(0f, playerContactTimer - dt);
        TickEdges(dt);
        HitFlash = Mathf.Max(0f, HitFlash - dt * 4f);

        // 充能：接触地面或另一名玩家 = 给这一次充能"上弦"。
        // 上弦之后计时器自己走完，离开地面也照常回复；用掉之后需要重新接触才能再上弦。
        if (Touching) refillArmed = true;

        if (refillArmed && dashLock <= 0f && Charge < 1f)
            Charge = Mathf.Min(1f, Charge + dt / Mathf.Max(0.01f, lab.dashCooldown));

        if (Charge >= 1f) refillArmed = false;

        // ---- 冲刺：进入"冲刺态"，位移恒定，速度由进入时的 V前 分三档 ----
        // 向下冲刺只允许在空中；站在地上按下去= 什么都不发生，也不消耗充能。
        bool downDashBlocked = vertInput < -0.01f && grounded;
        if (dashQueued && Charge >= 1f && dashPhase != DashPhase.Dash && !downDashBlocked
            && (!lab.groundOnlyDash || grounded))
        {
            // 冲刺方向优先级：竖直输入 > 水平输入 > 空中的竖直速度方向 > 面朝方向
            // 方向由 Aim 统一决定（和眼睛显示的是同一个东西）
            Vector2 dir = Aim;

            // 抹掉垂直分量，只留沿冲刺方向的分量
            float vBefore = Vector2.Dot(v, dir);

            bool opposite = vBefore < -0.001f;                 // 和冲刺方向反向
            bool zeroTier = vBefore <= lab.V0Speed + 0.0001f;  // 零级（含静止）

            float dashSpeed, exitSpeed;
            if (opposite)
            {
                dashSpeed = lab.Vd1;      // 略大于 v0
                exitSpeed = 0f;           // 结束后归零
            }
            else if (zeroTier)
            {
                dashSpeed = lab.Vd1;      // 略大于 v0
                exitSpeed = Mathf.Max(0f, vBefore);   // 结束后回到 V前
            }
            else
            {
                dashSpeed = vBefore;      // 已经够快：原速穿过
                exitSpeed = vBefore;      // 结束后保持
            }

            Charge = 0f;
            dashLock = lab.dashLockTime;
            dashDir = dir;
            dashSpeedNow = dashSpeed;
            dashExitSpeed = exitSpeed;
            dashTimer = lab.DashDistance / Mathf.Max(0.01f, dashSpeed);   // 位移恒定 → 时长反算
            dashPhase = DashPhase.Dash;

            v = dir * dashSpeed;
            rb.linearVelocity = v;
            rb.gravityScale = 0f;
            lastFixedVelocity = v;
            dashQueued = false;
            return;                     // 本帧到此为止
        }
        dashQueued = false;

        // ---- 冲刺态推进：不受任何外力、屏蔽任何输入 ----
        if (dashPhase == DashPhase.Dash)
        {
            dashTimer -= dt;
            if (dashTimer > 0f)
            {
                v = dashDir * dashSpeedNow;
                rb.gravityScale = 0f;
                rb.linearVelocity = v;
                lastFixedVelocity = v;
                return;
            }

            // 冲刺结束：给出结束速度，本帧继续走常规流程
            dashPhase = DashPhase.None;
            v = dashDir * dashExitSpeed;
        }

        // ---- 水平控制 ----
        if (dashLock <= 0f)
        {
            // 空气制动：空中水平方向持续 -g，和竖直方向的重力对称。
            // 这样"一个间距"的水平位移和竖直上冲高度天然相等。
            if (!grounded && lab.horizontalAirDrag)
                v.x = Mathf.MoveTowards(v.x, 0f, lab.gravity * dt);

            if (Mathf.Abs(moveX) > 0.01f)
            {
                facing = Mathf.Sign(moveX);
                float target = moveX * lab.walkSpeed;

                if (grounded)
                {
                    // 地面永远有摩擦：按住同方向也照样把超出走路速度的部分拉回来。
                    // 否则"按住右"就能把冲刺速度永久锁死 = 无限滑行。
                    float rate = Mathf.Abs(v.x) > lab.walkSpeed ? lab.groundBrake : lab.walkAccel;
                    v.x = Mathf.MoveTowards(v.x, target, rate * dt);
                }
                else
                {
                    // 空中：只负责把速度往走路速度推，超过走路速度的部分交给上面的 -g 制动
                    if (Mathf.Abs(v.x) <= lab.walkSpeed)
                        v.x = Mathf.MoveTowards(v.x, target, lab.walkAccel * dt);
                }
            }
            else
            {
                float brake = grounded ? lab.groundBrake : lab.airBrake;
                if (brake > 0f) v.x = Mathf.MoveTowards(v.x, 0f, brake * dt);
            }
        }

        // ---- 跳跃：固定高度，无变高 ----
        // 按住连跳是个开关：默认关闭（必须重新按），打开后落地即跳。
        bool wantsJump = jumpBuffer > 0f || (lab.holdToJump && jumpHeld);
        if (wantsJump && (grounded || coyoteTimer > 0f))
        {
            v.y = lab.JumpVelocity;
            jumpBuffer = 0f;
            coyoteTimer = 0f;
            grounded = false;
            BeginAir();
        }

        if (v.y < -lab.maxFallSpeed) v.y = -lab.maxFallSpeed;

        // ---- 驮着走：站在队友头上时横向跟随他，自己的方向键仍然有效（用来在头上挪位置）----
        // 冲刺那几段不接管，否则会在别人头上冲刺时被硬拉回来
        if (grounded && dashLock <= 0f && dashPhase == DashPhase.None && IsRidingOn(other))
            v.x = other.Velocity.x + moveX * lab.walkSpeed;

        // 冲刺态在上面的早退里已经把重力关掉了，走到这里一定是常规状态
        rb.gravityScale = lab.GravityScale;

        rb.linearVelocity = v;
        // 撞地形时用它算分量：碰撞回调触发时速度已经被物理解算改掉（贴地时竖直分量已经是 0）
        lastFixedVelocity = v;
    }

    void BeginAir()
    {
        LaunchFootY = FootY;
        ApexFootY = FootY;
    }

    void OnLand()
    {
        LandedNetDescent = Mathf.Max(0f, LaunchFootY - FootY);
        LandedApexDescent = Mathf.Max(0f, ApexFootY - FootY);
    }

    // ------------------------------------------------------------------
    // 碰撞：方块不旋转，所以接触只可能是水平面或竖直面，永不产生斜向。
    // 撞击是"设定"：被撞者直接升到 v2 顶端，撞击者不掉速。
    // ------------------------------------------------------------------

    void OnCollisionEnter2D(Collision2D c)
    {
        bool isPlayer = c.collider != null && c.collider.GetComponent<LabPlayer>() != null;
        if (isPlayer) playerContactTimer = 0.12f;
        else RecordTerrainImpact(c);      // 地形撞击按法线分量结算

        TryResolveHit(c);
    }

    void OnCollisionStay2D(Collision2D c)
    {
        bool isPlayer = c.collider != null && c.collider.GetComponent<LabPlayer>() != null;
        if (isPlayer) playerContactTimer = 0.12f;

        TryResolveHit(c);
    }

    /// <summary>
    /// 撞墙 / 撞地 / 撞天花板：只取沿接触法线的速度分量来算等级。
    /// 合速度 v3 但只是横向飞得快的人，撞地板时竖直分量可能还是 v0。
    /// </summary>
    void RecordTerrainImpact(Collision2D c)
    {
        if (lab == null || c.contactCount == 0) return;

        Vector2 n = c.GetContact(0).normal;
        // 方块不旋转，接触法线必定是轴；取绝对值大的那根轴，杜绝斜向
        n = Mathf.Abs(n.x) >= Mathf.Abs(n.y)
            ? new Vector2(Mathf.Sign(n.x), 0f)
            : new Vector2(0f, Mathf.Sign(n.y));

        // 用上一帧 FixedUpdate 写进去的速度：碰撞回调里速度已经被解算改掉了
        float impact = Mathf.Abs(Vector2.Dot(lastFixedVelocity, n));

        LastImpactNormal = n;
        LastImpactSpeed = impact;
        LastImpactTier = lab.TierOfSpeed(impact);
        LastImpactTime = Time.time;
    }

    void TryResolveHit(Collision2D c)
    {
        if (lab == null || other == null) return;
        if (c.collider == null || c.collider.GetComponent<LabPlayer>() == null) return;
        if (Time.time < nextHitTime) return;

        Vector2 delta = (Vector2)other.transform.position - (Vector2)transform.position;
        if (delta.sqrMagnitude < 0.0001f) return;
        Vector2 axis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? new Vector2(Mathf.Sign(delta.x), 0f)
            : new Vector2(0f, Mathf.Sign(delta.y));

        if (Mathf.Abs(axis.y) > 0.5f && !lab.verticalHitEnabled) return;

        // 只看沿碰撞轴的有符号分量：只有这一条轴上的东西才会有"同向/反向"
        // 必须读碰撞前速度：回调里 rb.linearVelocity 已经被解算改掉了（对撞时可能已经是 0）
        Vector2 vA = lastFixedVelocity;
        Vector2 vB = other.PreImpactVelocity;
        float a = Vector2.Dot(vA, axis);
        float b = Vector2.Dot(vB, axis);

        // ---- 红热：双方都严格低于第一道门槛就不判 ----
        float cellsA = lab.CellsForSpeed(Mathf.Abs(a));
        float cellsB = lab.CellsForSpeed(Mathf.Abs(b));
        bool anyAboveV0 = Mathf.Max(cellsA, cellsB) >= lab.Boundary(0) - 0.0001f;

        bool redHot = false;
        float Vp = 0f;
        if (anyAboveV0)
        {
            float a2 = a * a;
            float b2 = b * b;
            bool sameDir = a * b > 0f;
            Vp = sameDir ? Mathf.Abs(a2 - b2) : a2 + b2;

            float threshold = lab.SpeedForCells(lab.Boundary(lab.redHotTierIndex));
            threshold *= threshold;

            redHot = Vp >= threshold;
            if (redHot)
            {
                // 红热只在 A（大框）上判定：有"边"这个概念的只有它
                if (canBeRedHot) MarkEdge(EdgeFor(axis));
                if (other.canBeRedHot) other.MarkEdge(LabPlayer.EdgeFor(-axis));
            }
        }

        // ---- 动量交换：速度 + 冲刺态整体互换 ----
        // 冲刺态带着"什么速度、还剩多少距离"一起交出去，接手的人从同一位置继续冲。
        DashState myDash = CaptureDash();
        DashState theirDash = other.CaptureDash();

        SetVelocity(vB);
        ApplyDash(theirDash);          // 对方在冲刺的话，速度会被冲刺速度接管

        other.SetVelocity(vA);
        other.ApplyDash(myDash);

        nextHitTime = Time.time + 0.18f;
        other.SetHitCooldown(Time.time + 0.18f);

        if (lab != null)
        {
            lab.RaisePlayerHit(new TuningLab.HitInfo
            {
                hitter = Mathf.Abs(a) >= Mathf.Abs(b) ? this : other,
                victim = Mathf.Abs(a) >= Mathf.Abs(b) ? other : this,
                normal = axis,
                horizontal = Mathf.Abs(axis.x) > 0.5f,
                tierBefore = lab.TierOfSpeed(Mathf.Min(Mathf.Abs(a), Mathf.Abs(b))),
                tierAfter = lab.TierOfSpeed(Mathf.Max(Mathf.Abs(a), Mathf.Abs(b))),
                speed = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)),
                vp = Vp,
                redHot = redHot
            });
        }
    }

    /// <summary>把自己沿 axis 的分量改成 value，垂直分量不动。</summary>
    public void ApplyAxisVelocity(Vector2 axis, float value)
    {
        var v = rb.linearVelocity;
        v += axis * (value - Vector2.Dot(v, axis));
        rb.linearVelocity = v;
    }

    public void SetVelocity(Vector2 v) => rb.linearVelocity = v;

    /// <summary>冲刺态的可交换快照：方向、速度、剩余距离（= 剩余时长 × 速度）、结束速度。</summary>
    public struct DashState
    {
        public bool active;
        public Vector2 dir;
        public float speed;
        public float exitSpeed;
        public float timer;
        public float RemainingDistance => timer * speed;
    }

    public DashState CaptureDash()
    {
        return new DashState
        {
            active = dashPhase == DashPhase.Dash,
            dir = dashDir,
            speed = dashSpeedNow,
            exitSpeed = dashExitSpeed,
            timer = dashTimer
        };
    }

    public void ApplyDash(DashState s)
    {
        if (!s.active)
        {
            dashPhase = DashPhase.None;
            dashTimer = 0f;
            return;
        }

        dashPhase = DashPhase.Dash;
        dashDir = s.dir;
        dashSpeedNow = s.speed;
        dashExitSpeed = s.exitSpeed;
        dashTimer = s.timer;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * dashSpeedNow;
    }

    /// <summary>上一个 FixedUpdate 写进刚体的速度，也就是碰撞前的速度。</summary>
    public Vector2 PreImpactVelocity => lastFixedVelocity;

    // ---- 红热态 ----

    public enum Edge { Top = 0, Bottom = 1, Left = 2, Right = 3 }
    public enum EdgeState { Normal = 0, Hot1 = 1, Hot2 = 2 }

    readonly EdgeState[] edgeStates = new EdgeState[4];
    readonly float[] edgeTimers = new float[4];

    public EdgeState GetEdge(Edge e) => edgeStates[(int)e];
    public bool AnyHot => edgeStates[0] != EdgeState.Normal || edgeStates[1] != EdgeState.Normal
                       || edgeStates[2] != EdgeState.Normal || edgeStates[3] != EdgeState.Normal;

    /// <summary>红热态变化的外抛接口，后续"红热 2 再变化"直接挂这里。</summary>
    public event System.Action<LabPlayer, Edge, EdgeState, EdgeState> EdgeStateChanged;

    public void SetEdge(Edge e, EdgeState next)
    {
        int i = (int)e;
        var prev = edgeStates[i];
        if (prev == next) return;
        edgeStates[i] = next;
        edgeTimers[i] = next == EdgeState.Hot1 ? lab.redHotDuration : 0f;
        if (visual != null) visual.SetEdge(e, next);
        if (EdgeStateChanged != null) EdgeStateChanged(this, e, prev, next);
    }

    void MarkEdge(Edge e)
    {
        int i = (int)e;
        if (edgeStates[i] == EdgeState.Hot1) SetEdge(e, EdgeState.Hot2);        // 热 1 再撞 → 永久热 2
        else if (edgeStates[i] == EdgeState.Normal) SetEdge(e, EdgeState.Hot1);
    }

    public static Edge EdgeFor(Vector2 axis)
    {
        if (Mathf.Abs(axis.x) > 0.5f) return axis.x > 0f ? Edge.Right : Edge.Left;
        return axis.y > 0f ? Edge.Top : Edge.Bottom;
    }

    void TickEdges(float dt)
    {
        for (int i = 0; i < 4; i++)
        {
            if (edgeStates[i] != EdgeState.Hot1) continue;
            edgeTimers[i] -= dt;
            if (edgeTimers[i] <= 0f) SetEdge((Edge)i, EdgeState.Normal);
        }
    }

    public void SetHitCooldown(float until) => nextHitTime = until;

    public void Teleport(Vector2 position)
    {
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        transform.position = position;
        Charge = 1f;
        dashLock = 0f;
        grounded = false;
        playerContactTimer = 0f;
        dashTimer = 0f;
        dashPhase = DashPhase.None;
        refillArmed = false;
        LandedNetDescent = 0f;
        LandedApexDescent = 0f;
        LaunchFootY = FootY;
        ApexFootY = FootY;
    }

    // ------------------------------------------------------------------

    bool ProbeGround()
    {
        support = null;
        if (rb.linearVelocity.y > 0.1f) return false;   // 上升途中不要粘地，否则起跳当帧就会被判回地面
        Bounds b = col.bounds;
        var origin = new Vector2(b.center.x, b.min.y + 0.01f);
        var size = new Vector2(b.size.x * 0.9f, 0.02f);
        var hits = Physics2D.BoxCastAll(origin, size, 0f, Vector2.down, 0.06f, -1);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i].collider;
            if (hit != null && hit != col)
            {
                support = hit;
                return true;
            }
        }
        return false;
    }

    /// <summary>脚下踩的这块是不是那名玩家。</summary>
    bool IsRidingOn(LabPlayer p)
    {
        if (p == null || support == null) return false;
        return support.GetComponentInParent<LabPlayer>() == p;
    }

    /// <summary>正站在队友头上。</summary>
    public bool RidingOnOther => grounded && IsRidingOn(other);

    void ReadInput()
    {
        moveX = 0f;
        vertInput = 0f;
        jumpDownThisFrame = false;
        jumpHeld = false;
        dashDownThisFrame = false;

        var kb = Keyboard.current;
        if (kb == null) return;

        bool left, right, up, down;
        if (scheme == Scheme.P1)
        {
            // A（大框）：A/D 移动，W/S 冲刺方向，空格 跳，左 Shift 冲刺
            left = kb.aKey.isPressed;
            right = kb.dKey.isPressed;
            up = kb.wKey.isPressed;
            down = kb.sKey.isPressed;
            jumpHeld = kb.spaceKey.isPressed;
            jumpDownThisFrame = kb.spaceKey.wasPressedThisFrame;
            dashDownThisFrame = kb.leftShiftKey.wasPressedThisFrame;
        }
        else
        {
            // B（小块）：P = 上 / L = 左 / ; = 下 / ' = 右，K 冲刺，右 Shift 跳
            up = kb.pKey.isPressed;
            down = kb.semicolonKey.isPressed;
            left = kb.lKey.isPressed;
            right = kb.quoteKey.isPressed;
            jumpHeld = kb.rightShiftKey.isPressed;
            jumpDownThisFrame = kb.rightShiftKey.wasPressedThisFrame;
            dashDownThisFrame = kb.kKey.wasPressedThisFrame;
        }

        moveX = (right ? 1f : 0f) - (left ? 1f : 0f);
        vertInput = (up ? 1f : 0f) - (down ? 1f : 0f);
    }

    // ---- 视觉分级：颜色就是等级 ----

    public static Color TintForTier(int tier)
    {
        switch (tier)
        {
            case 1: return new Color(1f, 0.86f, 0.2f);    // v1 黄
            case 2: return new Color(1f, 0.55f, 0.15f);   // v2 橙
            case 3: return new Color(0.3f, 0.8f, 1f);     // v3 蓝
            case 4: return Color.white;                   // v4 白
            default: return new Color(0.36f, 0.62f, 1f);  // v0
        }
    }
}
