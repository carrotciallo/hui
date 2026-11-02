using UnityEngine;

/// <summary>
/// 把 <see cref="LabLevelData"/> 搭成真实场景物体：实心格按矩形合并后生成 BoxCollider2D，
/// 绳子生成 LabVerticalRope。
///
/// 编辑器里由关卡编辑器调用 <see cref="RebuildPreview"/>，物体只用于预览、不存盘；
/// Play 时由 TuningLab.BuildRoom 调用 <see cref="BuildRuntime"/>，走的是同一条生成路径。
/// 所以"编辑器里看到什么"和"Play 时撞到什么"是同一份几何。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LabLevel : MonoBehaviour
{
    public const string GeneratedRootName = "LabLevel_Generated";

    [SerializeField, HideInInspector] LabLevelData data = new LabLevelData();

    public LabLevelData Data
    {
        get
        {
            if (data == null) data = new LabLevelData();
            return data;
        }
    }

    public Rect CameraBounds =>
        new Rect(Data.bounds.x, Data.bounds.y, Data.bounds.width, Data.bounds.height);

    public int SolidCellCount => Data.solids != null ? Data.solids.Count : 0;
    public int MergedRectCount => Data.MergedRects().Count;
    public int RopeCount => Data.ropes != null ? Data.ropes.Count : 0;

    Transform generatedRoot;

    void OnEnable()
    {
        if (!Application.isPlaying) RebuildPreview();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    /// <summary>用 TuningLab 的内置房间覆盖当前关卡。编辑器的"从 Tuning Lab 重置"走这里。</summary>
    public void ApplyBaseline()
    {
        data = TuningLab.CaptureBaseline();
        if (!Application.isPlaying) RebuildPreview();
    }

    public void CopyFrom(LabLevelData source)
    {
        Data.CopyFrom(source);
        if (!Application.isPlaying) RebuildPreview();
    }

    /// <summary>编辑器预览。只在非 Play 下跑，生成的物体带 DontSaveInEditor，不会被存进场景。</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        ClearGenerated();
        Build(transform, null, true);
    }

    /// <summary>Play 时由 TuningLab 调用。material 是 TuningLab 那块摩擦 0 的 LabSlick。</summary>
    public void BuildRuntime(Transform parent, PhysicsMaterial2D material)
    {
        ClearGenerated();
        Build(parent, material, false);
    }

    void Build(Transform parent, PhysicsMaterial2D material, bool preview)
    {
        if (data == null) data = new LabLevelData();

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(parent, false);
        // 预览物体不存场景；Play 时的运行时物体本来就不会被存。
        if (preview) root.hideFlags = HideFlags.DontSaveInEditor;
        generatedRoot = root.transform;

        data.Normalize();
        var rects = data.MergedRects();
        for (int i = 0; i < rects.Count; i++) AddSolid(root.transform, rects[i], material);

        for (int i = 0; i < data.ropes.Count; i++) AddRope(root.transform, data.ropes[i], material);
    }

    void AddSolid(Transform parent, RectInt rect, PhysicsMaterial2D material)
    {
        var go = new GameObject("Solid");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(
            rect.x + rect.width * 0.5f,
            rect.y + rect.height * 0.5f,
            0f);
        go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.color = TuningLab.SolidColor;
        sr.sortingOrder = 0;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        if (material != null) bc.sharedMaterial = material;
    }

    void AddRope(Transform parent, LabRopeSpec spec, PhysicsMaterial2D material)
    {
        var go = new GameObject("VerticalRope");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.center;

        var bc = go.AddComponent<BoxCollider2D>();
        if (material != null) bc.sharedMaterial = material;

        var rope = go.AddComponent<LabVerticalRope>();
        rope.cells = Mathf.Max(1, spec.cells);
        rope.chainId = spec.chainId;
        rope.ApplyShape();
    }

    public void ClearGenerated()
    {
        Transform existing = generatedRoot;
        if (existing == null && transform != null) existing = transform.Find(GeneratedRootName);
        generatedRoot = null;
        if (existing == null) return;

        bool preview = (existing.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0;
        if (Application.isPlaying && !preview) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    [ContextMenu("重建关卡预览")]
    void RebuildPreviewMenu()
    {
        if (!Application.isPlaying) RebuildPreview();
    }
}
