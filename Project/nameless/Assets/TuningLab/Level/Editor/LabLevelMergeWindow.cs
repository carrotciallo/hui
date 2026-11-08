#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 合并关卡场景：把多个关卡场景的 <see cref="LabLevelData"/> 按格偏移拼进同一个场景。
///
/// 多个大关卡同时开在一个 scene 里编辑会很卡，所以这里不把源场景真的叠在场景里摆：
/// 打开窗口时逐个把源场景 additively 读一遍数据（读完立刻关掉，且不触发 LabLevel 预览生成），
/// 然后在 Scene 视图里只画每个源场景的轮廓方块，用移动工具拖动决定摆放位置。
/// 合并的那一刻才把数据拼起来写进目标场景。
///
/// TuningLab 参数只会留一份，所以合并时必须选“参数听谁的”：
///   - 合并成新场景：复制参数基准场景的整个场景文件（TuningLab 参数、引用、渲染设置原样保留），
///     再把 LabLevelData 换成合并结果；出生点和相机尺寸也取它的。
///   - 合并进当前场景：默认保留当前场景的参数，也可以在这里选一个源场景把参数复制过来。
///     复制只搬规则参数，不动 level 引用、autoBuild、roomScale。
/// </summary>
public sealed class LabLevelMergeWindow : EditorWindow
{
    enum Mode
    {
        IntoCurrent = 0,
        NewScene = 1
    }

    /// <summary>一个待合并的源场景。data / error 等是缓存，不写进窗口布局。</summary>
    [Serializable]
    sealed class Source
    {
        public string path;
        public Vector2Int offset;
        public bool include = true;
        /// <summary>已经算过初始摆放位置。用户拖过之后不要再自动挪。</summary>
        public bool placed;

        [NonSerialized] public LabLevelData data;
        [NonSerialized] public string error;
        [NonSerialized] public long stamp;
        [NonSerialized] public int paramsHash;
        [NonSerialized] public bool hasTuning;
        [NonSerialized] public bool loadAttempted;
    }

    const int BlockGapCells = 8;
    const int MaxReportedWarnings = 5;
    static readonly Color OverlapColor = new Color(1f, 0.32f, 0.26f, 1f);
    static readonly string[] ModeLabels = { "合并进当前场景", "合并成新场景" };
    static readonly string[] ParamLabelsCurrent = { "当前场景（不复制参数）" };

    [SerializeField] Mode mode = Mode.NewScene;
    [SerializeField] List<Source> sources = new List<Source>();
    [SerializeField] int paramIndex = -1;
    [SerializeField] string outputPath = "";
    [SerializeField] bool moveTool = true;
    [SerializeField] bool showPartitions = true;
    [SerializeField] Vector2 scroll;

    [NonSerialized] bool loading;
    [NonSerialized] bool merging;
    [NonSerialized] bool loadScheduled;
    [NonSerialized] int dragIndex = -1;
    [NonSerialized] Vector2Int dragStartCell;
    [NonSerialized] Vector2Int dragStartOffset;
    [NonSerialized] string status;
    [NonSerialized] MessageType statusType = MessageType.Info;
    [NonSerialized] GUIStyle labelStyle;

    // ------------------------------------------------------------------
    // 打开
    // ------------------------------------------------------------------

    [MenuItem("GameJam/Merge Level Scenes")]
    public static void Open()
    {
        var window = GetWindow<LabLevelMergeWindow>("合并场景");
        window.minSize = new Vector2(380f, 420f);
        window.Show();
        window.EnsureLoadedDeferred();
    }

    /// <summary>关卡编辑器里的入口：默认把合并结果写回当前场景。</summary>
    public static void OpenForCurrentScene()
    {
        var window = GetWindow<LabLevelMergeWindow>("合并场景");
        window.minSize = new Vector2(380f, 420f);
        window.mode = Mode.IntoCurrent;
        window.paramIndex = -1;
        window.Show();
        window.EnsureLoadedDeferred();
    }

    void OnEnable()
    {
        // 上一次如果异常中断，别让预览一直关着。
        LabLevel.SuppressPreviewBuild = false;
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        EnsureLoadedDeferred();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        LabLevel.SuppressPreviewBuild = false;
    }

    void OnUndoRedo()
    {
        if (Application.isPlaying) return;

        Scene scene = SceneManager.GetActiveScene();
        LabLevel level = FindInScene<LabLevel>(scene);
        if (level != null) level.RebuildPreview();
        Repaint();
    }

    void EnsureLoadedDeferred()
    {
        if (loadScheduled) return;
        loadScheduled = true;
        EditorApplication.delayCall += () =>
        {
            loadScheduled = false;
            if (this == null) return;
            EnsureLoaded();
            Repaint();
        };
    }

