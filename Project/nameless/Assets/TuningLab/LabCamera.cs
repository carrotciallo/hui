using UnityEngine;

/// <summary>
/// 自由相机：跟随两名玩家的中点，按两人的距离缩放，并限制在当前关卡范围内。
/// 从仓库的 CoopCamera 移植，去掉 PlayerLife / World 依赖。
/// 锻造塌框会销毁一名玩家：那时相机改绑存活者，尺寸收到最小值，不再按距离伸缩。
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
    bool focusActive;
    Vector3 focusFrom;
    Vector3 focusTo;
    float focusStarted;
    float focusDuration;

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
        if (cam == null) return;

        if (focusActive)
        {
            float progress = focusDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.unscaledTime - focusStarted) / focusDuration);
            Vector3 focused = Vector3.Lerp(focusFrom, focusTo, progress);
            focused.z = transform.position.z;
            transform.position = Clamp(focused, cam.orthographicSize);
            if (progress >= 1f) focusActive = false;
            return;
        }

        // 一方死亡（GameObject 被销毁）后，相机绑在活着的这个人身上，spread 归零 = 不再伸缩。
        bool hasA = a != null;
        bool hasB = b != null;
        if (!hasA && !hasB) return;
        bool solo = !hasA || !hasB;

        Vector3 focus = solo
            ? (hasA ? a.position : b.position)
            : (a.position + b.position) * 0.5f;
        focus.z = transform.position.z;

        UpdateFrameTarget(focus);

        float spread = solo
            ? 0f
            : Mathf.Max(Mathf.Abs(a.position.x - b.position.x), Mathf.Abs(a.position.y - b.position.y));

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        float targetSize = Mathf.Clamp(activeMinSize + spread * sizePerUnit, activeMinSize, activeMaxSize);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);

        Vector3 next = Vector3.Lerp(transform.position, focus + offset, t);
        transform.position = Clamp(next, cam.orthographicSize);
    }

    public void FocusOn(Vector2 worldPosition, float duration)
    {
        focusActive = true;
        focusFrom = transform.position;
        focusTo = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        focusStarted = Time.unscaledTime;
        focusDuration = Mathf.Max(0f, duration);
    }

    public void ResumeFollowing()
    {
        focusActive = false;
    }

    /// <summary>
    /// 每帧从 TuningLab 取当前关卡分区（用取景点的位置判定），再按 frameBlend 混合 active 取景。
    /// 没有 TuningLab 时退回 Inspector 上的 bounds / minSize / maxSize。
    /// </summary>
    void UpdateFrameTarget(Vector2 probe)
    {
        Rect targetBounds = bounds;
        float targetMin = minSize;
        float targetMax = maxSize;

        if (lab != null)
        {
            lab.GetCameraFrame(probe, out targetBounds, out targetMin, out targetMax);
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
