using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpeedTierProvider))]
public class PlayerMotor : MonoBehaviour
{
    public enum Scheme { WASD, Arrows }

    public enum MotionState { Idle, Move, Jump, Fall, Dash }

    [SerializeField] Scheme scheme = Scheme.WASD;

    [Header("Move")]
    [SerializeField] float groundAccel = 90f;
    [SerializeField] float airAccel = 45f;
    [SerializeField] float turnAccelMultiplier = 1.6f;

    [Header("Jump")]
    [SerializeField] float jumpVelocity = GameConstants.JumpVelocity;
    [SerializeField] float riseGravityScale = GameConstants.RiseGravityScale;
    [SerializeField] float fallGravityScale = GameConstants.FallGravityScale;
    [SerializeField] float maxFallSpeed = 20f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.1f;
    [SerializeField] float jumpCutMultiplier = 0.5f;

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
    public bool CanDash => !dashSpent && dashCooldownTimer <= 0f && !isDashing;
    public float DashCooldownRemaining => dashCooldownTimer;
    public SpeedTier Tier => tierProvider.Tier;
    public SpeedTierProvider SpeedTiers => tierProvider;
    public Vector2 Velocity => rb.linearVelocity;
    public Vector2 SpawnPosition { get; private set; }
    public event System.Action<MotionState, MotionState> StateChanged;

    Rigidbody2D rb;
    Collider2D body;
    SpeedTierProvider tierProvider;

    readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];

    float moveInput;
    float verticalInput;
    float facing = 1f;
    bool jumpPressed;
    bool jumpHeld;
    bool jumpCutApplied;
    float dashBufferTimer;

    float coyoteTimer;
    float bufferTimer;
    float controlLockTimer;
    bool grounded;

    bool isDashing;
    float dashTimer;
    Vector2 dashVelocity;
    float dashCooldownTimer;
    bool cooldownPending;
    bool dashSpent;

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

        grounded = ProbeGround();
        coyoteTimer = grounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        controlLockTimer = Mathf.Max(0f, controlLockTimer - Time.fixedDeltaTime);
        TickDashCooldown();

        if (isDashing)
        {
            TickDash();
            TickState();
            return;
        }

        if (dashBufferTimer > 0f && !dashSpent && dashCooldownTimer <= 0f && TryStartDash())
        {
            TickState();
            return;
        }

        Vector2 v = rb.linearVelocity;

        if (controlLockTimer <= 0f)
            v.x = StepTowards(v.x, moveInput * tierProvider.TargetSpeed, grounded ? groundAccel : airAccel);

        // 用按住状态而不是抬手那一帧判断，否则漏掉的物理步会让可变跳跃高度时灵时不灵
        if (!jumpHeld && !jumpCutApplied && v.y > 0f)
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
        }

        rb.gravityScale = v.y < 0f ? fallGravityScale : riseGravityScale;
        v.y = Mathf.Max(v.y, -maxFallSpeed);

        rb.linearVelocity = v;

        TickState();
    }

    bool TryStartDash()
    {
        Vector2 direction;
        float distance;

        if (verticalInput > 0.01f)
        {
            direction = Vector2.up;
            distance = dashUpDistance;
        }
        else if (verticalInput < -0.01f)
        {
            if (grounded) return false;
            direction = Vector2.down;
            distance = dashDownDistance;
        }
        else
        {
            direction = Vector2.right * (Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput) : facing);
            distance = dashHorizontalDistance;
        }

        float speed = tierProvider.SpeedFor(SpeedTier.V1);
        dashVelocity = direction * speed;
        dashTimer = distance / speed;
        isDashing = true;
        dashSpent = true;
        cooldownPending = false;
        dashCooldownTimer = 0f;
        dashBufferTimer = 0f;
        bufferTimer = 0f;
        tierProvider.Request(SpeedTier.V1, dashTimer);
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
            EndDash();
            return;
        }

        if (dashTimer > 0f) return;

        EndDash();
    }

    void EndDash()
    {
        isDashing = false;
        rb.gravityScale = rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;

        if (grounded) dashCooldownTimer = dashCooldown;
        else cooldownPending = true;
    }

    void TickDashCooldown()
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.fixedDeltaTime);
            if (dashCooldownTimer <= 0f) dashSpent = false;
        }

        if (cooldownPending && grounded)
        {
            cooldownPending = false;
            dashCooldownTimer = dashCooldown;
        }
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
        Bounds b = body.bounds;
        var probeSize = new Vector2(b.size.x * 0.9f, 0.04f);
        var origin = new Vector2(b.center.x, b.min.y + 0.02f);

        int count = Physics2D.BoxCastNonAlloc(origin, probeSize, 0f, Vector2.down,
                                              groundHits, groundProbeDistance, groundMask);

        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i].collider;
            if (hit != null && hit.attachedRigidbody != rb) return true;
        }
        return false;
    }

    void TickState()
    {
        MotionState next;
        if (isDashing)
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

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out PlayerMotor other)) return;

        // 只有横向撞击才算推挤；踩在队友头上是纵向接触，不该锁住横向操作
        if (c.contactCount > 0 && Mathf.Abs(c.GetContact(0).normal.x) < 0.5f) return;

        float otherSpeed = Mathf.Abs(other.rb.linearVelocity.x);
        float mySpeed = Mathf.Abs(rb.linearVelocity.x);

        // 被撞的一方短暂交出水平控制权，物理推挤才不会被下一帧的速度覆盖吃掉
        if (otherSpeed - mySpeed > pushSpeedThreshold)
            controlLockTimer = pushControlLockTime;
    }

    public void ResetTo(Vector2 position)
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
        jumpCutApplied = false;
        dashBufferTimer = 0f;
        bufferTimer = 0f;
        coyoteTimer = 0f;
        controlLockTimer = 0f;
        isDashing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        cooldownPending = false;
        dashSpent = false;
        rb.gravityScale = riseGravityScale;
        tierProvider.Clear();
        SetState(MotionState.Idle);
    }

    void ReadInput()
    {
        jumpPressed = false;
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
            jump = Key.RightAlt;
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
        if (s.wasPressedThisFrame) dashBufferTimer = dashBufferTime;
    }
}
