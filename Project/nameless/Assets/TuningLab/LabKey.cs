using UnityEngine;

/// <summary>可被玩家装入身体的钥匙。钥匙本体被收集后消失，钥匙数量记在玩家身上。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabKey : MonoBehaviour
{
    public Color color = new Color(1f, 0.85f, 0.1f);
    public bool Collected { get; private set; }

    static Sprite cachedSprite;

    void Awake() => ApplyShape();

    /// <summary>
    /// 把尺寸/颜色/贴图应用到碰撞箱和渲染器。关卡的编辑器预览也会直接调它，
    /// 所以不依赖 Awake；重复调用复用同一张贴图，不会每次新建一张。
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
        if (Collected || player == null || !player.TryCollectKey(this)) return false;
        Collected = true;
        Destroy(gameObject);
        return true;
    }

    static Sprite MakeSprite()
    {
        const int size = TuningLab.CellPixels;
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
