using System;
using UnityEngine;

public enum SpeedTier { V0 = 0, V1 = 1, V2 = 2, V3 = 3, V4 = 4 }

/// 挡位来源的统一接口。默认实现是行为申请挡位；也可以换成速度推导挡位的实现。
public interface ISpeedTierProvider
{
    SpeedTier Tier { get; }
    float TargetSpeed { get; }
    void Request(SpeedTier tier, float duration);
    void Tick(float deltaTime);
}

/// 挡位是行为申请的状态，速度由挡位给出。
/// 想换成“速度推导挡位”时，继承本类并重写 Tick / TargetSpeed，PlayerMotor 不需要改。
public class SpeedTierProvider : MonoBehaviour, ISpeedTierProvider
{
    [SerializeField] SpeedTier initialTier = SpeedTier.V0;
    [SerializeField] float[] tierSpeeds = { 4.7f, 15f, 18f, 21f, 24f };

    SpeedTier tier;
    float holdTimer;

    public SpeedTier Tier => tier;
    public virtual float TargetSpeed => SpeedFor(tier);
    public float HoldRemaining => holdTimer;
    public event Action<SpeedTier, SpeedTier> TierChanged;

    void Awake() => tier = initialTier;

    void Reset() => tierSpeeds = new[] { 4.7f, 15f, 18f, 21f, 24f };

    public virtual float SpeedFor(SpeedTier value)
    {
        if (tierSpeeds == null || tierSpeeds.Length == 0) return 0f;
        int index = Mathf.Clamp((int)value, 0, tierSpeeds.Length - 1);
        return tierSpeeds[index];
    }

    public virtual void Request(SpeedTier requested, float duration)
    {
        // 高挡位优先；同挡位刷新持续时间
        if (holdTimer > 0f && (int)requested < (int)tier) return;

        SetTier(requested);
        holdTimer = Mathf.Max(0f, duration);
    }

    /// 无视优先级的强制挡位，用于击飞这种必须覆盖当前冲刺档的场景。
    public virtual void Force(SpeedTier forced, float duration)
    {
        SetTier(forced);
        holdTimer = Mathf.Max(0f, duration);
    }

    public virtual void Tick(float deltaTime)
    {
        if (holdTimer <= 0f) return;

        holdTimer -= deltaTime;
        if (holdTimer <= 0f) SetTier(SpeedTier.V0);
    }

    public void Clear()
    {
        holdTimer = 0f;
        SetTier(SpeedTier.V0);
    }

    protected void SetTier(SpeedTier next)
    {
        if (next == tier) return;

        SpeedTier previous = tier;
        tier = next;
        TierChanged?.Invoke(previous, next);
    }

    public string TierLabel => tier switch
    {
        SpeedTier.V0 => "v0 常",
        SpeedTier.V1 => "v1 黄",
        SpeedTier.V2 => "v2 红",
        SpeedTier.V3 => "v3 蓝",
        SpeedTier.V4 => "v4 紫",
        _ => "?"
    };

    public Color TierColor => ColorFor(tier);

    public static Color ColorFor(SpeedTier value) => value switch
    {
        SpeedTier.V0 => new Color(0.85f, 0.87f, 0.92f),
        SpeedTier.V1 => new Color(1f, 0.85f, 0.25f),
        SpeedTier.V2 => new Color(1f, 0.35f, 0.3f),
        SpeedTier.V3 => new Color(0.35f, 0.65f, 1f),
        SpeedTier.V4 => new Color(0.75f, 0.45f, 1f),
        _ => Color.white
    };
}
