using UnityEngine;

/// 表面被撞击时的统一响应。P4 的绳、冰、墙、门都返回这个枚举，避免每关写一套 if。
public enum SurfaceResponse { Pass, Bounce, Break, Accelerate, Stick, Die }

/// 一次撞击的完整上下文，供材质、热锻、震屏、音效消费。
public struct ImpactInfo
{
    public PlayerMotor Attacker;
    public Component Target;
    public Vector2 Normal;
    public Vector2 Velocity;
    public SpeedTier Tier;
    public Vector2 Point;

    /// 用攻击方的当前状态补齐挡位与速度，避免各个物件各写一套取值。
    public static ImpactInfo Build(PlayerMotor attacker, Component target, Vector2 normal, Vector2 point)
    {
        return new ImpactInfo
        {
            Attacker = attacker,
            Target = target,
            Normal = normal,
            Velocity = attacker != null ? attacker.ImpactVelocity : Vector2.zero,
            Tier = attacker != null ? attacker.Tier : SpeedTier.V0,
            Point = point
        };
    }
}

public interface IImpactReceiver
{
    SurfaceResponse ReceiveImpact(in ImpactInfo impact);
}
