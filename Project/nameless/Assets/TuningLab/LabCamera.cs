using UnityEngine;

/// <summary>
/// 自由相机：跟随两名玩家的中点，按两人的距离缩放，并限制在当前关卡范围内。
/// 从仓库的 CoopCamera 移植，去掉 PlayerLife / World 依赖（调试台里玩家不会死）。
///
/// 连续关卡时，用 B 的位置向 TuningLab 查当前关卡分区（bounds / minSize / maxSize）；
/// 分区切换时 active 值会按 frameBlend 混合，避免相机直接跳。
/// </summary>
public sealed class LabCamera : MonoBehaviour
{
    public Transform a;
    public Transform b;
    public TuningLab lab;
    public float minSize = 6.75f;
    public float maxSize = 24f;
    public float sizePerUnit = 0.45f;
    public float smoothing = 8f;
    public float frameBlend = 8f;
    public Vector3 offset = new Vector3(0f, 1f, 0f);
    public bool hasBounds;
    public Rect bounds;

    Camera cam;

    // 实际用于取景的混合值；bounds/minSize/maxSize 是“目标值”，由 TuningLab.GetCameraFrame 每帧刷新。
    Rect activeBounds;
    float activeMinSize;
    float activeMaxSize;
    bool frameReady;

    void Awake() => cam = GetComponent<Camera>();

    public void Configure(Transform playerA, Transform playerB, Rect worldBounds)
    {
        a = playerA;
        b = playerB;
        bounds = worldBounds;
        hasBounds = true;
        SnapFrame();
    }

    public void Configure(TuningLab owner, Transform playerA, Transform playerB,
                          Rect worldBounds, float min, float max, float blend)
    {
        lab = owner;
        a = playerA;
        b = playerB;
        bounds = worldBounds;
        hasBounds = true;
        minSize = Mathf.Max(0.1f, min);
        maxSize = Mathf.Max(minSize, max);
        frameBlend = blend;
        SnapFrame();
    }

    void SnapFrame()
    {
        activeBounds = bounds;
        activeMinSize = Mathf.Max(0.1f, minSize);
        activeMaxSize = Mathf.Max(activeMinSize, maxSize);
        frameReady = true;
    }

    void LateUpdate()
    {
        if (cam == null || a == null || b == null) return;

        UpdateFrameTarget();

        Vector3 mid = (a.position + b.position) * 0.5f;
        mid.z = transform.position.z;

        float spread = Mathf.Max(Mathf.Abs(a.position.x - b.position.x),
                                 Mathf.Abs(a.position.y - b.position.y));

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        float targetSize = Mathf.Clamp(activeMinSize + spread * sizePerUnit, activeMinSize, activeMaxSize);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);

        Vector3 next = Vector3.Lerp(transform.position, mid + offset, t);
        transform.position = Clamp(next, cam.orthographicSize);
    }

    /// <summary>
    /// 每帧从 TuningLab 取当前关卡分区（B 的位置判定），再按 frameBlend 混合 active 取景。
    /// 没有 TuningLab 时退回 Inspector 上的 bounds / minSize / maxSize。
    /// </summary>
    void UpdateFrameTarget()
    {
        Rect targetBounds = bounds;
        float targetMin = minSize;
        float targetMax = maxSize;

        if (lab != null)
        {
            lab.GetCameraFrame(b.position, out targetBounds, out targetMin, out targetMax);
            bounds = targetBounds;
            minSize = targetMin;
            maxSize = targetMax;
        }

        if (!frameReady)
        {
            activeBounds = targetBounds;
            activeMinSize = targetMin;
            activeMaxSize = targetMax;
            frameReady = true;
            return;
        }

        float k = frameBlend <= 0f ? 1f : 1f - Mathf.Exp(-frameBlend * Time.deltaTime);
        activeBounds = LerpRect(activeBounds, targetBounds, k);
        activeMinSize = Mathf.Lerp(activeMinSize, targetMin, k);
        activeMaxSize = Mathf.Lerp(activeMaxSize, targetMax, k);
    }

    Vector3 Clamp(Vector3 position, float size)
    {
        if (!hasBounds || activeBounds.width <= 0f || activeBounds.height <= 0f) return position;

        float halfHeight = size;
        float halfWidth = size * cam.aspect;

        float x = activeBounds.width <= halfWidth * 2f
            ? activeBounds.center.x
            : Mathf.Clamp(position.x, activeBounds.xMin + halfWidth, activeBounds.xMax - halfWidth);

        float y = activeBounds.height <= halfHeight * 2f
            ? activeBounds.center.y
            : Mathf.Clamp(position.y, activeBounds.yMin + halfHeight, activeBounds.yMax - halfHeight);

        return new Vector3(x, y, position.z);
    }

    static Rect LerpRect(Rect from, Rect to, float t)
    {
        return new Rect(
            Mathf.Lerp(from.x, to.x, t),
            Mathf.Lerp(from.y, to.y, t),
            Mathf.Lerp(from.width, to.width, t),
            Mathf.Lerp(from.height, to.height, t));
    }
}
