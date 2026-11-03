using UnityEngine;

/// <summary>1×3 过关门：任意持有钥匙的玩家进入检测区后消耗一把钥匙并打开。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabDoor : MonoBehaviour
{
    public Color closedColor = new Color(0.25f, 0.75f, 1f);
    public Color openColor = new Color(0.25f, 1f, 0.45f);
    public bool IsOpen { get; private set; }

    BoxCollider2D body;
    SpriteRenderer renderer;

    void Awake()
    {
        body = GetComponent<BoxCollider2D>();
        body.size = new Vector2(1f, 3f);

        renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = MakeSprite();
        renderer.color = closedColor;
        renderer.sortingOrder = 1;

        var detector = gameObject.AddComponent<BoxCollider2D>();
        detector.isTrigger = true;
        detector.size = new Vector2(1.2f, 3f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player != null) TryOpen(player);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player != null) TryOpen(player);
    }

    public bool TryOpen(LabPlayer player)
    {
        if (IsOpen || player == null || !player.ConsumeKey()) return false;
        IsOpen = true;
        body.enabled = false;
        renderer.color = openColor;
        return true;
    }

    public void SetClosedColor(Color color)
    {
        closedColor = color;
        if (!IsOpen && renderer != null) renderer.color = color;
    }

    public void ResetDoor()
    {
        IsOpen = false;
        body.enabled = true;
        renderer.color = closedColor;
    }

    static Sprite MakeSprite()
    {
        const int width = TuningLab.CellPixels;
        const int height = TuningLab.CellPixels * 3;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
    }
}
