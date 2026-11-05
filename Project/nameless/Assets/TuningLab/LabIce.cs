using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格冰块：1×1 的蓝色实体方块，可以站立和滑行。
///
/// 破坏有两条路，都由玩家侧结算：
///   组内任意一块沿接触法线被 v2 撞击 → 整组一起碎（优先于红热）；
///   红热边（Hot2）的非承重接触 → 只融这一块，边回普通；
///   冰格落在玩家脚下 footprint 内的接触不融，避免融掉脚下承重块（含不同分组交界）。
///   在冰面上加速到 v2/v3 属于滑行，不算撞击，不会碎冰。
///
/// 关卡生成时冰和地形共用一个 CompositeCollider2D，相邻冰块、冰块和地形之间没有
/// 可碰撞的内侧竖面，所以冰面滑行不会卡脚。
/// 破坏时不销毁对象，只关碰撞和渲染，这样静态组表、地图重置和调试读数都还能找到它。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabIce : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(0.42f, 0.75f, 1f);

    /// <summary>Play 时生成的所有冰块，按 groupId 编组查询。</summary>
    public static readonly List<LabIce> All = new List<LabIce>();

    public Vector2Int cell;
    public int groupId;
    public Color color = DefaultColor;

    public bool Broken { get; private set; }

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;
    CompositeCollider2D composite;

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// 把格子和组号应用到碰撞箱和视觉。编辑器预览也直接调它，不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        composite = GetComponentInParent<CompositeCollider2D>();
        if (body != null)
        {
            body.isTrigger = false;
            body.size = Vector2.one;
            body.offset = Vector2.zero;
            // 关卡生成时冰和地形共用一个 CompositeCollider2D，把相邻 1×1 方块之间的
            // 内侧竖面焊掉；测试或手摆的独立方块没有父级 composite，仍用自身碰撞箱。
            body.compositeOperation = composite != null
                ? Collider2D.CompositeOperation.Merge
                : Collider2D.CompositeOperation.None;
            body.enabled = !Broken;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = TuningLab.WhiteSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
            spriteRenderer.enabled = !Broken;
        }
    }

    /// <summary>只碎这一块；所在组的其它冰块不动。</summary>
    public void BreakOne()
    {
        BreakSelf();
        RebuildComposite();
    }

    /// <summary>同一 groupId 的所有冰块一起碎。0 也是普通组。</summary>
    public void BreakGroup()
    {
        var members = GroupOf(groupId);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].BreakSelf();

        // 整组关完碰撞后只重建一次合并几何。
        if (members.Count > 0 && members[0] != null) members[0].RebuildComposite();
    }

    void BreakSelf()
    {
        if (Broken) return;
        Broken = true;
        if (body != null) body.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    /// <summary>地图重置 / 复活时恢复：碰撞和渲染都回来。</summary>
    public void ResetIce(bool rebuildCollision = true)
    {
        Broken = false;
        ApplyShape();
        if (rebuildCollision) RebuildComposite();
    }

    /// <summary>整组恢复后只重建一次合并几何。</summary>
    public void RebuildCollision() => RebuildComposite();

    void RebuildComposite()
    {
        if (composite != null) composite.GenerateGeometry();
    }

    /// <summary>这个点落在哪一块未破坏的冰上；合并地形需要靠接触点找回是哪一格。</summary>
    public static LabIce AtCell(Vector2 point)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        for (int i = 0; i < All.Count; i++)
        {
            var ice = All[i];
            if (ice != null && !ice.Broken && ice.cell == cell) return ice;
        }
        return null;
    }

    /// <summary>当前 Play 里同一组的冰块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabIce> GroupOf(int groupId)
    {
        var list = new List<LabIce>();
        for (int i = 0; i < All.Count; i++)
        {
            var ice = All[i];
            if (ice != null && ice.groupId == groupId) list.Add(ice);
        }
        return list;
    }
}
