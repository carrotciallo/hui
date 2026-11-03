using UnityEngine;

/// <summary>1×3 过关门：任意持有钥匙的玩家进入检测区后消耗一把钥匙并打开。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabDoor : MonoBehaviour
{
    public static readonly Color DefaultClosedColor = new Color(0.25f, 0.75f, 1f);

    public Color closedColor = DefaultClosedColor;
    public Color openColor = new Color(0.25f, 1f, 0.45f);
    public bool IsOpen { get; private set; }

    BoxCollider2D body;
    BoxCollider2D detector;
    SpriteRenderer renderer;
    static Sprite cachedSprite;

    void Awake() => ApplyShape();

    /// <summary>
    /// 把尺寸/颜色/贴图应用到本体碰撞、检测触发器和渲染器。关卡的编辑器预览也会直接调它，
    /// 所以不依赖 Awake；重复调用不会叠出第二个触发器，也不会每次新建贴图。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        if (body != null)
        {
            body.size = new Vector2(1f, 3f);
            body.enabled = !IsOpen;
        }

        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            if (cachedSprite == null) cachedSprite = MakeSprite();
            renderer.sprite = cachedSprite;
            renderer.color = IsOpen ? openColor : closedColor;
            renderer.sortingOrder = 1;
        }

        if (detector == null)
        {
            detector = gameObject.AddComponent<BoxCollider2D>();
            detector.isTrigger = true;
            detector.size = new Vector2(1.2f, 3f);
        }
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
        if (body != null) body.enabled = false;
        if (renderer != null) renderer.color = openColor;
        return true;
    }

    public void SetClosedColor(Color color)
    {
        closedColor = color;
        if (IsOpen) return;
        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = closedColor;
    }

    public void ResetDoor()
    {
        IsOpen = false;
        if (body == null) body = GetComponent<BoxCollider2D>();
        if (body != null) body.enabled = true;
        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = closedColor;
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
