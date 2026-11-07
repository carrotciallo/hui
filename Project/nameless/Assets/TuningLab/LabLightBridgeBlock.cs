using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 光桥方块：电梯形（‖ / =）幻影沿平行线方向延伸出来的临时薄片（Issue #76）。
///
/// 形状按设计：一格宽、只有普通方块三分之一高，上缘与相邻方格的上缘齐平，
/// 读起来是"贴着格子边缘的一条薄板"，不是一整块砖。美术未定，先用半透明白片顶替；
/// 透明度比幻影本体（0.35）高一点，看得出是同一条光桥。
///
/// 碰撞箱和显示一致。A（幻影来源）永远能穿过；B 默认被挡住，只有"出生时就压在 B 身上"
/// 的那些方块对 B 也让路，避免凭空把人夹进墙里。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabLightBridgeBlock : MonoBehaviour
{
    /// <summary>薄片占一格的高度比例：设计给的是"三分之一左右"。</summary>
    public const float HeightRatio = 1f / 3f;

    /// <summary>半透明白片，比幻影本体更实一点。</summary>
    public static readonly Color DefaultColor = new Color(0.86f, 0.95f, 1f, 0.55f);

    /// <summary>Play 时的全部光桥方块。</summary>
    public static readonly List<LabLightBridgeBlock> All = new List<LabLightBridgeBlock>();

    public Vector2Int Cell { get; private set; }
    public LabPhantom Owner { get; private set; }
    /// <summary>竖直光桥：薄墙竖着贴格子的左边或右边。</summary>
    public bool Vertical { get; private set; }
    /// <summary>贴哪条边：+1 上缘（横向）/ 右缘（竖直），-1 下缘（横向）/ 左缘（竖直）。</summary>
    public int SlabSide { get; private set; }
    public bool PassesForbiddenPlayer { get; private set; }

    BoxCollider2D body;

    void Awake() => body = GetComponent<BoxCollider2D>();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>生成一个光桥方块。</summary>
    public static LabLightBridgeBlock Spawn(
        LabPhantom owner, Vector2Int cell, bool vertical, int slabSide, bool passForbidden,
        Transform parent, PhysicsMaterial2D material)
    {
        var go = new GameObject("LightBridgeBlock");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        var bc = go.AddComponent<BoxCollider2D>();
        // 竖直：薄墙竖着，宽 1/3、高一格，贴左/右缘。
        // 横向：薄板横着，宽一格、高 1/3，贴上/下缘。
        float ratio = HeightRatio;
        bc.size = vertical
            ? new Vector2(ratio, 1f)
            : new Vector2(1f, ratio);
        // offset 让薄片外缘正好压在格边界上；上/右缘齐平取正，下/左缘取负。
        float edgeOffset = (1f - ratio) * 0.5f;
        bc.offset = vertical
            ? new Vector2(edgeOffset * slabSide, 0f)
            : new Vector2(0f, edgeOffset * slabSide);
        bc.isTrigger = false;
        if (material != null) bc.sharedMaterial = material;

        // 精灵挂在子物体上：缩放子物体不会连带缩放根节点的碰撞箱。
        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(go.transform, false);
        // 子物体位置要跟碰撞箱 offset 对齐，否则显示和碰撞对不上。
        spriteGo.transform.localPosition = vertical
            ? new Vector3(edgeOffset * slabSide, 0f, 0f)
            : new Vector3(0f, edgeOffset * slabSide, 0f);
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.color = DefaultColor;
        sr.sortingOrder = 1;

        return go.AddComponent<LabLightBridgeBlock>()
            .Configure(owner, cell, vertical, slabSide, passForbidden, spriteGo.transform);
    }

    LabLightBridgeBlock Configure(
        LabPhantom owner, Vector2Int cell, bool vertical, int slabSide, bool passForbidden,
        Transform spriteTransform)
    {
        Owner = owner;
        Cell = cell;
        Vertical = vertical;
        SlabSide = slabSide;
        PassesForbiddenPlayer = passForbidden;

        var sr = spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : null;
        if (sr != null && sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x > 0.0001f && spriteSize.y > 0.0001f)
            {
                sr.transform.localScale = new Vector3(
                    (vertical ? HeightRatio : 1f) / spriteSize.x,
                    (vertical ? 1f : HeightRatio) / spriteSize.y,
                    1f);
            }
        }

        return this;
    }

    /// <summary>让指定玩家穿过这个方块。</summary>
    public void IgnorePlayer(LabPlayer player)
    {
        if (player == null || body == null) return;
        var other = player.GetComponent<Collider2D>();
        if (other == null) return;
        Physics2D.IgnoreCollision(body, other, true);
    }

    /// <summary>这一格还空着吗？两条光桥不能叠在同一格。</summary>
    public static bool IsFree(Vector2Int cell)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != null && block.Cell == cell) return false;
        }
        return true;
    }

    void OnDestroy() => All.Remove(this);
}
