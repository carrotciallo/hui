using UnityEngine;

/// <summary>
/// A 的形态。由「哪几条边碎成了灵体」唯一决定，玩家不能选（Issue #14 形态清单，最终版本不再改动）。
/// </summary>
public enum LabForm
{
    Normal = 0,
    Cannon = 1,      // 碎 1 条：u 形大炮，口朝那条边，4 个朝向
    Converter = 2,   // 碎相邻 2 条：L 形转向器，4 个朝向
    Elevator = 3,    // 碎相对 2 条：= 或 ‖ 形电梯，2 个朝向
    Spear = 4,       // 碎 3 条：只剩一根，4 个朝向退化成 1 种矛
    Spirit = 5       // 全碎：灵体
}

/// <summary>
/// 碎边掩码 → 形态的纯函数，见 ADR-0005。
/// 位序和 LabPlayer.Edge 一致：上 0、下 1、左 2、右 3。
/// </summary>
public static class LabForms
{
    public const int TopBit = 1 << 0;
    public const int BottomBit = 1 << 1;
    public const int LeftBit = 1 << 2;
    public const int RightBit = 1 << 3;

    public static int BitOf(LabPlayer.Edge e) => 1 << (int)e;

    public static LabForm Of(int spiritMask)
    {
        switch (BitCount(spiritMask))
        {
            case 0: return LabForm.Normal;
            case 1: return LabForm.Cannon;
            case 2: return IsOppositePair(spiritMask) ? LabForm.Elevator : LabForm.Converter;
            case 3: return LabForm.Spear;
            default: return LabForm.Spirit;
        }
    }

    static bool IsOppositePair(int mask) =>
        mask == (TopBit | BottomBit) || mask == (LeftBit | RightBit);

    /// <summary>电梯的两种朝向：上下都碎 → 只剩左右两根竖杠 = ‖；左右都碎 → =。</summary>
    public static bool ElevatorIsVertical(int spiritMask) => spiritMask == (TopBit | BottomBit);

    /// <summary>碎 1 条时那条边就是炮口。</summary>
    public static LabPlayer.Edge MouthEdge(int spiritMask)
    {
        for (int i = 0; i < 4; i++)
            if ((spiritMask & (1 << i)) != 0) return (LabPlayer.Edge)i;
        return LabPlayer.Edge.Right;
    }

    /// <summary>只剩一根边时的朝向（矛不用朝向，读数里给个人看）。</summary>
    public static LabPlayer.Edge RemainingEdge(int spiritMask)
    {
        int present = (~spiritMask) & (TopBit | BottomBit | LeftBit | RightBit);
        return MouthEdge(present);
    }

    public static int BitCount(int mask)
    {
        int n = 0;
        for (int i = 0; i < 4; i++)
            if ((mask & (1 << i)) != 0) n++;
        return n;
    }

    // ---- 读数 ----

    public static string Label(int spiritMask, LabForm form)
    {
        switch (form)
        {
            case LabForm.Cannon: return "u 大炮 口朝" + Arrow(SpiritAxis(spiritMask));
            case LabForm.Converter: return "L 转向器 开口" + Corner(spiritMask);
            case LabForm.Elevator: return ElevatorIsVertical(spiritMask) ? "‖ 电梯（竖）" : "= 电梯（横）";
            case LabForm.Spear: return "矛（剩" + EdgeName(RemainingEdge(spiritMask)) + "）";
            case LabForm.Spirit: return "灵体";
            default: return "普通";
        }
    }

    /// <summary>掩码里唯一/最后一条碎边的单位轴。</summary>
    static Vector2 SpiritAxis(int spiritMask)
    {
        LabPlayer.Edge e = MouthEdge(spiritMask);
        switch (e)
        {
            case LabPlayer.Edge.Top: return Vector2.up;
            case LabPlayer.Edge.Bottom: return Vector2.down;
            case LabPlayer.Edge.Left: return Vector2.left;
            default: return Vector2.right;
        }
    }

    public static Vector2 EdgeNormal(LabPlayer.Edge e) => SpiritAxis(BitOf(e));

    static string Arrow(Vector2 axis)
    {
        if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y)) return axis.x > 0f ? "→" : "←";
        return axis.y > 0f ? "↑" : "↓";
    }

    static string Corner(int spiritMask)
    {
        bool up = (spiritMask & TopBit) != 0;
        bool down = (spiritMask & BottomBit) != 0;
        bool left = (spiritMask & LeftBit) != 0;
        bool right = (spiritMask & RightBit) != 0;

        if (up && left) return "↖";
        if (up && right) return "↗";
        if (down && left) return "↙";
        if (down && right) return "↘";
        return "·";
    }

    static string EdgeName(LabPlayer.Edge e)
    {
        switch (e)
        {
            case LabPlayer.Edge.Top: return "上";
            case LabPlayer.Edge.Bottom: return "下";
            case LabPlayer.Edge.Left: return "左";
            default: return "右";
        }
    }
}
