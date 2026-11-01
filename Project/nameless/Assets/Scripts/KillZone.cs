using UnityEngine;

/// 刺、岩浆、空形态共用的死亡触发器。
[RequireComponent(typeof(Collider2D))]
public sealed class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var life = other.GetComponentInParent<PlayerLife>();
        if (life != null) life.Kill(DeathCause.Hazard);
    }
}
