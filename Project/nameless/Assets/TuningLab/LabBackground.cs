using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 滚动拼接背景（Issue #130）：一张候选图复用成无限大的背景。
/// 相机视野周围铺一圈贴图块，相机移动时回收重摆；相邻块镜像翻转，
/// 原图不需要无缝就能拼接。背景按 scrollFactor 倍速跟随相机，形成视差。
/// 由 LabLevel 在运行时按关卡数据的 background 字段生成，预览不铺。
/// </summary>
public sealed class LabBackground : MonoBehaviour
{
    /// <summary>Resources 下的纹理路径，如 Backgrounds/level1。</summary>
    public string resource;
    /// <summary>一块背景的世界高度（格）；宽度按原图比例。</summary>
    public float tileHeight = 27f;
    /// <summary>背景相对相机的移动倍速：1 = 贴死相机，0 = 固定在世界里。</summary>
    public float scrollFactor = 0.5f;
    public int sortingOrder = -1000;

    Sprite tileSprite;
    float tileWidth;
    Camera cam;
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    void LateUpdate()
    {
        if (tileSprite == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        Refresh(cam.transform.position, cam.orthographicSize, cam.aspect);
    }

    /// <summary>加载纹理并生成贴图块精灵。找不到资源时禁用自己。</summary>
    public void Load()
    {
        tileSprite = null;
        if (string.IsNullOrEmpty(resource)) { enabled = false; return; }

        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null)
        {
            Debug.LogWarning($"[LabBackground] 找不到背景纹理 Resources/{resource}");
            enabled = false;
            return;
        }

        float height = tileHeight > 0f ? tileHeight : 27f;
        tileHeight = height;
        float ppu = texture.height / height;
        tileSprite = Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), ppu);
        tileWidth = texture.width / ppu;
        enabled = true;
    }

    /// <summary>
    /// 把贴图块摆到覆盖相机视野的位置。块 (i, j) 的中心在
    /// camPos*(1-scrollFactor) + (i+0.5, j+0.5)*块尺寸；奇数块镜像翻转，
    /// 相邻块的公共边永远是同一列像素，所以接缝看不见。
    /// </summary>
    public void Refresh(Vector3 camPos, float halfHeight, float aspect)
    {
        if (tileSprite == null) return;

        float viewHeight = halfHeight * 2f;
        float viewWidth = viewHeight * aspect;
        float factor = Mathf.Clamp01(scrollFactor);
        float originX = camPos.x * (1f - factor);
        float originY = camPos.y * (1f - factor);
        float scrollX = camPos.x * factor;
        float scrollY = camPos.y * factor;

        int i0 = Mathf.FloorToInt((scrollX - viewWidth * 0.5f) / tileWidth);
        int i1 = Mathf.FloorToInt((scrollX + viewWidth * 0.5f) / tileWidth);
        int j0 = Mathf.FloorToInt((scrollY - viewHeight * 0.5f) / tileHeight);
        int j1 = Mathf.FloorToInt((scrollY + viewHeight * 0.5f) / tileHeight);

        int index = 0;
        for (int i = i0; i <= i1; i++)
        {
            for (int j = j0; j <= j1; j++)
            {
                SpriteRenderer tile = GetTile(index++);
                tile.transform.position = new Vector3(
                    originX + (i + 0.5f) * tileWidth,
                    originY + (j + 0.5f) * tileHeight,
                    0f);
                tile.transform.localScale = new Vector3(
                    (i & 1) != 0 ? -1f : 1f,
                    (j & 1) != 0 ? -1f : 1f,
                    1f);
            }
        }

        for (int i = index; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }

    SpriteRenderer GetTile(int index)
    {
        while (pool.Count <= index)
        {
            var go = new GameObject("BackgroundTile");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = tileSprite;
            renderer.sortingOrder = sortingOrder;
            pool.Add(renderer);
        }

        SpriteRenderer tile = pool[index];
        if (!tile.gameObject.activeSelf) tile.gameObject.SetActive(true);
        return tile;
    }
}