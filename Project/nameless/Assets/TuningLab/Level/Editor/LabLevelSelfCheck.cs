#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 关卡基准自检：证明"从 Tuning Lab 抓出来的关卡数据"就是脚本生成的基准房间，
/// 并且格子 → 合并矩形 → 格子 的往返不丢格、不重叠。
///
/// 菜单 GameJam → Run Level Baseline Self Check，
/// 或 -executeMethod LabLevelSelfCheck.RunBatch（失败退出码 1）。
/// </summary>
public static class LabLevelSelfCheck
{
    [MenuItem("GameJam/Run Level Baseline Self Check")]
    public static void Run()
    {
        int fails;
        StringBuilder report = Check(out fails);
        Debug.Log("[LabLevelSelfCheck]\n" + report);
    }

    public static void RunBatch()
    {
        bool ok = RunBatchSilent(out int fails);
        Debug.Log("[LabLevelSelfCheck] " + (ok ? "全部通过。" : ("失败 " + fails + " 项。")));
        EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>无对话框版本：跑完整自检并输出报告，返回是否全部通过。</summary>
    public static bool RunBatchSilent(out int fails)
    {
        StringBuilder report = Check(out fails);
        Debug.Log("[LabLevelSelfCheck]\n" + report);
        return fails == 0;
    }

    static StringBuilder Check(out int fails)
    {
        var sb = new StringBuilder();
        int failures = 0;
        fails = 0;

        sb.AppendLine("=== 1. 从 Tuning Lab 抓基准房间 ===");
        LabLevelData data = TuningLab.CaptureBaseline();

        Expect(sb, ref failures, data.solids.Count == 73,
            "实心格 = 73（地板 26 + 台阶 15 + 墙 32），实际 " + data.solids.Count);
        Expect(sb, ref failures, data.ropes.Count == 1,
            "竖绳 = 1 根，实际 " + data.ropes.Count);
        if (data.ropes.Count == 1)
        {
            LabRopeSpec rope = data.ropes[0];
            Expect(sb, ref failures,
                Mathf.Abs(rope.center.x - TuningLab.RopeCenterX) < 0.0001f &&
                Mathf.Abs(rope.center.y - TuningLab.RopeCenterY) < 0.0001f &&
                rope.cells == TuningLab.RopeCells,
                string.Format("绳 = ({0}, {1}) 长 {2}", rope.center.x, rope.center.y, rope.cells));
        }

        Expect(sb, ref failures, SameRect(data.bounds, TuningLab.BaselineBounds),
            "相机范围 = " + data.bounds);
        Expect(sb, ref failures, SamePoint(data.spawnA, TuningLab.SpawnAFeet), "A 落点 = " + data.spawnA);
        Expect(sb, ref failures, SamePoint(data.spawnB, TuningLab.SpawnBFeet), "B 落点 = " + data.spawnB);
        Expect(sb, ref failures,
            Mathf.Abs(data.cameraMinSize - TuningLab.DefaultCameraMinSize) < 0.0001f,
            "相机最小 size = " + TuningLab.DefaultCameraMinSize);
        Expect(sb, ref failures,
            Mathf.Abs(data.cameraMaxSize - TuningLab.DefaultCameraMaxSize) < 0.0001f,
            "相机最大 size = " + TuningLab.DefaultCameraMaxSize);

        sb.AppendLine();
        sb.AppendLine("=== 2. 格子 → 合并矩形（应精确还原 8 块）===");
        var expected = new List<RectInt>
        {
            new RectInt(-13, -1, 26, 1),   // 地板
            new RectInt(3, 1, 3, 1),       // Step_y02：中心 4.5
            new RectInt(0, 3, 3, 1),       // Step_y04：中心 1.5
            new RectInt(-3, 5, 3, 1),      // Step_y06：中心 -1.5
            new RectInt(-6, 7, 3, 1),      // Step_y08：中心 -4.5
            new RectInt(-9, 9, 3, 1),      // Step_y10：中心 -7.5
            new RectInt(-14, 0, 1, 16),    // WallL
            new RectInt(13, 0, 1, 16)      // WallR
        };

        List<RectInt> merged = data.MergedRects();
        Expect(sb, ref failures, merged.Count == expected.Count,
            "合并矩形 = " + expected.Count + "，实际 " + merged.Count);
        foreach (var rect in expected)
            Expect(sb, ref failures, ContainsRect(merged, rect), "包含 " + rect);

        // 往返：把所有合并矩形展开回格子，必须和原集合完全一致且不重叠
        var roundTrip = new HashSet<Vector2Int>();
        bool overlap = false;
        foreach (var rect in merged)
            for (int y = rect.y; y < rect.y + rect.height; y++)
                for (int x = rect.x; x < rect.x + rect.width; x++)
                    if (!roundTrip.Add(new Vector2Int(x, y))) overlap = true;

        Expect(sb, ref failures, !overlap, "合并矩形互不重叠");
        Expect(sb, ref failures, roundTrip.Count == data.solids.Count,
            "展开后格数 = " + data.solids.Count + "，实际 " + roundTrip.Count);
        foreach (var cell in data.solids)
            if (!roundTrip.Contains(cell)) Expect(sb, ref failures, false, "丢格 " + cell);

        sb.AppendLine();
        sb.AppendLine("=== 3. 合并算法（孤立块 / L 形）===");
        var twoBlocks = new LabLevelData();
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                twoBlocks.AddCell(new Vector2Int(x, y));
        twoBlocks.AddCell(new Vector2Int(5, 0));
        var twoRects = twoBlocks.MergedRects();
        Expect(sb, ref failures, twoRects.Count == 2, "3×2 + 1 格 → 2 个矩形，实际 " + twoRects.Count);
        Expect(sb, ref failures, ContainsRect(twoRects, new RectInt(0, 0, 3, 2)), "含 3×2");
        Expect(sb, ref failures, ContainsRect(twoRects, new RectInt(5, 0, 1, 1)), "含 1×1");

        var lShape = new LabLevelData();
        lShape.AddCell(new Vector2Int(0, 0));
        lShape.AddCell(new Vector2Int(1, 0));
        lShape.AddCell(new Vector2Int(0, 1));
        var lRects = lShape.MergedRects();
        int lCells = 0;
        foreach (var rect in lRects) lCells += rect.width * rect.height;
        Expect(sb, ref failures, lCells == 3, "L 形 3 格不丢，实际 " + lCells);
        Expect(sb, ref failures, lRects.Count == 2, "L 形拆成 2 个矩形，实际 " + lRects.Count);

        sb.AppendLine();
        sb.AppendLine("=== 4. 归一化 / 拒绝不对齐矩形 ===");
        var dup = new LabLevelData();
        dup.solids.Add(new Vector2Int(1, 0));
        dup.solids.Add(new Vector2Int(0, 0));
        dup.solids.Add(new Vector2Int(1, 0));
        dup.Normalize();
        Expect(sb, ref failures, dup.solids.Count == 2, "重复格去重后 = 2，实际 " + dup.solids.Count);
        if (dup.solids.Count == 2)
            Expect(sb, ref failures,
                dup.solids[0] == new Vector2Int(0, 0) && dup.solids[1] == new Vector2Int(1, 0),
                "排序（y 再 x）正确");

        var aligned = new LabLevelData();
        Expect(sb, ref failures, aligned.AddAlignedRect(new Vector2(0f, -0.5f), new Vector2(26f, 1f)),
            "基准地板能写进格");
        Expect(sb, ref failures, aligned.solids.Count == 26, "地板 = 26 格，实际 " + aligned.solids.Count);
        Expect(sb, ref failures, !aligned.AddAlignedRect(new Vector2(-54f, 32f), new Vector2(1f, 64f)),
            "4 倍场地的墙（不对齐）被拒绝");

        var cameraClamp = new LabLevelData { cameraMinSize = 10f, cameraMaxSize = 5f };
        cameraClamp.Normalize();
        Expect(sb, ref failures, cameraClamp.cameraMaxSize >= cameraClamp.cameraMinSize,
            "最大 size 被抬到最小 size");

        var legacy = new LabLevelData { cameraMinSize = 0f, cameraMaxSize = 0f };
        legacy.Normalize();
        Expect(sb, ref failures,
            Mathf.Abs(legacy.cameraMinSize - TuningLab.DefaultCameraMinSize) < 0.0001f &&
            Mathf.Abs(legacy.cameraMaxSize - TuningLab.DefaultCameraMaxSize) < 0.0001f,
            "缺失的相机值回落到 Tuning Lab 默认");

        sb.AppendLine();
        sb.AppendLine("=== 5. 基准出生点 / 绳 / 范围 ===");
        CheckSpawn(sb, ref failures, "A", data.spawnA, data);
        CheckSpawn(sb, ref failures, "B", data.spawnB, data);
        Expect(sb, ref failures, data.ContainsPoint(data.spawnA), "A 落点在相机范围内");
        Expect(sb, ref failures, data.ContainsPoint(data.spawnB), "B 落点在相机范围内");
        if (data.ropes.Count == 1)
        {
            LabRopeSpec rope = data.ropes[0];
            Expect(sb, ref failures,
                rope.Bottom >= data.bounds.yMin && rope.Top <= data.bounds.yMax,
                "绳的上下端在相机范围内");
        }

        RectInt content = data.ContentBounds();
        Expect(sb, ref failures,
            content.x == -14 && content.y == -1 && content.width == 28 && content.height == 17,
            "内容范围包含两面墙 = " + content);

        sb.AppendLine();
        sb.AppendLine("=== 6. 关卡分区：移动（带物件）/ 删除 ===");
        var partitioned = new LabLevelData();
        partitioned.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        partitioned.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        partitioned.AddCell(new Vector2Int(1, 1));
        partitioned.AddCell(new Vector2Int(21, 1));
        partitioned.ropes.Add(new LabRopeSpec(new Vector2(5f, 5f), 2));
        partitioned.ropes.Add(new LabRopeSpec(new Vector2(25f, 5f), 2));
        partitioned.spawnA = new Vector2(2f, 0f);
        partitioned.spawnB = new Vector2(22f, 0f);

        Expect(sb, ref failures, partitioned.FindPartition(new Vector2(5f, 5f)) == 0,
            "B=5,5 命中分区 0");
        Expect(sb, ref failures, partitioned.FindPartition(new Vector2(25f, 5f)) == 1,
            "B=25,5 命中分区 1");

        Expect(sb, ref failures, partitioned.AddPartition(new RectInt(5, 5, 10, 10)) == null,
            "重叠的新分区被拒绝");
        Expect(sb, ref failures, !partitioned.MovePartition(0, new Vector2Int(20, 0), true),
            "移动进另一个分区被拒绝");
        Expect(sb, ref failures, !partitioned.ResizePartition(0, new RectInt(0, 0, 25, 10)),
            "放大到重叠被拒绝");

        var touching = new LabLevelData();
        touching.AddPartition(new RectInt(0, 0, 10, 10), "A");
        Expect(sb, ref failures, touching.AddPartition(new RectInt(10, 0, 10, 10), "B") != null,
            "贴边不算重叠，可以相邻");

        var partitionA = partitioned.partitions[0];
        Expect(sb, ref failures, partitioned.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures,
            partitionA.bounds.x == 3 && partitionA.bounds.y == 4 &&
            partitionA.bounds.width == 10 && partitionA.bounds.height == 10,
            "分区 A 移到 (3,4)，大小不变");
        Expect(sb, ref failures, partitioned.HasCell(new Vector2Int(4, 5)),
            "A 里的地形跟着移动");
        Expect(sb, ref failures, !partitioned.HasCell(new Vector2Int(1, 1)),
            "旧位置的地形清掉");
        Expect(sb, ref failures, SamePoint(partitioned.ropes[0].center, new Vector2(8f, 9f)),
            "A 里的绳跟着移动");
        Expect(sb, ref failures, SamePoint(partitioned.spawnA, new Vector2(5f, 4f)),
            "A 里的出生点跟着移动");
        Expect(sb, ref failures, partitioned.HasCell(new Vector2Int(21, 1)),
            "分区 B 的地形不动");
        Expect(sb, ref failures, SamePoint(partitioned.ropes[1].center, new Vector2(25f, 5f)),
            "分区 B 的绳不动");
        Expect(sb, ref failures, SamePoint(partitioned.spawnB, new Vector2(22f, 0f)),
            "分区 B 的出生点不动");

        Expect(sb, ref failures, partitioned.DeletePartition(0, true),
            "删除分区 A（带内容）");
        Expect(sb, ref failures, partitioned.partitions.Count == 1,
            "只剩分区 B");
        Expect(sb, ref failures, !partitioned.HasCell(new Vector2Int(4, 5)),
            "A 里的地形被删");
        Expect(sb, ref failures, partitioned.ropes.Count == 1 &&
            SamePoint(partitioned.ropes[0].center, new Vector2(25f, 5f)),
            "只剩 B 的绳");
        Expect(sb, ref failures, partitioned.FindPartition(new Vector2(25f, 5f)) == 0,
            "删除后 B 分区索引变成 0");

        sb.AppendLine();
        sb.AppendLine("=== 7. 相机分区内 / B 出界 ===");
        var probeGo = new GameObject("CameraFrameProbe");
        probeGo.SetActive(false);
        var probeLab = probeGo.AddComponent<TuningLab>();
        probeLab.autoBuild = false;
        var probeLevel = probeGo.AddComponent<LabLevel>();
        probeLab.level = probeLevel;

        probeLevel.Data.cameraMinSize = 8f;
        probeLevel.Data.cameraMaxSize = 34f;
        probeLab.defaultCameraMinSize = 7f;
        probeLab.defaultCameraMaxSize = 30f;
        probeLevel.Data.AddPartition(new RectInt(0, 0, 10, 10), "P");
        probeLevel.Data.Normalize();
        probeGo.SetActive(true);

        probeLab.GetCameraFrame(new Vector2(5f, 5f), out Rect inBounds, out float inMin, out float inMax);
        Expect(sb, ref failures,
            Mathf.Abs(inBounds.x) < 0.001f && Mathf.Abs(inBounds.y) < 0.001f &&
            Mathf.Abs(inBounds.width - 10f) < 0.001f && Mathf.Abs(inBounds.height - 10f) < 0.001f,
            "B 在分区内 → 分区 bounds 当相机范围");
        Expect(sb, ref failures,
            Mathf.Abs(inMin - 8f) < 0.001f && Mathf.Abs(inMax - 34f) < 0.001f,
            "B 在分区内 → 用关卡 min/max");

        probeLab.GetCameraFrame(new Vector2(50f, 50f), out Rect outBounds, out float outMin, out float outMax);
        Expect(sb, ref failures, outBounds.width > 1000f && outBounds.height > 1000f,
            "B 出界 → 没有相机范围约束");
        Expect(sb, ref failures,
            Mathf.Abs(outMin - 8f) < 0.001f && Mathf.Abs(outMax - 34f) < 0.001f,
            "B 出界 → 仍用关卡 min/max");

        Object.DestroyImmediate(probeGo);

        sb.AppendLine();
        sb.AppendLine("=== 8. 钥匙 / 门：随分区移动、随分区删除 ===");
        var keyDoor = new LabLevelData();
        keyDoor.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        keyDoor.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        keyDoor.keys.Add(new LabKeySpec(new Vector2(4.5f, 1.5f)));
        keyDoor.keys.Add(new LabKeySpec(new Vector2(24.5f, 1.5f)));
        keyDoor.doors.Add(new LabDoorSpec(new Vector2(6.5f, 1.5f), Color.red));
        keyDoor.doors.Add(new LabDoorSpec(new Vector2(26.5f, 1.5f), Color.blue));
        keyDoor.Normalize();

        Expect(sb, ref failures, keyDoor.MovePartition(0, new Vector2Int(2, 3), true), "移动分区 A");
        Expect(sb, ref failures, SamePoint(keyDoor.keys[0].position, new Vector2(6.5f, 4.5f)),
            "A 里的钥匙跟着移动");
        Expect(sb, ref failures, SamePoint(keyDoor.keys[1].position, new Vector2(24.5f, 1.5f)),
            "分区 B 的钥匙不动");
        Expect(sb, ref failures, SamePoint(keyDoor.doors[0].position, new Vector2(8.5f, 4.5f)),
            "A 里的门跟着移动");
        Expect(sb, ref failures, SamePoint(keyDoor.doors[1].position, new Vector2(26.5f, 1.5f)),
            "分区 B 的门不动");

        RectInt doorCells = keyDoor.doors[1].Cells();
        Expect(sb, ref failures,
            doorCells.x == 26 && doorCells.y == 0 && doorCells.width == 1 && doorCells.height == 3,
            "门盖住 1×3 格：" + doorCells);

        var keyCell = keyDoor.keys[0].Cells();
        Expect(sb, ref failures, keyCell.x == 6 && keyCell.y == 4 && keyCell.width == 1 && keyCell.height == 1,
            "钥匙盖住 1×1 格：" + keyCell);

        var invisibleDoor = new LabLevelData();
        invisibleDoor.doors.Add(new LabDoorSpec(new Vector2(0.5f, 1.5f), new Color(1f, 1f, 1f, 0f)));
        invisibleDoor.Normalize();
        Expect(sb, ref failures, invisibleDoor.doors[0].color.a > 0.5f, "透明门色回落到默认色");

        var cloned = keyDoor.Clone();
        Expect(sb, ref failures,
            cloned.keys.Count == keyDoor.keys.Count && cloned.doors.Count == keyDoor.doors.Count,
            "Clone 带上钥匙和门");
        cloned.keys[0] = new LabKeySpec(new Vector2(0f, 0f));
        Expect(sb, ref failures, !SamePoint(keyDoor.keys[0].position, new Vector2(0f, 0f)), "Clone 是深拷贝");

        Expect(sb, ref failures, keyDoor.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            keyDoor.keys.Count == 1 && SamePoint(keyDoor.keys[0].position, new Vector2(24.5f, 1.5f)),
            "A 里的钥匙被删，B 的留下");
        Expect(sb, ref failures,
            keyDoor.doors.Count == 1 && SamePoint(keyDoor.doors[0].position, new Vector2(26.5f, 1.5f)),
            "A 里的门被删，B 的留下");

        sb.AppendLine();
        sb.AppendLine("=== 9. 地刺：几何 / 去重 / 随分区移动与删除 ===");
        Expect(sb, ref failures, data.spikes.Count == 0,
            "基准房间没有地刺，实际 " + data.spikes.Count);

        Expect(sb, ref failures,
            Mathf.Abs(LabSpikeSpec.Thickness - 6f / TuningLab.CellPixels) < 0.0001f,
            "刺厚 = 6 美术像素 = " + LabSpikeSpec.Thickness);

        var spikeUp = new LabSpikeSpec(new Vector2Int(5, 3), LabSpikeDir.Up);
        Expect(sb, ref failures,
            SamePoint(spikeUp.StripCenter, new Vector2(5.5f, 3.1875f))
            && spikeUp.StripSize == new Vector2(1f, 0.375f)
            && spikeUp.BaseCell == new Vector2Int(5, 2),
            "朝上：贴格底边 1×0.375，底座在下一格");
        var spikeDown = new LabSpikeSpec(new Vector2Int(5, 3), LabSpikeDir.Down);
        Expect(sb, ref failures,
            SamePoint(spikeDown.StripCenter, new Vector2(5.5f, 3.8125f))
            && spikeDown.BaseCell == new Vector2Int(5, 4),
            "朝下：贴格顶边，底座在上一格");
        var spikeLeft = new LabSpikeSpec(new Vector2Int(5, 3), LabSpikeDir.Left);
        Expect(sb, ref failures,
            SamePoint(spikeLeft.StripCenter, new Vector2(5.8125f, 3.5f))
            && spikeLeft.StripSize == new Vector2(0.375f, 1f)
            && spikeLeft.BaseCell == new Vector2Int(6, 3),
            "朝左：贴格右边 0.375×1，底座在右一格");
        var spikeRight = new LabSpikeSpec(new Vector2Int(5, 3), LabSpikeDir.Right);
        Expect(sb, ref failures,
            SamePoint(spikeRight.StripCenter, new Vector2(5.1875f, 3.5f))
            && spikeRight.BaseCell == new Vector2Int(4, 3),
            "朝右：贴格左边，底座在左一格");

        foreach (LabSpikeDir dir in System.Enum.GetValues(typeof(LabSpikeDir)))
        {
            var stripSpec = new LabSpikeSpec(new Vector2Int(5, 3), dir);
            var strip = new Rect(
                stripSpec.StripCenter.x - stripSpec.StripSize.x * 0.5f,
                stripSpec.StripCenter.y - stripSpec.StripSize.y * 0.5f,
                stripSpec.StripSize.x, stripSpec.StripSize.y);
            Expect(sb, ref failures,
                strip.xMin >= 5f - 0.0001f && strip.xMax <= 6f + 0.0001f &&
                strip.yMin >= 3f - 0.0001f && strip.yMax <= 4f + 0.0001f,
                dir + " 的刺带完全在本格内，不伸进底座格");
        }

        var spikeLevel = new LabLevelData();
        spikeLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        spikeLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        spikeLevel.PlaceSpike(new Vector2Int(2, 1), LabSpikeDir.Up);
        spikeLevel.PlaceSpike(new Vector2Int(22, 1), LabSpikeDir.Left);
        Expect(sb, ref failures, spikeLevel.spikes.Count == 2, "两格各一根刺");
        Expect(sb, ref failures, !spikeLevel.PlaceSpike(new Vector2Int(2, 1), LabSpikeDir.Up),
            "同格同方向重复放 = 无变化");
        Expect(sb, ref failures, spikeLevel.PlaceSpike(new Vector2Int(2, 1), LabSpikeDir.Right),
            "同格换方向 = 有变化");
        Expect(sb, ref failures,
            spikeLevel.spikes.Count == 2 &&
            spikeLevel.FindSpike(new Vector2Int(2, 1)).Value.dir == LabSpikeDir.Right,
            "改方向只有一根刺，方向已更新");
        Expect(sb, ref failures, !spikeLevel.HasSpike(new Vector2Int(7, 7)), "没放过的格没有刺");

        spikeLevel.spikes.Add(new LabSpikeSpec(new Vector2Int(2, 1), LabSpikeDir.Down));
        spikeLevel.Normalize();
        Expect(sb, ref failures, spikeLevel.spikes.Count == 2,
            "Normalize 同格去重，实际 " + spikeLevel.spikes.Count);

        Expect(sb, ref failures, spikeLevel.RemoveSpike(new Vector2Int(2, 1)), "删掉 A 的刺");
        Expect(sb, ref failures, !spikeLevel.RemoveSpike(new Vector2Int(2, 1)), "再删同一格返回 false");
        spikeLevel.PlaceSpike(new Vector2Int(2, 1), LabSpikeDir.Up);

        var spikeClone = spikeLevel.Clone();
        Expect(sb, ref failures, spikeClone.spikes.Count == 2, "Clone 带上地刺");
        spikeClone.spikes[0] = new LabSpikeSpec(new Vector2Int(99, 99), LabSpikeDir.Down);
        Expect(sb, ref failures, spikeLevel.HasSpike(new Vector2Int(2, 1)), "Clone 是深拷贝");

        Expect(sb, ref failures, spikeLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, spikeLevel.HasSpike(new Vector2Int(5, 5)), "A 里的刺跟着移动");
        Expect(sb, ref failures, !spikeLevel.HasSpike(new Vector2Int(2, 1)), "旧位置的刺没有留下");
        Expect(sb, ref failures, spikeLevel.HasSpike(new Vector2Int(22, 1)), "分区 B 的刺不动");

        Expect(sb, ref failures, spikeLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            spikeLevel.spikes.Count == 1 && spikeLevel.HasSpike(new Vector2Int(22, 1)),
            "A 里的刺被删，B 的留下");

        sb.AppendLine();
        sb.AppendLine("=== 10. 冰块：几何 / 编组 / 去重 / 随分区移动与删除 ===");
        Expect(sb, ref failures, data.ices.Count == 0,
            "基准房间没有冰块，实际 " + data.ices.Count);

        var iceSpec = new LabIceSpec(new Vector2Int(5, 3), 2);
        Expect(sb, ref failures,
            SamePoint(iceSpec.Center, new Vector2(5.5f, 3.5f))
            && iceSpec.Cells() == new RectInt(5, 3, 1, 1),
            "冰块占 1×1 格，落点是格中心");

        var iceLevel = new LabLevelData();
        iceLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        iceLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        iceLevel.PlaceIce(new Vector2Int(2, 1), 7);
        iceLevel.PlaceIce(new Vector2Int(22, 1), 7);
        Expect(sb, ref failures, iceLevel.ices.Count == 2, "两格各一块冰");
        Expect(sb, ref failures, !iceLevel.PlaceIce(new Vector2Int(2, 1), 7),
            "同格同组重复放 = 无变化");
        Expect(sb, ref failures, iceLevel.PlaceIce(new Vector2Int(2, 1), 9),
            "同格换组 = 有变化");
        Expect(sb, ref failures,
            iceLevel.ices.Count == 2 &&
            iceLevel.FindIce(new Vector2Int(2, 1)).Value.groupId == 9,
            "换组只有一块冰，组号已更新");
        Expect(sb, ref failures, !iceLevel.HasIce(new Vector2Int(7, 7)), "没放过的格没有冰");
        Expect(sb, ref failures, iceLevel.NextIceGroupId() == 10,
            "新建组 = 当前最大组号 + 1");
        Expect(sb, ref failures, iceLevel.GroupIces(7).Count == 1,
            "按组查询只返回同组冰块");

        iceLevel.ices.Add(new LabIceSpec(new Vector2Int(2, 1), 3));
        iceLevel.Normalize();
        Expect(sb, ref failures, iceLevel.ices.Count == 2,
            "Normalize 同格去重，实际 " + iceLevel.ices.Count);

        var iceClone = iceLevel.Clone();
        Expect(sb, ref failures, iceClone.ices.Count == 2, "Clone 带上冰块");
        iceClone.ices[0] = new LabIceSpec(new Vector2Int(99, 99), 0);
        Expect(sb, ref failures, iceLevel.HasIce(new Vector2Int(2, 1)) || iceLevel.HasIce(new Vector2Int(22, 1)),
            "Clone 是深拷贝");

        Expect(sb, ref failures, iceLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, iceLevel.HasIce(new Vector2Int(5, 5)), "A 里的冰块跟着移动");
        Expect(sb, ref failures, !iceLevel.HasIce(new Vector2Int(2, 1)), "旧位置的冰块没有留下");
        Expect(sb, ref failures, iceLevel.HasIce(new Vector2Int(22, 1)), "分区 B 的冰块不动");

        Expect(sb, ref failures, iceLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            iceLevel.ices.Count == 1 && iceLevel.HasIce(new Vector2Int(22, 1)),
            "A 里的冰块被删，B 的留下");

        sb.AppendLine();
        sb.AppendLine("=== 11. 重生点：去重 / 随分区移动与删除 ===");
        Expect(sb, ref failures, data.respawnPoints.Count == 0,
            "基准房间没有重生点，实际 " + data.respawnPoints.Count);

        var respawnLevel = new LabLevelData();
        respawnLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        respawnLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });

