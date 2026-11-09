using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3×3 重生点：任一玩家从区域外进入触发区时激活；两人都死亡时由 <see cref="TuningLab"/> 强制触发。
/// 触发时重置地图、清钥匙，并把两人放回点中心左右各 1 格：活着进点的人保留当前形态，
/// 两人都死亡后的强制触发一律以普通状态复活。
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
            LabArt.Apply(renderer, "checkpoint");
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

    /// <summary>进点触发：活着的人保留当前形态；已经死亡的人以普通状态复活。</summary>
    public void RespawnOnEnter(TuningLab owner) => RespawnTeam(owner, true);

    /// <summary>两人都死亡时的强制触发：两人都清回普通状态，从点左右两侧站起来。</summary>
    public void ForceRespawn(TuningLab owner) => RespawnTeam(owner, false);

    /// <summary>一次触发只重置一次地图：两人清空钥匙和运动状态，再放回点左右各 1 格。</summary>
    void RespawnTeam(TuningLab owner, bool keepLivingForms)
    {
        if (owner == null) return;
        contactResolved = true;
        owner.ResetMap(transform.position);
        PlacePlayer(owner.PlayerA, (Vector2)transform.position + Vector2.left, keepLivingForms);
        PlacePlayer(owner.PlayerB, (Vector2)transform.position + Vector2.right, keepLivingForms);
    }

    /// <summary>
    /// 把一名玩家放到点位上。死亡的人先以普通状态复活；活着的人按 keepForm 决定
    /// 保留当前形态，还是走 <see cref="LabPlayer.Teleport"/> 把四条边清回普通状态。
    /// </summary>
    static void PlacePlayer(LabPlayer player, Vector2 position, bool keepForm)
    {
        if (player == null) return;
        player.ClearKeys();
        if (!player.IsAlive) player.ReviveAt(position);                   // ReviveAt → Teleport → ResetEdges
        else if (keepForm) player.TeleportPreservingState(position, Vector2.zero);
        else player.Teleport(position);
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
