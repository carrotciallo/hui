#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 无头自检：验证 v² ↔ 格 的换算、以及"让顶点正好到 h 格"的起跳初速补偿。
/// 菜单 GameJam → Run Speed Tier Self Check，或 -executeMethod TuningLabSelfCheck.Run。
/// </summary>
public static class TuningLabSelfCheck
{
    [MenuItem("GameJam/Run Speed Tier Self Check")]
    public static void Run()
    {
        var sb = new StringBuilder();
        int fails = 0;

        sb.AppendLine("=== 1. 起跳初速 → 实际顶点（半隐式欧拉模拟）===");
        foreach (float g in new[] { 25f, 50f, 120f })
        {
            foreach (float h in new[] { 1f, 2f, 2.2f, 3f, 4f })
            {
                float v0 = TierMath.JumpVelocity(h, g, 0.02f);
                float apex = SimulateApex(v0, g, 0.02f);
                bool ok = Mathf.Abs(apex - h) < 0.02f;
                if (!ok) fails++;
                sb.AppendLine(string.Format("  g={0,5:0}  h={1:0.00}  v0={2:0.00}  apex={3:0.000}  {4}",
                    g, h, v0, apex, ok ? "OK" : "FAIL"));
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== 2. 格 ↔ 速度 往返 ===");
        foreach (float c in new[] { 0f, 1f, 2f, 4f, 6f, 8f, 10f })
        {
            float v = TierMath.SpeedForCells(c, 50f);
            float back = TierMath.CellsForSpeed(v, 50f);
            bool ok = Mathf.Abs(back - c) < 0.001f;
            if (!ok) fails++;
            sb.AppendLine(string.Format("  {0:0.0} 格 → v={1:0.00} → {2:0.000} 格  {3}",
                c, v, back, ok ? "OK" : "FAIL"));
        }

        sb.AppendLine();
        sb.AppendLine("=== 3. 等级边界（bounds = 3/6/9/12 格，含等号）===");
        float[] bounds = { 3f, 6f, 9f, 12f };
        foreach (float c in new[] { 0f, 2.99f, 3f, 5.99f, 6f, 5.99f, 9f, 11.99f, 12f, 15f })
        {
            int t = TierMath.TierForCells(c, bounds);
            sb.AppendLine(string.Format("  {0,5:0.00} 格 → v{1}", c, t));
        }

        sb.AppendLine();
        sb.AppendLine("=== 4. 关键冲突：站跳落地 ===");
        foreach (float g in new[] { 25f, 50f, 120f })
        {
            float v0 = TierMath.JumpVelocity(2f, g, 0.02f);
            float cells = TierMath.CellsForSpeed(v0, g);
            int tier = TierMath.TierForCells(cells, bounds);
            sb.AppendLine(string.Format("  g={0,4:0}  站跳 2 格 落地 v={1,6:0.00}  折算 {2:0.000} 格 → v{3} {4}",
                g, v0, cells, tier, tier >= 1 ? "  ← 重力怎么改都一样，永远卡在门槛上" : ""));
        }

        sb.AppendLine();
        sb.AppendLine(fails == 0 ? "全部通过。" : ("失败 " + fails + " 项。"));
        Debug.Log("[TuningLabSelfCheck]\n" + sb);
    }

    static float SimulateApex(float v0, float g, float dt)
    {
        float v = v0, y = 0f, apex = 0f;
        for (int i = 0; i < 5000; i++)
        {
            v -= g * dt;
            y += v * dt;
            if (y > apex) apex = y;
            if (y < 0f) break;
        }
        return apex;
    }
}
#endif
