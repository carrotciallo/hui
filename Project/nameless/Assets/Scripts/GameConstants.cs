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
}
