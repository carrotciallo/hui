using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// 传送门：靠近即点亮；两名玩家都在门内并同帧按下，消耗一把钥匙激活。
[RequireComponent(typeof(Collider2D))]
public sealed class Portal : MonoBehaviour
{
    [SerializeField] int id;
    [SerializeField] int keyCost = GameConstants.PortalKeyCost;
    [SerializeField] SpriteRenderer body;
    [SerializeField] Color litColor = new Color(0.85f, 0.5f, 1f, 0.9f);
    [SerializeField] Color activeColor = new Color(1f, 0.9f, 0.4f, 0.95f);

    readonly List<PlayerMotor> inside = new List<PlayerMotor>();
    readonly Dictionary<PlayerMotor, bool> previousDown = new Dictionary<PlayerMotor, bool>();

    public int Id => id;
    public bool IsLit { get; private set; }
    public bool IsActive { get; private set; }

    public event Action<Portal> Activated;

    public void Configure(int portalId)
    {
        id = portalId;
    }

    void Awake()
    {
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (RunStateHost.Instance != null && RunStateHost.Instance.State.IsPortalLit(id)) MarkLit();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerMotor player) || inside.Contains(player)) return;

        inside.Add(player);
        // 进门前就按着下不算“本帧按下”，必须重新按一次
        previousDown[player] = IsDownHeld(player);
        MarkLit();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerMotor player)) inside.Remove(player);
    }

    void Update()
    {
        if (IsActive) return;

        int pressed = 0;
        foreach (var player in inside)
        {
            bool held = IsDownHeld(player);
            previousDown.TryGetValue(player, out bool previous);
            if (held && !previous) pressed++;
            previousDown[player] = held;
        }

        if (inside.Count < RunState.PlayerCount || pressed < RunState.PlayerCount) return;
        if (!IsLit || !TryConsumeKey()) return;

        IsActive = true;
        if (body != null) body.color = activeColor;
        Activated?.Invoke(this);
    }

    void MarkLit()
    {
        IsLit = true;
        if (body != null) body.color = litColor;
        RunStateHost.Instance?.State.LightPortal(id);
    }

    bool TryConsumeKey()
    {
        var carriers = new List<KeyCarrier>();
        foreach (var player in inside)
        {
            var carrier = player.GetComponent<KeyCarrier>();
            if (carrier != null && carrier.HasKey) carriers.Add(carrier);
        }

        if (carriers.Count < keyCost) return false;

        for (int i = 0; i < keyCost; i++) carriers[i].Consume();
        return true;
    }

    static bool IsDownHeld(PlayerMotor player)
    {
        var kb = Keyboard.current;
        if (kb == null || player == null) return false;

        Key key = player.InputScheme == PlayerMotor.Scheme.WASD ? Key.S : Key.DownArrow;
        return kb[key].isPressed;
    }
}
