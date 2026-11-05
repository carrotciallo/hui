using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 横绳：16×4 像素的平台。零级/一级挡住，二级允许向下通过，
/// 三级及以上向上弹回并提升一个速度等级。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabHorizontalRope : MonoBehaviour
{
    public const int CellPixels = 16;
    public const int ThicknessPixels = 4;
    const string VisualName = "Visual";

    public int cells = 4;
    public Color color = new Color(0.55f, 0.38f, 0.22f);
    public float passWindow = 0.22f;

    public struct RopeContact
    {
        public LabHorizontalRope rope;
        public LabPlayer player;
        public int tier;
        public bool passed;
        public bool bounced;
    }

    public event System.Action<RopeContact> Contacted;

    BoxCollider2D body;
    TuningLab lab;
    readonly List<Collider2D> passThrough = new List<Collider2D>();
    readonly List<float> passUntil = new List<float>();

    void Awake() => body = GetComponent<BoxCollider2D>();

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
        ApplyShape();
    }

    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        int length = Mathf.Max(1, cells);
        // 长度只进入碰撞箱尺寸。根节点不能再按长度缩放，否则 Collider 会被放大两次。
        transform.localScale = Vector3.one;
        body.size = new Vector2(length, ThicknessPixels / (float)CellPixels);
        body.offset = Vector2.zero;
        BuildVisual(length);
    }

    void Update()
    {
        for (int i = passThrough.Count - 1; i >= 0; i--)
        {
            if (Time.time < passUntil[i]) continue;
            if (passThrough[i] != null) Physics2D.IgnoreCollision(passThrough[i], body, false);
            passThrough.RemoveAt(i);
            passUntil.RemoveAt(i);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.TryGetComponent(out LabPlayer player)) return;
        Resolve(player);
    }

    void Resolve(LabPlayer player)
    {
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null) return;

        int tier = lab != null
            ? lab.TierOfSpeed(Mathf.Abs(Vector2.Dot(player.PreImpactVelocity, Vector2.down)))
            : player.VerticalTier;
        if (tier < 2)
        {
            Raise(player, tier, false, false);
            return;
        }

        if (tier == 2)
        {
            Physics2D.IgnoreCollision(playerCollider, body, true);
            passThrough.Add(playerCollider);
            passUntil.Add(Time.time + passWindow);
            Raise(player, tier, true, false);
            return;
        }

        player.ApplyBounce(Vector2.up, BounceSpeed(player));
        LabAudio.Play(LabAudio.Cue.RopeBounce);
        Raise(player, tier, false, true);
    }

    float BounceSpeed(LabPlayer player)
    {
        if (lab == null) return 30f;
        return lab.RopeBounceSpeed(Vector2.Dot(player.PreImpactVelocity, Vector2.down));
    }

    void Raise(LabPlayer player, int tier, bool passed, bool bounced)
    {
        Contacted?.Invoke(new RopeContact
        {
            rope = this,
            player = player,
            tier = tier,
            passed = passed,
            bounced = bounced
        });
    }

    void BuildVisual(int length)
    {
        // 兼容旧运行时对象：旧实现把 SpriteRenderer 放在根节点上。
        var legacyRenderer = GetComponent<SpriteRenderer>();
        if (legacyRenderer != null)
        {
            if (Application.isPlaying) Destroy(legacyRenderer);
            else DestroyImmediate(legacyRenderer);
        }

        Transform visual = transform.Find(VisualName);
        if (visual == null)
        {
            visual = new GameObject(VisualName).transform;
            visual.SetParent(transform, false);
        }

        var renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = RopeSprite();
        renderer.color = color;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(length, 1f, 1f);
        renderer.sortingOrder = 5;
    }

    static Sprite ropeSprite;

    static Sprite RopeSprite()
    {
        if (ropeSprite != null) return ropeSprite;
        var texture = new Texture2D(CellPixels, ThicknessPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[CellPixels * ThicknessPixels];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        ropeSprite = Sprite.Create(texture, new Rect(0f, 0f, CellPixels, ThicknessPixels),
            new Vector2(0.5f, 0.5f), CellPixels);
        return ropeSprite;
    }
}
