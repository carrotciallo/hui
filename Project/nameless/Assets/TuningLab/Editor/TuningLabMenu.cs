#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 生成调试台场景。场景里只有一个物体，其余（地面、玩家、相机、HUD）在 Play 时由 TuningLab 建出来。
/// </summary>
public static class TuningLabMenu
{
    const string ScenePath = "Assets/Scenes/TuningLab.unity";
    const string ScenePath4x = "Assets/Scenes/TuningLab4x.unity";
    const string ScenePathBaked = "Assets/Scenes/TuningLabEditable.unity";

    [MenuItem("GameJam/Build Tuning Lab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene(ScenePath, 1f, true);
        EditorUtility.DisplayDialog(
            "Tuning Lab",
            "已生成 " + ScenePath + "\n\n按 Play 开始：\nA  A/D + 空格 + 左Shift\nB  方向键 + / + .",
            "好");
    }

    /// <summary>调试台的 4 倍场地副本：地面和墙壁放大四倍，其余原封不动。</summary>
    [MenuItem("GameJam/Build Tuning Lab (4x Room)")]
    public static void Build4x()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene(ScenePath4x, 4f, true);
        EditorUtility.DisplayDialog(
            "Tuning Lab 4x",
            "已生成 " + ScenePath4x + "\n地面和墙壁放大 4 倍，台阶位置不变，默认开自由相机。",
            "好");
    }

    /// <summary>
    /// 烘焙一份"实体"场景：把运行时才生成的地面、玩家、相机、绳子全部实例化出来并存盘，
    /// 之后就能在编辑器里直接选中、拖动、增删，不必再按 Play。
    /// </summary>
    [MenuItem("GameJam/Bake Tuning Lab To Editable Scene")]
    public static void BakeEditable()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("TuningLab");

        var lab = go.AddComponent<TuningLab>();
        lab.autoBuild = false;          // 已经烘焙成实体，别再自动生成一遍
        lab.roomScale = 1f;
        lab.freeCamera = true;
        lab.BuildAll();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePathBaked));
        EditorSceneManager.SaveScene(scene, ScenePathBaked);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "TuningLab Baked",
            "已生成可编辑场景：\n" + ScenePathBaked +
            "\n\n场景里的地面、台阶、玩家、相机、绳子都是真实物体，可以直接改。\n" +
            "注意 TuningLab 上的 autoBuild 已关闭。",
            "好");
    }

    /// <summary>给 -executeMethod 用的无对话框版本。</summary>
    public static void BuildSilent()
    {
        BuildScene(ScenePath, 1f, true);
        Debug.Log("[TuningLab] scene built: " + ScenePath);
    }

    static void BuildScene(string path, float roomScale, bool freeCamera)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("TuningLab");
        var lab = go.AddComponent<TuningLab>();
        lab.roomScale = roomScale;
        lab.freeCamera = freeCamera;
        lab.defaultCameraBounds = TuningLab.RoomBoundsForScale(roomScale);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorSceneManager.SaveScene(scene, path);

        // 只把自己加进 Build Settings，不动已有的场景
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == path))
            list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();

        AssetDatabase.Refresh();
    }
}
#endif