        Expect(sb, ref failures, respawnLevel.PlaceRespawnPoint(new Vector2Int(2, 1)), "A 里放一个重生点");
        Expect(sb, ref failures, respawnLevel.PlaceRespawnPoint(new Vector2Int(22, 1)), "B 里放一个重生点");
        Expect(sb, ref failures, !respawnLevel.PlaceRespawnPoint(new Vector2Int(2, 1)),
            "同一格重复放 = 无变化");
        Expect(sb, ref failures, respawnLevel.HasRespawnPoint(new Vector2Int(2, 1)), "查得到 A 的重生点");
        Expect(sb, ref failures, !respawnLevel.HasRespawnPoint(new Vector2Int(7, 7)), "没放过的格没有重生点");
        Expect(sb, ref failures,
            SamePoint(respawnLevel.respawnPoints[0].position, new Vector2(2.5f, 1.5f)),
            "重生点落在格中心");

        respawnLevel.respawnPoints.Add(new LabRespawnPointSpec(new Vector2(2.5f, 1.5f)));
        respawnLevel.Normalize();
        Expect(sb, ref failures, respawnLevel.respawnPoints.Count == 2,
            "Normalize 同位置去重，实际 " + respawnLevel.respawnPoints.Count);

        var respawnClone = respawnLevel.Clone();
        Expect(sb, ref failures, respawnClone.respawnPoints.Count == 2, "Clone 带上重生点");
        respawnClone.respawnPoints[0] = new LabRespawnPointSpec(new Vector2(99f, 99f));
        Expect(sb, ref failures, respawnLevel.HasRespawnPoint(new Vector2Int(2, 1)), "Clone 是深拷贝");

