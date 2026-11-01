using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TierOutline : MonoBehaviour
{
    [SerializeField] PlayerMotor motor;
    [SerializeField] float sizeMultiplier = 1.35f;
    [SerializeField] float alpha = 0.9f;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();
    }

    void LateUpdate()
    {
        if (motor == null) return;

        Color c = motor.SpeedTiers.TierColor;
        c.a = motor.Tier == SpeedTier.V0 ? 0f : alpha;
        sr.color = c;
        transform.localScale = Vector3.one * sizeMultiplier;
    }
}
