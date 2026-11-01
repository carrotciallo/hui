#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 原自动验证脚本已作废：它断言的是被推翻的旧冲刺规则（悬停 / v1 顶端 / 两段式）。
/// 这里先放一个静态数值转储，保证编辑器程序集能编译、GameJam 菜单正常出现。
/// 新的回归测试待按现行规则（冲刺态、恒定位移、V前 三档）重写。
/// </summary>
public static class TuningLabPlayVerification
{
    [MenuItem("GameJam/Dump Tuning Values")]
    public static void Dump()
    {
        const float g = 50f;
        const float first = 4f;
        const float spacing = 4f;
        const float vd1Over = 0.05f;

        float v0 = TierMath.SpeedForCells(first, g);
        float v1 = TierMath.SpeedForCells(first + spacing, g);
        float v2 = TierMath.SpeedForCells(first + spacing * 2f, g);
        float v3 = TierMath.SpeedForCells(first + spacing * 3f, g);
        float vd1 = TierMath.SpeedForCells(first + vd1Over, g);

        Debug.Log(
            "[Tuning Dump]\n" +
            string.Format("  g={0}  首级={1} 格  间距={2} 格\n", g, first, spacing) +
            string.Format("  v0 = {0:0.00}  ({1} 格, 零级上限)\n", v0, first) +
            string.Format("  v1 = {0:0.00}  ({1} 格)\n", v1, first + spacing) +
            string.Format("  v2 = {0:0.00}  ({1} 格)\n", v2, first + spacing * 2f) +
            string.Format("  v3 = {0:0.00}  ({1} 格)\n", v3, first + spacing * 3f) +
            string.Format("  Vd1= {0:0.00}  ({1:0.00} 格, 略大于 v0)\n", vd1, first + vd1Over) +
            "  冲刺位移 = 4 格（恒定），时长 = 位移 / 冲刺速度");
    }
}
#endif
