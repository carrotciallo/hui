#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键生成隐藏方块演示关卡 Assets/Scenes/Levels/HiddenBlock_Test.unity。
/// 数据从 Tuning Lab 基准派生，加三组假墙：组 1（6..7 列）内藏一把钥匙，
/// 组 3 单格 (7,0) 嵌在组 1 里（验证同格多组独立判定），组 2（9..10 列）单独一组。
/// 两组假墙上方各盖一行实心 lintel，让整列从地面看起像 3 格厚的墙；
/// 底部两格无实体，人可以直接走进去，完全落入后整组变半透明。
/// </summary>
public static class HiddenBlockDemoLevel
{
    public const string DemoScenePath = LabLevelScene.LevelFolder + "/HiddenBlock_Test.unity";

    [MenuItem("GameJam/Build HiddenBlock Demo Level")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!LabLevelScene.CreateSilent(DemoScenePath)) return;

        Scene scene = EditorSceneManager.GetActiveScene();
        LabLevel level = FindLevel(scene);
        if (level == null)
        {
            Debug.LogError("[HiddenBlockDemo] 生成的场景里找不到 LabLevel。");
            return;
        }

        level.CopyFrom(BuildDemoData());
        EditorUtility.SetDirty(level);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, DemoScenePath)) return;

        Debug.Log(
            "[HiddenBlockDemo] 演示关卡已生成：" + DemoScenePath + "\n" +
            "Play 后从左侧出生点向右走，穿过 x=6..7 和 x=9..10 两段\"墙\"：\n" +
            "完全进入一组后该组永久变半透明，露出藏在墙里的钥匙；按 R 重置地图不会让它恢复。");
    }

    static LabLevelData BuildDemoData()
    {
        var data = TuningLab.CaptureBaseline();
        data.spawnA = new Vector2(-2f, 0f);
        data.spawnB = new Vector2(0f, 0f);

        // lintel：把假墙伪装成 3 格厚的墙。
        AddCells(data.solids, new[]
        {
            new Vector2Int(6, 2), new Vector2Int(7, 2),
            new Vector2Int(9, 2), new Vector2Int(10, 2)
        });

        // 组 1 / 组 2：两段 2×2 假墙。组 3：嵌在组 1 里的单格组。
        AddHidden(data, 1, new[]
        {
            new Vector2Int(6, 0), new Vector2Int(7, 0),
            new Vector2Int(6, 1), new Vector2Int(7, 1)
        });
        AddHidden(data, 2, new[]
        {
            new Vector2Int(9, 0), new Vector2Int(10, 0),
            new Vector2Int(9, 1), new Vector2Int(10, 1)
        });
        AddHidden(data, 3, new[] { new Vector2Int(7, 0) });

        // 藏在组 1 里的钥匙：揭示前看不见，机制照常可捡。
        data.keys.Add(new LabKeySpec(new Vector2(6.5f, 0.5f)));

        data.Normalize();
        data.AddPartition(data.bounds, "Level 1");
        return data;
    }

    static void AddCells(List<Vector2Int> cells, Vector2Int[] additions)
    {
        for (int i = 0; i < additions.Length; i++) cells.Add(additions[i]);
    }

    static void AddHidden(LabLevelData data, int groupId, Vector2Int[] cells)
    {
        for (int i = 0; i < cells.Length; i++) data.PlaceHiddenBlock(cells[i], groupId);
    }

    static LabLevel FindLevel(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var level = root.GetComponentInChildren<LabLevel>(true);
            if (level != null) return level;
        }
        return null;
    }
}
#endif
