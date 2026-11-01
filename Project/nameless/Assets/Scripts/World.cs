using System;
using System.Collections.Generic;
using UnityEngine;

/// 单场景世界的根：区域顺序、相机边界、掉落判定线。
public sealed class World : MonoBehaviour
{
    public static World Instance { get; private set; }

    [SerializeField] Region[] regions = Array.Empty<Region>();
    [SerializeField] Rect bounds = new Rect(-24f, -12f, 432f, 30f);
    [SerializeField] float fallFloor = -8f;

    public IReadOnlyList<Region> Regions => regions;
    public Rect Bounds => bounds;
    public float FallFloor => fallFloor;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Configure(Region[] orderedRegions, Rect worldBounds, float worldFallFloor)
    {
        regions = orderedRegions ?? Array.Empty<Region>();
        bounds = worldBounds;
        fallFloor = worldFallFloor;
    }

    public Region RegionAt(int index)
    {
        foreach (var region in regions)
        {
            if (region != null && region.Index == index) return region;
        }
        return null;
    }

    /// 优先用区域的水平范围判定；落在范围外（例如世界左右边缘）时取最近区域。
    public Region RegionOf(Vector2 position)
    {
        foreach (var region in regions)
        {
            if (region != null && region.ContainsX(position.x)) return region;
        }

        Region best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (var region in regions)
        {
            if (region == null) continue;

            float center = region.Area.width > 0f ? region.Area.center.x : region.SpawnPosition.x;
            float distance = Mathf.Abs(position.x - center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = region;
            }
        }

        return best;
    }

    public Vector2 SpawnFor(Vector2 position, int slot)
    {
        var region = RegionOf(position);
        return region != null ? region.SpawnPositionFor(slot) : (Vector2)transform.position;
    }
}
