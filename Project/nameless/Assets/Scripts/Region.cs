using UnityEngine;

/// 一个关卡区域的根节点。Index 即关卡号：0 = L0，10 = L10。
public sealed class Region : MonoBehaviour
{
    const float SpawnSpread = 0.75f;

    [SerializeField] int index;
    [SerializeField] string label;
    [SerializeField] Transform spawn;
    [SerializeField] Rect area;

    public int Index => index;
    public string Label => label;
    public Transform Spawn => spawn;
    public Rect Area => area;
    public Vector2 SpawnPosition => spawn != null ? spawn.position : transform.position;

    /// 只比较 x：掉落死亡的位置在 y 上已经出了世界，仍然要归到水平范围内。
    public bool ContainsX(float x) => area.width > 0f && x >= area.xMin && x <= area.xMax;

    /// 两名玩家并排出生，避免叠在同一格互相弹开。
    public Vector2 SpawnPositionFor(int slot)
    {
        float offset = slot == 0 ? -SpawnSpread : SpawnSpread;
        return SpawnPosition + new Vector2(offset, 0f);
    }

    public void Configure(int regionIndex, string regionLabel, Transform spawnTransform, Rect regionArea)
    {
        index = regionIndex;
        label = regionLabel;
        spawn = spawnTransform;
        area = regionArea;
    }
}
