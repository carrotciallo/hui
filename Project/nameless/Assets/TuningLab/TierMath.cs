using UnityEngine;

/// <summary>
/// 纯数学：格 ↔ 速度 ↔ 等级。没有任何状态，方便对照与单测。
/// 约定：1 格 = 1 个世界单位；v² 是唯一的桥梁，永远不对玩家开根号。
/// </summary>
public static class TierMath
{
    /// <summary>从静止下落 cells 格之后的速度。</summary>
    public static float SpeedForCells(float cells, float g)
        => Mathf.Sqrt(2f * Mathf.Max(0f, g) * Mathf.Max(0f, cells));

    /// <summary>某个速度折算成"相当于下落了多少格"。</summary>
    public static float CellsForSpeed(float speed, float g)
        => g <= 0.0001f ? 0f : speed * speed / (2f * g);

    /// <summary>
    /// 按门槛数组把"折算格数"映射成等级。
    /// 约定：**门槛值是它下面那一级的速度上限**（v0 是零级的最大值）。
    /// 所以恰好等于门槛仍停在低一级，必须严格超过才升一级。
    /// bounds = {4,8,12} 时：4.00 → v0，4.01 → v1。
    /// </summary>
    public static int TierForCells(float cells, float[] bounds)
    {
        int tier = 0;
        if (bounds == null) return tier;
        for (int i = 0; i < bounds.Length; i++)
            if (cells > bounds[i] + 0.0001f)
                tier = i + 1;
        return tier;
    }

    /// <summary>
    /// 让抛物线顶点恰好达到 height 格的起跳初速。
    /// 半隐式欧拉（先积分速度再积分位置）会让顶点比连续解低 v0*dt/2，
    /// 这里补偿掉，否则"跳高 2 格"实际只有 1.86 格，上不去 2 格台。
    /// </summary>
    public static float JumpVelocity(float height, float g, float dt)
    {
        float h = Mathf.Max(0f, height);
        if (g <= 0.0001f) return 0f;
        float a = g * dt;
        return 0.5f * (a + Mathf.Sqrt(a * a + 8f * g * h));
    }
}
