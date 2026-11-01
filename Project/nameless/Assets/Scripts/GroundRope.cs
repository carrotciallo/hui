using System.Collections;
using UnityEngine;

/// 地面绳：单向平台。v0/v1 正常站立，v2 从上方踩下会穿过去，v3 及以上向上弹并 +1 挡位。
[RequireComponent(typeof(BoxCollider2D))]
public sealed class GroundRope : MonoBehaviour, IImpactReceiver
{
    [SerializeField] float bounceDuration = GameConstants.RopeBounceDuration;
    [SerializeField] float dropDuration = GameConstants.GroundRopeDropDuration;

    BoxCollider2D rope;

    void Awake() => rope = GetComponent<BoxCollider2D>();

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out PlayerMotor player)) return;
        if (player.transform.position.y <= transform.position.y) return;

        var contact = c.contactCount > 0 ? c.GetContact(0) : default;
        var impact = ImpactInfo.Build(player, this, contact.normal, contact.point);
        ReceiveImpact(in impact);
    }

    public SurfaceResponse ReceiveImpact(in ImpactInfo impact)
    {
        var player = impact.Attacker;
        if (player == null) return SurfaceResponse.Pass;
        if (impact.Tier < GameConstants.RopePassTier) return SurfaceResponse.Pass;

        if (impact.Tier >= GameConstants.RopeBounceMinTier)
        {
            var next = (SpeedTier)Mathf.Min((int)impact.Tier + 1, (int)SpeedTier.V4);
            player.ReceiveBounce(Vector2.up, next, bounceDuration);
            return SurfaceResponse.Bounce;
        }

        var body = player.GetComponent<Collider2D>();
        if (body != null) StartCoroutine(DropThrough(body));
        return SurfaceResponse.Pass;
    }

    IEnumerator DropThrough(Collider2D player)
    {
        if (rope == null) yield break;

        Physics2D.IgnoreCollision(player, rope, true);
        yield return new WaitForSeconds(dropDuration);
        if (player != null && rope != null) Physics2D.IgnoreCollision(player, rope, false);
    }
}
