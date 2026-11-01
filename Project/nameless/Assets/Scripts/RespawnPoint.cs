using UnityEngine;

/// 触发者记下重生点；若队友已死，在这里立即复活队友。
[RequireComponent(typeof(Collider2D))]
public sealed class RespawnPoint : MonoBehaviour
{
    public Vector2 Point => transform.position;

    void OnTriggerEnter2D(Collider2D other)
    {
        var life = other.GetComponentInParent<PlayerLife>();
        if (life == null || !life.IsAlive) return;

        var state = RunStateHost.Instance?.State;
        state?.SetRespawn(life.Slot, Point);

        var teammate = life.Teammate;
        if (teammate == null || teammate.IsAlive) return;

        state?.SetRespawn(teammate.Slot, Point);
        teammate.ReviveAt(Point);
    }
}