        Expect(sb, ref failures, respawnLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, respawnLevel.HasRespawnPoint(new Vector2Int(5, 5)),
            "A 里的重生点跟着移动");
        Expect(sb, ref failures, !respawnLevel.HasRespawnPoint(new Vector2Int(2, 1)),
            "旧位置的重生点没有留下");
        Expect(sb, ref failures, respawnLevel.HasRespawnPoint(new Vector2Int(22, 1)),
            "分区 B 的重生点不动");

        Expect(sb, ref failures, respawnLevel.RemoveRespawnPoint(new Vector2Int(5, 5)),
            "按格删掉 A 的重生点");
        Expect(sb, ref failures, !respawnLevel.RemoveRespawnPoint(new Vector2Int(5, 5)),
            "同一格再删返回 false");

        Expect(sb, ref failures, respawnLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            respawnLevel.respawnPoints.Count == 1 && respawnLevel.HasRespawnPoint(new Vector2Int(22, 1)),
            "A 里的重生点被删，B 的留下");

        sb.AppendLine();
        sb.AppendLine("=== 12. 选区：并集 / 平移 / 删除 / 减法 ===");
        var marqueeLevel = new LabLevelData();
        marqueeLevel.AddCell(new Vector2Int(1, 1));
        marqueeLevel.AddCell(new Vector2Int(2, 1));
        marqueeLevel.AddCell(new Vector2Int(10, 1));
        marqueeLevel.keys.Add(new LabKeySpec(new Vector2(1.5f, 1.5f)));
        marqueeLevel.spikes.Add(new LabSpikeSpec(new Vector2Int(2, 1), LabSpikeDir.Up));
        marqueeLevel.respawnPoints.Add(new LabRespawnPointSpec(new Vector2(10.5f, 1.5f)));

