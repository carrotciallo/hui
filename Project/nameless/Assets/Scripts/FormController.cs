using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AForm { Normal, Cannon, Converter, Climb, Raft, Empty }

/// A（大块）的形态控制器：普通 / u 大炮 / L 转换器 / | 攀附 / _ 木筏 / 空。
/// 所有形态下 A 的走、跳、冲刺保持不变；形态只改变 A 对 B 和场景的交互。
[RequireComponent(typeof(PlayerMotor))]
public sealed class FormController : MonoBehaviour
{
    static readonly Color CannonColor = new Color(1f, 0.6f, 0.2f);
    static readonly Color ConverterColor = new Color(0.3f, 0.9f, 0.95f);
    static readonly Color ClimbColor = new Color(0.4f, 0.9f, 0.45f);
    static readonly Color RaftColor = new Color(0.8f, 0.6f, 0.35f);

    [SerializeField] AForm form = AForm.Normal;

    PlayerMotor motor;
    SpriteRenderer body;
    PlayerLife life;
    Color normalColor;

    PlayerMotor carried;
    SpeedTier carriedTier;
    float emptyTimer;

    VerticalRope rope;
    BoxCollider2D converterZone;
    readonly Dictionary<PlayerMotor, float> converterCooldown = new Dictionary<PlayerMotor, float>();

    public AForm CurrentForm => form;
    public bool IsRaft => form == AForm.Raft;
    public PlayerMotor Carried => carried;
    public SpeedTier CarriedTier => carriedTier;

