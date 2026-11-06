using System.Collections.Generic;
using UnityEngine;

/// <summary>可被玩家装入身体的钥匙。钥匙本体被收集后消失，钥匙数量记在玩家身上。</summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabKey : MonoBehaviour
{
    public Color color = new Color(1f, 0.85f, 0.1f);
    public bool Collected { get; private set; }
    /// <summary>灵体消散时吐出的临时钥匙：捡用规则和普通钥匙一样，但地图重置会把它清掉。</summary>
    public bool IsTemporary { get; private set; }

    static readonly List<LabKey> temporaries = new List<LabKey>();
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
            LabArt.Apply(sprite, "key");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<LabPlayer>();
        if (player != null) TryCollect(player);
    }

    /// <summary>持钥上限让"捡不动"变成常态：站着不动、把手里那把花掉之后，脚下的钥匙要能立刻补捡。</summary>
    void OnTriggerStay2D(Collider2D other)
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

    /// <summary>在给定格心长出一把临时钥匙，登记到重置名单。</summary>
    public static LabKey SpawnTemporary(Vector2 cellCenter)
    {
        var go = new GameObject("TempKey");
        go.transform.position = cellCenter;
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<SpriteRenderer>();
        var key = go.AddComponent<LabKey>();
        key.IsTemporary = true;
        temporaries.Add(key);
        return key;
    }

    /// <summary>地图重置（复活 / 传送）时清掉所有临时钥匙；关卡数据里的普通钥匙由 LabLevel 重新长回来。</summary>
    public static void ClearTemporaries()
    {
        for (int i = 0; i < temporaries.Count; i++)
        {
            LabKey key = temporaries[i];
            if (key == null) continue;
            Destroy(key.gameObject);
        }
        temporaries.Clear();
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
