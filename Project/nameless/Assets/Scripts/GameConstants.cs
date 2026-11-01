using UnityEngine;

public static class GameConstants
{
    public const float PixelsPerUnit = 16f;
    public const float TileSize = 1f;
    public const float PlayerPixels = 13f;
    public const float PlayerSize = PlayerPixels / PixelsPerUnit;

    // ---- 速度等级（1 格 = 1 世界单位 = 16px） ----
    // 挡位是速度的读数：每自由下落 TierSpacingCells 格升一级，
    // v0..v3 分别是零级..三级的速度上限（含该值），严格超过才进下一级。
    public const float Gravity = 50f;
    public const float GravityScale = Gravity / 9.81f;   // Rigidbody2D.gravityScale 的换算
    public const float TierSpacingCells = 4f;
    public const float MaxFallSpeed = 42f;               // 要能掉出三级（需 40）

    /// <summary>某一级的速度上限：√(2·g·间距·(tier+1))。四级没有上限，按五格给一个名义值。</summary>
    public static float TierCeiling(SpeedTier tier)
    {
        int steps = Mathf.Clamp((int)tier, 0, 4) + 1;
        return Mathf.Sqrt(2f * Gravity * TierSpacingCells * steps);
    }

    /// <summary>速度折算成"相当于下落了多少格"。</summary>
    public static float CellsForSpeed(float speed) => speed * speed / (2f * Gravity);

    // ---- 移动 / 跳跃 ----
    public const float WalkSpeed = 8f;
    public const float JumpHeight = 2.2f;
    public const float JumpDistance = 3f;
    // 重力对称：上升和下落用同一个倍率，否则站跳落地会白送能量
    public const float RiseGravityScale = GravityScale;
    public const float FallGravityScale = GravityScale;
    // √(2·50·2) = 14.14，半隐式欧拉会让顶点偏低，补偿到 14.2
    public const float JumpVelocity = 14.2f;

    /// <summary>让顶点正好到 height 格的起跳初速（补偿离散积分的损失）。</summary>
    public static float JumpVelocityForHeight(float height)
    {
        float h = Mathf.Max(0f, height);
        float dt = 0.02f;
        float a = Gravity * dt;
        return 0.5f * (a + Mathf.Sqrt(a * a + 8f * Gravity * h));
    }
    public const float HorizontalAirDrag = Gravity;   // 空中水平方向 -g，与竖直方向对称

    // ---- 冲刺态 ----
    public const float DashDistance = 4f;             // 冲刺态位移，恒定
    public const float DashOverV0Cells = 0.05f;       // Vd1 = 略大于 v0
    public const float DashCooldown = 0.5f;

    // 旧的三方向冲刺距离，等冲刺态改写完成后删除
    public const float DashHorizontalDistance = 3f;
    public const float DashUpDistance = 6f;
    public const float DashDownDistance = 3f;

    // 旧的推挤参数，等碰撞改成状态互换后删除
    public const float HorizontalPushDistance = 3f;
    public const float HorizontalPushFollowDistance = 2f;
    public const float HorizontalPushBufferDistance = 1f;

    /// <summary>Vd1：反向进入 / 零级进入时的冲刺速度，比 v0 高一点点。</summary>
    public static float DashMinSpeed => Mathf.Sqrt(2f * Gravity * (TierSpacingCells + DashOverV0Cells));

    // ---- 材质阈值（按挡位开门，规则不变） ----
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

    // ---- 双人碰撞：动量交换 ----
    public const float CollisionCooldown = 0.18f;

    // ---- P5 A 形态 ----
    public const float ClimbSpeed = 4f;
    public const float CannonLaunchDuration = 0.35f;
    public const SpeedTier ConverterMinTier = SpeedTier.V1;
    public const float ConverterCooldown = 0.2f;
    public const float EmptyFormDuration = 2.5f;
}