        var marqueeArea = new List<RectInt> { new RectInt(1, 1, 2, 1) };
        Expect(sb, ref failures, marqueeLevel.MoveContents(marqueeArea, new Vector2Int(0, 3)) == 4,
            "选区平移搬动 2 格地形 + 1 把钥匙 + 1 根地刺");
        Expect(sb, ref failures,
            marqueeLevel.HasCell(new Vector2Int(1, 4)) && marqueeLevel.HasCell(new Vector2Int(2, 4)),
            "地形跟着选区移动");
        Expect(sb, ref failures, SamePoint(marqueeLevel.keys[0].position, new Vector2(1.5f, 4.5f)),
            "钥匙跟着选区移动");
        Expect(sb, ref failures, marqueeLevel.spikes[0].cell == new Vector2Int(2, 4),
            "地刺跟着选区移动");
        Expect(sb, ref failures, SamePoint(marqueeLevel.respawnPoints[0].position, new Vector2(10.5f, 1.5f)),
            "选区外的重生点不动");
        Expect(sb, ref failures, marqueeLevel.HasCell(new Vector2Int(10, 1)) && !marqueeLevel.HasCell(new Vector2Int(1, 1)),
            "选区外的地形不动");

        var overlapLevel = new LabLevelData();
        overlapLevel.AddCell(new Vector2Int(1, 1));
        overlapLevel.AddCell(new Vector2Int(2, 1));
        var overlapAreas = new List<RectInt>
        {
            new RectInt(1, 1, 2, 1),
            new RectInt(2, 1, 1, 1)
        };
        overlapLevel.MoveContents(overlapAreas, new Vector2Int(1, 0));
        Expect(sb, ref failures,
            !overlapLevel.HasCell(new Vector2Int(1, 1)) && overlapLevel.HasCell(new Vector2Int(2, 1))
            && overlapLevel.HasCell(new Vector2Int(3, 1)) && !overlapLevel.HasCell(new Vector2Int(4, 1)),
            "两个重叠选区里每个物件只移动一次");

