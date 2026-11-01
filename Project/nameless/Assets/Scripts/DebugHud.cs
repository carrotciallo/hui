using UnityEngine;
using UnityEngine.InputSystem;

public class DebugHud : MonoBehaviour
{
    [SerializeField] PlayerMotor p1;
    [SerializeField] PlayerMotor p2;
    [SerializeField] World world;

    GUIStyle labelStyle;
    float maxX1, maxUp1, maxX2, maxUp2;

    int selectedRegion;
    bool showRunState = true;

    public void Configure(PlayerMotor player1, PlayerMotor player2, World gameWorld)
    {
        p1 = player1;
        p2 = player2;
        world = gameWorld;
    }

    void Start() => ResetTracking();

    void Update()
    {
        var kb = Keyboard.current;

#if UNITY_EDITOR || DEBUG
        if (kb != null)
        {
            if (kb[Key.R].wasPressedThisFrame) ReturnToRespawn();
            if (kb[Key.F6].wasPressedThisFrame) showRunState = !showRunState;
            if (kb[Key.PageUp].wasPressedThisFrame) CycleRegion(-1);
            if (kb[Key.PageDown].wasPressedThisFrame) CycleRegion(1);
            if (kb[Key.Home].wasPressedThisFrame) TeleportToSelectedRegion();
            if (kb[Key.K].wasPressedThisFrame) GrantKeys();

            int tier = TierKeyPressed(kb);
            if (tier >= 0) ForceTier(tier);
        }
#endif

        Track(p1, ref maxX1, ref maxUp1);
        Track(p2, ref maxX2, ref maxUp2);
    }

    void Track(PlayerMotor p, ref float maxX, ref float maxUp)
    {
        if (p == null) return;

        Vector2 delta = (Vector2)p.transform.position - p.SpawnPosition;
        maxX = Mathf.Max(maxX, Mathf.Abs(delta.x));
        maxUp = Mathf.Max(maxUp, delta.y);
    }

    void CycleRegion(int delta)
    {
        if (world == null || world.Regions.Count == 0) return;
        selectedRegion = Mathf.Clamp(selectedRegion + delta, 0, world.Regions.Count - 1);
    }

    void TeleportToSelectedRegion()
    {
        if (world == null || world.Regions.Count == 0) return;

        var region = world.Regions[Mathf.Clamp(selectedRegion, 0, world.Regions.Count - 1)];
        if (region == null) return;

        RunStateHost.Instance?.State.SetLastRegion(region.Index);
        TeleportPlayer(p1, region.SpawnPositionFor(0));
        TeleportPlayer(p2, region.SpawnPositionFor(1));
        ResetTracking();
    }

    void ReturnToRespawn()
    {
        ReturnPlayer(p1);
        ReturnPlayer(p2);
        ResetTracking();
    }

    void ReturnPlayer(PlayerMotor motor)
    {
        if (motor == null) return;

        var life = motor.GetComponent<PlayerLife>();
        if (life != null) life.Teleport(life.RespawnPosition);
        else motor.Teleport(motor.SpawnPosition);
    }

    void TeleportPlayer(PlayerMotor motor, Vector2 position)
    {
        if (motor == null) return;

        var life = motor.GetComponent<PlayerLife>();
        if (life != null) life.Teleport(position);
        else motor.Teleport(position);
    }

    void GrantKeys()
    {
        GrantKey(p1);
        GrantKey(p2);
    }

    static int TierKeyPressed(Keyboard kb)
    {
        if (kb[Key.F1].wasPressedThisFrame) return 0;
        if (kb[Key.F2].wasPressedThisFrame) return 1;
        if (kb[Key.F3].wasPressedThisFrame) return 2;
        if (kb[Key.F4].wasPressedThisFrame) return 3;
        if (kb[Key.F5].wasPressedThisFrame) return 4;
        return -1;
    }

    void ForceTier(int tier)
    {
        var value = (SpeedTier)Mathf.Clamp(tier, 0, 4);

        // 材质测试用：给 3 秒挡位，靠按住方向键自己加速到目标速度
        if (p1 != null) p1.ForceTier(value, 3f);
        if (p2 != null) p2.ForceTier(value, 3f);
    }

    static void GrantKey(PlayerMotor motor)
    {
        if (motor == null) return;
        var carrier = motor.GetComponent<KeyCarrier>();
        if (carrier != null) carrier.PickUp();
    }

