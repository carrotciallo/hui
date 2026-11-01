using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpeedTierProvider))]
public class PlayerMotor : MonoBehaviour
{
    public enum Scheme { WASD, Arrows }

    public enum MotionState { Idle, Move, Jump, Fall, Dash, Hit, Climb, Swim }

    public static readonly List<PlayerMotor> All = new List<PlayerMotor>();

    [SerializeField] Scheme scheme = Scheme.WASD;

    [Header("Move")]
    [SerializeField] float groundAccel = 90f;
    [SerializeField] float airAccel = 45f;
    [SerializeField] float turnAccelMultiplier = 1.6f;

    [Header("Jump")]
    [SerializeField] float jumpVelocity = GameConstants.JumpVelocity;
    [SerializeField] float riseGravityScale = GameConstants.RiseGravityScale;
    [SerializeField] float fallGravityScale = GameConstants.FallGravityScale;
    [SerializeField] float maxFallSpeed = GameConstants.MaxFallSpeed;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.1f;
    [SerializeField] float jumpCutMultiplier = 0.5f;
    [SerializeField] float minJumpTime = 0.1f;

    [Header("Dash")]
    [SerializeField] float dashHorizontalDistance = GameConstants.DashHorizontalDistance;
    [SerializeField] float dashUpDistance = GameConstants.DashUpDistance;
    [SerializeField] float dashDownDistance = GameConstants.DashDownDistance;
    [SerializeField] float dashCooldown = GameConstants.DashCooldown;
    [SerializeField] float dashBufferTime = 0.1f;

    [Header("Ground")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float groundProbeDistance = 0.08f;

    [Header("Being pushed")]
    [SerializeField] float pushControlLockTime = 0.12f;
    [SerializeField] float pushSpeedThreshold = 1.5f;

    public MotionState State { get; private set; } = MotionState.Idle;
    public bool Grounded => grounded;
    public bool IsDashing => isDashing;
    public bool IsKnocked => isKnocked;
    public Vector2 DashVelocity => isDashing ? dashVelocity : Vector2.zero;
    public bool CanDash => charge >= 1f && !isDashing && !isKnocked;
    public float DashCooldownRemaining => (1f - charge) * dashCooldown;
    public SpeedTier Tier => tierProvider.Tier;
    public SpeedTierProvider SpeedTiers => tierProvider;
    public Vector2 Velocity => rb.linearVelocity;
    public Vector2 ImpactVelocity => isDashing ? dashVelocity : rb.linearVelocity;
    public Scheme InputScheme => scheme;
    public float Facing => facing;
    public float MoveInput => moveInput;
    public float VerticalInput => verticalInput;
    public bool DownPressedThisFrame => downPressed;
    public bool DashPressedThisFrame => dashPressed;
    public bool JumpPressedThisFrame => jumpPressed;
    public PlayerMotor RideTarget => rideTarget;
    public bool IsSuspended => suspended;
    public bool IsClimbing => climbing;
    public bool IsFloating => floating;
    public bool IsEmptyForm => emptyBody;
    public float HalfHeight => body != null ? body.bounds.extents.y : GameConstants.PlayerSize * 0.5f;

    public void SetScheme(Scheme value) => scheme = value;
    public Vector2 SpawnPosition { get; private set; }
    public event System.Action<MotionState, MotionState> StateChanged;
    public event System.Action<ImpactInfo> ImpactReceived;

    Rigidbody2D rb;
    Collider2D body;
    SpeedTierProvider tierProvider;

    readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];

    float moveInput;
    float verticalInput;
    float facing = 1f;
    bool jumpPressed;
    bool jumpHeld;
    bool downPressed;
    bool dashPressed;
    bool jumpCutApplied;
    float jumpElapsed;
    float dashBufferTimer;

    float coyoteTimer;
    float bufferTimer;
    float controlLockTimer;
    bool grounded;
    bool onIce;
    PlayerMotor rideTarget;

    bool suspended;
    bool climbing;
    bool floating;
    float floatTargetY;
    bool emptyBody;

    bool isDashing;
    float dashTimer;
    Vector2 dashVelocity;
    Vector2 dashExitVelocity;
    float nextCollisionTime;
    float playerContactTimer;
    float charge = 1f;
    bool refillArmed;
    float dashCooldownTimer;
    bool cooldownPending;
    bool dashSpent;

