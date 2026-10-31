using UnityEngine;

/// 竖绳：纯触发器，v2 穿过，v3 及以上被弹回并 +1 挡位。
[RequireComponent(typeof(BoxCollider2D))]
public sealed class VerticalRope : MonoBehaviour, IImpactReceiver
{
    [SerializeField] float bounceDuration = GameConstants.RopeBounceDuration;

    BoxCollider2D rope;

    /// 绳的线段上下端，P5 的 A 形态攀附复用。
    public Vector2 SegmentTop => rope != null ? (Vector2)rope.bounds.max : (Vector2)transform.position + Vector2.up;
    public Vector2 SegmentBottom => rope != null ? (Vector2)rope.bounds.min : (Vector2)transform.position - Vector2.up;
    public Vector2 Center => rope != null ? (Vector2)rope.bounds.center : (Vector2)transform.position;

    void Awake() => rope = GetComponent<BoxCollider2D>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerMotor player)) return;

        var impact = ImpactInfo.Build(player, this, Vector2.zero, other.bounds.center);
        ReceiveImpact(in impact);
    }

    public SurfaceResponse ReceiveImpact(in ImpactInfo impact)
    {
        var player = impact.Attacker;
        if (player == null) return SurfaceResponse.Pass;
        if (impact.Tier < GameConstants.RopeBounceMinTier) return SurfaceResponse.Pass;

        Vector2 normal = impact.Normal;
        if (normal.sqrMagnitude < 0.01f)
        {
            Vector2 velocity = impact.Velocity;
            normal = velocity.sqrMagnitude > 0.01f
                ? -velocity
                : (Vector2)player.transform.position - Center;
        }
        if (normal.sqrMagnitude < 0.01f) normal = Vector2.left;

        var next = (SpeedTier)Mathf.Min((int)impact.Tier + 1, (int)SpeedTier.V4);
        player.ReceiveBounce(normal, next, bounceDuration);
        return SurfaceResponse.Bounce;
    }
}