        var deleteArea = new List<RectInt> { new RectInt(2, 4, 1, 1) };
        Expect(sb, ref failures, marqueeLevel.DeleteContents(deleteArea, true) > 0, "删选区内容");
        Expect(sb, ref failures, !marqueeLevel.HasCell(new Vector2Int(2, 4)), "选区里的地形被删");
        Expect(sb, ref failures, marqueeLevel.HasCell(new Vector2Int(1, 4)), "选区外的地形保留");

        var subtract = new List<RectInt> { new RectInt(0, 0, 10, 10) };
        LabLevelEditorWindow.SubtractRect(subtract, new RectInt(4, 4, 2, 2));
        int subtractArea = 0;
        bool coversCut = false;
        foreach (var rect in subtract)
        {
            subtractArea += rect.width * rect.height;
            if (rect.Contains(new Vector2Int(4, 4)) || rect.Contains(new Vector2Int(5, 5))) coversCut = true;
        }
        Expect(sb, ref failures, subtractArea == 100 - 4, "挖掉 2×2 后面积 = 96，实际 " + subtractArea);
        Expect(sb, ref failures, !coversCut, "被挖掉的格子不再属于任何选区");

        var union = new List<RectInt> { new RectInt(0, 0, 2, 2) };
        LabLevelEditorWindow.UnionRect(union, new RectInt(1, 1, 2, 2));
        int unionArea = 0;
        bool unionOverlaps = false;
        for (int i = 0; i < union.Count; i++)
        {
            unionArea += union[i].width * union[i].height;
            for (int j = i + 1; j < union.Count; j++)
                if (union[i].Overlaps(union[j])) unionOverlaps = true;
        }
        Expect(sb, ref failures, unionArea == 7 && !unionOverlaps,
            "增加选区按并集合并：重叠 1 格的 2×2 + 2×2 = 7 格且不重叠，实际 " + unionArea);
        Expect(sb, ref failures,
            union.Exists(rect => rect.Contains(new Vector2Int(0, 0))) &&
            union.Exists(rect => rect.Contains(new Vector2Int(1, 1))) &&
            union.Exists(rect => rect.Contains(new Vector2Int(2, 2))) &&
            !union.Exists(rect => rect.Contains(new Vector2Int(3, 3))),
            "并集覆盖两块矩形，且没有多选到外面");

        var disjoint = new List<RectInt> { new RectInt(0, 0, 2, 2) };
        LabLevelEditorWindow.UnionRect(disjoint, new RectInt(10, 10, 1, 1));
        int disjointArea = 0;
        foreach (var rect in disjoint) disjointArea += rect.width * rect.height;
        Expect(sb, ref failures, disjoint.Count == 2 && disjointArea == 5,
            "不相邻的矩形并进同一个选区，5 格；实际 " + disjointArea);

