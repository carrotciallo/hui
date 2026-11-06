using System.Collections.Generic;
using UnityEngine;

/// <summary>Selected Issue #26 art. The source renderer remains the gameplay state carrier.</summary>
public static class LabArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static string ResourcePath(string name)
    {
        string group = name == "body-b" || name.StartsWith("edge-") ? "characters/"
            : name == "terrain" || name == "crate" || name == "steel" || name == "ice"
                || name == "magma" || name == "lava" ? "materials/" : "props/";
        string file = name switch
        {
            "body-b" => "b_body_normal",
            "edge-h" => "a_edge_horizontal_normal",
            "edge-v" => "a_edge_vertical_normal",
            "door-closed" => "gate_locked_close",
            "door-open" => "gate_locked_open",
            "portal" => "portal_unlit",
            "portal-marked" => "portal_lit",
            "portal-active" => "portal_double_lit",
            "rope-v" => "rope_vertical_normal",
            "rope-h" => "rope_horizontal_normal",
            "spikes-up" => "spike_up",
            "spikes-side" => "spike_right",
            "checkpoint-active" => "checkpoint_lit",
            _ => name.Replace('-', '_') + "_normal"
        };
        return "HuiArt/" + group + file;
    }

    public static Sprite Load(string name)
    {
        if (!cache.TryGetValue(name, out var sprite) || sprite == null)
            cache[name] = sprite = Resources.Load<Sprite>(ResourcePath(name));
        return sprite;
    }

    // Art is delivered on a 16x16 canvas; crop its centred component without scaling pixels.
    public static Sprite Part(string name, int width, int height)
    {
        string key = name + ":" + width + "x" + height;
        if (cache.TryGetValue(key, out var part) && part != null) return part;
        var full = Load(name);
        if (full == null) return null;
        var rect = full.rect;
        rect = new Rect(rect.x + (rect.width - width) / 2, rect.y + (rect.height - height) / 2, width, height);
        cache[key] = part = Sprite.Create(full.texture, rect, new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        part.name = key;
        return part;
    }

    public static void Apply(SpriteRenderer source, string name, bool tiled = false)
    {
        if (source == null || Load(name) == null) return;
        var art = source.GetComponent<LabArtPresenter>();
        if (art == null) art = source.gameObject.AddComponent<LabArtPresenter>();
        art.Configure(source, name, tiled);
    }
}
