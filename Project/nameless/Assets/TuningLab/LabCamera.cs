using UnityEngine;

/// <summary>
/// 自由相机：跟随两名玩家的中点，按两人的距离缩放，并限制在房间范围内。
/// 从仓库的 CoopCamera 移植，去掉 PlayerLife / World 依赖（调试台里玩家不会死）。
/// </summary>
public sealed class LabCamera : MonoBehaviour
{
    public Transform a;
    public Transform b;
    public float minSize = 6.75f;
    public float maxSize = 24f;
    public float sizePerUnit = 0.45f;
    public float smoothing = 8f;
    public Vector3 offset = new Vector3(0f, 1f, 0f);
    public bool hasBounds;
    public Rect bounds;

    Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    public void Configure(Transform playerA, Transform playerB, Rect worldBounds)
    {
        a = playerA;
        b = playerB;
        bounds = worldBounds;
        hasBounds = true;
    }

    void LateUpdate()
    {
        if (cam == null || a == null || b == null) return;

        Vector3 mid = (a.position + b.position) * 0.5f;
        mid.z = transform.position.z;

        float spread = Mathf.Max(Mathf.Abs(a.position.x - b.position.x),
                                 Mathf.Abs(a.position.y - b.position.y));

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        float targetSize = Mathf.Clamp(minSize + spread * sizePerUnit, minSize, maxSize);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);

        Vector3 next = Vector3.Lerp(transform.position, mid + offset, t);
        transform.position = Clamp(next, cam.orthographicSize);
    }

    Vector3 Clamp(Vector3 position, float size)
    {
        if (!hasBounds || bounds.width <= 0f || bounds.height <= 0f) return position;

        float halfHeight = size;
        float halfWidth = size * cam.aspect;

        float x = bounds.width <= halfWidth * 2f
            ? bounds.center.x
            : Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);

        float y = bounds.height <= halfHeight * 2f
            ? bounds.center.y
            : Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);

        return new Vector3(x, y, position.z);
    }
}