    bool isKnocked;
    int knockbackStage;
    float knockbackTimer;
    float knockbackDir;

    // ---- 红热态：四条边各自的状态（只有 A 的框有边） ----
    public enum EdgeState { Normal = 0, Hot1 = 1, Hot2 = 2 }

    [Header("红热")]
    [SerializeField] int redHotTierIndex = 1;
    [SerializeField] float redHotDuration = 1.5f;
    [SerializeField] float dashDistance = GameConstants.DashDistance;

    readonly EdgeState[] edgeStates = new EdgeState[4];   // 上 下 左 右
    readonly float[] edgeTimers = new float[4];

    PlayerVisual visual;
    Vector2 preCollisionVelocity;

    public bool CanBeRedHot => scheme == Scheme.WASD;

    public EdgeState GetEdge(int index) =>
        index >= 0 && index < 4 ? edgeStates[index] : EdgeState.Normal;

    void SetEdge(int index, EdgeState next)
    {
        if (index < 0 || index >= 4 || edgeStates[index] == next) return;
        edgeStates[index] = next;
        edgeTimers[index] = next == EdgeState.Hot1 ? redHotDuration : 0f;
        if (visual != null) visual.SetEdge(index, next);
    }

    void TickEdges()
    {
        for (int i = 0; i < 4; i++)
        {
            if (edgeStates[i] != EdgeState.Hot1) continue;
            edgeTimers[i] -= Time.fixedDeltaTime;
            if (edgeTimers[i] <= 0f) SetEdge(i, EdgeState.Normal);
        }
    }

    static int EdgeFor(Vector2 axis)
    {
        if (Mathf.Abs(axis.x) > 0.5f) return axis.x > 0f ? 3 : 2;
        return axis.y > 0f ? 0 : 1;
    }

    void OnEnable() => All.Add(this);

    void OnDisable() => All.Remove(this);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        tierProvider = GetComponent<SpeedTierProvider>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (groundMask == 0) groundMask = LayerMask.GetMask("Ground", "Player");
        SpawnPosition = transform.position;

