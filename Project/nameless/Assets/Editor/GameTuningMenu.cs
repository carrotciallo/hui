#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成全局手感配置。放在 Assets/Resources/GameTuning.asset，
/// 所有场景的玩家在 Awake 时读它 —— 改一个资产，全部场景生效。
/// </summary>
public static class GameTuningMenu
{
    const string Dir = "Assets/Resources";
    const string Path = Dir + "/GameTuning.asset";

    [MenuItem("GameJam/Create Game Tuning Asset")]
    public static void Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameTuning>(Path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("Game Tuning",
                "已经存在：\n" + Path + "\n\n已在 Project 面板里选中。", "好");
            return;
        }

        Directory.CreateDirectory(Dir);
        var asset = ScriptableObject.CreateInstance<GameTuning>();
        AssetDatabase.CreateAsset(asset, Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        EditorUtility.DisplayDialog("Game Tuning",
            "已生成：\n" + Path + "\n\n改这里的值，所有场景都会生效。", "好");
    }
}
#endif
