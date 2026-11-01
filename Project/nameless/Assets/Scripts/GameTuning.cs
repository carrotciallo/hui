using UnityEngine;

/// <summary>
/// 全局手感参数。放在 <c>Assets/Resources/GameTuning.asset</c>，
/// 所有场景的玩家在 Awake 时读它并覆盖自己的序列化字段 —— 改这里，全部场景立刻生效。
///
/// 结构性的东西（等级怎么算、每级几格）不在这里，在 GameConstants。
/// </summary>
[CreateAssetMenu(menuName = "GameJam/Game Tuning", fileName = "GameTuning")]
public class GameTuning : ScriptableObject
{
    [Header("重力 / 跳跃")]
    public float jumpHeight = 2.2f;
    public float maxFallSpeed = 42f;
    public float riseGravityScale = GameConstants.GravityScale;
    public float fallGravityScale = GameConstants.GravityScale;

    [Header("移动")]
    public float walkSpeed = 8f;
    public float groundAccel = 90f;
    public float airAccel = 45f;
    [Tooltip("1 = 松手不减速（固定跳跃高度）；小于 1 = 点跳更矮")]
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 1f;

    [Header("冲刺")]
    public float dashDistance = 4f;
    public float dashCooldown = 0.5f;

    [Header("红热")]
    [Tooltip("Vp 判定的门槛取哪一级的边界（0 = 零级上限）")]
    public int redHotTierIndex = 1;
    public float redHotDuration = 1.5f;

    const string ResourcePath = "GameTuning";

    static GameTuning current;
    static bool loaded;

    /// <summary>
    /// 全局实例。没有资产时返回一个"代码默认值"的临时实例，
    /// 这样新物理在没建资产之前也已经生效，不会出现"改了像没改"。
    /// </summary>
    public static GameTuning Current
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                current = Resources.Load<GameTuning>(ResourcePath);
                if (current == null) current = CreateInstance<GameTuning>();
            }
            return current;
        }
    }

    /// <summary>测试或换场景时强制重读。</summary>
    public static void Reload()
    {
        loaded = false;
        current = null;
    }
}