    // ------------------------------------------------------------------
    // 窗口 UI
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Play 模式下不能合并场景。退出 Play 再操作。", MessageType.Info);
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorGUILayout.HelpBox("Unity 正在编译或导入资源，等它跑完再合并。", MessageType.Info);
            return;
        }

        NormalizeSelection();
        if (outputPath == null) outputPath = "";
        if (!loading && !loadScheduled && HasPendingLoad()) EnsureLoadedDeferred();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawTitle();
        DrawMode();
        DrawSourceList();
        DrawMoveTool();
        DrawParams();
        DrawOutput();
        DrawMerge();
        DrawStatus();
        EditorGUILayout.EndScrollView();
    }

    void DrawTitle()
    {
        EditorGUILayout.LabelField("合并关卡场景", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "源场景只读数据、不留在场景里；Scene 视图里拖动方块决定摆放位置。",
            EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    void DrawMode()
    {
        EditorGUILayout.LabelField("合并方式", EditorStyles.boldLabel);
        mode = (Mode)GUILayout.SelectionGrid((int)mode, ModeLabels, 2);

        if (mode == Mode.IntoCurrent)
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorGUILayout.HelpBox(
                "把合并结果写回当前场景：" + (string.IsNullOrEmpty(scene.path) ? "（未保存）" : scene.name) +
                "。\n当前 LabLevel 数据会被替换（可撤销），TuningLab 参数默认保持不变。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "复制「参数基准场景」的整个场景文件，再把数据换成合并结果。\n" +
                "源场景本身不会被修改。",
                MessageType.Info);
        }
    }

    void DrawSourceList()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("源场景（自动排列从左往右）", EditorStyles.boldLabel);
        DrawDropArea();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("添加场景…")) AddSceneDialog();
            if (GUILayout.Button("添加当前场景")) AddCurrentScene();
            if (GUILayout.Button("添加 Levels 全部")) AddLevelFolder();
        }

        if (sources.Count == 0)
        {
            EditorGUILayout.HelpBox("还没有源场景。添加后会自动读数据并排开。", MessageType.None);
            return;
        }

        for (int i = 0; i < sources.Count; i++) DrawSourceRow(i);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("自动排列")) AutoArrange();
            if (GUILayout.Button("全部归零")) ResetOffsets();
            if (GUILayout.Button("重新读取")) ReloadAll();
            if (GUILayout.Button("清空列表")) ClearSources();
        }
    }

    void ClearSources()
    {
        if (sources.Count == 0) return;
        if (!EditorUtility.DisplayDialog("清空列表", "移除所有源场景？（不会删除场景文件）", "清空", "取消")) return;
        sources.Clear();
        paramIndex = -1;
        status = null;
    }

    void DrawSourceRow(int index)
    {
        Source source = sources[index];
        string label = LabelOf(source);
        if (string.IsNullOrEmpty(label)) label = "（未命名）";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                source.include = EditorGUILayout.ToggleLeft(label, source.include, EditorStyles.boldLabel);
                if (!source.include && paramIndex == index) NormalizeSelection();
                if (GUILayout.Button("定位", GUILayout.Width(48f)))
                {
                    if (source.data != null) FrameBlock(source);
                }
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    sources.RemoveAt(index);
                    NormalizeSelection();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (source.data == null)
            {
                if (!string.IsNullOrEmpty(source.error))
                    EditorGUILayout.HelpBox(source.error, MessageType.Error);
                else
                    EditorGUILayout.LabelField("数据还没读取。", EditorStyles.miniLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Vector2Int offset = EditorGUILayout.Vector2IntField("偏移（格）", source.offset);
                if (offset != source.offset)
                {
                    source.offset = offset;
                    Repaint();
                }
            }

            EditorGUILayout.LabelField(Summarize(source.data), EditorStyles.miniLabel);
            if (mode == Mode.NewScene && index == paramIndex)
                EditorGUILayout.LabelField("★ 参数 / 出生点 / 相机尺寸取这个场景", EditorStyles.miniLabel);
        }
    }

    void DrawMoveTool()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("移动工具", EditorStyles.boldLabel);
        moveTool = EditorGUILayout.ToggleLeft("在 Scene 视图里拖动方块", moveTool);
        showPartitions = EditorGUILayout.ToggleLeft("显示分区框", showPartitions);

        if (moveTool)
        {
            EditorGUILayout.HelpBox(
                "左键按住某个方块拖动，按 1 格吸附。勾选框决定哪些场景参与合并。\n" +
                "红色 = 分区和别的场景重叠；分区不能重叠，合并会被拒绝。\n" +
                "如果同时在用关卡编辑器，先把它切到「选择」工具，避免两边抢同一个左键。",
                MessageType.None);
        }
    }

    void DrawParams()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("TuningLab 参数听谁的", EditorStyles.boldLabel);

        var included = IncludedIndices();
        if (included.Count == 0)
        {
            EditorGUILayout.HelpBox("先勾选至少一个源场景。", MessageType.None);
            return;
        }

        if (mode == Mode.NewScene)
        {
            int current = Mathf.Max(0, included.IndexOf(paramIndex));
            var labels = new string[included.Count];
            for (int i = 0; i < included.Count; i++) labels[i] = LabelOf(sources[included[i]]);
            int selected = EditorGUILayout.Popup("参数基准场景", current, labels);
            paramIndex = included[Mathf.Clamp(selected, 0, included.Count - 1)];

            EditorGUILayout.HelpBox(
                "新场景直接复制这个场景文件，所以 TuningLab 的参数、引用和渲染设置原样保留；\n" +
                "出生点 A/B 和相机尺寸也取它的。其它场景只贡献内容。",
                MessageType.Info);
        }
        else
        {
            var labels = new List<string>(ParamLabelsCurrent);
            for (int i = 0; i < included.Count; i++) labels.Add(LabelOf(sources[included[i]]));

            int current = 0;
            if (paramIndex >= 0)
            {
                int slot = included.IndexOf(paramIndex);
                current = slot >= 0 ? slot + 1 : 0;
            }

            int selected = EditorGUILayout.Popup("参数来源", current, labels.ToArray());
            paramIndex = selected <= 0 ? -1 : included[Mathf.Clamp(selected - 1, 0, included.Count - 1)];

            EditorGUILayout.HelpBox(
                paramIndex < 0
                    ? "保留当前场景的 TuningLab 参数。"
                    : "合并时把「" + LabelOf(sources[paramIndex]) + "」的规则参数复制到当前场景的 TuningLab。\n" +
                      "只复制规则参数，不动 level 引用、autoBuild 和 roomScale。",
                MessageType.Info);
        }

        if (mode == Mode.NewScene) DrawParamDifference(included);
    }

    void DrawParamDifference(List<int> included)
    {
        bool any = false;
        int first = 0;
        bool differs = false;
        for (int i = 0; i < included.Count; i++)
        {
            Source source = sources[included[i]];
            if (source.data == null || !source.hasTuning) continue;
            if (!any)
            {
                first = source.paramsHash;
                any = true;
            }
            else if (source.paramsHash != first)
            {
                differs = true;
            }
        }

        if (!any)
        {
            EditorGUILayout.HelpBox("源场景里找不到 TuningLab。", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            differs
                ? "各源场景的 TuningLab 参数不一样。合并后只会保留上面选的那个场景的参数。"
                : "各源场景的 TuningLab 参数一致，选哪个都不影响手感。",
            differs ? MessageType.Warning : MessageType.None);
    }

    void DrawOutput()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);

        if (mode == Mode.IntoCurrent)
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField(
                "目标：当前场景 " + (string.IsNullOrEmpty(scene.path) ? "（未保存）" : scene.path),
                EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("合并后当前场景会变成「未保存」状态，检查没问题再 Ctrl+S。", MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            outputPath = EditorGUILayout.TextField("场景路径", outputPath);
            if (GUILayout.Button("默认", GUILayout.Width(48f))) outputPath = DefaultOutputPath();
            if (GUILayout.Button("…", GUILayout.Width(26f))) PickOutputPath();
        }

        string normalized = NormalizeAssetPath(outputPath);
        if (!string.IsNullOrEmpty(normalized) && AssetDatabase.LoadAssetAtPath<SceneAsset>(normalized) != null)
        {
            EditorGUILayout.HelpBox(
                "目标场景已存在。合并会覆盖它的 LabLevel 数据（.meta / GUID 保持不变）。",
                MessageType.Warning);
        }
    }

    void DrawMerge()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !loading && !merging;
            if (GUILayout.Button(mode == Mode.NewScene ? "合并并生成场景" : "合并进当前场景", GUILayout.Height(34f)))
                EditorApplication.delayCall += Merge;
            GUI.enabled = true;
        }

        EditorGUILayout.LabelField(
            mode == Mode.NewScene
                ? "合并后会打开新场景；源场景文件不会被改动。"
                : "合并会替换当前 LabLevel 数据，可以用 Ctrl+Z 撤销。",
            EditorStyles.miniLabel);
    }

    void DrawStatus()
    {
        if (string.IsNullOrEmpty(status)) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status, statusType);
    }

    // ------------------------------------------------------------------
    // 源场景增删
    // ------------------------------------------------------------------

    void DrawDropArea()
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "把 .unity 场景拖到这里（可多选）", EditorStyles.helpBox);

        Event e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            var paths = new List<string>();
            foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                if (obj is SceneAsset sceneAsset) paths.Add(AssetDatabase.GetAssetPath(sceneAsset));
            paths.AddRange(DragAndDrop.paths);
            AddScenes(paths);
            e.Use();
        }
    }

    void AddSceneDialog()
    {
        string path = EditorUtility.OpenFilePanelWithFilters(
            "添加关卡场景",
            LabLevelScene.LevelFolder,
            new[] { "Unity 场景", "unity" });
        if (string.IsNullOrEmpty(path)) return;
        AddScenes(new[] { path });
    }

    void AddCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            status = "当前场景还没有保存成 .unity 文件。";
            statusType = MessageType.Warning;
            return;
        }
        AddScenes(new[] { scene.path });
    }

    void AddLevelFolder()
    {
        var paths = new List<string>();
        string folder = FullPathOf(LabLevelScene.LevelFolder);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*.unity", SearchOption.TopDirectoryOnly))
                paths.Add(file);
        }

        if (paths.Count == 0)
        {
            status = LabLevelScene.LevelFolder + " 里没有场景。";
            statusType = MessageType.Warning;
            return;
        }

        AddScenes(paths);
    }

    void AddScenes(IEnumerable<string> paths)
    {
        if (paths == null) return;

        int added = 0;
        foreach (string raw in paths)
        {
            string path = NormalizeAssetPath(raw);
            if (string.IsNullOrEmpty(path)) continue;
            if (path == LabLevelScene.LabScenePath)
            {
                status = "TuningLab.unity 是基准场景，不能当关卡合并。";
                statusType = MessageType.Warning;
                continue;
            }
            if (sources.Exists(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase))) continue;

            sources.Add(new Source { path = path, include = true });
            added++;
        }

        if (added == 0) return;

        NormalizeSelection();
        if (string.IsNullOrEmpty(outputPath)) outputPath = DefaultOutputPath();
        status = "已添加 " + added + " 个源场景，正在读取数据…";
        statusType = MessageType.Info;
        EnsureLoadedDeferred();
    }

    void ReloadAll()
    {
        foreach (Source source in sources)
        {
            source.loadAttempted = false;
            source.data = null;
            source.error = null;
        }
        status = "正在重新读取源场景…";
        statusType = MessageType.Info;
        EnsureLoadedDeferred();
    }

    // ------------------------------------------------------------------
    // 读取场景数据
    // ------------------------------------------------------------------

    bool NeedsLoad(Source source, bool force)
    {
        if (source == null || string.IsNullOrEmpty(source.path)) return false;
        if (source.data == null) return force || !source.loadAttempted;

        string full = FullPathOf(source.path);
        if (string.IsNullOrEmpty(full) || !File.Exists(full)) return true;
        return File.GetLastWriteTimeUtc(full).Ticks != source.stamp;
    }

    bool HasPendingLoad()
    {
        if (sources == null) return false;
        foreach (Source source in sources)
            if (NeedsLoad(source, false)) return true;
        return false;
    }

    void EnsureLoaded()
    {
        if (loading || sources == null || sources.Count == 0) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!HasPendingLoad()) return;

        loading = true;
        try
        {
            for (int i = 0; i < sources.Count; i++)
            {
                Source source = sources[i];
                if (!NeedsLoad(source, false)) continue;
                EditorUtility.DisplayProgressBar(
                    "合并场景", "读取 " + LabelOf(source), (float)i / Mathf.Max(1, sources.Count));
                LoadSource(source);
            }

            // 读完再算初始摆放，新加的场景排到已有方块右边；拖过的场景不再动。
            foreach (Source source in sources)
            {
                if (source.data == null || source.placed) continue;
                PlaceAfterExisting(source);
                source.placed = true;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            loading = false;
        }

        NormalizeSelection();
        Repaint();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 打开源场景读一份 LabLevelData 就关掉。读的时候关掉 LabLevel 的预览生成，
    /// 否则为了拿数据会把整关物体搭一遍。已经在编辑器里打开的场景直接读实例，不重复打开。
    /// </summary>
    bool LoadSource(Source source)
    {
        if (source == null) return false;

        source.data = null;
        source.error = null;
        source.hasTuning = false;
        source.paramsHash = 0;
        source.stamp = 0;
        source.loadAttempted = true;

        if (string.IsNullOrEmpty(source.path)) return false;

        string full = FullPathOf(source.path);
        if (string.IsNullOrEmpty(full) || !File.Exists(full))
        {
            source.error = "场景文件不存在：" + source.path;
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source.path) == null)
        {
            source.error = "不是有效的场景资源：" + source.path;
            return false;
        }

        Scene active = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(source.path);
        bool opened = false;
        bool previousSuppress = LabLevel.SuppressPreviewBuild;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                LabLevel.SuppressPreviewBuild = true;
                scene = EditorSceneManager.OpenScene(source.path, OpenSceneMode.Additive);
                opened = true;
                // 打开场景会把它设成 active；马上切回去，别打断用户当前在编的场景。
                RestoreActiveScene(active);
            }

            LabLevel level = FindInScene<LabLevel>(scene);
            if (level == null)
            {
                source.error = "场景里没有 LabLevel，不是关卡场景。";
                return false;
            }

            source.data = level.Data.Clone();
            source.data.Normalize();

            TuningLab tuning = FindInScene<TuningLab>(scene);
            source.hasTuning = tuning != null;
            if (tuning != null) source.paramsHash = TuningFingerprint(tuning).GetHashCode();
            source.stamp = File.GetLastWriteTimeUtc(full).Ticks;
            return true;
        }
        catch (Exception exception)
        {
            source.error = "读取失败：" + exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            LabLevel.SuppressPreviewBuild = previousSuppress;
            if (opened) EditorSceneManager.CloseScene(scene, true);
            RestoreActiveScene(active);
        }
    }

    /// <summary>合并时优先用场景里的实时数据，用户刚改还没保存也能带上。</summary>
    LabLevelData ResolveData(Source source)
    {
        if (source == null) return null;
        Scene scene = SceneManager.GetSceneByPath(source.path);
        if (scene.IsValid() && scene.isLoaded)
        {
            LabLevel level = FindInScene<LabLevel>(scene);
            if (level != null) return level.Data;
        }
        return source.data;
    }

    // ------------------------------------------------------------------
    // 布局与移动
    // ------------------------------------------------------------------

    void PlaceAfterExisting(Source source)
    {
        if (source == null || source.data == null) return;

        bool any = false;
        int right = 0;
        int baseline = 0;
        foreach (Source other in sources)
        {
            if (other == source || other.data == null || !other.include) continue;
            RectInt bounds = Shift(other.data.bounds, other.offset);
            if (!any)
            {
                right = bounds.xMax;
                baseline = bounds.yMin;
                any = true;
            }
            else
            {
                right = Mathf.Max(right, bounds.xMax);
                baseline = Mathf.Min(baseline, bounds.yMin);
            }
        }

        if (!any)
        {
            source.offset = Vector2Int.zero;
            return;
        }

        source.offset = new Vector2Int(
            right + BlockGapCells - source.data.bounds.xMin,
            baseline - source.data.bounds.yMin);
    }

    void AutoArrange()
    {
        int cursor = 0;
        int baseline = 0;
        bool any = false;
        foreach (Source source in sources)
        {
            if (!source.include || source.data == null) continue;
            RectInt bounds = source.data.bounds;
            if (!any)
            {
                baseline = bounds.yMin;
                any = true;
            }
            else
            {
                baseline = Mathf.Min(baseline, bounds.yMin);
            }
        }
        if (!any) return;

        foreach (Source source in sources)
        {
            if (!source.include || source.data == null) continue;
            RectInt bounds = source.data.bounds;
            source.offset = new Vector2Int(cursor - bounds.xMin, baseline - bounds.yMin);
            cursor += bounds.width + BlockGapCells;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    void ResetOffsets()
    {
        foreach (Source source in sources) source.offset = Vector2Int.zero;
        Repaint();
        SceneView.RepaintAll();
    }

    void FrameBlock(Source source)
    {
        if (source == null || source.data == null) return;
        Rect rect = LabLevelData.ToRect(Shift(source.data.bounds, source.offset));
        FrameSceneView(rect);
    }

    // ------------------------------------------------------------------
    // Scene 视图
    // ------------------------------------------------------------------

    void OnSceneGUI(SceneView view)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (sources == null || sources.Count == 0) return;

        Event e = Event.current;
        if (e == null) return;

        DrawBlocks();
        if (moveTool) HandleDrag(e, view);
    }

    void DrawBlocks()
    {
        for (int i = 0; i < sources.Count; i++)
        {
            Source source = sources[i];
            if (source == null || !source.include || source.data == null) continue;

            Rect rect = LabLevelData.ToRect(Shift(source.data.bounds, source.offset));
            Color color = SourceColor(i);
            bool overlaps = OverlapsOtherBlock(i);

            Color outline = overlaps ? OverlapColor : color;
            Handles.DrawSolidRectangleWithOutline(
                RectVerts(rect),
                new Color(outline.r, outline.g, outline.b, i == dragIndex ? 0.16f : 0.06f),
                outline);

            if (showPartitions)
            {
                foreach (RectInt partition in PartitionRects(source.data, source.offset))
                {
                    Handles.DrawSolidRectangleWithOutline(
                        RectVerts(LabLevelData.ToRect(partition)),
                        new Color(0f, 0f, 0f, 0f),
                        new Color(outline.r, outline.g, outline.b, 0.45f));
                }
            }

            if (mode == Mode.NewScene && i == paramIndex) DrawSpawnMarks(source.data, source.offset);

            string label = LabelOf(source) + "  偏移 (" + source.offset.x + ", " + source.offset.y + ")"
                + (overlaps ? "  分区重叠" : "")
                + (mode == Mode.NewScene && i == paramIndex ? "  ★参数" : "");
            Handles.Label(new Vector3(rect.x + 0.2f, rect.yMax + 0.4f, 0f), label, LabelStyle(outline));
        }
    }

    /// <summary>只和其它源场景比分区，不和自己比。完全相同位置的两个分区也算重叠。</summary>
    bool OverlapsOtherBlock(int index)
    {
        Source source = sources[index];
        if (source == null || source.data == null) return false;

        for (int other = 0; other < sources.Count; other++)
        {
            if (other == index) continue;
            Source candidate = sources[other];
            if (candidate == null || !candidate.include || candidate.data == null) continue;

            foreach (RectInt own in PartitionRects(source.data, source.offset))
                foreach (RectInt rect in PartitionRects(candidate.data, candidate.offset))
                    if (LabLevelData.Overlaps(own, rect)) return true;
        }
        return false;
    }

    void DrawSpawnMarks(LabLevelData data, Vector2Int offset)
    {
        var shift = new Vector2(offset.x, offset.y);
        DrawMark(data.spawnA + shift, TuningLab.ColorA);
        DrawMark(data.spawnB + shift, TuningLab.ColorB);
    }

    static void DrawMark(Vector2 point, Color color)
    {
        var rect = new Rect(point.x - 0.5f, point.y - 0.5f, 1f, 1f);
        Handles.DrawSolidRectangleWithOutline(RectVerts(rect), new Color(color.r, color.g, color.b, 0.35f), color);
    }

    void HandleDrag(Event e, SceneView view)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.control && !e.shift)
        {
            Vector2 world = ScenePoint(e.mousePosition);
            int hit = HitTest(world);
            if (hit < 0) return;

            dragIndex = hit;
            dragStartCell = CellOf(world);
            dragStartOffset = sources[hit].offset;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseDrag && dragIndex >= 0 && dragIndex < sources.Count)
        {
            Vector2Int cell = CellOf(ScenePoint(e.mousePosition));
            Vector2Int offset = dragStartOffset + (cell - dragStartCell);
            Source source = sources[dragIndex];
            if (source.offset != offset)
            {
                source.offset = offset;
                Repaint();
                SceneView.RepaintAll();
            }
            e.Use();
        }
        else if (e.type == EventType.MouseUp && dragIndex >= 0)
        {
            dragIndex = -1;
            e.Use();
            Repaint();
        }
    }

    int HitTest(Vector2 world)
    {
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            Source source = sources[i];
            if (source == null || !source.include || source.data == null) continue;
            Rect rect = LabLevelData.ToRect(Shift(source.data.bounds, source.offset));
            if (rect.Contains(world)) return i;
        }
        return -1;
    }

    GUIStyle LabelStyle(Color color)
    {
        if (labelStyle == null) labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = color;
        return labelStyle;
    }

    // ------------------------------------------------------------------
    // 合并
    // ------------------------------------------------------------------

    void Merge()
    {
        if (this == null || merging) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        merging = true;

        try
        {
            EnsureLoaded();

            string error = ValidateForMerge();
            if (error != null)
            {
                EditorUtility.DisplayDialog("不能合并", error, "好");
                return;
            }

            // 先把当前打开的场景存掉：合并成新场景是复制模板文件，没保存的改动不会进副本。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!BuildMergedData(out LabLevelData merged, out string summary, out string buildError))
            {
                EditorUtility.DisplayDialog("不能合并", buildError, "好");
                return;
            }

            if (!EditorUtility.DisplayDialog("合并场景", BuildConfirmText(summary), "合并", "取消")) return;

            bool ok = mode == Mode.NewScene
                ? CreateMergedScene(merged, out error)
                : MergeIntoCurrent(merged, out error);

            if (!ok)
            {
                status = error;
                statusType = MessageType.Error;
                EditorUtility.DisplayDialog("合并失败", error, "好");
                return;
            }

            status = "合并完成。\n" + summary;
            statusType = MessageType.Info;
            EditorUtility.DisplayDialog(
                "合并完成",
                mode == Mode.NewScene ? "已生成：" + NormalizeAssetPath(outputPath) + "\n\n" + summary : summary,
                "好");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            status = "合并失败：" + exception.Message;
            statusType = MessageType.Error;
            EditorUtility.DisplayDialog("合并失败", exception.Message, "好");
        }
        finally
        {
            merging = false;
            Repaint();
            SceneView.RepaintAll();
        }
    }

    string ValidateForMerge()
    {
        if (sources == null || sources.Count == 0) return "先添加至少一个源场景。";

        var errors = new List<string>();
        var included = IncludedIndices();
        if (included.Count == 0) return "至少勾选一个源场景。";

        foreach (int index in included)
        {
            Source source = sources[index];
            if (source.data == null)
                errors.Add("「" + LabelOf(source) + "」" + (string.IsNullOrEmpty(source.error) ? "没有读到关卡数据。" : source.error));
        }

        Scene active = SceneManager.GetActiveScene();
        if (mode == Mode.IntoCurrent)
        {
            if (!active.IsValid() || string.IsNullOrEmpty(active.path))
                errors.Add("当前场景还没有保存成 .unity 文件。");
            else if (active.path == LabLevelScene.LabScenePath)
                errors.Add("TuningLab.unity 是基准场景，不能把合并结果写进去。");
            else if (FindInScene<LabLevel>(active) == null)
                errors.Add("当前场景没有 LabLevel。");

            if (active.IsValid() && FindInScene<TuningLab>(active) == null)
                errors.Add("当前场景没有 TuningLab，合并后没有玩家规则。");
        }
        else
        {
            if (TemplateSource() == null) errors.Add("没有选参数基准场景。");

            string pathError = ValidateOutputPath(out string output);
            if (pathError != null)
            {
                errors.Add(pathError);
            }
            else
            {
                foreach (Source source in sources)
                {
                    if (!string.Equals(source.path, output, StringComparison.OrdinalIgnoreCase)) continue;
                    errors.Add("输出路径不能是源场景「" + LabelOf(source) + "」。\n" +
                               "要写回这个场景：先打开它，再用「合并进当前场景」。");
                    break;
                }

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene loaded = SceneManager.GetSceneAt(i);
                    if (!loaded.isLoaded) continue;
                    if (!string.Equals(loaded.path, output, StringComparison.OrdinalIgnoreCase)) continue;
                    errors.Add("输出路径是已经打开的场景「" + loaded.name + "」。先切走或关掉它，再合并。");
                    break;
                }
            }
        }

        if (errors.Count == 0) CheckPartitionOverlaps(included, errors);

        return errors.Count == 0 ? null : string.Join("\n", errors);
    }

    void CheckPartitionOverlaps(List<int> included, List<string> errors)
    {
        var placed = new List<RectInt>();
        var owners = new List<string>();

        foreach (int index in included)
        {
            Source source = sources[index];
            LabLevelData data = ResolveData(source);
            if (data == null) continue;

            foreach (RectInt rect in PartitionRects(data, source.offset))
            {
                for (int i = 0; i < placed.Count; i++)
                {
                    if (!LabLevelData.Overlaps(rect, placed[i])) continue;
                    if (errors.Count >= 3) return;
                    errors.Add(
                        "分区重叠：「" + owners[i] + "」和「" + LabelOf(source) + "」。\n" +
                        "用移动工具把它们错开（分区不能重叠，贴边相邻可以）。");
                }
                placed.Add(rect);
                owners.Add(LabelOf(source));
            }
        }
    }

    bool BuildMergedData(out LabLevelData merged, out string summary, out string error)
    {
        merged = null;
        summary = null;
        error = null;

        var included = IncludedIndices();
        if (included.Count == 0)
        {
            error = "至少勾选一个源场景。";
            return false;
        }

        var result = new LabLevelData();
        var reports = new List<LabLevelMergeReport>();
        var warnings = new List<string>();
        RectInt bounds = new RectInt();
        bool hasBounds = false;

        foreach (int index in included)
        {
            Source source = sources[index];
            LabLevelData data = ResolveData(source);
            if (data == null)
            {
                error = "「" + LabelOf(source) + "」没有读到关卡数据。";
                return false;
            }

            LabLevelData content = data;
            if (data.partitions == null || data.partitions.Count == 0)
            {
                content = data.Clone();
                content.partitions.Add(new LabLevelPartition { name = "Level 1", bounds = data.bounds });
                warnings.Add("「" + LabelOf(source) + "」没有分区，已按它的编辑范围补一个。");
            }

            LabLevelMergeReport report = result.AppendFrom(content, source.offset, LabelOf(source) + " / ");
            reports.Add(report);
            warnings.AddRange(report.warnings);

            RectInt block = Shift(data.bounds, source.offset);
            bounds = hasBounds ? Union(bounds, block) : block;
            hasBounds = true;
            foreach (RectInt partition in PartitionRects(content, source.offset))
                bounds = Union(bounds, partition);
        }

        if (mode == Mode.NewScene)
        {
            Source template = TemplateSource();
            if (template == null)
            {
                error = "没有可用的参数基准场景。";
                return false;
            }

            LabLevelData templateData = ResolveData(template);
            if (templateData == null)
            {
                error = "参数基准场景没有读到数据。";
                return false;
            }

            var shift = new Vector2(template.offset.x, template.offset.y);
            result.spawnA = templateData.spawnA + shift;
            result.spawnB = templateData.spawnB + shift;
            result.cameraMinSize = templateData.cameraMinSize;
            result.cameraMaxSize = templateData.cameraMaxSize;
            bounds = Union(bounds, Shift(templateData.bounds, template.offset));
        }
        else
        {
            LabLevelData current = CurrentLevelData();
            if (current == null)
            {
                error = "当前场景没有 LabLevel。";
                return false;
            }

            result.spawnA = current.spawnA;
            result.spawnB = current.spawnB;
            result.cameraMinSize = current.cameraMinSize;
            result.cameraMaxSize = current.cameraMaxSize;
            bounds = Union(bounds, current.bounds);
        }

        result.bounds = bounds;
        result.Normalize();

        if (result.FindPartition(result.spawnA) < 0 || result.FindPartition(result.spawnB) < 0)
            warnings.Add("出生点不在任何分区里，相机不会按分区夹取。");

        merged = result;
        summary = BuildSummary(reports, warnings);
        return true;
    }

    string BuildSummary(List<LabLevelMergeReport> reports, List<string> warnings)
    {
        int solids = 0, skipped = 0, ropes = 0, keys = 0, recalls = 0, doors = 0, spikes = 0;
        int ices = 0, magmas = 0, hidden = 0, repairs = 0, respawns = 0, teleports = 0;
        int partitions = 0, skippedPartitions = 0;

        foreach (LabLevelMergeReport report in reports)
        {
            solids += report.solids;
            skipped += report.skippedSolids;
            ropes += report.ropes;
            keys += report.keys;
            recalls += report.recalls;
            doors += report.doors;
            spikes += report.spikes;
            ices += report.ices;
            magmas += report.magmas;
            hidden += report.hiddenBlocks;
            repairs += report.repairBlocks;
            respawns += report.respawnPoints;
            teleports += report.teleports;
            partitions += report.partitions;
            skippedPartitions += report.skippedPartitions;
        }

        var sb = new StringBuilder();
        sb.AppendLine("地形 " + solids + " 格" + (skipped > 0 ? "（位置重复 " + skipped + " 格）" : ""));
        sb.AppendLine("绳 " + ropes + " · 钥匙 " + keys + " · 门 " + doors + " · 刺 " + spikes +
                      " · 冰 " + ices + " · 岩浆 " + magmas + " · 隐藏 " + hidden +
                      " · 修复 " + repairs +
                      " · 召回 " + recalls + " · 重生点 " + respawns + " · 传送点 " + teleports);
        sb.AppendLine("分区 " + partitions + (skippedPartitions > 0 ? "（跳过 " + skippedPartitions + "）" : ""));

        int shown = 0;
        foreach (string warning in warnings)
        {
            if (shown >= MaxReportedWarnings)
            {
                sb.AppendLine("… 还有 " + (warnings.Count - shown) + " 条警告。");
                break;
            }
            sb.AppendLine("· " + warning);
            shown++;
        }

        return sb.ToString().TrimEnd();
    }

    string BuildConfirmText(string summary)
    {
        var sb = new StringBuilder();
        if (mode == Mode.NewScene)
        {
            Source template = TemplateSource();
            sb.AppendLine("合并成新场景：");
            sb.AppendLine(NormalizeAssetPath(outputPath) ?? outputPath);
            sb.AppendLine();
            sb.AppendLine("TuningLab 参数、出生点、相机尺寸听「" + (template != null ? LabelOf(template) : "?") + "」的。");
        }
        else
        {
            sb.AppendLine("合并进当前场景：");
            sb.AppendLine(SceneManager.GetActiveScene().name);
            sb.AppendLine();
            sb.AppendLine(paramIndex < 0
                ? "TuningLab 参数保持当前场景不变。"
                : "TuningLab 参数改听「" + LabelOf(sources[paramIndex]) + "」的。");
        }

        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.Append("继续？");
        return sb.ToString();
    }

    bool CreateMergedScene(LabLevelData merged, out string error)
    {
        error = null;

        string pathError = ValidateOutputPath(out string path);
        if (pathError != null)
        {
            error = pathError;
            return false;
        }

        Source template = TemplateSource();
        if (template == null)
        {
            error = "没有参数基准场景。";
            return false;
        }

        if (!CopySceneFile(template.path, path, out error)) return false;

        Scene scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                error = "打开新场景失败：" + exception.Message;
                return false;
            }
        }
        else if (SceneManager.GetActiveScene() != scene)
        {
            SceneManager.SetActiveScene(scene);
        }

        LabLevel level = FindInScene<LabLevel>(scene);
        if (level == null)
        {
            error = "模板场景里没有 LabLevel：" + template.path;
            return false;
        }

        // 模板场景的关卡数据可能挂在独立的 LabLevelAsset 上（Issue #109）。复制场景文件
        // 会连引用一起复制，写进去等于改到模板场景正在用的那份数据，不能悄悄干。
        if (level.Asset != null)
        {
            error = "模板场景的关卡数据绑在独立资源「" + level.Asset.name
                + "」上，合并成新场景会改到那份共享数据。先在关卡编辑器里解绑数据资源，再合并。";
            return false;
        }

        level.CopyFrom(merged, false);

        TuningLab lab = level.GetComponent<TuningLab>();
        if (lab != null && lab.level == null)
        {
            lab.level = level;
            EditorUtility.SetDirty(lab);
        }

        EditorUtility.SetDirty(level);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            error = "保存场景失败：" + path;
            return false;
        }

        level.RebuildPreview();
        FrameSceneView(LabLevelData.ToRect(merged.bounds));
        Selection.activeGameObject = level.gameObject;
        return true;
    }

    bool MergeIntoCurrent(LabLevelData merged, out string error)
    {
        error = null;

        Scene scene = SceneManager.GetActiveScene();
        LabLevel level = FindInScene<LabLevel>(scene);
        if (level == null)
        {
            error = "当前场景没有 LabLevel。";
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("合并场景");

        if (paramIndex >= 0 && paramIndex < sources.Count)
        {
            TuningLab target = FindInScene<TuningLab>(scene);
            if (target == null)
            {
                error = "当前场景没有 TuningLab，参数没处可写。";
                return false;
            }
            if (!CopyTuningFrom(sources[paramIndex], target, out error)) return false;
        }

        Undo.RecordObject(level, "合并场景");
        level.CopyFrom(merged);
        EditorUtility.SetDirty(level);
        // 数据绑在独立资源上时，脏标记要同时打在资源上，否则合并结果不会存盘。
        if (level.Asset != null)
        {
            Undo.RecordObject(level.Asset, "合并场景");
            EditorUtility.SetDirty(level.Asset);
            AssetDatabase.SaveAssets();
        }

        TuningLab lab = FindInScene<TuningLab>(scene);
        if (lab != null && lab.level == null)
        {
            Undo.RecordObject(lab, "合并场景");
            lab.level = level;
            EditorUtility.SetDirty(lab);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        FrameSceneView(LabLevelData.ToRect(merged.bounds));
        return true;
    }

    /// <summary>把源场景的 TuningLab 规则参数复制到当前场景。只读一次源场景，读完立刻关掉。</summary>
    bool CopyTuningFrom(Source source, TuningLab target, out string error)
    {
        error = null;

        Scene active = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(source.path);
        bool opened = false;
        bool previousSuppress = LabLevel.SuppressPreviewBuild;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                LabLevel.SuppressPreviewBuild = true;
                scene = EditorSceneManager.OpenScene(source.path, OpenSceneMode.Additive);
                opened = true;
                RestoreActiveScene(active);
            }

            TuningLab tuning = FindInScene<TuningLab>(scene);
            if (tuning == null)
            {
                error = "「" + LabelOf(source) + "」没有 TuningLab。";
                return false;
            }

            CopyTuningValues(tuning, target);
            return true;
        }
        catch (Exception exception)
        {
            error = "复制 TuningLab 参数失败：" + exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            LabLevel.SuppressPreviewBuild = previousSuppress;
            if (opened) EditorSceneManager.CloseScene(scene, true);
            RestoreActiveScene(active);
        }
    }

    /// <summary>
    /// 只搬规则参数。跳过 Unity 内部字段（m_*）、所有对象引用（level）、autoBuild 和 roomScale：
    /// 前两个是场景引用，后两个是场景搭建方式，不属于“手感参数”。
    /// </summary>
    static void CopyTuningValues(TuningLab source, TuningLab target)
    {
        if (source == null || target == null || source == target) return;

        Undo.RecordObject(target, "合并场景：TuningLab 参数");

        var from = new SerializedObject(source);
        var to = new SerializedObject(target);
        to.Update();

        SerializedProperty property = from.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (IsExcludedTuningProperty(property)) continue;

            SerializedProperty destination = to.FindProperty(property.propertyPath);
            if (destination == null) continue;
            CopyPropertyValue(property, destination);
        }

        to.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    static void CopyPropertyValue(SerializedProperty from, SerializedProperty to)
    {
        switch (from.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
                to.intValue = from.intValue;
                break;
            case SerializedPropertyType.Enum:
                to.enumValueIndex = from.enumValueIndex;
                break;
            case SerializedPropertyType.Boolean:
                to.boolValue = from.boolValue;
                break;
            case SerializedPropertyType.Float:
                to.floatValue = from.floatValue;
                break;
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                to.stringValue = from.stringValue;
                break;
            case SerializedPropertyType.Color:
                to.colorValue = from.colorValue;
                break;
            case SerializedPropertyType.Vector2:
                to.vector2Value = from.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                to.vector3Value = from.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                to.vector4Value = from.vector4Value;
                break;
            case SerializedPropertyType.Quaternion:
                to.quaternionValue = from.quaternionValue;
                break;
            case SerializedPropertyType.Rect:
                to.rectValue = from.rectValue;
                break;
            case SerializedPropertyType.Bounds:
                to.boundsValue = from.boundsValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                to.animationCurveValue = from.animationCurveValue;
                break;
            case SerializedPropertyType.Gradient:
                to.gradientValue = from.gradientValue;
                break;
        }
    }

    static bool IsExcludedTuningProperty(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.ObjectReference) return true;

        switch (property.name)
        {
            case "level":
            case "autoBuild":
            case "roomScale":
                return true;
            default:
                return property.name.StartsWith("m_", StringComparison.Ordinal);
        }
    }

    /// <summary>只比较规则参数：跳过 Unity 内部字段、引用和场景搭建字段，用来判断各场景参数是否一致。</summary>
    static string TuningFingerprint(TuningLab tuning)
    {
        if (tuning == null) return "";

        var sb = new StringBuilder();
        var serialized = new SerializedObject(tuning);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (IsExcludedTuningProperty(property)) continue;
            sb.Append(property.propertyPath).Append('=').Append(PropertyValue(property)).Append('\n');
        }
        return sb.ToString();
    }

    static string PropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
                return property.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "1" : "0";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("R");
            case SerializedPropertyType.String:
            case SerializedPropertyType.Character:
                return property.stringValue;
            case SerializedPropertyType.Color:
                return property.colorValue.ToString();
            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();
            case SerializedPropertyType.Vector4:
                return property.vector4Value.ToString();
            case SerializedPropertyType.Rect:
                return property.rectValue.ToString();
            default:
                return property.propertyType + ":" + property.intValue;
        }
    }

    // ------------------------------------------------------------------
    // 场景文件
    // ------------------------------------------------------------------

    bool CopySceneFile(string sourcePath, string targetPath, out string error)
    {
        error = null;

        string sourceFull = FullPathOf(sourcePath);
        string targetFull = FullPathOf(targetPath);
        if (string.IsNullOrEmpty(sourceFull) || !File.Exists(sourceFull))
        {
            error = "找不到模板场景文件：" + sourcePath;
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "覆盖场景",
                    "目标场景已存在：\n" + targetPath + "\n\n用合并结果覆盖它的内容？（.meta / GUID 保持不变）",
                    "覆盖", "取消"))
            {
                error = "已取消。";
                return false;
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetFull));
            File.Copy(sourceFull, targetFull, true);
        }
        catch (Exception exception)
        {
            error = "复制场景文件失败：" + exception.Message;
            return false;
        }

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        return true;
    }

    string ValidateOutputPath(out string path)
    {
        path = NormalizeAssetPath(outputPath);
        if (string.IsNullOrEmpty(path)) return "输出路径必须是 Assets/ 下的 .unity 文件。";
        if (path == LabLevelScene.LabScenePath) return "不能覆盖 TuningLab 基准场景。";
        return null;
    }

    string DefaultOutputPath()
    {
        Source template = TemplateSource();
        string name = template != null ? LabelOf(template) : "Merged";
        if (string.IsNullOrEmpty(name)) name = "Merged";
        return LabLevelScene.LevelFolder + "/" + name + "_Merged.unity";
    }

    void PickOutputPath()
    {
        string directory = LabLevelScene.LevelFolder;
        string name = "Merged";
        Source template = TemplateSource();
        if (template != null)
        {
            directory = Path.GetDirectoryName(template.path);
            name = LabelOf(template) + "_Merged";
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "合并场景输出", name, "unity", "合并结果会写进这个场景。", directory);
        if (!string.IsNullOrEmpty(path)) outputPath = path;
    }

    // ------------------------------------------------------------------
    // 选择状态
    // ------------------------------------------------------------------

    Source TemplateSource()
    {
        if (mode != Mode.NewScene) return null;
        if (paramIndex < 0 || paramIndex >= sources.Count) return null;
        return sources[paramIndex].include ? sources[paramIndex] : null;
    }

    LabLevelData CurrentLevelData()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        LabLevel level = FindInScene<LabLevel>(scene);
        return level != null ? level.Data : null;
    }

    List<int> IncludedIndices()
    {
        var list = new List<int>();
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null && sources[i].include) list.Add(i);
        return list;
    }

    void NormalizeSelection()
    {
        if (sources == null) sources = new List<Source>();
        sources.RemoveAll(source => source == null || string.IsNullOrEmpty(source.path));

        if (mode == Mode.NewScene)
        {
            if (paramIndex < 0 || paramIndex >= sources.Count || !sources[paramIndex].include)
            {
                var included = IncludedIndices();
                paramIndex = included.Count > 0 ? included[0] : -1;
            }
        }
        else if (paramIndex >= sources.Count || (paramIndex >= 0 && !sources[paramIndex].include))
        {
            paramIndex = -1;
        }
    }

    // ------------------------------------------------------------------
    // 小工具
    // ------------------------------------------------------------------

    static string LabelOf(Source source)
    {
        if (source == null || string.IsNullOrEmpty(source.path)) return "";
        return Path.GetFileNameWithoutExtension(source.path);
    }

    static string Summarize(LabLevelData data)
    {
        if (data == null) return "";
        return "地形 " + data.solids.Count
            + " · 绳 " + data.ropes.Count
            + " · 机关 " + (data.keys.Count + data.doors.Count + data.spikes.Count
                + data.ices.Count + data.magmas.Count + data.hiddenBlocks.Count + data.recalls.Count)
            + " · 重生点 " + data.respawnPoints.Count
            + " · 传送点 " + data.teleports.Count
            + " · 分区 " + data.partitions.Count
            + " · 范围 " + data.bounds.width + "×" + data.bounds.height;
    }

    static List<RectInt> PartitionRects(LabLevelData data, Vector2Int offset)
    {
        var list = new List<RectInt>();
        if (data == null) return list;

        if (data.partitions == null || data.partitions.Count == 0)
        {
            list.Add(Shift(data.bounds, offset));
            return list;
        }

        foreach (LabLevelPartition partition in data.partitions)
        {
            if (partition == null) continue;
            list.Add(Shift(partition.bounds, offset));
        }
        return list;
    }

    static RectInt Shift(RectInt rect, Vector2Int offset)
        => new RectInt(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);

    static RectInt Union(RectInt a, RectInt b)
    {
        int xMin = Mathf.Min(a.xMin, b.xMin);
        int yMin = Mathf.Min(a.yMin, b.yMin);
        int xMax = Mathf.Max(a.xMax, b.xMax);
        int yMax = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    static Vector3[] RectVerts(Rect rect)
    {
        return new[]
        {
            new Vector3(rect.xMin, rect.yMin, 0f),
            new Vector3(rect.xMax, rect.yMin, 0f),
            new Vector3(rect.xMax, rect.yMax, 0f),
            new Vector3(rect.xMin, rect.yMax, 0f)
        };
    }

    static Vector2 ScenePoint(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    static Vector2Int CellOf(Vector2 point)
        => new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

    static void FrameSceneView(Rect bounds)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        var center = new Vector3(bounds.center.x, bounds.center.y, 0f);
        var size = new Vector3(bounds.width + 8f, bounds.height + 8f, 1f);
        view.Frame(new Bounds(center, size), false);
        view.Repaint();
    }

    static void RestoreActiveScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (SceneManager.GetActiveScene() != scene) SceneManager.SetActiveScene(scene);
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        path = path.Replace('\\', '/').Trim();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        if (path.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(projectRoot.Length + 1);

        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return null;
        if (path.IndexOf("..", StringComparison.Ordinal) >= 0) return null;
        if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return null;
        return path;
    }

    static string FullPathOf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    static Color SourceColor(int index)
    {
        return Color.HSVToRGB(Mathf.Repeat(0.52f + index * 0.115f, 1f), 0.62f, 1f);
    }
}
#endif
