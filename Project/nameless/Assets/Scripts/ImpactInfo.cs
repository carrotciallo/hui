using UnityEngine;

/// 表面被撞击时的统一响应。P4 的绳、冰、墙、门都返回这个枚举，避免每关写一套 if。
public enum SurfaceResponse { Pass, Bounce, Break, Accelerate, Stick, Die }

/// 一次撞击的完整上下文，供材质、热锻、震屏、音效消费。
public struct ImpactInfo
{
    public PlayerMotor Attacker;
    public PlayerMotor Target;
    public Vector2 Normal;
    public Vector2 Velocity;
    public SpeedTier Tier;
    public Vector2 Point;
}

public interface IImpactReceiver
{
    SurfaceResponse ReceiveImpact(in ImpactInfo impact);
}
