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

    [MenuItem("GameJam/Build Tuning Lab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        EditorUtility.DisplayDialog(
            "Tuning Lab",
            "已生成 " + ScenePath + "\n\n按 Play 开始：\nP1  A/D 移动，空格跳，左Shift 冲刺\nP2  J/L 移动，Enter 跳，右Shift 冲刺\nR 重置",
            "好");
    }

    /// <summary>给 -executeMethod 用的无对话框版本。</summary>
    public static void BuildSilent()
    {
        BuildScene();
        Debug.Log("[TuningLab] scene built: " + ScenePath);
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("TuningLab");
        go.AddComponent<TuningLab>();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        // 只把自己加进 Build Settings，不动已有的场景
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath))
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        AssetDatabase.Refresh();
    }
}
#endif
