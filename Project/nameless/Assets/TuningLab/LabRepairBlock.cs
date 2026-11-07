using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格修复方块：和 A 一样蓝的 1×1 实体方块，可以站立、可以挡路。
///
/// A 的红热边（热二）或灵体边接触到它的表面，那条边回到普通态。不限使用次数、不会被消耗。
/// 装填态和幽魂态的 A 不响应 —— 门禁只写在 <see cref="LabPlayer.TryRepairEdge"/> 一处。
///
/// 和冰、岩浆一样并进地形的 CompositeCollider2D：各块自带碰撞箱时，接缝的内侧竖面
/// 会卡住横走的玩家。合并后接触回调落在整块合并体上，所以"碰的是哪一格"由玩家侧
/// 用 <see cref="AtCell"/> 按接触点找回。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabRepairBlock : MonoBehaviour
{
    /// <summary>外形：与 A 同色的蓝方块。</summary>
    public static readonly Color DefaultColor = TuningLab.ColorA;

    /// <summary>Play 时生成的所有修复方块，供合并地形按接触点回查身份。</summary>
    public static readonly List<LabRepairBlock> All = new List<LabRepairBlock>();

    public Vector2Int cell;
    public Color color = DefaultColor;

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// 把格子应用到碰撞箱和视觉。编辑器预览也直接调它，不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        var composite = GetComponentInParent<CompositeCollider2D>();
        if (body != null)
        {
            body.isTrigger = false;
            body.size = Vector2.one;
            body.offset = Vector2.zero;
            body.compositeOperation = composite != null
                ? Collider2D.CompositeOperation.Merge
                : Collider2D.CompositeOperation.None;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = TuningLab.WhiteSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
        }
    }

    /// <summary>这个点落在哪一格修复方块上；合并地形要靠接触点找回是哪一格。</summary>
    public static LabRepairBlock AtCell(Vector2 point)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != null && block.cell == cell) return block;
        }
        return null;
    }
}
