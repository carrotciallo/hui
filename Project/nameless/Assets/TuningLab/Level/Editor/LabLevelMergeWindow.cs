#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 两个关卡场景合成一个：A 当底子（参数 / 出生点 / 相机都听 A），B 的内容按格偏移并进来。
///
/// 用法只有三步：选 A、选 B、调一次 B 的偏移，点合并。
/// 默认复制 A 的场景文件写新场景；输出填 A 的路径就是直接改 A。B 永远不会被改。
/// 数据合并走 <see cref="LabLevelData.AppendFrom"/>，组号会重排。
/// </summary>
public sealed class LabLevelMergeWindow : EditorWindow
{
    const int GapCells = 8;

    [SerializeField] SceneAsset sceneA;
    [SerializeField] SceneAsset sceneB;
    [SerializeField] Vector2Int offset;
    [SerializeField] string outputPath = "";
    [SerializeField] bool autoLayout = true;

    [NonSerialized] LabLevelData previewA;
    [NonSerialized] LabLevelData previewB;
    [NonSerialized] string previewError;
    [NonSerialized] bool loadScheduled;
    [NonSerialized] bool merging;
    [NonSerialized] Vector2 scroll;
    [NonSerialized] string status;
    [NonSerialized] MessageType statusType = MessageType.Info;

    // ------------------------------------------------------------------
    // 打开
    // ------------------------------------------------------------------

    [MenuItem("GameJam/Merge Level Scenes")]
    public static void Open()
    {
        var window = GetWindow<LabLevelMergeWindow>("合并两个场景");
        window.minSize = new Vector2(430f, 360f);
        window.Show();
        window.ScheduleLoad();
    }

