using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格岩浆块：1×1 的橙红色实体方块，可以站立，走动规则和普通地面相同。
///
/// 和冰块的差别：
///   任意方向的表面接触都会把 A 对应的边强制点到热二，且不会触发"热二遇热"的塌框；
///   红热边接触不会融掉岩浆块；
///   只有沿接触法线的速度到三级（v3）才整组破坏。
/// 破坏时不销毁对象，只关碰撞和渲染，这样静态组表、地图重置和调试读数都还能找到它。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabMagma : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(1f, 0.34f, 0.12f);

    /// <summary>整组破坏要求的速度等级：固定三级。</summary>
    public const int BreakTier = 3;

    /// <summary>Play 时生成的所有岩浆块，按 groupId 编组查询。</summary>
    public static readonly List<LabMagma> All = new List<LabMagma>();

    public Vector2Int cell;
    public int groupId;
    public Color color = DefaultColor;

    public bool Broken { get; private set; }

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;

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
        if (body != null)
        {
            body.isTrigger = false;
            body.size = Vector2.one;
            body.offset = Vector2.zero;
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

    /// <summary>
    /// 同一 groupId 的所有岩浆块一起碎。0 也是普通组。
    /// 岩浆块没有"红热融一块"的路径，所以不提供单块破坏接口。
    /// </summary>
    public void BreakGroup()
    {
        var members = GroupOf(groupId);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].BreakSelf();
    }

    void BreakSelf()
    {
        if (Broken) return;
        Broken = true;
        if (body != null) body.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    /// <summary>地图重置 / 复活时恢复：碰撞和渲染都回来。</summary>
    public void ResetMagma()
    {
        Broken = false;
        ApplyShape();
    }

    /// <summary>当前 Play 里同一组的岩浆块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabMagma> GroupOf(int groupId)
    {
        var list = new List<LabMagma>();
        for (int i = 0; i < All.Count; i++)
        {
            var magma = All[i];
            if (magma != null && magma.groupId == groupId) list.Add(magma);
        }
        return list;
    }
}
