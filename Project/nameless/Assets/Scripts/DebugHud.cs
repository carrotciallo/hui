using UnityEngine;
using UnityEngine.InputSystem;

public class DebugHud : MonoBehaviour
{
    [SerializeField] PlayerMotor p1;
    [SerializeField] PlayerMotor p2;

    GUIStyle labelStyle;
    float maxX1, maxUp1, maxX2, maxUp2;

    void Start() => ResetTracking();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[Key.R].wasPressedThisFrame) ResetPlayers();

        if (p1 != null && p1.transform.position.y < -6f) ResetPlayers();
        if (p2 != null && p2.transform.position.y < -6f) ResetPlayers();

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

    void ResetPlayers()
    {
        if (p1 != null) p1.ResetTo(p1.SpawnPosition);
        if (p2 != null) p2.ResetTo(p2.SpawnPosition);
        ResetTracking();
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

        GUI.Label(new Rect(10f, 292f, 560f, 24f),
                  "R 重置  |  1 格 = 1 单位 = 16px  |  黄条间隔 1 格，亮黄每 5 格",
                  labelStyle);
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

        string text =
            $"{title}\n" +
            $"挡位: <color=#{color}>{tier.TierLabel}</color>   目标速度: {tier.TargetSpeed:F1}\n" +
            $"速度: {v.x:F1}, {v.y:F1}   合速度: {v.magnitude:F1}\n" +
            $"状态: {p.State}   地面: {p.Grounded}   冲刺: {p.IsDashing}   冷却: {p.DashCooldownRemaining:F2}s   可冲: {p.CanDash}\n" +
            $"最远: {maxX:F2} 格   最高: {maxUp:F2} 格";

        GUI.Label(new Rect(x + 10f, y + 8f, 350f, 116f), text, labelStyle);
    }
}