    /// <summary>关卡编辑器里的入口：把当前场景先填成 A。</summary>
    public static void OpenForCurrentScene()
    {
        var window = GetWindow<LabLevelMergeWindow>("合并两个场景");
        window.minSize = new Vector2(430f, 360f);

        Scene scene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(scene.path))
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            if (asset != null)
            {
                window.sceneA = asset;
                window.autoLayout = true;
            }
        }

        window.Show();
        window.ScheduleLoad();
    }

    void OnEnable()
    {
        LabLevel.SuppressPreviewBuild = false;
        ScheduleLoad();
    }

    void OnDisable()
    {
        LabLevel.SuppressPreviewBuild = false;
    }

    // ------------------------------------------------------------------
    // 界面
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Play 模式下不能合并场景。", MessageType.Info);
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorGUILayout.HelpBox("Unity 正在编译或导入资源，等它跑完。", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("两个场景合成一个", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "A 是底子：TuningLab 参数、出生点、相机尺寸都听 A 的（想听 B 的就把它放在 A）。\n" +
            "B 的内容按下面的偏移平移后并进来。默认写到新场景；输出填 A 的路径就直接改 A。B 不会被修改。",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        sceneA = (SceneAsset)EditorGUILayout.ObjectField("场景 A（底子）", sceneA, typeof(SceneAsset), false);
        sceneB = (SceneAsset)EditorGUILayout.ObjectField("场景 B（并进来）", sceneB, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck()) OnScenesChanged();

        DrawPreview();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            offset = EditorGUILayout.Vector2IntField("B 的偏移（格）", offset);
            if (GUILayout.Button("放到 A 右边", GUILayout.Width(96f)))
            {
                offset = SideBySide(previewA, previewB);
                autoLayout = false;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            outputPath = EditorGUILayout.TextField("输出场景", outputPath);
            if (GUILayout.Button("默认", GUILayout.Width(48f))) outputPath = DefaultOutputPath();
            if (GUILayout.Button("用 A", GUILayout.Width(48f)) && sceneA != null)
                outputPath = AssetDatabase.GetAssetPath(sceneA);
        }

        EditorGUILayout.Space();
        GUI.enabled = !merging;
        if (GUILayout.Button("合并成一个场景", GUILayout.Height(34f)))
            EditorApplication.delayCall += Merge;
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

        EditorGUILayout.EndScrollView();
    }

    void DrawPreview()
    {
        if (previewError != null)
        {
            EditorGUILayout.HelpBox(previewError, MessageType.Error);
            return;
        }

        if (sceneA == null || sceneB == null)
        {
            EditorGUILayout.HelpBox("选两个场景。", MessageType.None);
            return;
        }

        if (previewA == null || previewB == null)
        {
            EditorGUILayout.HelpBox("还没读到场景数据。", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("A：" + Describe(previewA), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("B：" + Describe(previewB), EditorStyles.miniLabel);
    }

    void OnScenesChanged()
    {
        previewA = null;
        previewB = null;
        previewError = null;
        autoLayout = true;
        outputPath = DefaultOutputPath();

        ScheduleLoad();
    }

    // ------------------------------------------------------------------
    // 读取两个场景（只读，读完立刻关掉）
    // ------------------------------------------------------------------

    void ScheduleLoad()
    {
        if (loadScheduled) return;
        loadScheduled = true;

        EditorApplication.delayCall += () =>
        {
            loadScheduled = false;
            if (this == null) return;
            LoadPreview();
            Repaint();
        };
    }

    void LoadPreview()
    {
        previewA = null;
        previewB = null;
        previewError = null;

        if (sceneA == null || sceneB == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        string aPath = AssetDatabase.GetAssetPath(sceneA);
        string bPath = AssetDatabase.GetAssetPath(sceneB);
        if (string.IsNullOrEmpty(aPath) || string.IsNullOrEmpty(bPath)) return;

        if (Same(aPath, bPath))
        {
            previewError = "A 和 B 不能是同一个场景。";
            return;
        }

        previewA = ReadData(aPath, out previewError);
        if (previewA == null) return;

        previewB = ReadData(bPath, out previewError);
        if (previewB == null) return;

        if (autoLayout)
        {
            offset = SideBySide(previewA, previewB);
            autoLayout = false;
        }
    }

    /// <summary>读一份关卡数据。已经打开的场景直接读实例，否则 additively 打开、读完关掉。</summary>
    static LabLevelData ReadData(string path, out string error)
    {
        error = null;

        Scene active = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = false;
        bool previousSuppress = LabLevel.SuppressPreviewBuild;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                LabLevel.SuppressPreviewBuild = true;
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                opened = true;
                RestoreActiveScene(active);
            }

            LabLevel level = FindInScene<LabLevel>(scene);
            if (level == null)
            {
                error = "场景里没有 LabLevel，不是关卡场景：" + path;
                return null;
            }

            LabLevelData data = level.Data.Clone();
            data.Normalize();
            return data;
        }
        catch (Exception exception)
        {
            error = "读取场景失败：" + exception.Message;
            return null;
        }
        finally
        {
            LabLevel.SuppressPreviewBuild = previousSuppress;
            if (opened) EditorSceneManager.CloseScene(scene, true);
            RestoreActiveScene(active);
        }
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
            string error = Validate(out string aPath, out string bPath, out string path);
            if (error != null)
            {
                Fail(error);
                return;
            }

            LabLevelData dataA = ReadData(aPath, out error);
            if (dataA == null)
            {
                Fail(error);
                return;
            }

            LabLevelData dataB = ReadData(bPath, out error);
            if (dataB == null)
            {
                Fail(error);
                return;
            }

            // 窗口刚打开、还没跑过自动排列时，这里补一次默认“放到 A 右边”。
            if (autoLayout)
            {
                offset = SideBySide(dataA, dataB);
                autoLayout = false;
            }

            error = CheckPartitionOverlap(dataA, dataB, offset);
            if (error != null)
            {
                Fail(error);
                return;
            }

            string message =
                "A：" + Path.GetFileNameWithoutExtension(aPath) + "（底子，参数/出生点/相机都听它）\n" +
                "B：" + Path.GetFileNameWithoutExtension(bPath) + " → 偏移 (" + offset.x + ", " + offset.y + ")\n" +
                "输出：" + path + (Same(path, aPath) ? "（直接覆盖 A）" : "") + "\n\n继续？";

            if (!EditorUtility.DisplayDialog("合并两个场景", message, "合并", "取消")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildMerged(dataA, dataB, offset, out LabLevelData merged, out LabLevelMergeReport report);

            if (!CreateScene(aPath, path, merged, out error, out string warning))
            {
                Fail(error);
                return;
            }

            status = "合并完成：" + Summary(report) + "\n输出：" + path;
            if (!string.IsNullOrEmpty(warning)) status += "\n" + warning;
            statusType = string.IsNullOrEmpty(warning) ? MessageType.Info : MessageType.Warning;
            EditorUtility.DisplayDialog("合并完成", status, "好");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Fail("合并失败：" + exception.Message);
        }
        finally
        {
            merging = false;
            Repaint();
            SceneView.RepaintAll();
        }
    }

    string Validate(out string aPath, out string bPath, out string path)
    {
        aPath = null;
        bPath = null;
        path = null;

        if (sceneA == null || sceneB == null) return "先选两个场景。";

        aPath = AssetDatabase.GetAssetPath(sceneA);
        bPath = AssetDatabase.GetAssetPath(sceneB);
        if (string.IsNullOrEmpty(aPath) || string.IsNullOrEmpty(bPath)) return "场景没有保存成 .unity 文件。";
        if (Same(aPath, bPath)) return "A 和 B 不能是同一个场景。";

        path = NormalizeAssetPath(outputPath);
        if (string.IsNullOrEmpty(path)) return "输出路径必须是 Assets/ 下的 .unity 文件。";
        if (path == LabLevelScene.LabScenePath) return "不能覆盖 TuningLab 基准场景。";
        if (Same(path, bPath)) return "输出路径不能是 B 本身。";

        bool intoA = Same(path, aPath);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (!loaded.isLoaded) continue;
            if (!Same(loaded.path, path)) continue;
            if (intoA) continue;              // 直接写回 A 是允许的
            return "输出场景正开着（" + loaded.name + "），先切走或关掉它。";
        }

        return null;
    }

    static void BuildMerged(
        LabLevelData a, LabLevelData b, Vector2Int cellOffset,
        out LabLevelData merged, out LabLevelMergeReport report)
    {
        merged = a.Clone();

        LabLevelData shifted = b.Clone();
        if (shifted.partitions == null || shifted.partitions.Count == 0)
        {
            // 没有分区的关卡补一个，否则并进来以后没有相机范围。
            shifted.partitions = new List<LabLevelPartition>
            {
                new LabLevelPartition { name = "Level 1", bounds = shifted.bounds }
            };
        }

        report = merged.AppendFrom(shifted, cellOffset, "B / ");

        merged.bounds = Union(merged.bounds, Shift(b.bounds, cellOffset));
        foreach (LabLevelPartition partition in shifted.partitions)
            merged.bounds = Union(merged.bounds, Shift(partition.bounds, cellOffset));

        merged.Normalize();
    }

    bool CreateScene(string aPath, string path, LabLevelData merged, out string error, out string warning)
    {
        error = null;
        warning = null;

        bool intoA = Same(path, aPath);
        Scene scene;

        if (intoA)
        {
            // 输出就是 A：直接改 A，不复制、不新建。
            scene = SceneManager.GetSceneByPath(aPath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try
                {
                    scene = EditorSceneManager.OpenScene(aPath, OpenSceneMode.Single);
                }
                catch (Exception exception)
                {
                    error = "打开 A 失败：" + exception.Message;
                    return false;
                }
            }
            else
            {
                if (SceneManager.GetActiveScene() != scene) SceneManager.SetActiveScene(scene);
            }
        }
        else
        {
            if (!CopySceneFile(aPath, path, out error)) return false;

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

        LabLevel level = FindInScene<LabLevel>(scene);
        if (level == null)
        {
            error = "A 的场景里没有 LabLevel：" + aPath;
            return false;
        }

        if (level.Asset != null)
        {
            if (intoA)
            {
                // A 绑了数据资源：直接写回它自己的资源。
                level.Asset.Data.CopyFrom(merged);
                EditorUtility.SetDirty(level.Asset);
                AssetDatabase.SaveAssets();
            }
            else
            {
                // 复制场景会共用同一份资源，必须给新场景建一份自己的。
                string dataPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.ChangeExtension(path, null) + "_Data.asset");
                var asset = ScriptableObject.CreateInstance<LabLevelAsset>();
                asset.Data.CopyFrom(merged);
                AssetDatabase.CreateAsset(asset, dataPath);
                level.BindAsset(asset);
                EditorUtility.SetDirty(asset);
            }
        }
        else
        {
            level.CopyFrom(merged, false);
        }

        TuningLab lab = FindInScene<TuningLab>(scene);
        if (lab == null)
        {
            warning = "A 的场景里没有 TuningLab，合并结果不会生成场地。";
        }
        else if (lab.level == null)
        {
            lab.level = level;
            EditorUtility.SetDirty(lab);
        }

        EditorUtility.SetDirty(level);
        // 防御：读场景时临时关过预览，这里确保是开的，别让合并结果看起来是空场景。
        LabLevel.SuppressPreviewBuild = false;
        level.RebuildPreview();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
        {
            error = "保存场景失败：" + path;
            return false;
        }

        FrameSceneView(LabLevelData.ToRect(merged.bounds));
        return true;
    }

    // ------------------------------------------------------------------
    // 场景文件
    // ------------------------------------------------------------------

    static bool CopySceneFile(string sourcePath, string targetPath, out string error)
    {
        error = null;

        string sourceFull = FullPathOf(sourcePath);
        string targetFull = FullPathOf(targetPath);
        if (string.IsNullOrEmpty(sourceFull) || !File.Exists(sourceFull))
        {
            error = "找不到 A 的场景文件：" + sourcePath;
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "覆盖场景",
                    "已存在：\n" + targetPath + "\n\n用合并结果覆盖它？",
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

    string DefaultOutputPath()
    {
        string name = sceneA != null ? sceneA.name : "Merged";
        string folder = LabLevelScene.LevelFolder;

        string aPath = sceneA != null ? AssetDatabase.GetAssetPath(sceneA) : null;
        if (!string.IsNullOrEmpty(aPath))
        {
            string directory = Path.GetDirectoryName(aPath);
            if (!string.IsNullOrEmpty(directory)) folder = directory;
        }

        return folder + "/" + name + "_merged.unity";
    }

    // ------------------------------------------------------------------
    // 工具
    // ------------------------------------------------------------------

    static string CheckPartitionOverlap(LabLevelData a, LabLevelData b, Vector2Int cellOffset)
    {
        foreach (RectInt rect in PartitionRects(a, Vector2Int.zero))
        {
            foreach (RectInt other in PartitionRects(b, cellOffset))
            {
                if (!LabLevelData.Overlaps(rect, other)) continue;
                return "B 的分区和 A 重叠。把 B 的偏移调开（X 或 Y 再挪几格）再合并。";
            }
        }
        return null;
    }

    static List<RectInt> PartitionRects(LabLevelData data, Vector2Int cellOffset)
    {
        var rects = new List<RectInt>();
        if (data == null) return rects;

        if (data.partitions == null || data.partitions.Count == 0)
        {
            rects.Add(Shift(data.bounds, cellOffset));
            return rects;
        }

        foreach (LabLevelPartition partition in data.partitions)
        {
            if (partition == null) continue;
            rects.Add(Shift(partition.bounds, cellOffset));
        }
        return rects;
    }

    static Vector2Int SideBySide(LabLevelData a, LabLevelData b)
    {
        if (a == null || b == null) return Vector2Int.zero;
        return new Vector2Int(
            a.bounds.xMax + GapCells - b.bounds.xMin,
            a.bounds.yMin - b.bounds.yMin);
    }

    static string Describe(LabLevelData data)
    {
        if (data == null) return "";
        return "地形 " + data.solids.Count
            + " 格 · 分区 " + data.partitions.Count
            + " · 范围 (" + data.bounds.x + ", " + data.bounds.y + ") "
            + data.bounds.width + "×" + data.bounds.height;
    }

    static string Summary(LabLevelMergeReport report)
    {
        if (report == null) return "";
        return "地形 +" + report.solids + " 格"
            + "（重复 " + report.skippedSolids + "）"
            + "，分区 +" + report.partitions
            + "（跳过 " + report.skippedPartitions + "）";
    }

    void Fail(string message)
    {
        status = message;
        statusType = MessageType.Error;
        EditorUtility.DisplayDialog("不能合并", message, "好");
    }

    static bool Same(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

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
}
#endif
