using System;
using UnityEngine;

public enum SpeedTier { V0 = 0, V1 = 1, V2 = 2, V3 = 3, V4 = 4 }

/// <summary>
/// 挡位是<b>速度的读数</b>，不是状态：速度是因，挡位只是标签。
///
/// 每自由下落 <see cref="GameConstants.TierSpacingCells"/> 格升一级，
/// v0..v3 是零级..三级的速度上限，严格超过才进下一级。
///
/// 想给玩家"三级"时，做法是给他三级对应的速度（<see cref="SpeedFor"/>），
/// 而不是往身上挂一个 buff —— 速度和挡位永远一致。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SpeedTierProvider : MonoBehaviour
{
    [SerializeField] Rigidbody2D body;

    SpeedTier lastTier = SpeedTier.V0;

    /// <summary>当前挡位，由刚体速度实时推导。</summary>
    public SpeedTier Tier => TierOf(body != null ? body.linearVelocity.magnitude : 0f);

    /// <summary>走路速度。读全局配置，没配置就用常量。</summary>
    public float TargetSpeed
    {
        get
        {
            var tuning = GameTuning.Current;
            return tuning != null ? tuning.walkSpeed : GameConstants.WalkSpeed;
        }
    }

    /// <summary>速度推导模型下没有"剩余持续时间"这个概念，保留接口给旧调用方。</summary>
    public float HoldRemaining => 0f;

    public event Action<SpeedTier, SpeedTier> TierChanged;

    void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
    }

    // ---- 换算 ----

    public static float CellsFor(float speed) => GameConstants.CellsForSpeed(speed);

    public static SpeedTier TierOf(float speed)
    {
        float cells = GameConstants.CellsForSpeed(speed);
        int tier = 0;
        for (int i = 0; i < 4; i++)
            if (cells > GameConstants.TierSpacingCells * (i + 1) + 0.0001f)
                tier = i + 1;
        return (SpeedTier)tier;
    }

    /// <summary>某一级的速度上限。给"三级"就是给这个速度。</summary>
    public float SpeedFor(SpeedTier tier) => GameConstants.TierCeiling(tier);

    // ---- 对外接口（旧调用方仍在用，语义已改成"给速度"） ----

    /// <summary>把速度设成该挡位上限。方向沿用当前速度，静止时不动。</summary>
    public void Request(SpeedTier tier, float duration) => InjectSpeed(tier);

    /// <inheritdoc cref="Request"/>
    public void Force(SpeedTier tier, float duration) => InjectSpeed(tier);

    /// <summary>速度推导模型下没有可清的"状态"。</summary>
    public void Clear() { }

    /// <summary>每物理步检查一次挡位是否跳变，供 TierChanged 订阅者使用。</summary>
    public void Tick(float deltaTime)
    {
        SpeedTier now = Tier;
        if (now == lastTier) return;
        SpeedTier previous = lastTier;
        lastTier = now;
        TierChanged?.Invoke(previous, now);
    }

    void InjectSpeed(SpeedTier tier)
    {
        if (body == null) return;

        Vector2 v = body.linearVelocity;
        if (v.sqrMagnitude < 0.0001f) return;   // 静止时没有方向，交给调用方自己设速度

        float speed = SpeedFor(tier);
        body.linearVelocity = v.normalized * speed;
    }

    // ---- 表现 ----

    public string TierLabel => Tier switch
    {
        SpeedTier.V0 => "零级",
        SpeedTier.V1 => "一级",
        SpeedTier.V2 => "二级",
        SpeedTier.V3 => "三级",
        SpeedTier.V4 => "四级",
        _ => "?"
    };

    public Color TierColor => ColorFor(Tier);

    public static Color ColorFor(SpeedTier value) => value switch
    {
        SpeedTier.V0 => new Color(0.85f, 0.87f, 0.92f),
        SpeedTier.V1 => new Color(1f, 0.85f, 0.25f),
        SpeedTier.V2 => new Color(1f, 0.55f, 0.15f),
        SpeedTier.V3 => new Color(0.35f, 0.85f, 1f),
        SpeedTier.V4 => Color.white,
        _ => Color.white
    };
}