    public string FormLabel => form switch
    {
        AForm.Cannon => "u 大炮",
        AForm.Converter => "L 转换",
        AForm.Climb => "| 攀附",
        AForm.Raft => "_ 木筏",
        AForm.Empty => "空",
        _ => "普通"
    };

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        body = GetComponent<SpriteRenderer>();
        if (body != null) normalColor = body.color;
        CreateConverterZone();
    }

    void Start()
    {
        life = GetComponent<PlayerLife>();
        if (life != null)
        {
            life.Died += OnDied;
            life.Respawned += OnRespawned;
        }
    }

    void OnDestroy()
    {
        if (life != null)
        {
            life.Died -= OnDied;
            life.Respawned -= OnRespawned;
        }
    }

    void Update()
    {
        if (life != null && !life.IsAlive) return;

        HandleFormKeys();

        if (form == AForm.Empty)
        {
            emptyTimer -= Time.deltaTime;
            if (emptyTimer <= 0f) ExpireEmpty();
            return;
        }

        if (carried != null)
        {
            carried.transform.position = transform.position;

            if (form != AForm.Cannon) Eject();
            else if (WasPressed(carried, DashKey(carried))) Launch();
            return;
        }

        if (form == AForm.Cannon) TryAbsorbRider();
        else if (form == AForm.Climb) TickClimb();
    }

    public void SetForm(AForm next)
    {
        if (next == form)
        {
            ApplyFormState();
            return;
        }

        if (carried != null && next != AForm.Cannon) Eject();
        if (rope != null) DetachRope();
        if (form == AForm.Empty) EndEmpty();
        if (form == AForm.Raft && motor.IsFloating) motor.SetFloating(false, 0f);

        form = next;
        ApplyFormState();

        if (form == AForm.Empty) BeginEmpty();
    }

    /// 形态没变时也要重放一次外观和转换区，因为 PlayerLife 复活会把所有子碰撞体重新打开。
    void ApplyFormState()
    {
        if (converterZone != null) converterZone.enabled = form == AForm.Converter;
        UpdateVisual();
    }

    void HandleFormKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[Key.Digit1].wasPressedThisFrame) SetForm(AForm.Normal);
        else if (kb[Key.Digit2].wasPressedThisFrame) SetForm(AForm.Cannon);
        else if (kb[Key.Digit3].wasPressedThisFrame) SetForm(AForm.Converter);
        else if (kb[Key.Digit4].wasPressedThisFrame) SetForm(AForm.Climb);
        else if (kb[Key.Digit5].wasPressedThisFrame) SetForm(AForm.Raft);
        else if (kb[Key.Digit6].wasPressedThisFrame) SetForm(AForm.Empty);
    }

    // 大炮 -------------------------------------------------------------

    void TryAbsorbRider()
    {
        foreach (var other in PlayerMotor.All)
        {
            if (other == motor || other.RideTarget != motor) continue;
            if (!WasPressed(other, DownKey(other))) continue;

            carried = other;
            carriedTier = other.Tier;
            carried.transform.position = transform.position;
            SetVisible(other, false);
            other.SetSuspended(true);
            return;
        }
    }

    void Launch()
    {
        var b = carried;
        if (b == null) return;

        carried = null;
        Vector2 direction = LaunchDirection(b);
        var next = (SpeedTier)Mathf.Min((int)carriedTier + 1, (int)SpeedTier.V4);

        Vector2 position = (Vector2)transform.position
                           + direction * (motor.HalfHeight + b.HalfHeight + 0.1f);

        Release(b, position);
        b.ReceiveBounce(direction, next, GameConstants.CannonLaunchDuration);
    }

    void Eject()
    {
        var b = carried;
        if (b == null) return;

        carried = null;
        Vector2 position = (Vector2)transform.position + Vector2.up * (motor.HalfHeight + b.HalfHeight + 0.1f);
        var exitTier = carriedTier < SpeedTier.V1 ? SpeedTier.V1 : carriedTier;

        Release(b, position);
        b.ReceiveBounce(Vector2.up, exitTier, GameConstants.CannonLaunchDuration);
    }

    void Release(PlayerMotor b, Vector2 position)
    {
        SetVisible(b, true);
        b.SetSuspended(false);
        b.Teleport(position);
    }

    Vector2 LaunchDirection(PlayerMotor b)
    {
        if (Mathf.Abs(b.MoveInput) > 0.01f) return Vector2.right * Mathf.Sign(b.MoveInput);
        if (b.VerticalInput > 0.01f) return Vector2.up;
        if (b.VerticalInput < -0.01f) return Vector2.down;
        return Vector2.right * motor.Facing;
    }

    // L 转换器 ---------------------------------------------------------

    void CreateConverterZone()
    {
        var go = new GameObject("ConverterZone");
        go.transform.SetParent(transform, false);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        // 触发区比身体左右各宽 0.4、向下多 0.4；顶边略低于 A 顶面，避免 B 站着被转换
        float extend = 0.45f / GameConstants.PlayerSize;
        box.size = new Vector2(1f + 0.8f / GameConstants.PlayerSize, 1f + extend);
        box.offset = new Vector2(0f, -extend * 0.5f - 0.05f);
        box.enabled = false;

        var zone = go.AddComponent<ConverterZone>();
        zone.Configure(this);
        converterZone = box;
    }

    public void HandleConverterEnter(Collider2D other)
    {
        if (form != AForm.Converter) return;
        if (!other.TryGetComponent(out PlayerMotor player) || player == motor) return;
        if (player.Tier < GameConstants.ConverterMinTier) return;

        if (converterCooldown.TryGetValue(player, out float readyAt) && Time.time < readyAt) return;
        converterCooldown[player] = Time.time + GameConstants.ConverterCooldown;

        Vector2 velocity = player.ImpactVelocity;
        Vector2 direction;

        if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y))
        {
            direction = Vector2.up;
        }
        else
        {
            float side = Mathf.Sign(player.transform.position.x - transform.position.x);
            direction = Vector2.right * (Mathf.Abs(side) > 0.01f ? side : motor.Facing);
        }

        player.ReceiveBounce(direction, player.Tier, GameConstants.CannonLaunchDuration);
    }

    // | 攀附 -----------------------------------------------------------

    void TickClimb()
    {
        if (motor.IsClimbing)
        {
            if (WasPressed(motor, JumpKey(motor)) || IsHorizontalHeld(motor))
            {
                DetachRope();
                return;
            }

            if (rope != null)
            {
                float min = rope.SegmentBottom.y + motor.HalfHeight;
                float max = rope.SegmentTop.y - motor.HalfHeight;
                float y = min <= max
                    ? Mathf.Clamp(transform.position.y, min, max)
                    : rope.Center.y;
                motor.SetClimbPosition(rope.Center.x, y);
            }
            return;
        }

        if (!IsUpHeld(motor)) return;

        rope = NearestRope();
        if (rope == null) return;

        motor.Teleport(new Vector2(rope.Center.x, transform.position.y));
        motor.BeginClimb();
    }

    void DetachRope()
    {
        rope = null;
        motor.EndClimb();
    }

    VerticalRope NearestRope()
    {
        VerticalRope best = null;
        float bestDistance = 1.1f;

        foreach (var candidate in VerticalRope.All)
        {
            if (candidate == null) continue;
            float y = Mathf.Clamp(transform.position.y, candidate.SegmentBottom.y, candidate.SegmentTop.y);
            float distance = Vector2.Distance(transform.position, new Vector2(candidate.Center.x, y));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    // 空形态 -----------------------------------------------------------

    void BeginEmpty()
    {
        emptyTimer = GameConstants.EmptyFormDuration;
        motor.SetEmpty(true);
    }

    void EndEmpty()
    {
        emptyTimer = 0f;
        motor.SetEmpty(false);
    }

    void ExpireEmpty()
    {
        if (life != null) life.Kill(DeathCause.Hazard);
        else SetForm(AForm.Normal);
    }

    void OnDied(DeathCause cause)
    {
        if (carried != null) Eject();
        if (form != AForm.Normal) SetForm(AForm.Normal);
    }

    void OnRespawned(Vector2 position) => SetForm(AForm.Normal);

    // 表现 -------------------------------------------------------------

    void UpdateVisual()
    {
        if (body == null) return;

        body.color = form switch
        {
            AForm.Cannon => CannonColor,
            AForm.Converter => ConverterColor,
            AForm.Climb => ClimbColor,
            AForm.Raft => RaftColor,
            AForm.Empty => new Color(normalColor.r, normalColor.g, normalColor.b, 0.35f),
            _ => normalColor
        };
    }

    static void SetVisible(PlayerMotor player, bool visible)
    {
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visible;
    }

    static Key DownKey(PlayerMotor player) =>
        player.InputScheme == PlayerMotor.Scheme.WASD ? Key.S : Key.DownArrow;

    static Key DashKey(PlayerMotor player) =>
        player.InputScheme == PlayerMotor.Scheme.WASD ? Key.LeftShift : Key.Period;

    static Key JumpKey(PlayerMotor player) =>
        player.InputScheme == PlayerMotor.Scheme.WASD ? Key.Space : Key.RightAlt;

    static bool WasPressed(PlayerMotor player, Key key)
    {
        var kb = Keyboard.current;
        return kb != null && player != null && kb[key].wasPressedThisFrame;
    }

    static bool IsUpHeld(PlayerMotor player) =>
        player.VerticalInput > 0.01f;

    static bool IsHorizontalHeld(PlayerMotor player)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        return player.InputScheme == PlayerMotor.Scheme.WASD
            ? kb[Key.A].isPressed || kb[Key.D].isPressed
            : kb[Key.LeftArrow].isPressed || kb[Key.RightArrow].isPressed;
    }
}
