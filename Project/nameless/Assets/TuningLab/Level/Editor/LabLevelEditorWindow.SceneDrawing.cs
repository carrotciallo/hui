#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：Scene 视图绘制与几何助手。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{
    // ------------------------------------------------------------------
    // 绘制
    // ------------------------------------------------------------------

    void DrawGrid(LabLevelData data)
    {
        RectInt area = EditArea(data);
        // 范围特别大时抽稀网格，避免一次画几千条线把 Scene 视图拖死。
        int stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(area.width, area.height) / 400f));

        Handles.color = new Color(1f, 1f, 1f, 0.055f);
        for (int x = area.x; x <= area.x + area.width; x += stride)
            Handles.DrawLine(new Vector3(x, area.y, 0f), new Vector3(x, area.y + area.height, 0f));
        for (int y = area.y; y <= area.y + area.height; y += stride)
            Handles.DrawLine(new Vector3(area.x, y, 0f), new Vector3(area.x + area.width, y, 0f));

        // 每 4 格一条亮线，方便读关卡尺寸
        int major = 4 * stride;
        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        for (int x = Mathf.CeilToInt(area.x / (float)major) * major; x <= area.x + area.width; x += major)
            Handles.DrawLine(new Vector3(x, area.y, 0f), new Vector3(x, area.y + area.height, 0f));
        for (int y = Mathf.CeilToInt(area.y / (float)major) * major; y <= area.y + area.height; y += major)
            Handles.DrawLine(new Vector3(area.x, y, 0f), new Vector3(area.x + area.width, y, 0f));
    }

    void DrawBoundsHandle(LabLevelData data)
    {
        RectInt b = data.bounds;
        var p0 = new Vector3(b.x, b.y, 0f);
        var p1 = new Vector3(b.x + b.width, b.y, 0f);
        var p2 = new Vector3(b.x + b.width, b.y + b.height, 0f);
        var p3 = new Vector3(b.x, b.y + b.height, 0f);

        Handles.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Handles.DrawLine(p0, p1);
        Handles.DrawLine(p1, p2);
        Handles.DrawLine(p2, p3);
        Handles.DrawLine(p3, p0);
        Handles.Label(new Vector3(b.x + 0.5f, b.y + b.height - 0.5f, 0f), "编辑范围");
    }

    void DrawRopeHandles(LabLevelData data)
    {
        if (data.ropes == null) return;
        Handles.color = new Color(0.85f, 0.65f, 0.35f, 0.9f);
        for (int i = 0; i < data.ropes.Count; i++)
        {
            var rope = data.ropes[i];
            Vector3 size = rope.orientation == LabRopeOrientation.Horizontal
                ? new Vector3(rope.cells, 0.25f, 0.01f)
                : new Vector3(RopeStripWidth, rope.cells, 0.01f);
            Handles.DrawWireCube(new Vector3(rope.center.x, rope.center.y, 0f), size);
        }
    }

    void DrawKeyDoorHandles(LabLevelData data)
    {
        if (data.keys != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            for (int i = 0; i < data.keys.Count; i++)
            {
                var key = data.keys[i];
                Handles.DrawWireCube(new Vector3(key.position.x, key.position.y, 0f), Vector3.one);
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                var door = data.doors[i];
                Handles.color = new Color(door.color.r, door.color.g, door.color.b, 0.9f);
                Handles.DrawWireCube(
                    new Vector3(door.position.x, door.position.y, 0f),
                    new Vector3(1f, LabDoorSpec.CellHeight, 0.01f));
            }
        }
    }

    void DrawRecallHandles(LabLevelData data)
    {
        if (data.recalls == null || data.recalls.Count == 0) return;
        Handles.color = new Color(0.2f, 0.9f, 0.8f, 0.95f);
        for (int i = 0; i < data.recalls.Count; i++)
        {
            var recall = data.recalls[i];
            Handles.DrawWireCube(CellCenter(recall.cell), Vector3.one);
            Handles.Label(CellCenter(recall.cell) + Vector3.up * 0.6f, "召回");
        }
    }

    void DrawSelectionRegion()
    {
        if (selection.Count > 0)
        {
            Color fill = new Color(0.35f, 0.9f, 1f, 0.10f);
            for (int i = 0; i < selection.Count; i++)
            {
                RectInt rect = selection[i];
                // 只铺底色，不画每块自己的边；外轮廓统一在下面按并集画。
                Handles.DrawSolidRectangleWithOutline(
                    RectVerts(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height),
                    fill, Color.clear);
            }

            Handles.color = new Color(0.35f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < selection.Count; i++)
            {
                RectInt rect = selection[i];
                DrawRegionEdge(selection, i, true, rect.y, false);              // 下边界
                DrawRegionEdge(selection, i, true, rect.y + rect.height, true); // 上边界
                DrawRegionEdge(selection, i, false, rect.x, false);             // 左边界
                DrawRegionEdge(selection, i, false, rect.x + rect.width, true); // 右边界
            }

            RectInt bounds = SelectionBounds(selection);
            Handles.Label(new Vector3(bounds.x + 0.5f, bounds.y + bounds.height - 0.5f, 0f),
                "选区 " + SelectionArea(selection) + " 格");
        }

        if (!marqueeDragging) return;

        RectInt preview = MarqueeRect(marqueeStart, marqueeEnd);
        DrawFilledRect(preview.x, preview.y, preview.x + preview.width - 1, preview.y + preview.height - 1,
            new Color(1f, 0.9f, 0.35f, 0.16f), new Color(1f, 0.9f, 0.35f, 0.95f));
        Handles.Label(new Vector3(preview.x + 0.5f, preview.y + preview.height - 0.5f, 0f),
            preview.width + "×" + preview.height);
    }

    /// <summary>
    /// 画选区的一块外边界：挖掉同一条边上相邻矩形覆盖的区间，只画真正露在外面的线段。
    /// 这样多块矩形拼出来的选区看起来仍是一个整体，不会出现内部接缝。
    /// towardPositive 表示相邻矩形在边的正方向一侧：上边界 / 右边界为 true。
    /// </summary>
    static void DrawRegionEdge(List<RectInt> region, int index, bool horizontal, int edge, bool towardPositive)
    {
        RectInt rect = region[index];
        int start = horizontal ? rect.x : rect.y;
        int end = horizontal ? rect.x + rect.width : rect.y + rect.height;

        var covered = new List<Vector2Int>();
        for (int i = 0; i < region.Count; i++)
        {
            if (i == index) continue;
            RectInt other = region[i];
            if (horizontal)
            {
                int otherEdge = towardPositive ? other.y : other.y + other.height;
                if (otherEdge != edge) continue;
                int from = Mathf.Max(start, other.x);
                int to = Mathf.Min(end, other.x + other.width);
                if (from < to) covered.Add(new Vector2Int(from, to));
            }
            else
            {
                int otherEdge = towardPositive ? other.x : other.x + other.width;
                if (otherEdge != edge) continue;
                int from = Mathf.Max(start, other.y);
                int to = Mathf.Min(end, other.y + other.height);
                if (from < to) covered.Add(new Vector2Int(from, to));
            }
        }

        covered.Sort((a, b) => a.x.CompareTo(b.x));
        int cursor = start;
        for (int i = 0; i < covered.Count && cursor < end; i++)
        {
            Vector2Int span = covered[i];
            if (span.x > cursor) DrawRegionLine(horizontal, edge, cursor, span.x);
            cursor = Mathf.Max(cursor, span.y);
        }
        if (cursor < end) DrawRegionLine(horizontal, edge, cursor, end);
    }

    static void DrawRegionLine(bool horizontal, int edge, int from, int to)
    {
        if (from >= to) return;
        if (horizontal)
            Handles.DrawLine(new Vector3(from, edge, 0f), new Vector3(to, edge, 0f));
        else
            Handles.DrawLine(new Vector3(edge, from, 0f), new Vector3(edge, to, 0f));
    }

    void DrawPickedHighlight()
    {
        if (!ValidPick(picked)) return;
        RectInt area = PickArea(picked);
        DrawFilledRect(area.x, area.y, area.x + area.width - 1, area.y + area.height - 1,
            new Color(1f, 0.55f, 0.2f, 0.10f), new Color(1f, 0.55f, 0.2f, 0.95f));
        Handles.Label(new Vector3(area.x + 0.5f, area.y + area.height - 0.5f, 0f), PickLabel(picked));
    }

    void DrawSpawnHandles(LabLevelData data)
    {
        DrawPlayerMarker(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, "A", TuningLab.ColorA);
        DrawPlayerMarker(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, "B", TuningLab.ColorB);
    }

    void DrawPartitionHandles(LabLevelData data)
    {
        if (data.partitions == null) return;

        for (int i = 0; i < data.partitions.Count; i++)
        {
            var partition = data.partitions[i];
            if (partition == null) continue;

            Rect rect = LabLevelData.ToRect(partition.bounds);
            bool selected = i == partitionIndex;
            Handles.color = selected
                ? new Color(1f, 0.85f, 0.3f, 0.95f)
                : new Color(1f, 0.75f, 0.2f, 0.35f);
            Handles.DrawWireCube(rect.center, new Vector3(rect.width, rect.height, 0.01f));

            string label = string.IsNullOrEmpty(partition.name) ? "Level" : partition.name;
            Handles.Label(new Vector3(rect.xMin + 0.5f, rect.yMax - 0.5f, 0f),
                (selected ? "▶ " : "") + label);
        }

        if (!partitionCreating) return;

        RectInt bounds = PreviewPartitionBounds();
        bool overlaps = data.OverlapsAny(bounds);
        Color fill = overlaps ? new Color(1f, 0.25f, 0.2f, 0.2f) : new Color(1f, 0.85f, 0.3f, 0.12f);
        Color line = overlaps ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
        DrawFilledRect(bounds.x, bounds.y, bounds.x + bounds.width - 1, bounds.y + bounds.height - 1, fill, line);
        Handles.Label(new Vector3(bounds.x + 0.5f, bounds.y + bounds.height - 0.5f, 0f),
            overlaps ? "重叠，不能创建" : "新分区 " + bounds.width + "×" + bounds.height);
    }

    void DrawPartitionHover(Vector2Int cell)
    {
        var data = level.Data;

        if (partitionCreating)
        {
            RectInt bounds = PreviewPartitionBounds();
            bool overlaps = data.OverlapsAny(bounds);
            Color fill = overlaps ? new Color(1f, 0.25f, 0.2f, 0.25f) : new Color(1f, 0.85f, 0.3f, 0.15f);
            Color line = overlaps ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            DrawFilledRect(bounds.x, bounds.y, bounds.x + bounds.width - 1, bounds.y + bounds.height - 1, fill, line);
            return;
        }

        int index = data.FindPartition(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
        Handles.color = index >= 0
            ? new Color(1f, 0.85f, 0.3f, 0.9f)
            : new Color(0.4f, 0.9f, 1f, 0.7f);
        Handles.DrawWireCube(CellCenter(cell), Vector3.one);
        if (index >= 0)
            Handles.Label(CellCenter(cell) + Vector3.up * 0.8f, "拖动移动");
    }

    void DrawCameraFrames(LabLevelData data)
    {
        // 以出生点估算相机起点：脚底 + 两名玩家半高的平均 + LabCamera.offset(0, 1)
        Vector2 mid = (data.spawnA + data.spawnB) * 0.5f;
        mid.y += (TuningLab.A_Pixels + TuningLab.B_Pixels) / (4f * TuningLab.CellPixels);
        mid.y += 1f;
        var center = new Vector3(mid.x, mid.y, 0f);

        float min = data.cameraMinSize;
        float max = data.cameraMaxSize;

        // B 的位置决定开局用哪一关；视野框要按那个分区画，不能只看 LabLevelData。
        if (lab != null)
        {
            Vector2 probe = data.spawnB;
            probe.y += TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
            lab.GetCameraFrame(probe, out _, out min, out max);
        }

        DrawCameraFrame(center, min, new Color(0.3f, 0.9f, 1f, 0.55f), "最小视野");
        if (max > min + 0.01f)
            DrawCameraFrame(center, max, new Color(1f, 0.6f, 0.2f, 0.3f), "最大视野");
    }

    static void DrawCameraFrame(Vector3 center, float size, Color color, string label)
    {
        float height = size * 2f;
        float width = height * (16f / 9f);
        Handles.color = color;
        Handles.DrawWireCube(center, new Vector3(width, height, 0.01f));
        Handles.Label(new Vector3(center.x - width * 0.5f + 0.5f, center.y + height * 0.5f - 0.5f, 0f), label);
    }

    void DrawPlayerMarker(Vector2 feet, float size, string label, Color color)
    {
        float half = size * 0.5f;
        var center = new Vector3(feet.x, feet.y + half, 0f);
        Handles.color = color;
        Handles.DrawWireCube(center, new Vector3(size, size, 0.01f));
        Handles.Label(center + Vector3.up * (half + 0.2f), label + " 落点");
        Handles.DrawLine(center, new Vector3(feet.x, feet.y, 0f));
    }

    void DrawSpikeHandles(LabLevelData data)
    {
        if (data.spikes == null) return;
        for (int i = 0; i < data.spikes.Count; i++)
        {
            var spike = data.spikes[i];
            // 底座没有实心支撑的刺画红，有支撑的画钢灰。
            Handles.color = data.HasCell(spike.BaseCell)
                ? new Color(0.75f, 0.82f, 0.92f, 0.95f)
                : new Color(1f, 0.35f, 0.3f, 0.95f);
            var center = spike.StripCenter;
            var size = spike.StripSize;
            Handles.DrawWireCube(new Vector3(center.x, center.y, 0f),
                new Vector3(size.x, size.y, 0.01f));
        }
    }

    void DrawIceHandles(LabLevelData data)
    {
        if (data.ices == null) return;
        for (int i = 0; i < data.ices.Count; i++)
        {
            var ice = data.ices[i];
            bool blocked = data.HasCell(ice.cell) || data.HasMagma(ice.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : IceGroupColor(ice.groupId);
            Handles.DrawWireCube(CellCenter(ice.cell), Vector3.one);
        }

    }

    void DrawMagmaHandles(LabLevelData data)
    {
        if (data.magmas == null) return;
        for (int i = 0; i < data.magmas.Count; i++)
        {
            var magma = data.magmas[i];
            bool blocked = data.HasCell(magma.cell) || data.HasIce(magma.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : MagmaGroupColor(magma.groupId);
            Handles.DrawWireCube(CellCenter(magma.cell), Vector3.one);
        }
    }

    void DrawHiddenBlockHandles(LabLevelData data)
    {
        if (data.hiddenBlocks == null) return;
        for (int i = 0; i < data.hiddenBlocks.Count; i++)
        {
            var block = data.hiddenBlocks[i];
            // 隐藏方块只禁止压实心格；压别的物件正是它的用途。
            bool blocked = data.HasCell(block.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : HiddenGroupColor(block.groupId);
            Handles.DrawWireCube(CellCenter(block.cell), Vector3.one);
        }
    }

    /// <summary>修复方块和 A 同色；压在别的实体上画红，和校验里的冲突条件一致。</summary>
    void DrawRepairBlockHandles(LabLevelData data)
    {
        if (data.repairBlocks == null) return;
        for (int i = 0; i < data.repairBlocks.Count; i++)
        {
            var block = data.repairBlocks[i];
            bool blocked = data.HasCell(block.cell) || data.HasIce(block.cell)
                || data.HasMagma(block.cell) || data.HasSpike(block.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : TuningLab.ColorA;
            Handles.DrawWireCube(CellCenter(block.cell), Vector3.one);
        }
    }

    void DrawPixelArtHandles(LabLevelData data)
    {
        if (data.pixelArts == null) return;
        for (int i = 0; i < data.pixelArts.Count; i++)
        {
            var art = data.pixelArts[i];
            Handles.color = art.sprite != null
                ? new Color(1f, 0.55f, 0.9f, 0.75f)
                : new Color(1f, 0.3f, 0.25f, 0.95f);
            var cells = art.Cells();
            Handles.DrawWireCube(
                new Vector3(cells.x + cells.width * 0.5f, cells.y + cells.height * 0.5f, 0f),
                new Vector3(cells.width, cells.height, 0.01f));
        }
    }

    void DrawRespawnPointHandles(LabLevelData data)
    {
        if (data.respawnPoints == null) return;
        Handles.color = new Color(0.35f, 1f, 0.65f, 0.95f);
        for (int i = 0; i < data.respawnPoints.Count; i++)
        {
            var point = data.respawnPoints[i];
            Handles.DrawWireCube(new Vector3(point.position.x, point.position.y, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(point.position.x - 1.4f, point.position.y + 1.6f, 0f), "重生点");
        }
    }

    void DrawTeleportHandles(LabLevelData data)
    {
        if (data.teleports == null || data.teleports.Count == 0) return;

        // 全互联：每两个点之间连一条线，说明进入任意一个都能选其它点作终点。
        Handles.color = new Color(0.75f, 0.55f, 1f, 0.28f);
        for (int i = 0; i < data.teleports.Count; i++)
            for (int j = i + 1; j < data.teleports.Count; j++)
                Handles.DrawLine(
                    new Vector3(data.teleports[i].position.x, data.teleports[i].position.y, 0f),
                    new Vector3(data.teleports[j].position.x, data.teleports[j].position.y, 0f));

        Handles.color = new Color(0.8f, 0.5f, 1f, 0.95f);
        for (int i = 0; i < data.teleports.Count; i++)
        {
            var point = data.teleports[i];
            Handles.DrawWireCube(new Vector3(point.position.x, point.position.y, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(point.position.x - 1.4f, point.position.y + 1.6f, 0f), "传送点");
        }
    }

    static Color IceGroupColor(int groupId) => LabIce.GroupColor(groupId);

    /// <summary>岩浆块用暖色区分组号，和冰块的冷色调分开。</summary>
    static Color MagmaGroupColor(int groupId)
    {
        float hue = Mathf.Lerp(0f, 0.14f, Mathf.Repeat(groupId * 0.37f, 1f));
        float value = 0.72f + 0.28f * Mathf.Repeat(groupId * 0.53f, 1f);
        return Color.HSVToRGB(hue, 0.85f, value);
    }

    /// <summary>隐藏方块用低饱和绿色，和冰（冷色）、岩浆（暖色）都拉开。</summary>
    static Color HiddenGroupColor(int groupId)
    {
        float hue = Mathf.Lerp(0.3f, 0.46f, Mathf.Repeat(groupId * 0.37f, 1f));
        float value = 0.72f + 0.28f * Mathf.Repeat(groupId * 0.53f, 1f);
        return Color.HSVToRGB(hue, 0.6f, value);
    }

    void DrawHover(Vector2Int cell, bool inArea, bool inBounds, LabLevelData data)
    {
        if (tool == Tool.Partition)
        {
            DrawPartitionHover(cell);
            return;
        }

        if (tool == Tool.Marquee)
        {
            RectInt preview = marqueeDragging
                ? MarqueeRect(marqueeStart, marqueeEnd)
                : new RectInt(cell.x, cell.y, 1, 1);
            DrawFilledRect(preview.x, preview.y, preview.x + preview.width - 1, preview.y + preview.height - 1,
                new Color(0.35f, 0.9f, 1f, 0.12f), new Color(0.35f, 0.9f, 1f, 0.9f));
            return;
        }

        if (tool == Tool.Move)
        {
            Handles.color = new Color(1f, 0.55f, 0.2f, 0.9f);

            // 有选区时移动工具不按鼠标位置挑物件，任何位置起手都是拖整个选区。
            if (selection.Count > 0)
            {
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "拖动整个选区");
                return;
            }

            var hover = PickAt(cell);
            if (hover.kind == PickKind.None)
            {
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            RectInt area = PickArea(hover);
            Handles.DrawWireCube(
                new Vector3(area.x + area.width * 0.5f, area.y + area.height * 0.5f, 0f),
                new Vector3(area.width, area.height, 0.01f));
            return;
        }

        if (!inArea) return;

        Color color = ToolColor();
        Handles.color = color;

        if (tool == Tool.Rect && gesture)
        {
            int x0 = Mathf.Min(rectStart.x, rectEnd.x);
            int y0 = Mathf.Min(rectStart.y, rectEnd.y);
            int x1 = Mathf.Max(rectStart.x, rectEnd.x);
            int y1 = Mathf.Max(rectStart.y, rectEnd.y);
            DrawFilledRect(x0, y0, x1, y1, new Color(color.r, color.g, color.b, 0.18f), color);
            return;
        }

        if (tool == Tool.Rope || tool == Tool.HorizontalRope)
        {
            if (!inBounds)
            {
                Handles.color = new Color(1f, 0.3f, 0.25f, 0.9f);
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            bool horizontal = tool == Tool.HorizontalRope;
            var center = horizontal
                ? new Vector3(cell.x + ropeLength * 0.5f, cell.y + 1.125f, 0f)
                : new Vector3(cell.x + 0.5f, cell.y + ropeLength * 0.5f, 0f);
            var size = horizontal
                ? new Vector3(ropeLength, 0.25f, 0.01f)
                : new Vector3(RopeStripWidth, ropeLength, 0.01f);
            Handles.DrawWireCube(center, size);
            Handles.Label(center + (horizontal ? Vector3.up * 0.3f : Vector3.up * (ropeLength * 0.5f + 0.3f)),
                (horizontal ? "横绳 " : "竖绳 ") + ropeLength + " 格");
            return;
        }

        if (tool == Tool.SpawnA || tool == Tool.SpawnB)
        {
            if (!inBounds)
            {
                Handles.color = new Color(1f, 0.3f, 0.25f, 0.9f);
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            float size = tool == Tool.SpawnA
                ? TuningLab.A_Pixels / (float)TuningLab.CellPixels
                : TuningLab.B_Pixels / (float)TuningLab.CellPixels;
            var center = new Vector3(cell.x + 0.5f, cell.y + size * 0.5f, 0f);
            Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            Handles.DrawWireCube(center, new Vector3(size, size, 0.01f));
            return;
        }

        if (tool == Tool.Key)
        {
            Handles.color = inBounds
                ? new Color(color.r, color.g, color.b, 0.6f)
                : new Color(1f, 0.3f, 0.25f, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            return;
        }

        if (tool == Tool.Recall)
        {
            bool blocked = !inBounds || data.HasCell(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.75f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f,
                data.HasRecall(cell) ? "召回道具（再点删除）" : "召回道具");
            return;
        }

        if (tool == Tool.Door)
        {
            Handles.color = inBounds
                ? new Color(color.r, color.g, color.b, 0.7f)
                : new Color(1f, 0.3f, 0.25f, 0.9f);
            Handles.DrawWireCube(
                new Vector3(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f, 0f),
                new Vector3(1f, LabDoorSpec.CellHeight, 0.01f));
            return;
        }

        if (tool == Tool.Spike)
        {
            var spec = new LabSpikeSpec(cell, newSpikeDir);
            bool blocked = data.HasCell(cell);
            bool floating = !data.HasCell(spec.BaseCell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, floating ? 0.45f : 0.9f);

            var center = spec.StripCenter;
            var size = spec.StripSize;
            Handles.DrawWireCube(new Vector3(center.x, center.y, 0f),
                new Vector3(size.x, size.y, 0.01f));
            // 整格描一圈：点下去的是这一格，刺只占靠边的那 6 像素。
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            if (blocked) Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "实心格里放不下");
            else if (floating) Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "底座悬空");
            return;
        }

        if (tool == Tool.Ice)
        {
            bool blocked = !inBounds || data.HasCell(cell) || data.HasSpike(cell) || data.HasMagma(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(IceGroupColor(iceGroupId).r, IceGroupColor(iceGroupId).g,
                            IceGroupColor(iceGroupId).b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "冰 组 " + iceGroupId);
            return;
        }

        if (tool == Tool.Magma)
        {
            bool blocked = !inBounds || data.HasCell(cell) || data.HasSpike(cell) || data.HasIce(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(MagmaGroupColor(magmaGroupId).r, MagmaGroupColor(magmaGroupId).g,
                            MagmaGroupColor(magmaGroupId).b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "岩浆 组 " + magmaGroupId);
            return;
        }

        if (tool == Tool.HiddenBlock)
        {
            bool blocked = !inBounds || data.HasCell(cell);
            bool inGroup = data.HasHiddenBlock(cell, hiddenGroupId);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(HiddenGroupColor(hiddenGroupId).r, HiddenGroupColor(hiddenGroupId).g,
                            HiddenGroupColor(hiddenGroupId).b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f,
                (inGroup ? "移除 隐 组 " : "隐 组 ") + hiddenGroupId);
            return;
        }

        if (tool == Tool.RepairBlock)
        {
            bool blocked = !inBounds || data.HasCell(cell) || data.HasSpike(cell)
                || data.HasIce(cell) || data.HasMagma(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(TuningLab.ColorA.r, TuningLab.ColorA.g, TuningLab.ColorA.b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f,
                data.HasRepairBlock(cell) ? "修复方块（再点删除）" : "修复方块");
            return;
        }

        if (tool == Tool.PixelArt)
        {
            Vector2Int size = new Vector2Int(
                Mathf.Max(1, newPixelArtSize.x),
                Mathf.Max(1, newPixelArtSize.y));
            bool blocked = newPixelArtSprite == null || !PixelArtFits(cell, size, data);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(1f, 0.55f, 0.9f, 0.9f);
            Handles.DrawWireCube(
                new Vector3(cell.x + size.x * 0.5f, cell.y + size.y * 0.5f, 0f),
                new Vector3(size.x, size.y, 0.01f));
            Handles.Label(
                new Vector3(cell.x + 0.5f, cell.y + size.y + 0.2f, 0f),
                blocked ? "像素画不可放置" : "像素画 " + size.x + "×" + size.y);
            return;
        }

        if (tool == Tool.RespawnPoint)
        {
            bool blocked = !inBounds;
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.75f);
            Handles.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(cell.x + 0.5f, cell.y + 2.1f, 0f), "重生点");
            return;
        }

        if (tool == Tool.TeleportPoint)
        {
            bool blocked = !inBounds;
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.8f);
            Handles.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(cell.x + 0.5f, cell.y + 2.1f, 0f),
                data.HasTeleport(cell) ? "传送点（再点删除）" : "传送点");
            return;
        }

        Handles.color = new Color(color.r, color.g, color.b, 0.18f);
        Handles.DrawSolidRectangleWithOutline(
            RectVerts(cell.x, cell.y, cell.x + 1, cell.y + 1),
            new Color(color.r, color.g, color.b, 0.16f),
            color);
    }

    void DrawFilledRect(int x0, int y0, int x1, int y1, Color fill, Color outline)
    {
        Handles.DrawSolidRectangleWithOutline(RectVerts(x0, y0, x1 + 1, y1 + 1), fill, outline);
    }

    static Vector3[] RectVerts(int x0, int y0, int x1, int y1)
    {
        return new[]
        {
            new Vector3(x0, y0, 0f),
            new Vector3(x1, y0, 0f),
            new Vector3(x1, y1, 0f),
            new Vector3(x0, y1, 0f)
        };
    }

    Color ToolColor()
    {
        switch (tool)
        {
            case Tool.Erase: return new Color(1f, 0.35f, 0.3f, 0.95f);
            case Tool.Rect: return new Color(1f, 0.85f, 0.3f, 0.95f);
            case Tool.Rope:
            case Tool.HorizontalRope: return new Color(0.85f, 0.65f, 0.35f, 0.95f);
            case Tool.SpawnA: return TuningLab.ColorA;
            case Tool.SpawnB: return TuningLab.ColorB;
            case Tool.Partition: return new Color(1f, 0.85f, 0.3f, 0.95f);
            case Tool.Key: return new Color(1f, 0.85f, 0.1f, 0.95f);
            case Tool.Recall: return new Color(0.2f, 0.9f, 0.8f, 0.95f);
            case Tool.Door: return new Color(newDoorColor.r, newDoorColor.g, newDoorColor.b, 0.95f);
            case Tool.Spike: return new Color(0.75f, 0.82f, 0.92f, 0.95f);
            case Tool.Ice:
            {
                Color c = LabIce.GroupColor(iceGroupId);
                return new Color(c.r, c.g, c.b, 0.95f);
            }
            case Tool.Magma: return new Color(1f, 0.34f, 0.12f, 0.95f);
            case Tool.HiddenBlock: return new Color(0.45f, 0.9f, 0.55f, 0.95f);
            case Tool.RepairBlock: return TuningLab.ColorA;
            case Tool.PixelArt: return new Color(1f, 0.55f, 0.9f, 0.95f);
            case Tool.RespawnPoint: return new Color(0.35f, 1f, 0.65f, 0.95f);
            case Tool.TeleportPoint: return new Color(0.8f, 0.5f, 1f, 0.95f);
            case Tool.Marquee: return new Color(0.35f, 0.9f, 1f, 0.95f);
            case Tool.Move: return new Color(1f, 0.55f, 0.2f, 0.95f);
            default: return new Color(1f, 0.85f, 0.3f, 0.95f);
        }
    }

    static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

    static Vector3 ScenePoint(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    static Vector2Int CellOf(Vector3 point)
        => new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

    static bool InEditArea(Vector2Int cell, LabLevelData data)
    {
        RectInt area = EditArea(data);
        return cell.x >= area.x && cell.x < area.x + area.width
            && cell.y >= area.y && cell.y < area.y + area.height;
    }

    static RectInt EditArea(LabLevelData data)
    {
        return new RectInt(
            data.bounds.x - EditMargin,
            data.bounds.y - EditMargin,
            data.bounds.width + EditMargin * 2,
            data.bounds.height + EditMargin * 2);
    }

    static void Notify(SceneView view, string message)
    {
        if (view == null) return;
        view.ShowNotification(new GUIContent(message));
    }
}
#endif
