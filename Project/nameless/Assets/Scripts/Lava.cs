using UnityEngine;

/// 岩浆：普通玩家接触即死；A 的 _ 木筏形态会浮在液面，骑在木筏上的 B 也安全。
[RequireComponent(typeof(Collider2D))]
public sealed class Lava : MonoBehaviour
{
    Collider2D area;

    public float SurfaceY => area != null ? area.bounds.max.y : transform.position.y;

    void Awake() => area = GetComponent<Collider2D>();

    void OnTriggerEnter2D(Collider2D other) => Touch(other);

    void OnTriggerStay2D(Collider2D other) => Touch(other);

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerMotor player)) return;

        var form = player.GetComponent<FormController>();
        if (form != null && player.IsFloating) player.SetFloating(false, 0f);
    }

    void Touch(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerMotor player)) return;

        var form = player.GetComponent<FormController>();
        if (form != null && form.IsRaft)
        {
            player.SetFloating(true, SurfaceY);
            return;
        }

        var rider = player.RideTarget;
        if (rider != null)
        {
            var riderForm = rider.GetComponent<FormController>();
            if (riderForm != null && riderForm.IsRaft) return;
        }

        var life = player.GetComponent<PlayerLife>();
        if (life != null) life.Kill(DeathCause.Hazard);
    }
}
