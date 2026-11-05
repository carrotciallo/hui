using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3×3 重生点：任一玩家从区域外进入触发区时激活，并记录两人的边框形态。
/// 活着进点的人保留当前形态，已死亡的人以全新状态复活；两人都死亡时按记录形态一起重生。
/// 已经在区域里、贴着边缘停留都不会重复触发；全员离开后再次进入才再次触发。
/// 被传送、复活或出生直接放进区域不算“进入”。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabRespawnPoint : MonoBehaviour
{
    public Color inactiveColor = new Color(0.35f, 0.55f, 0.95f);
    public Color activeColor = new Color(0.35f, 1f, 0.65f);
    public bool IsActive { get; private set; }

    TuningLab lab;
    BoxCollider2D trigger;
    SpriteRenderer renderer;
    readonly HashSet<LabPlayer> touching = new HashSet<LabPlayer>();
    bool contactResolved;

    // 形态记录：激活时把两人当时的四条边存下来，两人都死亡后按它复活。
    bool hasFormRecord;
    LabPlayer.FormState formA;
    LabPlayer.FormState formB;

    // 每名玩家上一次采样时的状态。只有“活着、不在膛里”的身体从区域外跨进来才算进入；
    // TeleportCount 变了说明这次重叠是传送/复活放进去的，也不能算进入。
    LabPlayer trackedA;
    LabPlayer trackedB;
    bool activeA;
    bool activeB;
    bool insideA;
    bool insideB;
    int teleportsA;
    int teleportsB;

    static Sprite cachedSprite;

    void Awake() => ApplyShape();

    /// <summary>
    /// 把 3×3 触发区和贴图应用到碰撞箱和渲染器。关卡的编辑器预览也会直接调它，
    /// 所以不依赖 Awake；重复调用复用同一张贴图。
    /// </summary>
    public void ApplyShape()
    {
        if (trigger == null) trigger = GetComponent<BoxCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.size = new Vector2(3f, 3f);
        }

        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            if (cachedSprite == null) cachedSprite = MakeSprite();
            renderer.sprite = cachedSprite;
            renderer.color = IsActive ? activeColor : inactiveColor;
            renderer.sortingOrder = -1;
        }
    }

    void Start()
    {
        lab = GetComponentInParent<TuningLab>();
        if (lab == null) lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.RegisterRespawnPoint(this);
        SeedTracks();
    }

    void FixedUpdate()
    {
        if (lab == null) return;

        bool entered = false;
        entered |= Sample(lab.PlayerA, ref trackedA, ref activeA, ref insideA, ref teleportsA);
        entered |= Sample(lab.PlayerB, ref trackedB, ref activeB, ref insideB, ref teleportsB);

        if (entered && !contactResolved)
        {
            contactResolved = true;
            lab.ActivateRespawnPoint(this);
            RespawnOnEnter(lab);
        }

        // 被销毁的玩家不会再来采样，清掉之后才能判定“全员离开”。
        touching.RemoveWhere(player => player == null);

        // 全员离开（或都死亡/进膛）之后才重新允许触发。
        if (touching.Count == 0) contactResolved = false;
    }

    /// <summary>Start 时先记录一次当前位置：出生/放置就在区域里的玩家不算“进入”。</summary>
    void SeedTracks()
    {
        if (lab == null) return;
        Seed(lab.PlayerA, ref trackedA, ref activeA, ref insideA, ref teleportsA);
        Seed(lab.PlayerB, ref trackedB, ref activeB, ref insideB, ref teleportsB);
    }

    void Seed(LabPlayer player, ref LabPlayer tracked, ref bool active, ref bool inside, ref int teleports)
    {
        if (tracked != null && tracked != player) touching.Remove(tracked);
        tracked = player;
        active = IsTrackable(player);
        inside = active && Overlaps(player);
        teleports = player != null ? player.TeleportCount : 0;
        SetTouching(player, inside);
    }

    /// <summary>采样一名玩家；返回 true 表示这一步发生了真正的“从区域外进入”。</summary>
    bool Sample(LabPlayer player, ref LabPlayer tracked, ref bool active, ref bool inside, ref int teleports)
    {
        if (player != tracked)
        {
            // 玩家引用换了（例如后生成的玩家）：重新取初值，不把换人当成进入。
            Seed(player, ref tracked, ref active, ref inside, ref teleports);
            return false;
        }

        bool wasActive = active;
        bool wasInside = inside;
        int wasTeleports = teleports;

        active = IsTrackable(player);
        inside = active && Overlaps(player);
        teleports = player != null ? player.TeleportCount : 0;
        SetTouching(player, inside);

        bool teleported = teleports != wasTeleports;
        return active && wasActive && inside && !wasInside && !teleported;
    }

    static bool IsTrackable(LabPlayer player)
        => player != null && player.IsAlive && !player.Suspended;

    bool Overlaps(LabPlayer player)
    {
        if (trigger == null) trigger = GetComponent<BoxCollider2D>();
        return trigger != null && player.BodyBounds.Intersects(trigger.bounds);
    }

    void SetTouching(LabPlayer player, bool inside)
    {
        if (player == null) return;
        if (inside) touching.Add(player);
        else touching.Remove(player);
    }

    internal void SetActive(bool active)
    {
        IsActive = active;
        if (renderer != null) renderer.color = active ? activeColor : inactiveColor;
    }

    public bool IsTouching(LabPlayer player) => player != null && touching.Contains(player);

    /// <summary>
    /// 玩家进入触发区：先记录两人当前形态，再传送两人、清钥匙、重置地图。
    /// 活着的人保留当前形态；已经死亡的人以全新状态复活（同时按全新状态记录）。
    /// </summary>
    public void RespawnOnEnter(TuningLab owner)
    {
        if (owner == null) return;
        contactResolved = true;
        RecordForms(owner);
        owner.ResetMap();
        PlacePlayer(owner.PlayerA, (Vector2)transform.position + Vector2.left);
        PlacePlayer(owner.PlayerB, (Vector2)transform.position + Vector2.right);
    }

    /// <summary>
    /// 两人都死亡时结算：按最后一次激活时记录的状态复活；这条点从没被激活过则用全新状态。
    /// </summary>
    public void RespawnRecordedPlayers(TuningLab owner)
    {
        if (owner == null) return;
        contactResolved = true;
        owner.ResetMap();
        ReviveWithRecord(owner.PlayerA, (Vector2)transform.position + Vector2.left, formA);
        ReviveWithRecord(owner.PlayerB, (Vector2)transform.position + Vector2.right, formB);
    }

    void RecordForms(TuningLab owner)
    {
        formA = CaptureForm(owner.PlayerA);
        formB = CaptureForm(owner.PlayerB);
        hasFormRecord = true;
    }

    static LabPlayer.FormState CaptureForm(LabPlayer player)
        => player != null && player.IsAlive ? player.CaptureForm() : LabPlayer.FormState.Normal;

    /// <summary>进点时活着的人保留形态；死的人用 ReviveAt 的全新状态从点位上站起来。</summary>
    static void PlacePlayer(LabPlayer player, Vector2 position)
    {
        if (player == null) return;
        player.ClearKeys();
        if (player.IsAlive) player.TeleportPreservingState(position, Vector2.zero);
        else player.ReviveAt(position);
    }

    void ReviveWithRecord(LabPlayer player, Vector2 position, LabPlayer.FormState form)
    {
        if (player == null) return;
        player.ClearKeys();
        player.ReviveAt(position);
        if (hasFormRecord) player.ApplyForm(form);
    }

    public void ResetPoint()
    {
        IsActive = false;
        contactResolved = false;
        touching.Clear();
        trackedA = null;
        trackedB = null;
        activeA = false;
        activeB = false;
        insideA = false;
        insideB = false;
        hasFormRecord = false;
        formA = LabPlayer.FormState.Normal;
        formB = LabPlayer.FormState.Normal;
        if (renderer != null) renderer.color = inactiveColor;
    }

    static Sprite MakeSprite()
    {
        const int size = TuningLab.CellPixels * 3;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
    }
}
