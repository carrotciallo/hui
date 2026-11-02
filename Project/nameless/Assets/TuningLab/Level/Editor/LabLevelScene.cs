#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 从 Tuning Lab 基准场景派生一个可编辑的关卡场景。
///
/// 派生方式不是"重新摆一遍房间"，而是：
///   1. 复制 Assets/Scenes/TuningLab.unity —— 调参值、相机、玩家规则全部原样继承；
///   2. 清掉烘焙场景可能留下的生成物体；
///   3. 挂上 <see cref="LabLevel"/>，用 TuningLab.CaptureBaseline() 写入基准房间数据。
///
/// 所以关卡场景和调试台是同一套脚本、同一套格子、同一套相机，不存在"第二套规范"。
/// </summary>
public static class LabLevelScene
{
    public const string LabScenePath = "Assets/Scenes/TuningLab.unity";
    public const string LevelFolder = "Assets/Scenes/Levels";
    public const string DefaultLevelName = "Level_01";

    [MenuItem("GameJam/New Level From Tuning Lab")]
    public static void CreateInteractive()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EnsureLabScene()) return;

        EnsureFolder(LevelFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "从 Tuning Lab 新建关卡",
            DefaultLevelName,
            "unity",
            "关卡场景会复制 Tuning Lab 的场景与调参值，再写入一份 LabLevel 数据。",
            LevelFolder);

        if (string.IsNullOrEmpty(path)) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            EditorUtility.DisplayDialog("新建关卡", "目标场景已存在：\n" + path + "\n换个文件名。", "好");
            return;
        }

        if (!CreateScene(path, out string error))
        {
            EditorUtility.DisplayDialog("新建关卡失败", error, "好");
            return;
        }

        LabLevelEditorWindow.Open();
        EditorUtility.DisplayDialog(
            "关卡场景已生成",
            "已从 Tuning Lab 派生：\n" + path +
            "\n\n场景里只有 TuningLab + LabLevel，场地由基准数据生成。\n" +
            "打开 GameJam → Tuning Lab Level Editor 开始编辑。\n" +
            "Play 时跑的就是调试台同一套玩家、相机和手感。",
            "好");
    }

    /// <summary>给 -executeMethod / 批处理用：不弹对话框，直接派生到指定路径。</summary>
    public static bool CreateSilent(string path)
    {
        if (!EnsureLabScene())
        {
            Debug.LogError("[LabLevelScene] 缺少基准场景 " + LabScenePath);
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            AssetDatabase.DeleteAsset(path);

        bool ok = CreateScene(path, out string error);
        if (!ok) Debug.LogError("[LabLevelScene] " + error);
        return ok;
    }

    /// <summary>
    /// 命令行入口：
    ///   -executeMethod LabLevelScene.CreateFromCommandLine -levelPath Assets/Scenes/Levels/Verify.unity
    /// 成功退出码 0，失败 1。
    /// </summary>
    public static void CreateFromCommandLine()
    {
        string path = LevelFolder + "/__VerifyLevel.unity";
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-levelPath" && i + 1 < args.Length) path = args[i + 1];
            else if (args[i].StartsWith("-levelPath=")) path = args[i].Substring("-levelPath=".Length);
        }

        bool ok = CreateSilent(path);
        if (ok) Debug.Log("[LabLevelScene] created " + path);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("GameJam/Convert Current Scene To Level")]
    public static void ConvertCurrentInteractive()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!ConvertCurrent(out string error))
        {
            EditorUtility.DisplayDialog("转换关卡场景失败", error, "好");
            return;
        }

        LabLevelEditorWindow.Open();
        EditorUtility.DisplayDialog(
            "已转成关卡场景",
            "当前场景已挂上 LabLevel，并写入 Tuning Lab 基准房间。\n" +
            "烘焙出来的相机 / 玩家 / 地形已清掉，Play 时按关卡数据重新生成。",
            "好");
    }

    /// <summary>
    /// 把当前已经存在的 Tuning Lab 副本（比如 Bake Tuning Lab 烘焙出来的场景）转成关卡：
    /// 清掉生成物、挂 LabLevel、写入基准数据、保存。基准场景 TuningLab.unity 本身禁止转换。
    /// </summary>
    public static bool ConvertCurrent(out string error)
    {
        error = null;
        Scene scene = EditorSceneManager.GetActiveScene();

        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            error = "当前场景还没有保存成 .unity 文件。";
            return false;
        }

        if (scene.path == LabScenePath)
        {
            error = "TuningLab.unity 是绝对基准场景，不能直接转成关卡。\n先另存为副本再转换。";
            return false;
        }

        if (FindInScene<LabLevel>(scene) != null) return true;

        if (FindInScene<TuningLab>(scene) == null)
        {
            error = "当前场景里没有 TuningLab 组件，不是从调试台派生的场景。";
            return false;
        }

        if (!PrepareScene(scene, out error)) return false;
        EditorSceneManager.SaveScene(scene, scene.path);
        Debug.Log("[LabLevelScene] 已把当前场景转成关卡：" + scene.path);
        return true;
    }

    [MenuItem("GameJam/Open Tuning Lab Scene")]
    public static void OpenLab()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScenePath) == null)
        {
            EditorUtility.DisplayDialog("打开 Tuning Lab", "找不到基准场景：\n" + LabScenePath, "好");
            return;
        }
        EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
    }

    public static void OpenScene(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    static bool EnsureLabScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScenePath) != null) return true;

        // 没有基准场景就先让 TuningLabMenu 生成一份。它内部会 NewScene，所以先提示保存。
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;
        if (!Application.isBatchMode)
        {
            bool build = EditorUtility.DisplayDialog(
                "缺少 Tuning Lab 基准场景",
                "找不到 " + LabScenePath + "。\n先跑一次 GameJam → Build Tuning Lab 生成基准，再派生关卡。\n\n现在生成？",
                "生成", "取消");
            if (!build) return false;
        }

        TuningLabMenu.BuildSilent();
        return AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScenePath) != null;
    }

    static bool CreateScene(string path, out string error)
    {
        error = null;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScenePath) == null)
        {
            error = "找不到基准场景 " + LabScenePath;
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            error = "目标场景已存在：" + path;
            return false;
        }

        EnsureFolder(Path.GetDirectoryName(path));

        if (!AssetDatabase.CopyAsset(LabScenePath, path))
        {
            error = "复制 " + LabScenePath + " → " + path + " 失败。";
            return false;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        if (!PrepareScene(scene, out error)) return false;

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            error = "保存场景失败：" + path;
            return false;
        }

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Rect bounds = TuningLab.BaselineBounds;
            var center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            view.Frame(new Bounds(center, new Vector3(bounds.width + 4f, bounds.height + 4f, 1f)), false);
            view.Repaint();
        }

        Debug.Log("[LabLevelScene] 关卡场景已从 Tuning Lab 派生：" + path);
        return true;
    }

    /// <summary>把复制过来的 TuningLab 场景收拾成"只有一个 TuningLab + LabLevel 数据"的关卡场景。</summary>
    static bool PrepareScene(Scene scene, out string error)
    {
        error = null;

        TuningLab lab = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (lab != null)
            {
                Object.DestroyImmediate(root);
                continue;
            }

            var found = root.GetComponentInChildren<TuningLab>(true);
            if (found == null)
            {
                Object.DestroyImmediate(root);
                continue;
            }

            lab = found;

            // 基准场景正常只有 TuningLab 一个根；如果它被放在别的根下面，先摘出来再清根。
            if (lab.transform.parent != null)
            {
                var oldRoot = lab.transform.root.gameObject;
                lab.transform.SetParent(null, true);
                Object.DestroyImmediate(oldRoot);
            }
        }

        if (lab == null)
        {
            error = "Tuning Lab 场景里没有 TuningLab 组件。";
            return false;
        }

        // 烘焙版会有 Floor/Step/Player/Camera 等生成物。关卡场景只保留数据，
        // Play 时由 TuningLab 重新生成，避免"烘焙一份 + 数据再生成一份"。
        for (int i = lab.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(lab.transform.GetChild(i).gameObject);

        lab.autoBuild = true;
        lab.roomScale = 1f;

        var level = lab.GetComponent<LabLevel>();
        if (level == null) level = lab.gameObject.AddComponent<LabLevel>();
        level.ApplyBaseline();
        CopyCameraDefaults(lab, level.Data);
        EnsureDefaultPartition(level.Data);
        level.ClearGenerated();          // 预览物体不写进场景，打开场景时 OnEnable 会重建

        lab.level = level;
        EditorUtility.SetDirty(lab);
        EditorUtility.SetDirty(level);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    /// <summary>新关卡从 TuningLab 的默认相机字段起步；之后可以在关卡编辑器里单独改。</summary>
    public static void CopyCameraDefaults(TuningLab lab, LabLevelData data)
    {
        if (lab == null || data == null) return;

        data.cameraMinSize = lab.defaultCameraMinSize > 0f
            ? lab.defaultCameraMinSize
            : TuningLab.DefaultCameraMinSize;
        data.cameraMaxSize = lab.defaultCameraMaxSize > 0f
            ? lab.defaultCameraMaxSize
            : TuningLab.DefaultCameraMaxSize;

        Rect bounds = lab.defaultCameraBounds;
        bounds = NormalizeRect(bounds);
        if (bounds.width > 0f && bounds.height > 0f)
        {
            data.bounds = new RectInt(
                Mathf.RoundToInt(bounds.xMin),
                Mathf.RoundToInt(bounds.yMin),
                Mathf.RoundToInt(bounds.width),
                Mathf.RoundToInt(bounds.height));
        }

        data.Normalize();
    }

    static Rect NormalizeRect(Rect rect)
    {
        if (rect.width < 0f)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }
        if (rect.height < 0f)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }
        return rect;
    }

    /// <summary>新关卡至少有一个覆盖整块编辑范围的分区，B 一出生就有相机范围可用。</summary>
    public static void EnsureDefaultPartition(LabLevelData data)
    {
        if (data == null) return;
        data.Normalize();
        if (data.partitions.Count > 0) return;
        data.AddPartition(data.bounds, "Level 1");
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(Path.GetFullPath(path));
        AssetDatabase.Refresh();
    }
}
#endif