        var contained = new List<RectInt> { new RectInt(1, 1, 1, 1) };
        LabLevelEditorWindow.UnionRect(contained, new RectInt(0, 0, 4, 4));
        int containedArea = 0;
        foreach (var rect in contained) containedArea += rect.width * rect.height;
        Expect(sb, ref failures, contained.Count == 1 && containedArea == 16,
            "大矩形盖住已有选区时并成一块，实际 " + containedArea);

        // 拖动选区：框和内容一起走，沿途扫过的物件不能被带走。
        var dragLevel = new LabLevelData();
        dragLevel.AddCell(new Vector2Int(1, 1));
        dragLevel.AddCell(new Vector2Int(2, 1));
        var dragSelection = new List<RectInt> { new RectInt(1, 1, 1, 1) };
        var dragCapture = dragLevel.CaptureContents(dragSelection);
        Expect(sb, ref failures, dragCapture.Count == 1,
            "拖动开始时只锁定选区里的 1 格地形，实际 " + dragCapture.Count);
        for (int step = 0; step < 3; step++)
        {
            dragLevel.MoveCapturedContents(dragCapture, new Vector2Int(1, 0));
            LabLevelEditorWindow.TranslateRects(dragSelection, new Vector2Int(1, 0));
        }
        Expect(sb, ref failures, dragLevel.HasCell(new Vector2Int(4, 1)),
            "选区框和里面的地形一起连拖 3 格");
        Expect(sb, ref failures,
            dragLevel.HasCell(new Vector2Int(2, 1)) && !dragLevel.HasCell(new Vector2Int(5, 1)),
            "框扫过的选区外地块留在原处");
        Expect(sb, ref failures, dragSelection[0].x == 4,
            "选区框跟着内容平移，实际 x = " + dragSelection[0].x);

        var captureLevel = new LabLevelData();
        captureLevel.AddCell(new Vector2Int(3, 3));
        captureLevel.ropes.Add(new LabRopeSpec(new Vector2(3.5f, 3.5f), 1));
        captureLevel.keys.Add(new LabKeySpec(new Vector2(3.5f, 3.5f)));
        captureLevel.recalls.Add(new LabRecallSpec(new Vector2Int(3, 3)));
        captureLevel.doors.Add(new LabDoorSpec(new Vector2(3.5f, 3.5f), Color.white));
        captureLevel.spikes.Add(new LabSpikeSpec(new Vector2Int(3, 3), LabSpikeDir.Up));
        captureLevel.ices.Add(new LabIceSpec(new Vector2Int(3, 3), 1));
        captureLevel.magmas.Add(new LabMagmaSpec(new Vector2Int(3, 3), 1));
        captureLevel.hiddenBlocks.Add(new LabHiddenBlockSpec(new Vector2Int(3, 3), 1));
        captureLevel.respawnPoints.Add(new LabRespawnPointSpec(new Vector2(3.5f, 3.5f)));
        captureLevel.teleports.Add(new LabTeleportSpec(new Vector2(3.5f, 3.5f)));
        var captureArea = new List<RectInt> { new RectInt(3, 3, 1, 1) };
        var allCapture = captureLevel.CaptureContents(captureArea);
        Expect(sb, ref failures, allCapture.Count == 11,
            "选区锁定地形 + 10 种摆放物件，实际 " + allCapture.Count);
        Expect(sb, ref failures, captureLevel.MoveCapturedContents(allCapture, new Vector2Int(1, 0)) == 11,
            "锁定内容一次平移到 11 条");
        Expect(sb, ref failures,
            captureLevel.HasCell(new Vector2Int(4, 3)) &&
            SamePoint(captureLevel.ropes[0].center, new Vector2(4.5f, 3.5f)) &&
            SamePoint(captureLevel.keys[0].position, new Vector2(4.5f, 3.5f)) &&
            captureLevel.recalls[0].cell == new Vector2Int(4, 3) &&
            SamePoint(captureLevel.doors[0].position, new Vector2(4.5f, 3.5f)) &&
            captureLevel.spikes[0].cell == new Vector2Int(4, 3) &&
            captureLevel.ices[0].cell == new Vector2Int(4, 3) &&
            captureLevel.magmas[0].cell == new Vector2Int(4, 3) &&
            captureLevel.hiddenBlocks[0].cell == new Vector2Int(4, 3) &&
            SamePoint(captureLevel.respawnPoints[0].position, new Vector2(4.5f, 3.5f)) &&
            SamePoint(captureLevel.teleports[0].position, new Vector2(4.5f, 3.5f)),
            "地形 + 10 种摆放物件都跟着选区框平移");

        sb.AppendLine();
        sb.AppendLine("=== 13. 传送点：全互联数据 / 去重 / 随分区移动与删除 ===");
        Expect(sb, ref failures, data.teleports.Count == 0,
            "基准房间没有传送点，实际 " + data.teleports.Count);

