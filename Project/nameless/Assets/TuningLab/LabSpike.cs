using UnityEngine;

/// <summary>
/// 一格一根地刺：16×6 美术像素，长边紧贴格边，玩家碰到立刻死亡。
/// 碰撞箱 = 视觉外接矩形，一点不让，所以齿之间的凹陷同样致命。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabSpike : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(0.72f, 0.8f, 0.92f);

    /// <summary>齿之间的凹陷：不透明但明显更暗，保证整块 16×6 都是"看得见的刺"。</summary>
    static readonly Color32 Recess = new Color32(82, 90, 104, 255);

    public Vector2Int cell;
    public LabSpikeDir dir = LabSpikeDir.Up;
    public Color color = DefaultColor;

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;

    static Sprite horizontalSprite;     // 16 宽 6 高，齿朝上
    static Sprite verticalSprite;       // 6 宽 16 高，齿朝右

    void Awake() => ApplyShape();

    /// <summary>
    /// 把格子里的朝向换算成碰撞带和贴图。编辑器预览也直接调它，不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        var spec = new LabSpikeSpec(cell, dir);

        if (body == null) body = GetComponent<BoxCollider2D>();
        if (body != null)
        {
            body.isTrigger = true;
            body.size = spec.StripSize;
            body.offset = Vector2.zero;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            bool vertical = spec.dir == LabSpikeDir.Left || spec.dir == LabSpikeDir.Right;
            spriteRenderer.sprite = vertical ? MakeVertical() : MakeHorizontal();
            // 齿尖朝下 = 上下翻，齿尖朝左 = 左右翻；pivot 在中心，翻完仍然贴紧同一条格边。
            spriteRenderer.flipX = spec.dir == LabSpikeDir.Left;
            spriteRenderer.flipY = spec.dir == LabSpikeDir.Down;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
            LabArt.Apply(spriteRenderer, vertical ? "spikes-side" : "spikes-up");
        }
    }

    void OnTriggerEnter2D(Collider2D other) => Touch(other);
    void OnTriggerStay2D(Collider2D other) => Touch(other);

    /// <summary>接触判定。玩家侧的高速扫掠也走这里，两条路共用同一个致死入口。</summary>
    public void Touch(Collider2D other)
    {
        if (other == null) return;
        var player = other.GetComponentInParent<LabPlayer>();
        if (player == null || !HasPositiveOverlap(body, other)) return;
        player.Kill();
    }

    /// <summary>
    /// 触发器会在接触偏移范围内提前回调；零面积接触不算刺中，必须实际重叠。
    /// 玩家和刺都不旋转，因此两个 world bounds 的严格交集就是有效接触面积。
    /// </summary>
    static bool HasPositiveOverlap(Collider2D spike, Collider2D other)
    {
        if (spike == null || other == null) return false;
        Bounds a = spike.bounds;
        Bounds b = other.bounds;
        const float epsilon = 0.0001f;
        float overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float overlapY = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        return overlapX > epsilon && overlapY > epsilon;
    }

    static Sprite MakeHorizontal()
    {
        if (horizontalSprite == null) horizontalSprite = MakeSprite(false);
        return horizontalSprite;
    }

    static Sprite MakeVertical()
    {
        if (verticalSprite == null) verticalSprite = MakeSprite(true);
        return verticalSprite;
    }

    static Sprite MakeSprite(bool vertical)
    {
        const int longSide = TuningLab.CellPixels;                                  // 16
        int shortSide = Mathf.RoundToInt(LabSpikeSpec.Thickness * TuningLab.CellPixels);  // 6
        int width = vertical ? shortSide : longSide;
        int height = vertical ? longSide : shortSide;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        // 齿尖在第 2、6、10、14 根长边像素上，谷底留 2 像素厚的底座。
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int along = vertical ? y : x;
                int across = vertical ? x : y;
                pixels[y * width + x] = across < ToothHeight(along) ? Color.white : Recess;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
    }

    static int ToothHeight(int along)
    {
        // 每 4 像素一个齿：谷底在第 0 列，齿尖在第 2 列，底座厚 2 像素。
        int distance = Mathf.Abs(along % 4 - 2);
        return 6 - 2 * distance;
    }
}
