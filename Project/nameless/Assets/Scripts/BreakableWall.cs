using UnityEngine;

/// 可破坏实体：达到最低挡位（和可选最低质量）才碎，否则照常阻挡。
[RequireComponent(typeof(Collider2D))]
public class BreakableWall : MonoBehaviour, IImpactReceiver
{
    [SerializeField] SpeedTier minimumTier = GameConstants.WallBreakTier;
    [SerializeField] int minimumMass = 1;

    public void Configure(SpeedTier tier, int mass = 1)
    {
        minimumTier = tier;
        minimumMass = Mathf.Max(1, mass);
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
        if (impact.Tier < minimumTier) return SurfaceResponse.Pass;

        int mass = RunStateHost.Instance != null ? RunStateHost.Instance.State.BMass : 1;
        if (mass < minimumMass) return SurfaceResponse.Pass;

        Break(impact);
        return SurfaceResponse.Break;
    }

    protected virtual void Break(in ImpactInfo impact) => Destroy(gameObject);
}
