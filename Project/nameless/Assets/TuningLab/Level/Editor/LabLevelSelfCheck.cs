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
        int fails;
        StringBuilder report = Check(out fails);
        Debug.Log("[LabLevelSelfCheck]\n" + report);
        EditorApplication.Exit(fails == 0 ? 0 : 1);
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
