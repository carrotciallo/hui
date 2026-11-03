using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>双人传送点：两人同时进入后由冲刺确认，传送保留速度和形态。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabTeleportPoint : MonoBehaviour
{
    public LabTeleportPoint destination;
    public Color inactiveColor = new Color(0.65f, 0.35f, 1f);
    public Color activeColor = new Color(1f, 0.7f, 0.25f);

    BoxCollider2D trigger;
    SpriteRenderer renderer;
    TuningLab lab;
    bool aInside;
    bool bInside;
    bool activating;

    void Awake()
    {
        trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(3f, 3f);

        renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = MakeSprite();
        renderer.color = inactiveColor;
        renderer.sortingOrder = -1;
    }

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
    }

    void OnTriggerEnter2D(Collider2D other) => SetInside(other, true);
    void OnTriggerExit2D(Collider2D other) => SetInside(other, false);

    void SetInside(Collider2D other, bool inside)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player == null) return;
        if (player.scheme == LabPlayer.Scheme.P1) aInside = inside;
        else bInside = inside;
    }

    void Update()
    {
        if (activating || !aInside || !bInside || destination == null || lab == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftShiftKey.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame)
            Activate();
    }

    void FixedUpdate()
    {
        if (!activating || lab == null) return;
        if (lab.PlayerA != null) lab.PlayerA.TeleportPreservingState(lab.PlayerA.transform.position, Vector2.zero);
        if (lab.PlayerB != null) lab.PlayerB.TeleportPreservingState(lab.PlayerB.transform.position, Vector2.zero);
    }

    void Activate()
    {
        activating = true;
        renderer.color = activeColor;
        Vector2 velocityA = lab.PlayerA != null ? lab.PlayerA.Velocity : Vector2.zero;
        Vector2 velocityB = lab.PlayerB != null ? lab.PlayerB.Velocity : Vector2.zero;

        lab.ResetMap();
        if (lab.PlayerA != null)
            lab.PlayerA.TeleportPreservingState(destination.transform.position + Vector3.left * 0.65f, velocityA);
        if (lab.PlayerB != null)
            lab.PlayerB.TeleportPreservingState(destination.transform.position + Vector3.right * 0.65f, velocityB);
        if (lab.PlayerA != null) lab.PlayerA.ClearKeys();
        if (lab.PlayerB != null) lab.PlayerB.ClearKeys();
        activating = false;
        renderer.color = inactiveColor;
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
