using UnityEngine;

/// <summary>3×3 检查点：持钥匙的玩家进入后记录两名玩家的位置。</summary>
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
        lab = GetComponentInParent<TuningLab>();
        if (lab == null) lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.RegisterRespawnPoint(this);
    }

    void OnTriggerEnter2D(Collider2D other) => TryActivate(other);
    void OnTriggerStay2D(Collider2D other) => TryActivate(other);

    void TryActivate(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (IsActive || player == null || lab == null || !player.ConsumeKey()) return;
        savedA = lab.PlayerA != null ? lab.PlayerA.transform.position : transform.position;
        savedB = lab.PlayerB != null ? lab.PlayerB.transform.position : transform.position;
        IsActive = true;
        renderer.color = activeColor;
    }

    public void RespawnPlayers(TuningLab owner)
    {
        owner.ResetMap();
        if (owner.PlayerA != null)
        {
            owner.PlayerA.ClearKeys();
            owner.PlayerA.Teleport(savedA);
        }
        if (owner.PlayerB != null)
        {
            owner.PlayerB.ClearKeys();
            owner.PlayerB.Teleport(savedB);
        }
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
