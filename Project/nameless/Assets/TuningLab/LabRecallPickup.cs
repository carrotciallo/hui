using UnityEngine;

/// <summary>
/// 召回道具：1×1 的触发区，任一玩家拾取后给全队永久解锁召回。
/// 一次性道具 —— 拾取后销毁，地图重置不会把它放回来（能力本身留在 TuningLab 上）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabRecallPickup : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(0.2f, 0.9f, 0.8f);

    public Color color = DefaultColor;
    public bool Collected { get; private set; }

    static Sprite cachedSprite;

    void Awake() => ApplyShape();

    /// <summary>
    /// 把尺寸/颜色/贴图应用到碰撞箱和渲染器。编辑器预览也直接调它，所以不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        var body = GetComponent<BoxCollider2D>();
        if (body != null)
        {
            body.isTrigger = true;
            body.size = Vector2.one;
        }

        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            if (cachedSprite == null) cachedSprite = MakeSprite();
            sprite.sprite = cachedSprite;
            sprite.color = color;
            sprite.sortingOrder = 2;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player != null) TryCollect(player);
    }

    public bool TryCollect(LabPlayer player)
    {
        if (Collected || player == null) return false;

        var lab = player.lab != null ? player.lab : FindAnyObjectByType<TuningLab>();
        if (lab == null) return false;

        Collected = true;
        lab.UnlockRecall();
        Destroy(gameObject);
        return true;
    }

    /// <summary>青色菱形，和圆钥匙一眼可分。</summary>
    static Sprite MakeSprite()
    {
        const int size = TuningLab.CellPixels;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        int center = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - center) + Mathf.Abs(y - center) <= center;
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
    }
}
