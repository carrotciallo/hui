using UnityEngine;

public sealed class CoopCamera : MonoBehaviour
{
    [SerializeField] Transform a;
    [SerializeField] Transform b;
    [SerializeField] PlayerLife aLife;
    [SerializeField] PlayerLife bLife;
    [SerializeField] float minSize = 5f;
    [SerializeField] float maxSize = 9f;
    [SerializeField] float sizePerWorldUnit = 0.42f;
    [SerializeField] float smoothing = 8f;
    [SerializeField] Vector3 offset = new Vector3(0f, 1f, 0f);
    [SerializeField] Rect bounds;
    [SerializeField] bool hasBounds;

    Camera cam;
    Rect? viewRestriction;

    void Awake() => cam = GetComponent<Camera>();

    public void Configure(Transform playerA, Transform playerB, PlayerLife lifeA, PlayerLife lifeB,
                          Rect worldBounds)
    {
        a = playerA;
        b = playerB;
        aLife = lifeA;
        bLife = lifeB;
        SetBounds(worldBounds);
    }

    public void SetBounds(Rect worldBounds)
    {
        bounds = worldBounds;
        hasBounds = true;
    }

    /// 留给 P7 的视野收缩揭示；传 null 解除限制。
    public void SetViewRestriction(Rect? restriction) => viewRestriction = restriction;

    void LateUpdate()
    {
        if (cam == null) return;

        if (!hasBounds && World.Instance != null) SetBounds(World.Instance.Bounds);
        ResolveLives();

        bool aAlive = a != null && (aLife == null || aLife.IsAlive);
        bool bAlive = b != null && (bLife == null || bLife.IsAlive);

        // 两人都死：保持最后位置，等复活后再回跟
        if (!aAlive && !bAlive) return;

        Vector3 mid;
        float spread;
        if (aAlive && bAlive)
        {
            mid = (a.position + b.position) * 0.5f;
            spread = Mathf.Max(Mathf.Abs(a.position.x - b.position.x),
                               Mathf.Abs(a.position.y - b.position.y));
        }
        else
        {
            mid = aAlive ? a.position : b.position;
            spread = 0f;
        }

        mid.z = transform.position.z;

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        float targetSize = Mathf.Clamp(minSize + spread * sizePerWorldUnit, minSize, maxSize);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);

        Vector3 next = Vector3.Lerp(transform.position, mid + offset, t);
        transform.position = ClampToLimits(next, cam.orthographicSize);
    }

    void ResolveLives()
    {
        if (aLife == null && a != null) aLife = a.GetComponent<PlayerLife>();
        if (bLife == null && b != null) bLife = b.GetComponent<PlayerLife>();
    }

    Vector3 ClampToLimits(Vector3 position, float size)
    {
        if (!hasBounds) return position;

        Rect limits = EffectiveLimits();
        if (limits.width <= 0f || limits.height <= 0f) return position;

        float halfHeight = size;
        float halfWidth = size * cam.aspect;

        float x = limits.width <= halfWidth * 2f
            ? limits.center.x
            : Mathf.Clamp(position.x, limits.xMin + halfWidth, limits.xMax - halfWidth);

        float y = limits.height <= halfHeight * 2f
            ? limits.center.y
            : Mathf.Clamp(position.y, limits.yMin + halfHeight, limits.yMax - halfHeight);

        return new Vector3(x, y, position.z);
    }

    Rect EffectiveLimits()
    {
        if (!viewRestriction.HasValue) return bounds;

        Rect restrict = viewRestriction.Value;
        float xMin = Mathf.Max(bounds.xMin, restrict.xMin);
        float yMin = Mathf.Max(bounds.yMin, restrict.yMin);
        float xMax = Mathf.Min(bounds.xMax, restrict.xMax);
        float yMax = Mathf.Min(bounds.yMax, restrict.yMax);

        return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : bounds;
    }
}
