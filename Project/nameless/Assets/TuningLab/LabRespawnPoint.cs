using System.Collections.Generic;
using UnityEngine;

/// <summary>3×3 重生点：唯一玩家接触时记录两名玩家的位置。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabRespawnPoint : MonoBehaviour
{
    public Color inactiveColor = new Color(0.35f, 0.55f, 0.95f);
    public Color activeColor = new Color(0.35f, 1f, 0.65f);
    public bool IsActive { get; private set; }

    TuningLab lab;
    BoxCollider2D trigger;
    SpriteRenderer renderer;
    Vector2 savedA;
    Vector2 savedB;
    readonly HashSet<LabPlayer> touching = new HashSet<LabPlayer>();
    bool contactResolved;
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
    }

    void OnTriggerEnter2D(Collider2D other) => TryActivate(other);
    void OnTriggerStay2D(Collider2D other) => TryActivate(other);
    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player == null) return;
        touching.Remove(player);
        if (touching.Count == 0)
        {
            contactResolved = false;
        }
    }

    void TryActivate(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player == null || lab == null) return;
        touching.Add(player);
    }

    void FixedUpdate()
    {
        if (lab == null || contactResolved || touching.Count == 0) return;

        var player = lab.PlayerA != null && touching.Contains(lab.PlayerA)
            ? lab.PlayerA
            : lab.PlayerB != null && touching.Contains(lab.PlayerB) ? lab.PlayerB : null;
        if (player == null) return;

        var otherPlayer = player == lab.PlayerA ? lab.PlayerB : lab.PlayerA;
        bool bothTouching = otherPlayer != null && touching.Contains(otherPlayer);
        if (bothTouching)
        {
            contactResolved = true;
            return;
        }

        contactResolved = true;

        if (!player.IsAlive)
        {
            RespawnDeadPlayer(lab, player);
            return;
        }

        if (otherPlayer != null && !otherPlayer.IsAlive)
        {
            RespawnDeadPlayer(lab, otherPlayer);
            return;
        }

        lab.ActivateRespawnPoint(this);
        if (otherPlayer != null)
            otherPlayer.Teleport(transform.position);
        RecordState(lab);
    }

    void RecordState(TuningLab owner)
    {
        savedA = owner.PlayerA != null ? owner.PlayerA.transform.position : transform.position;
        savedB = owner.PlayerB != null ? owner.PlayerB.transform.position : transform.position;
    }

    internal void SetActive(bool active)
    {
        IsActive = active;
        if (renderer != null) renderer.color = active ? activeColor : inactiveColor;
    }

    public bool IsTouching(LabPlayer player) => player != null && touching.Contains(player);

    public void RespawnAfterDeath(TuningLab owner, LabPlayer deadPlayer)
    {
        var survivor = deadPlayer == owner.PlayerA ? owner.PlayerB : owner.PlayerA;
        if (deadPlayer != null && survivor != null && survivor.IsAlive && touching.Contains(survivor))
        {
            deadPlayer.ReviveAt(transform.position);
            RecordState(owner);
            return;
        }

        RespawnPlayers(owner);
    }

    void RespawnDeadPlayer(TuningLab owner, LabPlayer deadPlayer)
    {
        if (deadPlayer == null || deadPlayer.IsAlive) return;
        deadPlayer.ReviveAt(transform.position);
        RecordState(owner);
    }

    public void RespawnPlayers(TuningLab owner)
    {
        owner.ResetMap();
        if (owner.PlayerA != null)
        {
            owner.PlayerA.ClearKeys();
            owner.PlayerA.ReviveAt(savedA);
        }
        if (owner.PlayerB != null)
        {
            owner.PlayerB.ClearKeys();
            owner.PlayerB.ReviveAt(savedB);
        }
        contactResolved = false;
    }

    public void ResetPoint()
    {
        IsActive = false;
        savedA = Vector2.zero;
        savedB = Vector2.zero;
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