        ApplyGlobalTuning();
        BuildVisual();
    }

    /// <summary>从 Resources/GameTuning.asset 覆盖序列化字段：改一处，所有场景生效。</summary>
    void ApplyGlobalTuning()
    {
        var tuning = GameTuning.Current;
        if (tuning == null) return;

        jumpVelocity = GameConstants.JumpVelocityForHeight(tuning.jumpHeight);
        maxFallSpeed = tuning.maxFallSpeed;
        riseGravityScale = tuning.riseGravityScale;
        fallGravityScale = tuning.fallGravityScale;
        groundAccel = tuning.groundAccel;
        airAccel = tuning.airAccel;
        dashCooldown = tuning.dashCooldown;
        redHotTierIndex = tuning.redHotTierIndex;
        redHotDuration = tuning.redHotDuration;
        dashDistance = tuning.dashDistance;
        jumpCutMultiplier = tuning.jumpCutMultiplier;
    }

    /// <summary>把场景里的方块换成三层视觉：外圈等级 / 中框红热 / 白眼睛朝向。</summary>
    void BuildVisual()
    {
        var existing = GetComponent<SpriteRenderer>();
        Color color = Color.white;
        if (existing != null)
        {
            color = existing.color;
            existing.enabled = false;   // 旧方块让位给分层视觉
        }

        bool hollow = scheme == Scheme.WASD;   // A（大框）空心，B（小块）实心
        int pixels = Mathf.RoundToInt(GameConstants.PlayerPixels);
        visual = PlayerVisual.Build(transform, pixels, hollow, color);
    }

    void LateUpdate()
    {
        if (visual == null) return;

        Vector2 v = rb.linearVelocity;
        visual.SetRingTiers(
            v.y > 0.01f ? (int)SpeedTierProvider.TierOf(v.y) : 0,
            v.y < -0.01f ? (int)SpeedTierProvider.TierOf(-v.y) : 0,
            v.x < -0.01f ? (int)SpeedTierProvider.TierOf(-v.x) : 0,
            v.x > 0.01f ? (int)SpeedTierProvider.TierOf(v.x) : 0);

        visual.SetAim(AimDirection());
    }

    Vector2 AimDirection()
    {
        if (Mathf.Abs(verticalInput) > 0.01f) return new Vector2(0f, Mathf.Sign(verticalInput));
        if (Mathf.Abs(moveInput) > 0.01f) return new Vector2(Mathf.Sign(moveInput), 0f);
        if (!grounded && Mathf.Abs(rb.linearVelocity.y) > 0.5f) return new Vector2(0f, Mathf.Sign(rb.linearVelocity.y));
        return new Vector2(Mathf.Sign(facing), 0f);
    }

    void Update()
    {
        ReadInput();
        // 按键在 Update 采样、在 FixedUpdate 消费；60fps 下约每 6 帧有一个物理步，不缓冲会吞输入
        bufferTimer = jumpPressed ? jumpBufferTime : bufferTimer - Time.deltaTime;
        dashBufferTimer = dashBufferTimer > 0f
            ? dashBufferTimer - Time.deltaTime
            : 0f;
    }

    void FixedUpdate()
    {
        tierProvider.Tick(Time.fixedDeltaTime);
        preCollisionVelocity = rb.linearVelocity;   // 碰撞回调里速度已被解算，先用这里存一份
        TickEdges();
        controlLockTimer = Mathf.Max(0f, controlLockTimer - Time.fixedDeltaTime);

        if (suspended)
        {
            TickState();
            return;
        }

        if (climbing)
        {
            TickClimb();
            TickState();
            return;
        }

        if (emptyBody)
        {
            TickEmpty();
            TickState();
            return;
        }

        grounded = ProbeGround();
        coyoteTimer = grounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        TickDashCooldown();

        if (floating)
        {
            TickFloat();
            TickState();
            return;
        }

        if (isKnocked)
        {
            TickKnockback();
            TickState();
            return;
        }

        if (isDashing)
        {
            TickDash();
            TickState();
            return;
        }

        if (dashBufferTimer > 0f && charge >= 1f && TryStartDash())
        {
            TickState();
            return;
        }

        Vector2 v = rb.linearVelocity;

        // 空气制动：空中水平方向持续 -g，和竖直方向的重力对称
        if (!grounded)
            v.x = Mathf.MoveTowards(v.x, 0f, GameConstants.HorizontalAirDrag * Time.fixedDeltaTime);

        if (controlLockTimer <= 0f)
        {
            float accel = grounded ? groundAccel : airAccel;
            if (grounded && onIce) accel *= GameConstants.IceAccelerationScale;

            // 站在队友身上时，队友的水平速度作为目标速度基底，驮着走才不会被摩擦力甩掉
            float target = moveInput * tierProvider.TargetSpeed;
            if (grounded && rideTarget != null) target += rideTarget.Velocity.x;
            v.x = StepTowards(v.x, target, accel);
        }

        // 用按住状态而不是抬手那一帧判断，否则漏掉的物理步会让可变跳跃高度时灵时不灵；
        // 再给一个最小起跳时间，避免「点一下」时跳跃在第一个物理步就被截成半高
        if (v.y > 0f) jumpElapsed += Time.fixedDeltaTime;
        if (!jumpHeld && !jumpCutApplied && v.y > 0f && jumpElapsed >= minJumpTime)
        {
            v.y *= jumpCutMultiplier;
            jumpCutApplied = true;
        }

        if (bufferTimer > 0f && coyoteTimer > 0f)
        {
            v.y = jumpVelocity;
            bufferTimer = 0f;
            coyoteTimer = 0f;
            jumpCutApplied = false;
            jumpElapsed = 0f;
        }

        rb.gravityScale = v.y < 0f ? fallGravityScale : riseGravityScale;
        v.y = Mathf.Max(v.y, -maxFallSpeed);

        rb.linearVelocity = v;

        TickState();
    }

    void TickClimb()
    {
        grounded = false;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0f, verticalInput * GameConstants.ClimbSpeed);
    }

    /// 空形态：无重力、无碰撞，靠输入自由漂浮，方便穿墙拿钥匙。
    void TickEmpty()
    {
        grounded = false;
        Vector2 v = rb.linearVelocity;
        v.x = StepTowards(v.x, moveInput * tierProvider.TargetSpeed, airAccel);
        v.y = verticalInput * GameConstants.WalkSpeed;
        rb.gravityScale = 0f;
        rb.linearVelocity = v;
    }

    void TickFloat()
    {
        Vector2 v = rb.linearVelocity;
        float targetY = (floatTargetY - rb.position.y) * 8f;
        v.y = Mathf.MoveTowards(v.y, targetY, 40f * Time.fixedDeltaTime);
        if (controlLockTimer <= 0f)
            v.x = StepTowards(v.x, moveInput * tierProvider.TargetSpeed, groundAccel);

        rb.gravityScale = 0f;
        rb.linearVelocity = v;
    }

    /// <summary>
    /// 进入冲刺态：位移恒定，速度由进入时的 V前 分三档。
    /// 反向 → Vd1，结束后归零；零级同向 → Vd1，结束后回到 V前；非零级同向 → 原速穿过。
    /// </summary>
    bool TryStartDash()
    {
        Vector2 direction;

        if (verticalInput > 0.01f)
        {
            direction = Vector2.up;
        }
        else if (verticalInput < -0.01f)
        {
            if (grounded) return false;   // 向下冲刺只允许在空中
            direction = Vector2.down;
        }
        else
        {
            direction = Vector2.right * (Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput) : facing);
        }

        float before = Vector2.Dot(rb.linearVelocity, direction);
        float v0Speed = GameConstants.TierCeiling(SpeedTier.V0);

        float dashSpeed;
        float exitSpeed;

        if (before < -0.001f)
        {
            dashSpeed = GameConstants.DashMinSpeed;
            exitSpeed = 0f;
        }
        else if (before <= v0Speed + 0.0001f)
        {
            dashSpeed = GameConstants.DashMinSpeed;
            exitSpeed = Mathf.Max(0f, before);
        }
        else
        {
            dashSpeed = before;
            exitSpeed = before;
        }

        dashVelocity = direction * dashSpeed;
        dashExitVelocity = direction * exitSpeed;
        dashTimer = Mathf.Max(0.01f, dashDistance) / Mathf.Max(0.01f, dashSpeed);
        isDashing = true;
        charge = 0f;
        refillArmed = false;
        dashBufferTimer = 0f;
        bufferTimer = 0f;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashVelocity;
        return true;
    }

    void TickDash()
    {
        dashTimer -= Time.fixedDeltaTime;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashVelocity;

        if (dashVelocity.y < 0f && grounded)
        {
            EndDash(true);
            return;
        }

        if (dashTimer > 0f) return;

        EndDash(true);
    }

    /// <summary>结束冲刺态。applyExit = 正常结束时把速度换成结束速度。</summary>
    void EndDash(bool applyExit = false)
    {
        isDashing = false;
        if (applyExit) rb.linearVelocity = dashExitVelocity;
        rb.gravityScale = rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;
    }

    /// <summary>
    /// 充能：接触地面或队友一次就"上弦"，之后计时自己跑完；
    /// 冲刺用掉后回到 0，需要重新接触才能再上弦。
    /// </summary>
    void TickDashCooldown()
    {
        playerContactTimer = Mathf.Max(0f, playerContactTimer - Time.fixedDeltaTime);
        if (grounded || playerContactTimer > 0f) refillArmed = true;

        if (refillArmed && charge < 1f)
            charge = Mathf.Min(1f, charge + Time.fixedDeltaTime / Mathf.Max(0.01f, dashCooldown));

        if (charge >= 1f) refillArmed = false;
    }

    void BeginKnockback(PlayerMotor attacker, Vector2 normal, Vector2 point)
    {
        if (isDashing) EndDash();

        // 只做水平击飞。方向以「攻击方 → 受击方」为准，不依赖物理后端给的法线朝向
        float dx = transform.position.x - attacker.transform.position.x;
        float dir = Mathf.Sign(dx);
        if (Mathf.Abs(dx) < 0.05f)
        {
            float dashDir = attacker.DashVelocity.x;
            dir = Mathf.Abs(dashDir) > 0.01f ? Mathf.Sign(dashDir) : 1f;
        }
        if (Mathf.Abs(normal.x) > 0.01f && Mathf.Sign(normal.x) != dir) normal = -normal;

        var impact = ImpactInfo.Build(attacker, this, normal, point);

        isKnocked = true;
        knockbackStage = 0;
        knockbackDir = dir;
        knockbackTimer = GameConstants.HorizontalPushDistance / tierProvider.SpeedFor(SpeedTier.V2);
        tierProvider.Force(SpeedTier.V2, knockbackTimer);

        // 击飞期间不接受跳/冲输入，清掉缓冲，避免硬直结束时补放一次旧输入
        bufferTimer = 0f;
        dashBufferTimer = 0f;
        jumpCutApplied = true;

        ImpactReceived?.Invoke(impact);
    }

    void TickKnockback()
    {
        knockbackTimer -= Time.fixedDeltaTime;

        if (knockbackTimer <= 0f)
        {
            if (knockbackStage == 0)
            {
                knockbackStage = 1;
                knockbackTimer = GameConstants.HorizontalPushFollowDistance / tierProvider.SpeedFor(SpeedTier.V1);
                tierProvider.Force(SpeedTier.V1, knockbackTimer);
            }
            else
            {
                isKnocked = false;
                // 保留 v1 速度交给正常减速，再滑约 1 格，凑成横碰的 3 + 2 + 1
                return;
            }
        }

        float speed = knockbackStage == 0
            ? tierProvider.SpeedFor(SpeedTier.V2)
            : tierProvider.SpeedFor(SpeedTier.V1);

        Vector2 v = rb.linearVelocity;
        v.x = knockbackDir * speed;
        rb.gravityScale = v.y < 0f ? fallGravityScale : riseGravityScale;
        rb.linearVelocity = v;
    }

    /// 调试用：不改变速度，只把挡位压到指定值。
    public void ForceTier(SpeedTier tier, float duration)
    {
        tierProvider.Force(tier, Mathf.Max(0.05f, duration));
    }

    /// B 被收进 A 的大炮时暂停物理，但 Update 仍采样输入，射出时再用。
    public void SetSuspended(bool value)
    {
        suspended = value;
        if (value)
        {
            isDashing = false;
            isKnocked = false;
            climbing = false;
            floating = false;
            dashBufferTimer = 0f;
            bufferTimer = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        rb.simulated = !value;
        body.enabled = !value && !emptyBody;
    }

    /// A 的 | 形态：贴住竖绳后由形态控制器接管升降。
    public void BeginClimb()
    {
        if (suspended || climbing) return;

        if (isDashing) EndDash();
        climbing = true;
        isKnocked = false;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
    }

    public void EndClimb()
    {
        if (!climbing) return;

        climbing = false;
        rb.gravityScale = rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;
    }

    /// 攀附时把 A 锁在绳上；不走 Teleport，避免清掉挡位和输入。
    public void SetClimbPosition(float x, float y)
    {
        if (!climbing) return;

        rb.position = new Vector2(x, y);
        transform.position = rb.position;
    }

    /// A 的 _ 形态：漂浮在液面，关闭重力并保留水平操作。
    public void SetFloating(bool value, float surfaceY)
    {
        floating = value;
        floatTargetY = surfaceY + HalfHeight;
        if (!value) return;

        if (isDashing) EndDash();
        isKnocked = false;
        climbing = false;
        rb.gravityScale = 0f;
    }

    /// A 的空形态：身体改成触发器，可穿墙但仍会触发钥匙、岩浆等区域。
    public void SetEmpty(bool value)
    {
        emptyBody = value;
        if (body != null) body.isTrigger = value;

        if (value)
        {
            if (isDashing) EndDash();
            isKnocked = false;
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
        else if (!floating && !climbing)
        {
            rb.gravityScale = rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;
        }
    }

    /// 绳、地面绳等材质把玩家弹开时调用：结束冲刺 / 击飞，按法线方向给速度并压上新挡位。
    public void ReceiveBounce(Vector2 normal, SpeedTier tier, float duration)
    {
        if (isDashing) EndDash();

        if (normal.sqrMagnitude < 0.0001f)
            normal = Velocity.sqrMagnitude > 0.0001f ? -Velocity : Vector2.up;
        normal.Normalize();

        SpeedTier capped = (SpeedTier)Mathf.Min((int)tier, (int)SpeedTier.V4);
        float speed = tierProvider.SpeedFor(capped);

        isKnocked = false;
        knockbackStage = 0;
        knockbackTimer = 0f;
        knockbackDir = 0f;

        Vector2 v = normal * speed;
        rb.gravityScale = v.y < 0f ? fallGravityScale : riseGravityScale;
        rb.linearVelocity = v;
        tierProvider.Force(capped, Mathf.Max(0.05f, duration));

        bufferTimer = 0f;
        dashBufferTimer = 0f;
        jumpCutApplied = true;
    }

    float StepTowards(float current, float target, float accel)
    {
        bool turning = Mathf.Abs(target) > 0.01f
                       && Mathf.Sign(target) != Mathf.Sign(current)
                       && Mathf.Abs(current) > 0.1f;
        if (turning) accel *= turnAccelMultiplier;
        return Mathf.MoveTowards(current, target, accel * Time.fixedDeltaTime);
    }

    bool ProbeGround()
    {
        rideTarget = null;
        onIce = false;

        // 空形态的身体是触发器，穿墙期间不再认地面，避免踩空气起跳
        if (emptyBody) return false;

        Bounds b = body.bounds;
        var probeSize = new Vector2(b.size.x * 0.9f, 0.04f);
        var origin = new Vector2(b.center.x, b.min.y + 0.02f);

        int count = Physics2D.BoxCastNonAlloc(origin, probeSize, 0f, Vector2.down,
                                              groundHits, groundProbeDistance, groundMask);

        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i].collider;
            if (hit == null || hit.isTrigger || hit.attachedRigidbody == rb) continue;

            found = true;
            if (hit.TryGetComponent(out IceSurface _)) onIce = true;
            if (hit.TryGetComponent(out PlayerMotor other) && other != this) rideTarget = other;
        }
        return found;
    }

    void TickState()
    {
        MotionState next;
        if (climbing)
            next = MotionState.Climb;
        else if (floating)
            next = MotionState.Swim;
        else if (isKnocked)
            next = MotionState.Hit;
        else if (isDashing)
            next = MotionState.Dash;
        else if (!grounded)
            next = rb.linearVelocity.y > 0.05f ? MotionState.Jump : MotionState.Fall;
        else if (Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(rb.linearVelocity.x) > 0.1f)
            next = MotionState.Move;
        else
            next = MotionState.Idle;

        SetState(next);
    }

    void SetState(MotionState next)
    {
        if (next == State) return;

        var previous = State;
        State = next;
        StateChanged?.Invoke(previous, next);
    }

    void OnCollisionEnter2D(Collision2D c) => HandlePlayerContact(c, true);

    // 已经贴在一起时再起冲不会有 Enter 事件，所以 Stay 也要接
    void OnCollisionStay2D(Collision2D c) => HandlePlayerContact(c, false);

    /// <summary>
    /// 双人碰撞 = 动量交换：速度矢量 + 冲刺态整体互换。
    /// 冲刺态带着"什么速度、还剩多少距离、结束后是什么速度"一起交出去。
    /// </summary>
    void HandlePlayerContact(Collision2D c, bool entering)
    {
        if (!c.collider.TryGetComponent(out PlayerMotor other)) return;

        playerContactTimer = 0.12f;   // 碰到队友 = 充能上弦

        if (Time.time < nextCollisionTime) return;
        if (Time.time < other.nextCollisionTime) return;

        TryMarkRedHot(other, c);
        SwapStateWith(other);

        nextCollisionTime = Time.time + GameConstants.CollisionCooldown;
        other.nextCollisionTime = nextCollisionTime;
    }

    /// <summary>
    /// 红热判定：Vp = 同向相减、反向相加（都用碰撞前、沿碰撞轴的分量）。
    /// Vp 超过门槛就把"参与碰撞的那条边"点亮；热一期间再撞变成永久的红热二。
    /// </summary>
    void TryMarkRedHot(PlayerMotor other, Collision2D c)
    {
        if (!CanBeRedHot) return;

        Vector2 axis = c.contactCount > 0 ? c.GetContact(0).normal : Vector2.right;
        if (Mathf.Abs(axis.x) < 0.5f && Mathf.Abs(axis.y) < 0.5f) return;

        float a = Vector2.Dot(preCollisionVelocity, axis);
        float b = Vector2.Dot(other.preCollisionVelocity, axis);

        float a2 = a * a;
        float b2 = b * b;
        float vp = a * b > 0f ? Mathf.Abs(a2 - b2) : a2 + b2;

        float threshold = tierProvider.SpeedFor((SpeedTier)Mathf.Clamp(redHotTierIndex, 0, 4));
        if (vp < threshold * threshold) return;

        int index = EdgeFor(axis);
        SetEdge(index, edgeStates[index] == EdgeState.Hot1 ? EdgeState.Hot2 : EdgeState.Hot1);
    }

    void SwapStateWith(PlayerMotor other)
    {
        Vector2 myVelocity = rb.linearVelocity;
        Vector2 otherVelocity = other.rb.linearVelocity;

        DashSnapshot myDash = CaptureDash();
        DashSnapshot otherDash = other.CaptureDash();

        if (isDashing) EndDash(false);
        if (other.isDashing) other.EndDash(false);

        rb.linearVelocity = otherVelocity;
        other.rb.linearVelocity = myVelocity;

        ApplyDash(otherDash);
        other.ApplyDash(myDash);
    }

    /// <summary>冲刺态快照：速度、剩余时长、结束速度。</summary>
    public struct DashSnapshot
    {
        public bool active;
        public Vector2 velocity;
        public float timer;
        public Vector2 exitVelocity;
    }

    public DashSnapshot CaptureDash()
    {
        return new DashSnapshot
        {
            active = isDashing,
            velocity = dashVelocity,
            timer = dashTimer,
            exitVelocity = dashExitVelocity
        };
    }

    public void ApplyDash(DashSnapshot snapshot)
    {
        if (!snapshot.active)
        {
            isDashing = false;
            dashTimer = 0f;
            return;
        }

        isDashing = true;
        dashVelocity = snapshot.velocity;
        dashExitVelocity = snapshot.exitVelocity;
        dashTimer = snapshot.timer;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashVelocity;
    }

    public void Teleport(Vector2 position)
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
        transform.position = position;

        moveInput = 0f;
        verticalInput = 0f;
        facing = 1f;
        jumpPressed = false;
        jumpHeld = false;
        downPressed = false;
        dashPressed = false;
        jumpCutApplied = false;
        jumpElapsed = 0f;
        dashBufferTimer = 0f;
        bufferTimer = 0f;
        coyoteTimer = 0f;
        controlLockTimer = 0f;
        isDashing = false;
        dashTimer = 0f;
        charge = 1f;
        refillArmed = false;
        isKnocked = false;
        knockbackStage = 0;
        knockbackTimer = 0f;
        knockbackDir = 0f;
        rideTarget = null;
        climbing = false;
        floating = false;
        suspended = false;
        rb.simulated = true;
        if (body != null) body.enabled = true;
        rb.gravityScale = riseGravityScale;
        tierProvider.Clear();
        SetState(MotionState.Idle);
    }

    void ReadInput()
    {
        jumpPressed = false;
        downPressed = false;
        dashPressed = false;
        moveInput = 0f;
        verticalInput = 0f;

        var kb = Keyboard.current;
        if (kb == null) return;

        Key left, right, up, down, jump, dash;
        if (scheme == Scheme.WASD)
        {
            left = Key.A;
            right = Key.D;
            up = Key.W;
            down = Key.S;
            jump = Key.Space;
            dash = Key.LeftShift;
        }
        else
        {
            left = Key.LeftArrow;
            right = Key.RightArrow;
            up = Key.UpArrow;
            down = Key.DownArrow;
            jump = Key.Slash;
            dash = Key.Period;
        }

        var l = kb[left];
        var r = kb[right];
        var u = kb[up];
        var d = kb[down];
        var j = kb[jump];
        var s = kb[dash];

        moveInput = (r.isPressed ? 1f : 0f) - (l.isPressed ? 1f : 0f);
        verticalInput = (u.isPressed ? 1f : 0f) - (d.isPressed ? 1f : 0f);
        if (Mathf.Abs(moveInput) > 0.01f) facing = Mathf.Sign(moveInput);

        jumpPressed = j.wasPressedThisFrame;
        jumpHeld = j.isPressed;
        downPressed = d.wasPressedThisFrame;
        dashPressed = s.wasPressedThisFrame;
        if (s.wasPressedThisFrame) dashBufferTimer = dashBufferTime;
    }
}
