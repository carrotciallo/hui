using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格隐藏方块：无实体、外形与墙一致，用来盖住后面的物件。
///
/// 任一玩家（不分形态）的 BodyBounds 完全落入本组格子并集时，整组揭示为半透明；
/// 揭示不可逆，且不订阅 MapReset——地图重置/传送后保持半透明，场景重载才恢复。
/// 各组独立求值：同一格可以属于多个组，嵌套组各自判定自己的并集。
/// 编辑器预览不登记到 <see cref="All"/>，也不做判定。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class LabHiddenBlock : MonoBehaviour
{
    /// <summary>盖住实心格（0）、门/冰/岩浆/刺（1）、钥匙（2）、绳（5），但低于玩家（20）。</summary>
    public const int SortingOrder = 6;

    /// <summary>Play 时生成的所有隐藏方块，按 groupId 编组查询。</summary>
    public static readonly List<LabHiddenBlock> All = new List<LabHiddenBlock>();

    public Vector2Int cell;
    public int groupId;
    public Color color = TuningLab.SolidColor;

    public bool Revealed { get; private set; }

    SpriteRenderer spriteRenderer;
    TuningLab lab;
    HashSet<Vector2Int> groupCells;

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (!Application.isPlaying) return;
        lab = FindAnyObjectByType<TuningLab>();
        RebuildGroupCells();
    }

    /// <summary>
    /// 把格子和组号应用到视觉。编辑器预览也直接调它，不依赖 Awake。
    /// 隐藏方块逐格生成、不进地形合并矩形，否则没法按组改透明度。
    /// </summary>
    public void ApplyShape()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = TuningLab.WhiteSprite;
        spriteRenderer.color = CurrentColor();
        spriteRenderer.sortingOrder = SortingOrder;
    }

    void Update()
    {
        if (Revealed || lab == null || groupCells == null) return;
        if (CoversFully(lab.PlayerA) || CoversFully(lab.PlayerB)) RevealGroup();
    }

    /// <summary>
    /// 收集本组全部格子做并集；同组方块在 Build 里一次生成，之后组内容不变。
    /// 单格组也是合法判定对象。
    /// </summary>
    void RebuildGroupCells()
    {
        groupCells = new HashSet<Vector2Int> { cell };
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != this && block != null && block.groupId == groupId)
                groupCells.Add(block.cell);
        }
    }

    /// <summary>
    /// 玩家包围盒是否整体落在本组格子并集内（所有覆盖到的格都属于本组）。
    /// 贴地时物理会让包围盒底边扎进地面一条缝，按格边界收 0.01 的容差，
    /// 避免"贴着地走过假墙"因浮点毛刺判成越界。
    /// </summary>
    bool CoversFully(LabPlayer player)
    {
        if (player == null || !player.IsAlive || player.Suspended) return false;
        const float edge = 0.01f;
        Bounds bounds = player.BodyBounds;
        int x0 = Mathf.FloorToInt(bounds.min.x + edge);
        int x1 = Mathf.CeilToInt(bounds.max.x - edge) - 1;
        int y0 = Mathf.FloorToInt(bounds.min.y + edge);
        int y1 = Mathf.CeilToInt(bounds.max.y - edge) - 1;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (!groupCells.Contains(new Vector2Int(x, y))) return false;
        return true;
    }

    /// <summary>整组揭示：只改透明度，不关渲染、不销毁。0 也是普通组。</summary>
    public void RevealGroup()
    {
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != null && block.groupId == groupId) block.RevealSelf();
        }
    }

    void RevealSelf()
    {
        if (Revealed) return;
        Revealed = true;
        ApplyShape();
    }

    Color CurrentColor()
    {
        if (!Revealed) return color;
        float alpha = lab != null ? Mathf.Clamp01(lab.hiddenRevealedAlpha) : 0.5f;
        return new Color(color.r, color.g, color.b, alpha);
    }

    /// <summary>当前 Play 里同一组的隐藏方块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabHiddenBlock> GroupOf(int groupId)
    {
        var list = new List<LabHiddenBlock>();
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != null && block.groupId == groupId) list.Add(block);
        }
        return list;
    }
}
