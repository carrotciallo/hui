using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一套关卡数据。坐标和 TuningLab 完全同一套：1 格 = 1 世界单位，
/// 格 (x, y) 占 [x, x+1) × [y, y+1)。不引入第二套网格或第二套尺度。
///
/// 数据只描述"关卡里有什么"，不负责搭场景；搭场景由 <see cref="LabLevel"/> 和
/// TuningLab 完成，两边读的是同一份数据。
/// </summary>
[System.Serializable]
public sealed class LabLevelData
{
    /// <summary>相机 clamp 范围。注意 TuningLab 基准房间的两面墙就在这个范围外 1 格，这是基准行为。</summary>
    public RectInt bounds = new RectInt(-13, -1, 26, 17);
    /// <summary>A 的落点（脚底），不是中心。</summary>
    public Vector2 spawnA = new Vector2(8f, 0f);
    /// <summary>B 的落点（脚底），不是中心。</summary>
    public Vector2 spawnB = new Vector2(10f, 0f);
    /// <summary>自由相机跟随的最小正交尺寸（格，半高）。</summary>
    public float cameraMinSize = TuningLab.DefaultCameraMinSize;
    /// <summary>自由相机跟随的最大正交尺寸（格，半高）。</summary>
    public float cameraMaxSize = TuningLab.DefaultCameraMaxSize;
    public List<Vector2Int> solids = new List<Vector2Int>();
    public List<LabRopeSpec> ropes = new List<LabRopeSpec>();
    /// <summary>连续关卡分区。每个分区的 bounds 同时是它的相机范围；B 进入哪个分区就用哪个分区。</summary>
    public List<LabLevelPartition> partitions = new List<LabLevelPartition>();

    public bool HasCell(Vector2Int cell) => solids != null && solids.Contains(cell);

