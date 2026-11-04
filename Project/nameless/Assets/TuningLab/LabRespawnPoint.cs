using System.Collections.Generic;
using UnityEngine;

/// <summary>3×3 重生点：任一玩家接触时激活，并让两人以全新状态固定在左右重生。</summary>
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

        if (!touching.Contains(lab.PlayerA) && !touching.Contains(lab.PlayerB)) return;
        lab.ActivateRespawnPoint(this);
        RespawnPlayers(lab);
    }

    internal void SetActive(bool active)
    {
        IsActive = active;
        if (renderer != null) renderer.color = active ? activeColor : inactiveColor;
    }

    public bool IsTouching(LabPlayer player) => player != null && touching.Contains(player);

    public void RespawnAfterDeath(TuningLab owner, LabPlayer _)
    {
        RespawnPlayers(owner);
    }

    public void RespawnPlayers(TuningLab owner)
    {
        if (owner == null) return;
        contactResolved = true;
        owner.ResetMap();
        if (owner.PlayerA != null)
        {
            owner.PlayerA.ClearKeys();
            owner.PlayerA.ReviveAt((Vector2)transform.position + Vector2.left);
        }
        if (owner.PlayerB != null)
        {
            owner.PlayerB.ClearKeys();
            owner.PlayerB.ReviveAt((Vector2)transform.position + Vector2.right);
        }
    }

    public void ResetPoint()
    {
        IsActive = false;
        contactResolved = false;
        touching.Clear();
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
