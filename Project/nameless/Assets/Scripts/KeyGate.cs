using UnityEngine;

/// 过关门：带钥匙接触即消耗并永久打开；B 获得质量后可以 v2 撞穿。
[RequireComponent(typeof(Collider2D))]
public sealed class KeyGate : MonoBehaviour, IImpactReceiver
{
    [SerializeField] int id;
    [SerializeField] int breakMass = GameConstants.GateBreakMass;
    [SerializeField] Color openColor = new Color(0.32f, 0.33f, 0.36f);

    SpriteRenderer body;
    Collider2D[] solids;

    public int Id => id;
    public bool IsOpen { get; private set; }

    void Awake()
    {
        body = GetComponentInChildren<SpriteRenderer>();
        solids = GetComponentsInChildren<Collider2D>();
    }

    void Start()
    {
        if (RunStateHost.Instance != null && RunStateHost.Instance.State.IsGateOpen(id)) SetOpen(false);
    }

    public void Configure(int gateId, int massToBreak = GameConstants.GateBreakMass)
    {
        id = gateId;
        breakMass = Mathf.Max(2, massToBreak);
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out PlayerMotor player)) return;

        var contact = c.contactCount > 0 ? c.GetContact(0) : default;
        var impact = ImpactInfo.Build(player, this, contact.normal, contact.point);
        ReceiveImpact(in impact);
    }

    public SurfaceResponse ReceiveImpact(in ImpactInfo impact)
    {
        var player = impact.Attacker;
        if (IsOpen || player == null) return SurfaceResponse.Pass;

        var carrier = player.GetComponent<KeyCarrier>();
        if (carrier != null && carrier.HasKey)
        {
            carrier.Consume();
            SetOpen(true);
            return SurfaceResponse.Break;
        }

        int mass = RunStateHost.Instance != null ? RunStateHost.Instance.State.BMass : 1;
        if (mass >= breakMass && impact.Tier >= GameConstants.GateBreakTier)
        {
            SetOpen(true);
            return SurfaceResponse.Break;
        }

        return SurfaceResponse.Pass;
    }

    void SetOpen(bool record)
    {
        IsOpen = true;

        if (solids != null)
            foreach (var solid in solids) solid.enabled = false;

        if (body != null) body.color = openColor;
        if (record) RunStateHost.Instance?.State.OpenGate(id);
    }
}
