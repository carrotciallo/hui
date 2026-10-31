using UnityEngine;

/// 钥匙触发器：玩家碰到就挂到头顶，已携带时不再拾取。
[RequireComponent(typeof(Collider2D))]
public sealed class KeyPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var carrier = other.GetComponentInParent<KeyCarrier>();
        if (carrier == null || !carrier.PickUp()) return;

        Destroy(gameObject);
    }
}
