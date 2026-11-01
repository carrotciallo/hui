using UnityEngine;

public static class GameConstants
{
    public const float PixelsPerUnit = 16f;
    public const float TileSize = 1f;
    public const float PlayerPixels = 13f;
    public const float PlayerSize = PlayerPixels / PixelsPerUnit;

    public const float Gravity = 9.81f;

    public const float JumpHeight = 2f;
    public const float JumpDistance = 3f;
    public const float RiseGravityScale = 3.2f;
    public const float FallGravityScale = 5.2f;
    // v = sqrt(2 * 9.81 * 3.2 * 2) ≈ 11.2；配 4.7 的走速，滞空约 0.64s，跳远约 3 格。
    public const float JumpVelocity = 11.2f;
    public const float WalkSpeed = 4.7f;

    public const float DashCooldown = 0.5f;
    public const float DashHorizontalDistance = 3f;
    public const float DashUpDistance = 6f;
    public const float DashDownDistance = 3f;

    public const float HorizontalPushDistance = 3f;
    public const float HorizontalPushFollowDistance = 2f;
    public const float HorizontalPushBufferDistance = 1f;

    // P4 材质阈值
    public const SpeedTier RopePassTier = SpeedTier.V2;
    public const SpeedTier RopeBounceMinTier = SpeedTier.V3;
    public const float RopeBounceDuration = 0.35f;
    public const float GroundRopeDropDuration = 0.2f;
    public const SpeedTier IceBreakTier = SpeedTier.V2;
    public const SpeedTier WallBreakTier = SpeedTier.V2;
    public const SpeedTier GateBreakTier = SpeedTier.V2;
    public const int GateBreakMass = 2;
    public const int PortalKeyCost = 1;
    public const float KeyCarryHeight = 0.8f;
    // 冰面把地面加速度压到 30%，起步更快，同时保留明显滑行
    public const float IceAccelerationScale = 0.3f;

    // P5 A 形态
    public const float ClimbSpeed = 4f;
    public const float CannonLaunchDuration = 0.35f;
    public const SpeedTier ConverterMinTier = SpeedTier.V1;
    public const float ConverterCooldown = 0.2f;
    public const float EmptyFormDuration = 2.5f;
}
