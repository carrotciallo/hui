using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3×3 传送点：两人同时进入后标记该点；至少两个已标记点时才打开终点选择。
/// 选择列表由 <see cref="TuningLab"/> 在进入时收集，数据里不保存配对。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabTeleportPoint : MonoBehaviour
{
    public Color inactiveColor = new Color(0.65f, 0.35f, 1f);
    public Color markedColor = new Color(0.45f, 0.9f, 0.8f);
    public Color activeColor = new Color(1f, 0.7f, 0.25f);

    BoxCollider2D trigger;
    SpriteRenderer renderer;
    TuningLab lab;
    bool aInside;
    bool bInside;
    static Sprite cachedSprite;

    public bool IsMarked { get; private set; }

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
            renderer.color = inactiveColor;
            renderer.sortingOrder = -1;
        }
    }

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.RegisterTeleport(this);
    }

    void OnDestroy()
    {
        if (lab != null) lab.UnregisterTeleport(this);
    }

    void Update()
    {
        if (lab == null || lab.TeleportSelecting) return;

        // 死亡/进膛会关掉碰撞箱但不会发 OnTriggerExit，所以每帧按真实包围盒重新判定。
        aInside = IsInside(lab.PlayerA);
        bInside = IsInside(lab.PlayerB);
        if (!aInside || !bInside) return;

        if (!IsMarked) lab.MarkTeleportPoint(this);
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftShiftKey.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame)
            lab.EnterTeleportSelection(this);
    }

    bool IsInside(LabPlayer player)
        => player != null && player.IsAlive && !player.Suspended
           && trigger != null && player.BodyBounds.Intersects(trigger.bounds);

    /// <summary>选择界面里当前选中的终点会亮起来；离开选择界面时由 TuningLab 熄灭。</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (renderer != null)
            renderer.color = highlighted ? activeColor : (IsMarked ? markedColor : inactiveColor);
    }

    public void SetMarked(bool marked)
    {
        IsMarked = marked;
        SetHighlighted(false);
    }

    /// <summary>
    /// 把两名玩家放到这个传送点两侧，保留形态并清零速度。传送会重置地图并吞掉钥匙；
    /// 重生点记录不受影响（地图重置不会清 ActiveRespawnPoint）。
    /// </summary>
    public void TeleportPlayersHere(TuningLab owner)
    {
        if (owner == null) return;
        owner.PlayerA?.ResumeFromTeleportSelection();
        owner.PlayerB?.ResumeFromTeleportSelection();
        owner.ResetMap();

        Vector2 center = transform.position;
        owner.PlayerA?.TeleportPreservingState(center + Vector2.left * 0.65f, Vector2.zero);
        owner.PlayerB?.TeleportPreservingState(center + Vector2.right * 0.65f, Vector2.zero);
        owner.PlayerA?.ClearKeys();
        owner.PlayerB?.ClearKeys();
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
