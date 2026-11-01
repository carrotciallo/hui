using System;
using System.Collections.Generic;
using UnityEngine;

/// 跨区域的运行状态。纯 C# 类，不依赖场景，死亡不重置。
public sealed class RunState
{
    public const int PlayerCount = 2;

    readonly HashSet<int> litPortals = new HashSet<int>();
    readonly HashSet<int> openGates = new HashSet<int>();
    readonly Vector2[] respawns = new Vector2[PlayerCount];
    readonly bool[] hasRespawn = new bool[PlayerCount];

    public int Keys { get; private set; }
    public int BMass { get; private set; } = 1;
    public int LastRegion { get; private set; }
    public int LitPortalCount => litPortals.Count;
    public int OpenedGateCount => openGates.Count;

    public void AddKey() => Keys++;

    public bool TryConsumeKey()
    {
        if (Keys <= 0) return false;
        Keys--;
        return true;
    }

    /// 幂等：同一个传送门重复点亮返回 false，不重复计数。
    public bool LightPortal(int id) => litPortals.Add(id);

    public bool IsPortalLit(int id) => litPortals.Contains(id);

    /// 过关门打开后跨区域、跨死亡保持打开；重复打开返回 false。
    public bool OpenGate(int id) => openGates.Add(id);

    public bool IsGateOpen(int id) => openGates.Contains(id);

    public void SetRespawn(int slot, Vector2 position)
    {
        if (!IsValidSlot(slot)) throw new ArgumentOutOfRangeException(nameof(slot));

        respawns[slot] = position;
        hasRespawn[slot] = true;
    }

    public bool TryGetRespawn(int slot, out Vector2 position)
    {
        if (IsValidSlot(slot) && hasRespawn[slot])
        {
            position = respawns[slot];
            return true;
        }

        position = default;
        return false;
    }

    public void SetBMass(int mass) => BMass = Mathf.Max(0, mass);

    public void SetLastRegion(int index) => LastRegion = index;

    public void Reset()
    {
        Keys = 0;
        BMass = 1;
        LastRegion = 0;
        litPortals.Clear();
        openGates.Clear();
        Array.Clear(hasRespawn, 0, hasRespawn.Length);
    }

    public static bool IsValidSlot(int slot) => slot >= 0 && slot < PlayerCount;
}
