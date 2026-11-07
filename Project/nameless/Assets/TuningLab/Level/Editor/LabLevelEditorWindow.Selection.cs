#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：涂改、选区与快捷键。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{
    void ApplyCell(Vector2Int cell, bool erase)
    {
        var data = level.Data;
        if (!InEditArea(cell, data)) return;
        if (erase)
        {
            data.RemoveCell(cell);
            EraseObjectsAt(data, cell);  // 擦地形 = 这一格摆的东西一起擦掉
        }
        else data.AddCell(cell);
    }

    /// <summary>擦掉落在这一格上的所有摆放物件（地形格另算）。</summary>
    static void EraseObjectsAt(LabLevelData data, Vector2Int cell)
    {
        if (data.keys != null) data.keys.RemoveAll(key => key.Cells().Contains(cell));
        if (data.recalls != null) data.recalls.RemoveAll(recall => recall.cell == cell);
        if (data.doors != null) data.doors.RemoveAll(door => door.Cells().Contains(cell));
        if (data.spikes != null) data.spikes.RemoveAll(spike => spike.cell == cell);
        if (data.ices != null) data.ices.RemoveAll(ice => ice.cell == cell);
        if (data.magmas != null) data.magmas.RemoveAll(magma => magma.cell == cell);
        if (data.hiddenBlocks != null) data.hiddenBlocks.RemoveAll(block => block.cell == cell);
        if (data.repairBlocks != null) data.repairBlocks.RemoveAll(block => block.cell == cell);
        if (data.respawnPoints != null)
            data.respawnPoints.RemoveAll(point => point.Cells().Contains(cell));
        if (data.teleports != null)
            data.teleports.RemoveAll(point => point.Cells().Contains(cell));
        if (data.ropes != null) data.ropes.RemoveAll(rope => RopeCoversCell(rope, cell));
    }

    /// <summary>这根绳（竖或横）是否盖住这一格。</summary>
    static bool RopeCoversCell(LabRopeSpec rope, Vector2Int cell)
    {
        if (rope.orientation == LabRopeOrientation.Horizontal)
        {
            int row = Mathf.FloorToInt(rope.center.y - 0.125f);
            if (cell.y != row) return false;
            float half = rope.cells * 0.5f;
            return cell.x + 1 > rope.center.x - half && cell.x < rope.center.x + half;
        }

        if (cell.x != Mathf.FloorToInt(rope.center.x)) return false;
        float bottom = rope.center.y - rope.cells * 0.5f;
        float top = rope.center.y + rope.cells * 0.5f;
        return cell.y + 1 > bottom && cell.y < top;
    }

    void PaintLine(Vector2Int from, Vector2Int to, bool erase)
    {
        int x = from.x;
        int y = from.y;
        int dx = Mathf.Abs(to.x - from.x);
        int dy = -Mathf.Abs(to.y - from.y);
        int sx = from.x < to.x ? 1 : -1;
        int sy = from.y < to.y ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            ApplyCell(new Vector2Int(x, y), erase);
            if (x == to.x && y == to.y) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    // ------------------------------------------------------------------
    // 摆放 / 擦除：同一个手势里按格连续执行
    // ------------------------------------------------------------------

    bool IsPlaceTool()
    {
        switch (tool)
        {
            case Tool.Key:
            case Tool.Recall:
            case Tool.Door:
            case Tool.Rope:
            case Tool.HorizontalRope:
            case Tool.Spike:
            case Tool.Ice:
            case Tool.Magma:
            case Tool.HiddenBlock:
            case Tool.RepairBlock:
            case Tool.RespawnPoint:
            case Tool.TeleportPoint:
                return true;
            default:
                return false;
        }
    }

    /// <summary>按下第一格时决定这个手势是"放"还是"删"：开关型工具点在已存在的同类上就是删。</summary>
    bool ShouldAddAtStart(Vector2Int cell)
    {
        var data = level.Data;
        switch (tool)
        {
            case Tool.Spike:
            {
                var existing = data.FindSpike(cell);
                return !(existing.HasValue && existing.Value.dir == newSpikeDir);
            }
            case Tool.Ice:
            {
                var existing = data.FindIce(cell);
                return !(existing.HasValue && existing.Value.groupId == iceGroupId);
            }
            case Tool.Magma:
            {
                var existing = data.FindMagma(cell);
                return !(existing.HasValue && existing.Value.groupId == magmaGroupId);
            }
            case Tool.HiddenBlock:
                return !data.HasHiddenBlock(cell, hiddenGroupId);
            case Tool.RepairBlock:
                return !data.HasRepairBlock(cell);
            case Tool.RespawnPoint:
                return !data.HasRespawnPoint(cell);
            case Tool.TeleportPoint:
                return !data.HasTeleport(cell);
            case Tool.Recall:
                return !data.HasRecall(cell);
            default:
                return true;
        }
    }

    /// <summary>对单个格子执行当前摆放工具的"放"或"删"。不 Record，由手势开始时统一记一次。</summary>
    bool ApplyToolAtCell(Vector2Int cell, bool add)
    {
        var data = level.Data;
        switch (tool)
        {
            case Tool.Key:
                if (!add) return data.keys.RemoveAll(key => key.Cells().Contains(cell)) > 0;
                if (!data.ContainsCell(cell)) return false;
                data.keys.Add(new LabKeySpec(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
                return true;

            case Tool.Recall:
                if (!add) return data.RemoveRecall(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell)) return false;
                return data.PlaceRecall(cell);

            case Tool.Door:
                if (!add) return data.doors.RemoveAll(door => door.Cells().Contains(cell)) > 0;
                if (!data.ContainsCell(cell)) return false;
                if (cell.y + LabDoorSpec.CellHeight > data.bounds.yMax) return false;
                data.doors.Add(new LabDoorSpec(
                    new Vector2(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f), newDoorColor));
                return true;

            case Tool.Rope:
            case Tool.HorizontalRope:
            {
                if (!add) return data.ropes.RemoveAll(rope => RopeCoversCell(rope, cell)) > 0;

                var orientation = tool == Tool.HorizontalRope
                    ? LabRopeOrientation.Horizontal
                    : LabRopeOrientation.Vertical;
                if (orientation == LabRopeOrientation.Vertical && cell.y + ropeLength > data.bounds.yMax)
                    return false;
                if (orientation == LabRopeOrientation.Horizontal &&
                    (cell.x + ropeLength > data.bounds.xMax || cell.y + 1 > data.bounds.yMax))
                    return false;

                Vector2 center = orientation == LabRopeOrientation.Horizontal
                    ? new Vector2(cell.x + ropeLength * 0.5f, cell.y + 1.125f)
                    : new Vector2(cell.x + 0.5f, cell.y + ropeLength * 0.5f);
                data.ropes.Add(new LabRopeSpec(center, ropeLength, ropeChainId, orientation));
                return true;
            }

            case Tool.Spike:
                if (!add) return data.RemoveSpike(cell);
                if (data.HasCell(cell)) return false;
                return data.PlaceSpike(cell, newSpikeDir);

            case Tool.Ice:
                if (!add) return data.RemoveIce(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell) || data.HasMagma(cell)) return false;
                return data.PlaceIce(cell, iceGroupId);

            case Tool.Magma:
                if (!add) return data.RemoveMagma(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell) || data.HasIce(cell)) return false;
                return data.PlaceMagma(cell, magmaGroupId);

            case Tool.HiddenBlock:
                if (!add) return data.RemoveHiddenBlock(cell, hiddenGroupId);
                if (!data.ContainsCell(cell) || data.HasCell(cell)) return false;
                return data.PlaceHiddenBlock(cell, hiddenGroupId);

            case Tool.RepairBlock:
                if (!add) return data.RemoveRepairBlock(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell) || data.HasIce(cell)
                    || data.HasMagma(cell)) return false;
                return data.PlaceRepairBlock(cell);

            case Tool.RespawnPoint:
                if (!add) return data.RemoveRespawnPoint(cell);
                if (!data.ContainsCell(cell)) return false;
                return data.PlaceRespawnPoint(cell);

            case Tool.TeleportPoint:
                if (!add) return data.RemoveTeleport(cell);
                if (!data.ContainsCell(cell)) return false;
                return data.PlaceTeleport(cell);
        }
        return false;
    }

    /// <summary>Bresenham 走过的格子，拖动时用来补齐，免得快速拖动漏格。</summary>
    static IEnumerable<Vector2Int> LineCells(Vector2Int from, Vector2Int to)
    {
        int x = from.x;
        int y = from.y;
        int dx = Mathf.Abs(to.x - from.x);
        int dy = -Mathf.Abs(to.y - from.y);
        int sx = from.x < to.x ? 1 : -1;
        int sy = from.y < to.y ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            yield return new Vector2Int(x, y);
            if (x == to.x && y == to.y) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    // ------------------------------------------------------------------
    // 选择 / 选区
    // ------------------------------------------------------------------

    static RectInt CellRect(Vector2 point)
        => new RectInt(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), 1, 1);

    /// <summary>这一格上最上层的物件。优先级：传送点 &gt; 重生点 &gt; 隐藏方块 &gt; 钥匙 &gt; 召回道具 &gt; 冰块 &gt; 岩浆块 &gt; 修复方块 &gt; 地刺 &gt; 门 &gt; 绳 &gt; 地形。</summary>
    Pick PickAt(Vector2Int cell)
    {
        var data = level.Data;

        if (data.teleports != null)
            for (int i = 0; i < data.teleports.Count; i++)
                if (data.teleports[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.TeleportPoint, index = i };

        if (data.respawnPoints != null)
            for (int i = 0; i < data.respawnPoints.Count; i++)
                if (data.respawnPoints[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.RespawnPoint, index = i };

        // 隐藏方块盖在钥匙/门/绳上面，先按视觉最上层认领它。
        if (data.hiddenBlocks != null)
            for (int i = 0; i < data.hiddenBlocks.Count; i++)
                if (data.hiddenBlocks[i].cell == cell)
                    return new Pick { kind = PickKind.HiddenBlock, index = i };

        if (data.keys != null)
            for (int i = 0; i < data.keys.Count; i++)
                if (data.keys[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.Key, index = i };

        if (data.recalls != null)
            for (int i = 0; i < data.recalls.Count; i++)
                if (data.recalls[i].cell == cell)
                    return new Pick { kind = PickKind.Recall, index = i };

        if (data.ices != null)
            for (int i = 0; i < data.ices.Count; i++)
                if (data.ices[i].cell == cell)
                    return new Pick { kind = PickKind.Ice, index = i };

        if (data.magmas != null)
            for (int i = 0; i < data.magmas.Count; i++)
                if (data.magmas[i].cell == cell)
                    return new Pick { kind = PickKind.Magma, index = i };

        if (data.repairBlocks != null)
            for (int i = 0; i < data.repairBlocks.Count; i++)
                if (data.repairBlocks[i].cell == cell)
                    return new Pick { kind = PickKind.RepairBlock, index = i };

        if (data.spikes != null)
            for (int i = 0; i < data.spikes.Count; i++)
                if (data.spikes[i].cell == cell)
                    return new Pick { kind = PickKind.Spike, index = i };

        if (data.doors != null)
            for (int i = 0; i < data.doors.Count; i++)
                if (data.doors[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.Door, index = i };

        if (data.ropes != null)
            for (int i = 0; i < data.ropes.Count; i++)
                if (RopeCoversCell(data.ropes[i], cell))
                    return new Pick { kind = PickKind.Rope, index = i };

        if (data.solids != null)
            for (int i = 0; i < data.solids.Count; i++)
                if (data.solids[i] == cell)
                    return new Pick { kind = PickKind.Solid, index = i };

        return new Pick { kind = PickKind.None, index = -1 };
    }

    /// <summary>撤销之后索引可能失效，用之前先验一遍。</summary>
    bool ValidPick(Pick pick)
    {
        var data = level.Data;
        switch (pick.kind)
        {
            case PickKind.Solid: return pick.index >= 0 && pick.index < data.solids.Count;
            case PickKind.Rope: return pick.index >= 0 && pick.index < data.ropes.Count;
            case PickKind.Key: return pick.index >= 0 && pick.index < data.keys.Count;
            case PickKind.Recall: return pick.index >= 0 && pick.index < data.recalls.Count;
            case PickKind.Door: return pick.index >= 0 && pick.index < data.doors.Count;
            case PickKind.Spike: return pick.index >= 0 && pick.index < data.spikes.Count;
            case PickKind.Ice: return pick.index >= 0 && pick.index < data.ices.Count;
            case PickKind.Magma: return pick.index >= 0 && pick.index < data.magmas.Count;
            case PickKind.HiddenBlock:
                return data.hiddenBlocks != null && pick.index >= 0 && pick.index < data.hiddenBlocks.Count;
            case PickKind.RepairBlock: return pick.index >= 0 && pick.index < data.repairBlocks.Count;
            case PickKind.RespawnPoint: return pick.index >= 0 && pick.index < data.respawnPoints.Count;
            case PickKind.TeleportPoint: return pick.index >= 0 && pick.index < data.teleports.Count;
            default: return false;
        }
    }

    RectInt PickArea(Pick pick)
    {
        var data = level.Data;
        switch (pick.kind)
        {
            case PickKind.Solid: return new RectInt(data.solids[pick.index], Vector2Int.one);
            case PickKind.Rope: return CellRect(data.ropes[pick.index].center);
            case PickKind.Key: return CellRect(data.keys[pick.index].position);
            case PickKind.Recall: return new RectInt(data.recalls[pick.index].cell, Vector2Int.one);
            case PickKind.Door: return CellRect(data.doors[pick.index].position);
            case PickKind.Spike: return new RectInt(data.spikes[pick.index].cell, Vector2Int.one);
            case PickKind.Ice: return new RectInt(data.ices[pick.index].cell, Vector2Int.one);
            case PickKind.Magma: return new RectInt(data.magmas[pick.index].cell, Vector2Int.one);
            case PickKind.HiddenBlock:
                return new RectInt(data.hiddenBlocks[pick.index].cell, Vector2Int.one);
            case PickKind.RepairBlock: return new RectInt(data.repairBlocks[pick.index].cell, Vector2Int.one);
            case PickKind.RespawnPoint: return CellRect(data.respawnPoints[pick.index].position);
            case PickKind.TeleportPoint: return CellRect(data.teleports[pick.index].position);
            default: return new RectInt(0, 0, 0, 0);
        }
    }

    string PickLabel(Pick pick)
    {
        switch (pick.kind)
        {
            case PickKind.Solid: return "地形格 " + level.Data.solids[pick.index];
            case PickKind.Rope: return "绳 #" + pick.index;
            case PickKind.Key: return "钥匙 #" + pick.index;
            case PickKind.Recall: return "召回道具 #" + pick.index;
            case PickKind.Door: return "门 #" + pick.index;
            case PickKind.Spike: return "地刺 #" + pick.index;
            case PickKind.Ice: return "冰块 #" + pick.index;
            case PickKind.Magma: return "岩浆块 #" + pick.index;
            case PickKind.HiddenBlock:
                return "隐藏方块 组" + level.Data.hiddenBlocks[pick.index].groupId +
                    " @ " + level.Data.hiddenBlocks[pick.index].cell;
            case PickKind.RepairBlock: return "修复方块 #" + pick.index;
            case PickKind.RespawnPoint: return "重生点 #" + pick.index;
            case PickKind.TeleportPoint: return "传送点 #" + pick.index;
            default: return "（无）";
        }
    }

    /// <summary>拖动时作用到的范围：有选区就是选区，否则是点中的那个物件。</summary>
    IList<RectInt> CurrentMoveAreas()
    {
        if (selection.Count > 0) return selection;
        if (!ValidPick(picked)) return null;
        return new List<RectInt> { PickArea(picked) };
    }

    void DeleteSelection()
    {
        bool hasSelection = selection.Count > 0;
        if (!hasSelection && !ValidPick(picked)) return;

        Record("删除选区内容");

        bool did;
        if (hasSelection)
        {
            did = level.Data.DeleteContents(selection, true) > 0;
        }
        else
        {
            var area = new List<RectInt> { PickArea(picked) };
            did = level.Data.DeleteContents(area, picked.kind == PickKind.Solid) > 0;
        }

        picked = default;
        if (!did)
        {
            Repaint();
            return;
        }
        AfterStructuralEdit();
    }

    static RectInt MarqueeRect(Vector2Int a, Vector2Int b)
    {
        int x0 = Mathf.Min(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y);
        return new RectInt(x0, y0, Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
    }

    void CommitMarquee()
    {
        RectInt rect = MarqueeRect(marqueeStart, marqueeEnd);
        bool empty = rect.width <= 1 && rect.height <= 1;

        if (empty)
        {
            // 单击不产生选区；重新选择模式下单击 = 清掉当前选区。
            if (marqueeMode == MarqueeMode.Replace) selection.Clear();
            return;
        }

        switch (marqueeMode)
        {
            case MarqueeMode.Replace:
                selection.Clear();
                AddRect(selection, rect);
                break;
            case MarqueeMode.Add:
                UnionRect(selection, rect);
                break;
            case MarqueeMode.Subtract:
                SubtractRect(selection, rect);
                break;
        }
    }

    static void AddRect(List<RectInt> list, RectInt rect)
    {
        if (rect.width > 0 && rect.height > 0) list.Add(rect);
    }

    /// <summary>
    /// 把一块矩形并进选区。选区用一组互不重叠的矩形表示：先把新矩形从旧矩形里挖掉，
    /// 再把新矩形加回去，结果正好是并集，且不会留下重叠。
    /// </summary>
    internal static void UnionRect(List<RectInt> list, RectInt rect)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        SubtractRect(list, rect);
        list.Add(rect);
    }

    /// <summary>从一组选区里挖掉一块：被切开的部分按 左/右/上/下 拆成最多四块。</summary>
    internal static void SubtractRect(List<RectInt> list, RectInt cut)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            RectInt rect = list[i];
            if (rect.x >= cut.x + cut.width || cut.x >= rect.x + rect.width ||
                rect.y >= cut.y + cut.height || cut.y >= rect.y + rect.height)
                continue;

            list.RemoveAt(i);

            int x0 = rect.x, x1 = rect.x + rect.width;
            int y0 = rect.y, y1 = rect.y + rect.height;
            int cx0 = cut.x, cx1 = cut.x + cut.width;
            int cy0 = cut.y, cy1 = cut.y + cut.height;

            AddRect(list, new RectInt(x0, y0, cx0 - x0, y1 - y0));           // 左
            AddRect(list, new RectInt(cx1, y0, x1 - cx1, y1 - y0));          // 右
            int midX0 = Mathf.Max(x0, cx0);
            int midX1 = Mathf.Min(x1, cx1);
            AddRect(list, new RectInt(midX0, y0, midX1 - midX0, cy0 - y0));  // 下
            AddRect(list, new RectInt(midX0, cy1, midX1 - midX0, y1 - cy1)); // 上
        }
    }

    /// <summary>选区的格数。矩形互不重叠，所以直接累加面积。</summary>
    static int SelectionArea(IList<RectInt> rects)
    {
        int area = 0;
        for (int i = 0; i < rects.Count; i++) area += rects[i].width * rects[i].height;
        return area;
    }

    /// <summary>选区的包围盒，只用于面板显示和标签定位。</summary>
    static RectInt SelectionBounds(IList<RectInt> rects)
    {
        if (rects.Count == 0) return new RectInt();
        int xMin = int.MaxValue, yMin = int.MaxValue;
        int xMax = int.MinValue, yMax = int.MinValue;
        for (int i = 0; i < rects.Count; i++)
        {
            RectInt rect = rects[i];
            xMin = Mathf.Min(xMin, rect.x);
            yMin = Mathf.Min(yMin, rect.y);
            xMax = Mathf.Max(xMax, rect.x + rect.width);
            yMax = Mathf.Max(yMax, rect.y + rect.height);
        }
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    /// <summary>平移选区矩形。拖动选区时框和内容保持相对位置一起走。</summary>
    internal static void TranslateRects(List<RectInt> rects, Vector2Int delta)
    {
        if (delta == Vector2Int.zero) return;
        for (int i = 0; i < rects.Count; i++)
        {
            RectInt rect = rects[i];
            rect.x += delta.x;
            rect.y += delta.y;
            rects[i] = rect;
        }
    }

    /// <summary>
    /// 只保留四个快捷键：1 铺地形 / 3 擦地形 / 4 选区 / 5 选择。
    /// 不要新增或恢复其它快捷键；其它工具一律从工具面板选择。
    /// </summary>
    bool HandleKey(Event e)
    {
        // 拖动过程中不让工具键改工具，否则一个手势会跨到别的工具上。
        if (gesture) return false;

        switch (e.keyCode)
        {
            case KeyCode.Alpha1: tool = Tool.Paint; break;
            case KeyCode.Alpha3: tool = Tool.Erase; break;
            case KeyCode.Alpha4: tool = Tool.Marquee; break;
            case KeyCode.Alpha5: tool = Tool.Move; break;
            default: return false;
        }

        Repaint();
        e.Use();
        return true;
    }

}
#endif
