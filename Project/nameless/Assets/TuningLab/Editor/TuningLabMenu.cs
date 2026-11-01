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

    [MenuItem("GameJam/Build Tuning Lab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene(ScenePath, 1f, false);
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

    /// <summary>给 -executeMethod 用的无对话框版本。</summary>
    public static void BuildSilent()
    {
        BuildScene(ScenePath, 1f, false);
        Debug.Log("[TuningLab] scene built: " + ScenePath);
    }

    static void BuildScene(string path, float roomScale, bool freeCamera)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("TuningLab");
        var lab = go.AddComponent<TuningLab>();
        lab.roomScale = roomScale;
        lab.freeCamera = freeCamera;

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
