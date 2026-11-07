#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：操作、存档与关卡校验。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{
    // ------------------------------------------------------------------
    // 操作
    // ------------------------------------------------------------------

    void Record(string name)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(name);
        Undo.RecordObject(level, name);
    }

    void MarkDirty(bool rebuildPreview = false)
    {
        if (rebuildPreview && !Application.isPlaying) level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void MarkSceneDirty()
    {
        if (level == null) return;
        Scene scene = level.gameObject.scene;
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
    }

    void AfterStructuralEdit()
    {
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void SaveScene()
    {
        Scene scene = level.gameObject.scene;
        if (!scene.IsValid()) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[LabLevelEditor] 已保存 " + scene.path);
    }

    void Validate()
    {
        issues.Clear();
        var data = level.Data;

        if (data.bounds.width <= 0 || data.bounds.height <= 0)
            issues.Add("编辑范围的宽/高必须大于 0。");

        if (data.cameraMinSize <= 0f)
            issues.Add("相机最小 size 必须大于 0。");
        if (data.cameraMaxSize < data.cameraMinSize)
            issues.Add("相机最大 size 不能小于最小 size。");
        if (data.cameraMaxSize > 128f)
            issues.Add("相机最大 size 超过 128 格，画面会小到看不清。");

        if (data.partitions != null)
        {
            var partitionRects = new List<Rect>();
            for (int i = 0; i < data.partitions.Count; i++)
            {
                var partition = data.partitions[i];
                if (partition == null)
                {
                    issues.Add("第 " + i + " 个关卡分区是空引用。");
                    continue;
                }

                if (partition.bounds.width <= 0 || partition.bounds.height <= 0)
                    issues.Add("关卡分区「" + partition.name + "」宽/高必须大于 0。");

                Rect rect = LabLevelData.ToRect(partition.bounds);
                for (int j = 0; j < partitionRects.Count; j++)
                {
                    if (!partitionRects[j].Overlaps(rect)) continue;
                    issues.Add("关卡分区 " + j + " 和 " + i + " 重叠；B 会命中列表里靠前的那个。");
                }
                partitionRects.Add(rect);
            }

            if (data.partitions.Count > 0)
            {
                Vector2 probe = data.spawnB;
                probe.y += TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
                if (data.FindPartition(probe) < 0)
                    issues.Add("B 出生点不在任何关卡分区里，开局相机会用 TuningLab 默认范围。");
            }
        }

        if (data.solids == null || data.solids.Count == 0)
            issues.Add("关卡没有任何地形。");
        else
        {
            var seen = new HashSet<Vector2Int>();
            foreach (var cell in data.solids)
                if (!seen.Add(cell)) issues.Add("地形有重复格：" + cell);
        }

        CheckSpawn("A", data.spawnA, data);
        CheckSpawn("B", data.spawnB, data);

        if (Vector2.Distance(data.spawnA, data.spawnB) < 1f)
            issues.Add("A / B 出生点距离小于 1 格。");

        if (data.ropes != null)
        {
            for (int i = 0; i < data.ropes.Count; i++)
            {
                var rope = data.ropes[i];
                if (rope.cells < 1)
                {
                    issues.Add("第 " + i + " 根绳长度小于 1 格。");
                    continue;
                }

                if (rope.center.x < data.bounds.xMin || rope.center.x > data.bounds.xMax)
                    issues.Add("第 " + i + " 根绳的 x 不在编辑范围内。");
                if (rope.Bottom < data.bounds.yMin || rope.Top > data.bounds.yMax)
                    issues.Add("第 " + i + " 根绳的上下端不在编辑范围内。");
            }
        }

        if (data.keys != null)
        {
            var seenKeys = new HashSet<Vector2>();
            for (int i = 0; i < data.keys.Count; i++)
            {
                var key = data.keys[i];
                if (!data.ContainsPoint(key.position))
                    issues.Add("第 " + i + " 把钥匙不在编辑范围内：" + key.position);
                if (!seenKeys.Add(key.position))
                    issues.Add("第 " + i + " 把钥匙和前面的钥匙重合：" + key.position);

                RectInt cell = key.Cells();
                if (data.HasCell(new Vector2Int(cell.x, cell.y)))
                    issues.Add("第 " + i + " 把钥匙在地形里：" + new Vector2Int(cell.x, cell.y));
            }
        }

        if (data.recalls != null)
        {
            var seenRecalls = new HashSet<Vector2Int>();
            for (int i = 0; i < data.recalls.Count; i++)
            {
                var recall = data.recalls[i];
                if (!data.ContainsCell(recall.cell))
                    issues.Add("第 " + i + " 个召回道具不在编辑范围内：" + recall.cell);
                if (!seenRecalls.Add(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和前面的重合：" + recall.cell);
                if (data.HasCell(recall.cell))
                    issues.Add("第 " + i + " 个召回道具在地形里：" + recall.cell);
                if (data.HasSpike(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和地刺在同一格：" + recall.cell);
                if (data.HasIce(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和冰块在同一格：" + recall.cell);
                if (data.HasMagma(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和岩浆块在同一格：" + recall.cell);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, recall.cell))
                    issues.Add("第 " + i + " 个召回道具压在 A 的出生点上：" + recall.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, recall.cell))
                    issues.Add("第 " + i + " 个召回道具压在 B 的出生点上：" + recall.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具和第 " + k + " 把钥匙在同一格：" + recall.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + d + " 扇门里：" + recall.cell);

                if (data.respawnPoints != null)
                    for (int r = 0; r < data.respawnPoints.Count; r++)
                        if (data.respawnPoints[r].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + r + " 个重生点里：" + recall.cell);

                if (data.teleports != null)
                    for (int t = 0; t < data.teleports.Count; t++)
                        if (data.teleports[t].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + t + " 个传送点里：" + recall.cell);
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                var door = data.doors[i];
                if (!data.ContainsPoint(door.position))
                    issues.Add("第 " + i + " 扇门不在编辑范围内：" + door.position);

                RectInt cells = door.Cells();
                for (int y = cells.y; y < cells.y + cells.height; y++)
                {
                    var cell = new Vector2Int(cells.x, y);
                    if (data.HasCell(cell)) issues.Add("第 " + i + " 扇门和地形重叠：" + cell);
                }
            }
        }

        if (data.spikes != null)
        {
            for (int i = 0; i < data.spikes.Count; i++)
            {
                var spike = data.spikes[i];
                if (!data.ContainsCell(spike.cell))
                    issues.Add("第 " + i + " 根刺不在编辑范围内：" + spike.cell);
                if (data.HasCell(spike.cell))
                    issues.Add("第 " + i + " 根刺和地形重叠：" + spike.cell);
                if (!data.HasCell(spike.BaseCell))
                    issues.Add("第 " + i + " 根刺悬空，底座那格不是实心：" + spike.cell + " 需要 " + spike.BaseCell);

                if (SpawnTouchesSpike(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, spike))
                    issues.Add("第 " + i + " 根刺压在 A 的出生点上：" + spike.cell);
                if (SpawnTouchesSpike(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, spike))
                    issues.Add("第 " + i + " 根刺压在 B 的出生点上：" + spike.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().x == spike.cell.x && data.keys[k].Cells().y == spike.cell.y)
                            issues.Add("第 " + i + " 根刺和第 " + k + " 把钥匙在同一格：" + spike.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(spike.cell))
                            issues.Add("第 " + i + " 根刺在第 " + d + " 扇门里：" + spike.cell);
            }
        }

        if (data.ices != null)
        {
            for (int i = 0; i < data.ices.Count; i++)
            {
                var ice = data.ices[i];
                if (!data.ContainsCell(ice.cell))
                    issues.Add("第 " + i + " 块冰不在编辑范围内：" + ice.cell);
                if (data.HasCell(ice.cell))
                    issues.Add("第 " + i + " 块冰和地形重叠：" + ice.cell);
                if (data.HasSpike(ice.cell))
                    issues.Add("第 " + i + " 块冰和地刺在同一格：" + ice.cell);
                if (data.HasMagma(ice.cell))
                    issues.Add("第 " + i + " 块冰和岩浆块在同一格：" + ice.cell);
                if (ice.groupId < 0)
                    issues.Add("第 " + i + " 块冰的组号不能是负数：" + ice.groupId);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, ice.cell))
                    issues.Add("第 " + i + " 块冰压在 A 的出生点上：" + ice.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, ice.cell))
                    issues.Add("第 " + i + " 块冰压在 B 的出生点上：" + ice.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(ice.cell))
                            issues.Add("第 " + i + " 块冰和第 " + k + " 把钥匙在同一格：" + ice.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(ice.cell))
                            issues.Add("第 " + i + " 块冰在第 " + d + " 扇门里：" + ice.cell);
            }
        }

        if (data.magmas != null)
        {
            for (int i = 0; i < data.magmas.Count; i++)
            {
                var magma = data.magmas[i];
                if (!data.ContainsCell(magma.cell))
                    issues.Add("第 " + i + " 块岩浆不在编辑范围内：" + magma.cell);
                if (data.HasCell(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和地形重叠：" + magma.cell);
                if (data.HasSpike(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和地刺在同一格：" + magma.cell);
                if (data.HasIce(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和冰块在同一格：" + magma.cell);
                if (magma.groupId < 0)
                    issues.Add("第 " + i + " 块岩浆的组号不能是负数：" + magma.groupId);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, magma.cell))
                    issues.Add("第 " + i + " 块岩浆压在 A 的出生点上：" + magma.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, magma.cell))
                    issues.Add("第 " + i + " 块岩浆压在 B 的出生点上：" + magma.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(magma.cell))
                            issues.Add("第 " + i + " 块岩浆和第 " + k + " 把钥匙在同一格：" + magma.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(magma.cell))
                            issues.Add("第 " + i + " 块岩浆在第 " + d + " 扇门里：" + magma.cell);
            }
        }

        if (data.hiddenBlocks != null)
        {
            for (int i = 0; i < data.hiddenBlocks.Count; i++)
            {
                var block = data.hiddenBlocks[i];
                if (!data.ContainsCell(block.cell))
                    issues.Add("第 " + i + " 条隐藏方块不在编辑范围内：" + block.cell);
                if (data.HasCell(block.cell))
                    issues.Add("第 " + i + " 条隐藏方块和地形重叠：" + block.cell);
                if (block.groupId < 0)
                    issues.Add("第 " + i + " 条隐藏方块的组号不能是负数：" + block.groupId);

                // 无实体不挡人，但出生点整个压在未揭示的隐藏格里会开局即触发揭示。
                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, block.cell))
                    issues.Add("第 " + i + " 条隐藏方块压在 A 的出生点上：" + block.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, block.cell))
                    issues.Add("第 " + i + " 条隐藏方块压在 B 的出生点上：" + block.cell);
            }
        }

        if (data.repairBlocks != null)
        {
            for (int i = 0; i < data.repairBlocks.Count; i++)
            {
                var block = data.repairBlocks[i];
                if (!data.ContainsCell(block.cell))
                    issues.Add("第 " + i + " 块修复方块不在编辑范围内：" + block.cell);
                if (data.HasCell(block.cell))
                    issues.Add("第 " + i + " 块修复方块和地形重叠：" + block.cell);
                if (data.HasSpike(block.cell))
                    issues.Add("第 " + i + " 块修复方块和地刺在同一格：" + block.cell);
                if (data.HasIce(block.cell))
                    issues.Add("第 " + i + " 块修复方块和冰块在同一格：" + block.cell);
                if (data.HasMagma(block.cell))
                    issues.Add("第 " + i + " 块修复方块和岩浆块在同一格：" + block.cell);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, block.cell))
                    issues.Add("第 " + i + " 块修复方块压在 A 的出生点上：" + block.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, block.cell))
                    issues.Add("第 " + i + " 块修复方块压在 B 的出生点上：" + block.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(block.cell))
                            issues.Add("第 " + i + " 块修复方块和第 " + k + " 把钥匙在同一格：" + block.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(block.cell))
                            issues.Add("第 " + i + " 块修复方块在第 " + d + " 扇门里：" + block.cell);
            }
        }

        if (data.respawnPoints != null)
        {
            for (int i = 0; i < data.respawnPoints.Count; i++)
            {
                var point = data.respawnPoints[i];
                if (!data.ContainsPoint(point.position))
                    issues.Add("第 " + i + " 个重生点不在编辑范围内：" + point.position);

                var cell = new Vector2Int(
                    Mathf.FloorToInt(point.position.x), Mathf.FloorToInt(point.position.y));
                if (data.HasCell(cell))
                    issues.Add("第 " + i + " 个重生点在地形里：" + cell);

                Rect rect = RespawnPointRect(point.position);
                for (int j = 0; j < i; j++)
                {
                    if (rect.Overlaps(RespawnPointRect(data.respawnPoints[j].position)))
                        issues.Add("第 " + i + " 个重生点和第 " + j + " 个重叠，接触时会抢同一个触发区。");
                }
            }
        }

        if (data.teleports != null)
        {
            var seenTeleports = new HashSet<Vector2>();
            for (int i = 0; i < data.teleports.Count; i++)
            {
                var point = data.teleports[i];
                if (!data.ContainsPoint(point.position))
                    issues.Add("第 " + i + " 个传送点不在编辑范围内：" + point.position);

                var cell = new Vector2Int(
                    Mathf.FloorToInt(point.position.x), Mathf.FloorToInt(point.position.y));
                if (data.HasCell(cell))
                    issues.Add("第 " + i + " 个传送点在地形里：" + cell);

                if (!seenTeleports.Add(point.position))
                    issues.Add("第 " + i + " 个传送点和前面的传送点重合：" + point.position);

                Rect rect = RespawnPointRect(point.position);
                for (int j = 0; j < i; j++)
                {
                    if (data.teleports[j].position == point.position) continue;
                    if (rect.Overlaps(RespawnPointRect(data.teleports[j].position)))
                        issues.Add("第 " + i + " 个传送点和第 " + j + " 个触发区重叠，玩家会同时踩进两个点。");
                }
            }
        }

        if (data.teleports != null && data.teleports.Count == 1)
            issues.Add("只有一个传送点：运行时最多只能标记一个，无法打开选择界面。至少放置两个并让双人进入标记。");

        validated = true;

        if (issues.Count == 0)
        {
            Debug.Log(string.Format("[LabLevelEditor] 校验通过：{0} 个实心格，{1} 个碰撞矩形，{2} 根绳，{3} 把钥匙，{4} 个召回道具，{5} 扇门，{6} 根刺，{7} 块冰，{8} 块岩浆，{9} 条隐藏方块，{10} 块修复方块，{11} 个重生点，{12} 个传送点，{13} 个关卡分区。",
                level.SolidCellCount, level.MergedRectCount, level.RopeCount, level.KeyCount, level.RecallCount,
                level.DoorCount, level.SpikeCount, level.IceCount, level.MagmaCount, level.HiddenBlockCount,
                level.RepairBlockCount, level.RespawnPointCount, level.TeleportCount,
                data.partitions != null ? data.partitions.Count : 0), level);
        }
        else
        {
            foreach (var issue in issues) Debug.LogWarning("[LabLevelEditor] " + issue, level);
        }

        Repaint();
    }

    /// <summary>出生点上的玩家方框会不会一开场就蹭到这根刺（按刺带自己的矩形算，留 1 像素不算）。</summary>
    static bool SpawnTouchesSpike(Vector2 feet, float size, LabSpikeSpec spike)
    {
        var player = new Rect(feet.x - size * 0.5f, feet.y, size, size);
        Vector2 center = spike.StripCenter;
        Vector2 strip = spike.StripSize;
        return player.Overlaps(new Rect(center.x - strip.x * 0.5f, center.y - strip.y * 0.5f, strip.x, strip.y));
    }

    /// <summary>出生点上的玩家方框会不会一开场就压在冰块上。</summary>
    static bool SpawnTouchesCell(Vector2 feet, float size, Vector2Int cell)
    {
        var player = new Rect(feet.x - size * 0.5f, feet.y, size, size);
        return player.Overlaps(new Rect(cell.x, cell.y, 1f, 1f));
    }

    void CheckSpawn(string name, Vector2 feet, LabLevelData data)
    {
        if (!data.ContainsPoint(feet))
            issues.Add(name + " 出生点不在编辑范围内：" + feet);

        var cell = new Vector2Int(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
        if (data.HasCell(cell))
            issues.Add(name + " 出生点在地形里：" + cell);
        if (data.HasCell(cell + Vector2Int.up))
            issues.Add(name + " 出生点头顶被地形堵住：" + (cell + Vector2Int.up));
    }

    void FrameLevel()
    {
        var data = level.Data;
        RectInt content = data.ContentBounds();
        RectInt bounds = data.bounds;

        int x0 = Mathf.Min(content.x, bounds.x);
        int y0 = Mathf.Min(content.y, bounds.y);
        int x1 = Mathf.Max(content.x + content.width, bounds.x + bounds.width);
        int y1 = Mathf.Max(content.y + content.height, bounds.y + bounds.height);

        var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        var size = new Vector3(Mathf.Max(8f, x1 - x0 + 4f), Mathf.Max(8f, y1 - y0 + 4f), 1f);

        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        view.Frame(new Bounds(center, size), false);
        view.Repaint();
    }

    static RectInt ToRectInt(Rect rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.xMin),
            Mathf.RoundToInt(rect.yMin),
            Mathf.RoundToInt(rect.width),
            Mathf.RoundToInt(rect.height));
    }

    /// <summary>重生点的 3×3 触发区（中心坐标 → 世界矩形）。</summary>
    static Rect RespawnPointRect(Vector2 center)
        => new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f);

}
#endif