    public bool AddCell(Vector2Int cell)
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (solids.Contains(cell)) return false;
        solids.Add(cell);
        return true;
    }

    public bool RemoveCell(Vector2Int cell) => solids != null && solids.Remove(cell);

    public void ClearCells()
    {
        if (solids == null) solids = new List<Vector2Int>();
        else solids.Clear();
    }

    public void ClearRopes()
    {
        if (ropes == null) ropes = new List<LabRopeSpec>();
        else ropes.Clear();
    }

    public bool ContainsCell(Vector2Int cell)
    {
        return cell.x >= bounds.x && cell.x < bounds.x + bounds.width
            && cell.y >= bounds.y && cell.y < bounds.y + bounds.height;
    }

    public bool ContainsPoint(Vector2 point)
    {
        return point.x >= bounds.x && point.x < bounds.x + bounds.width
            && point.y >= bounds.y && point.y < bounds.y + bounds.height;
    }

    /// <summary>排序 + 去重。编辑器每次改完都调一次，保证存盘顺序稳定、序列化 diff 干净。</summary>
    public void Normalize()
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (ropes == null) ropes = new List<LabRopeSpec>();
        if (partitions == null) partitions = new List<LabLevelPartition>();

        // 旧场景里没有这两个字段时读出来是 0：回落到 Tuning Lab 默认，避免相机被锁死在 0.1。
        if (cameraMinSize <= 0f) cameraMinSize = TuningLab.DefaultCameraMinSize;
        if (cameraMaxSize <= 0f) cameraMaxSize = TuningLab.DefaultCameraMaxSize;
        cameraMinSize = Mathf.Clamp(cameraMinSize, 0.1f, 128f);
        cameraMaxSize = Mathf.Clamp(cameraMaxSize, cameraMinSize, 128f);

        for (int i = partitions.Count - 1; i >= 0; i--)
        {
            var partition = partitions[i];
            if (partition == null)
            {
                partitions.RemoveAt(i);
                continue;
            }

            partition.bounds = NormalizeRect(partition.bounds);
            if (string.IsNullOrEmpty(partition.name)) partition.name = "Level " + (i + 1);
        }

        solids.Sort(CompareCells);

        int write = 0;
        for (int read = 0; read < solids.Count; read++)
        {
            if (write > 0 && solids[read] == solids[write - 1]) continue;
            solids[write++] = solids[read];
        }
        if (write < solids.Count) solids.RemoveRange(write, solids.Count - write);

        for (int i = ropes.Count - 1; i >= 0; i--)
        {
            var rope = ropes[i];
            if (rope.cells <= 0)
            {
                ropes.RemoveAt(i);
                continue;
            }
            rope.cells = Mathf.Min(rope.cells, 256);
            ropes[i] = rope;
        }
    }

    public void CopyFrom(LabLevelData other)
    {
        if (other == null)
        {
            bounds = new RectInt(-13, -1, 26, 17);
            spawnA = new Vector2(8f, 0f);
            spawnB = new Vector2(10f, 0f);
            cameraMinSize = TuningLab.DefaultCameraMinSize;
            cameraMaxSize = TuningLab.DefaultCameraMaxSize;
            solids = new List<Vector2Int>();
            ropes = new List<LabRopeSpec>();
            partitions = new List<LabLevelPartition>();
            return;
        }

        bounds = other.bounds;
        spawnA = other.spawnA;
        spawnB = other.spawnB;
        cameraMinSize = other.cameraMinSize;
        cameraMaxSize = other.cameraMaxSize;
        solids = new List<Vector2Int>(other.solids ?? new List<Vector2Int>());
        ropes = new List<LabRopeSpec>(other.ropes ?? new List<LabRopeSpec>());
        partitions = new List<LabLevelPartition>();
        if (other.partitions != null)
        {
            foreach (var partition in other.partitions)
            {
                if (partition == null) continue;
                partitions.Add(new LabLevelPartition
                {
                    name = partition.name,
                    bounds = partition.bounds
                });
            }
        }
        Normalize();
    }

    public LabLevelData Clone()
    {
        var copy = new LabLevelData();
        copy.CopyFrom(this);
        return copy;
    }

    public static Rect ToRect(RectInt rect)
        => new Rect(rect.x, rect.y, rect.width, rect.height);

    public static RectInt NormalizeRect(RectInt rect)
    {
        if (rect.width < 0)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }
        if (rect.height < 0)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }
        if (rect.width <= 0) rect.width = 1;
        if (rect.height <= 0) rect.height = 1;
        return rect;
    }

    /// <summary>B 的判定：点在哪个分区的 bounds 里；没有返回 -1。</summary>
    public int FindPartition(Vector2 point)
    {
        if (partitions == null) return -1;
        for (int i = 0; i < partitions.Count; i++)
        {
            var partition = partitions[i];
            if (partition != null && ToRect(partition.bounds).Contains(point)) return i;
        }
        return -1;
    }

    public LabLevelPartition AddPartition(RectInt bounds, string name = null)
    {
        if (partitions == null) partitions = new List<LabLevelPartition>();
        var partition = new LabLevelPartition
        {
            name = string.IsNullOrEmpty(name) ? "Level " + (partitions.Count + 1) : name,
            bounds = NormalizeRect(bounds)
        };
        partitions.Add(partition);
        return partition;
    }

    /// <summary>
    /// 移动分区。moveContents = true 时，把旧 bounds 里的地形、绳、出生点一起平移；
    /// 判定用格中心 / 绳中心 / 出生点，避免边界格被误带走。
    /// </summary>
    public bool MovePartition(int index, Vector2Int delta, bool moveContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;
        var partition = partitions[index];
        if (partition == null || delta == Vector2Int.zero) return false;

        Rect oldRect = ToRect(partition.bounds);
        partition.bounds = new RectInt(
            partition.bounds.x + delta.x,
            partition.bounds.y + delta.y,
            partition.bounds.width,
            partition.bounds.height);

        if (!moveContents) return true;

        var shift = new Vector2(delta.x, delta.y);

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                if (oldRect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)))
                    solids[i] = cell + delta;
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                if (!oldRect.Contains(rope.center)) continue;
                rope.center += shift;
                ropes[i] = rope;
            }
        }

        if (oldRect.Contains(spawnA)) spawnA += shift;
        if (oldRect.Contains(spawnB)) spawnB += shift;

        return true;
    }

    /// <summary>删除分区。deleteContents = true 时清掉分区里的地形和绳；出生点保留。</summary>
    public bool DeletePartition(int index, bool deleteContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;

        var partition = partitions[index];
        if (partition != null && deleteContents)
        {
            Rect rect = ToRect(partition.bounds);
            if (solids != null)
                solids.RemoveAll(cell => rect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
            if (ropes != null)
                ropes.RemoveAll(rope => rect.Contains(rope.center));
        }

        partitions.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// 把实心格合并成尽量少的矩形，降低 Collider 数量，也保证运行时生成出来的
    /// 就是 TuningLab 那种"一整块平台一个 BoxCollider2D"。
    /// 贪心：从最下、最左的格开始，先向右扩，再整行向上扩。
    /// </summary>
    public List<RectInt> MergedRects()
    {
        var rects = new List<RectInt>();
        var set = new HashSet<Vector2Int>();
        if (solids != null)
            for (int i = 0; i < solids.Count; i++) set.Add(solids[i]);

        while (set.Count > 0)
        {
            Vector2Int start = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (var cell in set)
            {
                if (cell.y < start.y || (cell.y == start.y && cell.x < start.x))
                    start = cell;
            }

            int width = 1;
            while (set.Contains(new Vector2Int(start.x + width, start.y))) width++;

            int height = 1;
            while (true)
            {
                bool fullRow = true;
                int row = start.y + height;
                for (int x = start.x; x < start.x + width; x++)
                {
                    if (set.Contains(new Vector2Int(x, row))) continue;
                    fullRow = false;
                    break;
                }
                if (!fullRow) break;
                height++;
            }

            rects.Add(new RectInt(start.x, start.y, width, height));
            for (int y = start.y; y < start.y + height; y++)
                for (int x = start.x; x < start.x + width; x++)
                    set.Remove(new Vector2Int(x, y));
        }

        return rects;
    }

    /// <summary>
    /// 把 TuningLab.BuildRoom 里的"中心 + 尺寸"矩形写成格。
    /// 必须完整对齐 1 格，否则返回 false（4 倍场地就是故意不对齐的，不能拿来当关卡基准）。
    /// </summary>
    public bool AddAlignedRect(Vector2 center, Vector2 size, float tolerance = 0.001f)
    {
        float xMin = center.x - size.x * 0.5f;
        float xMax = center.x + size.x * 0.5f;
        float yMin = center.y - size.y * 0.5f;
        float yMax = center.y + size.y * 0.5f;

        int x0 = Mathf.RoundToInt(xMin);
        int x1 = Mathf.RoundToInt(xMax);
        int y0 = Mathf.RoundToInt(yMin);
        int y1 = Mathf.RoundToInt(yMax);

        if (x1 <= x0 || y1 <= y0) return false;
        if (Mathf.Abs(xMin - x0) > tolerance || Mathf.Abs(xMax - x1) > tolerance) return false;
        if (Mathf.Abs(yMin - y0) > tolerance || Mathf.Abs(yMax - y1) > tolerance) return false;

        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                AddCell(new Vector2Int(x, y));
        return true;
    }

    /// <summary>地形和绳子的格子范围；没有内容时退回相机范围。给"跳到关卡"用。</summary>
    public RectInt ContentBounds()
    {
        bool any = false;
        int x0 = 0, y0 = 0, x1 = 0, y1 = 0;

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                int cx0 = cell.x, cx1 = cell.x + 1;
                int cy0 = cell.y, cy1 = cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                int cx0 = Mathf.FloorToInt(rope.center.x - 0.5f);
                int cx1 = Mathf.CeilToInt(rope.center.x + 0.5f);
                int cy0 = Mathf.FloorToInt(rope.Bottom);
                int cy1 = Mathf.CeilToInt(rope.Top);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        return any ? new RectInt(x0, y0, x1 - x0, y1 - y0) : bounds;
    }

    static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int y = a.y.CompareTo(b.y);
        return y != 0 ? y : a.x.CompareTo(b.x);
    }
}

[System.Serializable]
public struct LabRopeSpec
{
    /// <summary>绳中心（世界坐标）。宽 6 像素、每格 16 像素，所以 x 是连续值。</summary>
    public Vector2 center;
    /// <summary>绳长（格）。</summary>
    public int cells;
    public int chainId;

    public float Bottom => center.y - cells * 0.5f;
    public float Top => center.y + cells * 0.5f;

    public LabRopeSpec(Vector2 center, int cells, int chainId = 0)
    {
        this.center = center;
        this.cells = cells;
        this.chainId = chainId;
    }
}

/// <summary>
/// 连续关卡分区。bounds 就是这一关的相机 clamp 范围：
/// B 进入哪个分区的 bounds，相机就用哪个分区的大小。移动分区时可以一起移动里面的物件。
/// </summary>
[System.Serializable]
public sealed class LabLevelPartition
{
    public string name = "Level";
    public RectInt bounds = new RectInt(0, 0, 16, 12);
}