    void ResetTracking()
    {
        maxX1 = maxUp1 = maxX2 = maxUp2 = 0f;
    }

    void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true,
                alignment = TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = Color.white;
        }

        DrawPanel(10f, 10f, p1, "P1  WASD", maxX1, maxUp1);
        DrawPanel(10f, 150f, p2, "P2  方向键", maxX2, maxUp2);
        DrawRunState(10f, 292f);

#if UNITY_EDITOR || DEBUG
        GUI.Label(new Rect(10f, 424f, 620f, 24f),
                  "R 回重生点  |  PageUp/PageDown 选区域  |  Home 传送到选中区域  |  K 发钥匙  |  F1–F5 强制挡位 3s  |  F6 运行状态",
                  labelStyle);
        GUI.Label(new Rect(10f, 446f, 620f, 24f),
                  "A 按 1–6 选形态：普通 / u / L / | / _ / 空  |  1 格 = 1 单位 = 16px",
                  labelStyle);
#endif
    }

    void DrawPanel(float x, float y, PlayerMotor p, string title, float maxX, float maxUp)
    {
        GUI.Box(new Rect(x, y, 370f, 130f), GUIContent.none);

        if (p == null)
        {
            GUI.Label(new Rect(x + 10f, y + 8f, 350f, 24f), title + "  (未绑定)", labelStyle);
            return;
        }

        var tier = p.SpeedTiers;
        string color = ColorUtility.ToHtmlStringRGB(tier.TierColor);
        Vector2 v = p.Velocity;
        var carrier = p.GetComponent<KeyCarrier>();
        string hasKey = carrier != null && carrier.HasKey ? "有" : "无";
        var form = p.GetComponent<FormController>();
        string formLine = form != null
            ? $"\n形态: {form.FormLabel}   携带: {(form.Carried != null ? "B" : "无")}"
            : "";

        string text =
            $"{title}\n" +
            $"挡位: <color=#{color}>{tier.TierLabel}</color>   目标速度: {tier.TargetSpeed:F1}\n" +
            $"速度: {v.x:F1}, {v.y:F1}   合速度: {v.magnitude:F1}\n" +
            $"状态: {p.State}   地面: {p.Grounded}   冲刺: {p.IsDashing}   冷却: {p.DashCooldownRemaining:F2}s   可冲: {p.CanDash}\n" +
            $"最远: {maxX:F2} 格   最高: {maxUp:F2} 格   钥匙: {hasKey}" +
            formLine;

        GUI.Label(new Rect(x + 10f, y + 8f, 350f, 116f), text, labelStyle);
    }

    void DrawRunState(float x, float y)
    {
        if (!showRunState) return;

        GUI.Box(new Rect(x, y, 370f, 120f), GUIContent.none);

        string regionLine = "区域: -";
        if (world != null)
        {
            var reference = p1 != null ? p1 : p2;
            Region current = reference != null ? world.RegionOf(reference.transform.position) : null;
            var selected = world.Regions.Count > 0
                ? world.Regions[Mathf.Clamp(selectedRegion, 0, world.Regions.Count - 1)]
                : null;

            regionLine = $"区域: {RegionName(current)}   调试目标: {RegionName(selected)}";
        }

        string stateLine;
        var host = RunStateHost.Instance;
        if (host == null)
        {
            stateLine = "RunState: 未挂载 RunStateHost";
        }
        else
        {
            var state = host.State;
            stateLine = $"钥匙: {state.Keys}   传送门: {state.LitPortalCount}   已开门: {state.OpenedGateCount}   B 质量: {state.BMass}";
        }

        string text =
            $"运行状态（F3）\n" +
            regionLine + "\n" +
            stateLine + "\n" +
            $"P1 {LifeLine(p1)}   P2 {LifeLine(p2)}";

        GUI.Label(new Rect(x + 10f, y + 8f, 350f, 106f), text, labelStyle);
    }

    static string RegionName(Region region) =>
        region == null ? "-" : $"{region.Label}({region.Index})";

    static string LifeLine(PlayerMotor motor)
    {
        if (motor == null) return "未绑定";

        var life = motor.GetComponent<PlayerLife>();
        if (life == null) return "无 PlayerLife";

        string spawn = life.RespawnPosition.ToString("F1");
        return life.IsAlive ? $"重生 {spawn} 存活" : $"死亡（重生 {spawn}）";
    }
}