        var teleportLevel = new LabLevelData();
        teleportLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        teleportLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });

        Expect(sb, ref failures, teleportLevel.PlaceTeleport(new Vector2Int(2, 1)), "A 里放一个传送点");
        Expect(sb, ref failures, teleportLevel.PlaceTeleport(new Vector2Int(22, 1)), "B 里放一个传送点");
        Expect(sb, ref failures, !teleportLevel.PlaceTeleport(new Vector2Int(2, 1)),
            "同一格重复放 = 无变化");
        Expect(sb, ref failures, teleportLevel.HasTeleport(new Vector2Int(2, 1)), "查得到 A 的传送点");
        Expect(sb, ref failures, !teleportLevel.HasTeleport(new Vector2Int(7, 7)), "没放过的格没有传送点");
        Expect(sb, ref failures,
            SamePoint(teleportLevel.teleports[0].position, new Vector2(2.5f, 1.5f)),
            "传送点落在格中心");
        Expect(sb, ref failures,
            teleportLevel.teleports[0].Cells() == new RectInt(1, 0, 3, 3),
            "触发区是 3×3：" + teleportLevel.teleports[0].Cells());

        teleportLevel.teleports.Add(new LabTeleportSpec(new Vector2(2.5f, 1.5f)));
        teleportLevel.Normalize();
        Expect(sb, ref failures, teleportLevel.teleports.Count == 2,
            "Normalize 同位置去重，实际 " + teleportLevel.teleports.Count);

        var teleportClone = teleportLevel.Clone();
        Expect(sb, ref failures, teleportClone.teleports.Count == 2, "Clone 带上传送点");
        teleportClone.teleports[0] = new LabTeleportSpec(new Vector2(99f, 99f));
        Expect(sb, ref failures, teleportLevel.HasTeleport(new Vector2Int(2, 1)), "Clone 是深拷贝");

        Expect(sb, ref failures, teleportLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, teleportLevel.HasTeleport(new Vector2Int(5, 5)),
            "A 里的传送点跟着移动");
        Expect(sb, ref failures, !teleportLevel.HasTeleport(new Vector2Int(2, 1)),
            "旧位置的传送点没有留下");
        Expect(sb, ref failures, teleportLevel.HasTeleport(new Vector2Int(22, 1)),
            "分区 B 的传送点不动");

        Expect(sb, ref failures, teleportLevel.RemoveTeleport(new Vector2Int(5, 5)),
            "按格删掉 A 的传送点");
        Expect(sb, ref failures, !teleportLevel.RemoveTeleport(new Vector2Int(5, 5)),
            "同一格再删返回 false");
        teleportLevel.PlaceTeleport(new Vector2Int(5, 5));

        var teleportArea = new List<RectInt> { new RectInt(5, 5, 1, 1) };
        Expect(sb, ref failures, teleportLevel.CountContents(teleportArea, false) == 1,
            "选区计数带上传送点");
        Expect(sb, ref failures, teleportLevel.MoveContents(teleportArea, new Vector2Int(1, 0)) == 1,
            "选区平移带上传送点");
        Expect(sb, ref failures, teleportLevel.HasTeleport(new Vector2Int(6, 5)),
            "传送点跟着选区移动");

        Expect(sb, ref failures, teleportLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            teleportLevel.teleports.Count == 1 && teleportLevel.HasTeleport(new Vector2Int(22, 1)),
            "A 里的传送点被删，B 的留下");

        sb.AppendLine();
        sb.AppendLine("=== 14. 岩浆块：几何 / 编组 / 去重 / 随分区移动与删除 / v3 门槛 ===");
        Expect(sb, ref failures, data.magmas.Count == 0,
            "基准房间没有岩浆块，实际 " + data.magmas.Count);

        var magmaSpec = new LabMagmaSpec(new Vector2Int(5, 3), 2);
        Expect(sb, ref failures,
            SamePoint(magmaSpec.Center, new Vector2(5.5f, 3.5f))
            && magmaSpec.Cells() == new RectInt(5, 3, 1, 1),
            "岩浆块占 1×1 格，落点是格中心");

        var magmaLevel = new LabLevelData();
        magmaLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        magmaLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        magmaLevel.PlaceMagma(new Vector2Int(2, 1), 7);
        magmaLevel.PlaceMagma(new Vector2Int(22, 1), 7);
        Expect(sb, ref failures, magmaLevel.magmas.Count == 2, "两格各一块岩浆");
        Expect(sb, ref failures, !magmaLevel.PlaceMagma(new Vector2Int(2, 1), 7),
            "同格同组重复放 = 无变化");
        Expect(sb, ref failures, magmaLevel.PlaceMagma(new Vector2Int(2, 1), 9),
            "同格换组 = 有变化");
        Expect(sb, ref failures,
            magmaLevel.magmas.Count == 2 &&
            magmaLevel.FindMagma(new Vector2Int(2, 1)).Value.groupId == 9,
            "换组只有一块岩浆，组号已更新");
        Expect(sb, ref failures, !magmaLevel.HasMagma(new Vector2Int(7, 7)), "没放过的格没有岩浆");
        Expect(sb, ref failures, magmaLevel.NextMagmaGroupId() == 10,
            "新建组 = 当前最大组号 + 1");
        Expect(sb, ref failures, magmaLevel.GroupMagmas(7).Count == 1,
            "按组查询只返回同组岩浆");

        magmaLevel.magmas.Add(new LabMagmaSpec(new Vector2Int(2, 1), 3));
        magmaLevel.Normalize();
        Expect(sb, ref failures, magmaLevel.magmas.Count == 2,
            "Normalize 同格去重，实际 " + magmaLevel.magmas.Count);

        var magmaClone = magmaLevel.Clone();
        Expect(sb, ref failures, magmaClone.magmas.Count == 2, "Clone 带上岩浆块");
        magmaClone.magmas[0] = new LabMagmaSpec(new Vector2Int(99, 99), 0);
        Expect(sb, ref failures,
            magmaLevel.HasMagma(new Vector2Int(2, 1)) || magmaLevel.HasMagma(new Vector2Int(22, 1)),
            "Clone 是深拷贝");

        Expect(sb, ref failures, magmaLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, magmaLevel.HasMagma(new Vector2Int(5, 5)),
            "A 里的岩浆块跟着移动");
        Expect(sb, ref failures, !magmaLevel.HasMagma(new Vector2Int(2, 1)),
            "旧位置的岩浆块没有留下");
        Expect(sb, ref failures, magmaLevel.HasMagma(new Vector2Int(22, 1)),
            "分区 B 的岩浆块不动");

        Expect(sb, ref failures, magmaLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            magmaLevel.magmas.Count == 1 && magmaLevel.HasMagma(new Vector2Int(22, 1)),
            "A 里的岩浆块被删，B 的留下");

        var tierProbeGo = new GameObject("MagmaTierProbe");
        tierProbeGo.SetActive(false);
        var tierProbe = tierProbeGo.AddComponent<TuningLab>();
        tierProbe.autoBuild = false;
        tierProbeGo.SetActive(true);
        Expect(sb, ref failures, LabMagma.BreakTier == 3, "岩浆块固定按 v3 破坏");
        Expect(sb, ref failures, tierProbe.TierOfSpeed(tierProbe.MagmaBreakSpeed + 0.01f) == 3,
            "岩浆块门槛刚过就是 v3");
        Expect(sb, ref failures, tierProbe.TierOfSpeed(tierProbe.MagmaBreakSpeed - 0.01f) == 2,
            "低于岩浆块门槛还是 v2");
        Object.DestroyImmediate(tierProbeGo);

        sb.AppendLine();
        sb.AppendLine("=== 15. 隐藏方块：几何 / 编组 / 同格多组 / 去重 / 随分区移动与删除 ===");
        Expect(sb, ref failures, data.hiddenBlocks.Count == 0,
            "基准房间没有隐藏方块，实际 " + data.hiddenBlocks.Count);

        var hiddenSpec = new LabHiddenBlockSpec(new Vector2Int(5, 3), 2);
        Expect(sb, ref failures,
            SamePoint(hiddenSpec.Center, new Vector2(5.5f, 3.5f))
            && hiddenSpec.Cells() == new RectInt(5, 3, 1, 1),
            "隐藏方块落点与占地和岩浆同构");

        var hiddenLevel = new LabLevelData();
        hiddenLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        hiddenLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        hiddenLevel.PlaceHiddenBlock(new Vector2Int(2, 1), 7);
        hiddenLevel.PlaceHiddenBlock(new Vector2Int(22, 1), 7);
        Expect(sb, ref failures, hiddenLevel.hiddenBlocks.Count == 2, "两格各一条");
        Expect(sb, ref failures, !hiddenLevel.PlaceHiddenBlock(new Vector2Int(2, 1), 7),
            "同格同组重复放 = 无变化");
        Expect(sb, ref failures, hiddenLevel.PlaceHiddenBlock(new Vector2Int(2, 1), 9),
            "同格另一组可以共存（嵌套组）");
        Expect(sb, ref failures,
            hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1)) &&
            hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1), 7) &&
            hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1), 9) &&
            !hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1), 11),
            "按格 / 按 (格, 组) 两种查询都正确");
        Expect(sb, ref failures, !hiddenLevel.HasHiddenBlock(new Vector2Int(7, 7)), "没放过的格没有隐藏方块");
        Expect(sb, ref failures, hiddenLevel.GroupHiddenBlocks(7).Count == 2, "组 7 跨两个分区共 2 条");
        Expect(sb, ref failures, hiddenLevel.NextHiddenGroupId() == 10, "下一组号 = 最大组号 + 1");

        Expect(sb, ref failures, !hiddenLevel.RemoveHiddenBlock(new Vector2Int(2, 1), 9)
                || hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1), 7),
            "从一组摘掉后另一组还在");
        Expect(sb, ref failures, hiddenLevel.RemoveHiddenBlock(new Vector2Int(2, 1), 7),
            "再摘掉组 7 的这格");
        Expect(sb, ref failures, !hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1)),
            "两组都摘干净后这格不再隐藏");
        hiddenLevel.PlaceHiddenBlock(new Vector2Int(2, 1), 7);

        hiddenLevel.hiddenBlocks.Add(new LabHiddenBlockSpec(new Vector2Int(2, 1), 7));
        hiddenLevel.hiddenBlocks.Add(new LabHiddenBlockSpec(new Vector2Int(2, 1), 3));
        hiddenLevel.Normalize();
        Expect(sb, ref failures, hiddenLevel.hiddenBlocks.Count == 3,
            "Normalize 只按 (格, 组) 去重，实际 " + hiddenLevel.hiddenBlocks.Count);

        var hiddenClone = hiddenLevel.Clone();
        Expect(sb, ref failures, hiddenClone.hiddenBlocks.Count == 3, "Clone 带上隐藏方块");
        hiddenClone.hiddenBlocks[0] = new LabHiddenBlockSpec(new Vector2Int(99, 99), 0);
        Expect(sb, ref failures,
            hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1)) || hiddenLevel.HasHiddenBlock(new Vector2Int(22, 1)),
            "Clone 是深拷贝");

        Expect(sb, ref failures, hiddenLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures,
            hiddenLevel.HasHiddenBlock(new Vector2Int(5, 5)) && !hiddenLevel.HasHiddenBlock(new Vector2Int(2, 1)),
            "A 里的隐藏方块跟着移动，旧位置不残留");
        Expect(sb, ref failures, hiddenLevel.HasHiddenBlock(new Vector2Int(22, 1)),
            "分区 B 的隐藏方块不动");

        Expect(sb, ref failures, hiddenLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            hiddenLevel.hiddenBlocks.Count == 1 && hiddenLevel.HasHiddenBlock(new Vector2Int(22, 1)),
            "A 里的隐藏方块被删，B 的留下");

        sb.AppendLine("=== 16. 召回道具：几何 / 去重 / 随分区移动与删除 / 全队解锁 ===");
        Expect(sb, ref failures, data.recalls.Count == 0,
            "基准房间没有召回道具，实际 " + data.recalls.Count);

        var recallSpec = new LabRecallSpec(new Vector2Int(5, 3));
        Expect(sb, ref failures,
            SamePoint(recallSpec.Center, new Vector2(5.5f, 3.5f))
            && recallSpec.Cells() == new RectInt(5, 3, 1, 1),
            "召回道具占 1×1 格，落点是格中心");

        var recallLevel = new LabLevelData();
        recallLevel.partitions.Add(new LabLevelPartition { name = "A", bounds = new RectInt(0, 0, 10, 10) });
        recallLevel.partitions.Add(new LabLevelPartition { name = "B", bounds = new RectInt(20, 0, 10, 10) });
        Expect(sb, ref failures, recallLevel.PlaceRecall(new Vector2Int(2, 1)), "A 里放一个召回道具");
        Expect(sb, ref failures, recallLevel.PlaceRecall(new Vector2Int(22, 1)), "B 里放一个召回道具");
        Expect(sb, ref failures, !recallLevel.PlaceRecall(new Vector2Int(2, 1)),
            "同一格重复放 = 无变化");
        Expect(sb, ref failures, recallLevel.HasRecall(new Vector2Int(2, 1)), "查得到 A 的召回道具");
        Expect(sb, ref failures, !recallLevel.HasRecall(new Vector2Int(7, 7)), "没放过的格没有召回道具");
        Expect(sb, ref failures, recallLevel.RemoveRecall(new Vector2Int(7, 7)) == false,
            "删不存在的召回道具返回 false");
        recallLevel.recalls.Add(new LabRecallSpec(new Vector2Int(2, 1)));
        recallLevel.Normalize();
        Expect(sb, ref failures, recallLevel.recalls.Count == 2,
            "Normalize 同格去重，实际 " + recallLevel.recalls.Count);

        var recallClone = recallLevel.Clone();
        recallClone.recalls[0] = new LabRecallSpec(new Vector2Int(99, 99));
        Expect(sb, ref failures, recallLevel.HasRecall(new Vector2Int(2, 1)),
            "Clone 是深拷贝");

        Expect(sb, ref failures, recallLevel.MovePartition(0, new Vector2Int(3, 4), true),
            "移动分区 A");
        Expect(sb, ref failures, recallLevel.HasRecall(new Vector2Int(5, 5)),
            "A 里的召回道具跟着移动");
        Expect(sb, ref failures, recallLevel.HasRecall(new Vector2Int(22, 1)),
            "分区 B 的召回道具不动");
        Expect(sb, ref failures, recallLevel.DeletePartition(0, true), "删除分区 A（带内容）");
        Expect(sb, ref failures,
            recallLevel.recalls.Count == 1 && recallLevel.HasRecall(new Vector2Int(22, 1)),
            "A 里的召回道具被删，B 的留下");

        var recallProbeGo = new GameObject("RecallProbe");
        recallProbeGo.SetActive(false);
        var recallProbe = recallProbeGo.AddComponent<TuningLab>();
        recallProbe.autoBuild = false;
        recallProbeGo.SetActive(true);
        Expect(sb, ref failures, !recallProbe.RecallUnlocked, "召回默认未解锁");
        recallProbe.UnlockRecall();
        recallProbe.UnlockRecall();
        Expect(sb, ref failures, recallProbe.RecallUnlocked, "拾取后全队解锁，重复解锁无害");
        recallProbe.ResetMap();
        Expect(sb, ref failures, recallProbe.RecallUnlocked, "地图重置不清空召回能力");
        Object.DestroyImmediate(recallProbeGo);

        sb.AppendLine();
        fails = failures;
        sb.AppendLine(failures == 0 ? "全部通过。" : ("失败 " + failures + " 项。"));
        return sb;
    }

    static void CheckSpawn(StringBuilder sb, ref int fails, string name, Vector2 feet, LabLevelData data)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
        Expect(sb, ref fails, !data.HasCell(cell), name + " 落点格 " + cell + " 不是实心");
        Expect(sb, ref fails, !data.HasCell(cell + Vector2Int.up), name + " 头顶格不是实心");
    }

    static bool SamePoint(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;

    static bool SameRect(RectInt a, Rect b)
        => a.x == Mathf.RoundToInt(b.xMin) && a.y == Mathf.RoundToInt(b.yMin)
        && a.width == Mathf.RoundToInt(b.width) && a.height == Mathf.RoundToInt(b.height);

    static bool ContainsRect(List<RectInt> rects, RectInt expected)
    {
        foreach (var rect in rects)
            if (rect.x == expected.x && rect.y == expected.y
                && rect.width == expected.width && rect.height == expected.height)
                return true;
        return false;
    }

    static void Expect(StringBuilder sb, ref int fails, bool condition, string label)
    {
        if (!condition) fails++;
        sb.AppendLine((condition ? "  OK   " : "  FAIL ") + label);
    }
}
#endif
