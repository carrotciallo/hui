using UnityEngine;

/// <summary>
/// Presentation only: mirrors visibility, alpha and state without replacing collision geometry,
/// state colours or the renderers observed by gameplay. Used by both preview and runtime builds.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LabArtPresenter : MonoBehaviour
{
    SpriteRenderer source;
    SpriteRenderer display;
    string asset;
    bool tiled;
    bool previousForceOff;
    LabDoor door;
    LabTeleportPoint portal;
    LabRespawnPoint checkpoint;
    State lastState;
    bool hasLastState;

    // Every input Refresh reads. Equal signatures skip all renderer writes.
    struct State
    {
        public string selected;
        public Color color;
        public bool enabled;
        public bool flipX;
        public bool flipY;
        public int sortingLayerID;
        public int sortingOrder;
        public Material material;
        public Vector3 scale;
        public Sprite sprite;

        public bool Equals(State other)
        {
            return selected == other.selected && color.Equals(other.color)
                && enabled == other.enabled && flipX == other.flipX && flipY == other.flipY
                && sortingLayerID == other.sortingLayerID && sortingOrder == other.sortingOrder
                && material == other.material && scale.Equals(other.scale) && sprite == other.sprite;
        }
    }

    public void Configure(SpriteRenderer renderer, string name, bool repeat)
    {
        if (source == null) previousForceOff = renderer.forceRenderingOff;
        source = renderer;
        asset = name;
        tiled = repeat;
        door = GetComponent<LabDoor>();
        portal = GetComponent<LabTeleportPoint>();
        checkpoint = GetComponent<LabRespawnPoint>();
        if (display == null)
        {
            var child = new GameObject("SelectedArt");
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(transform, false);
            display = child.AddComponent<SpriteRenderer>();
        }
        hasLastState = false; // Rebound inputs: force a full rewrite even if the signature looks unchanged.
        Refresh();
    }

    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (source == null || display == null || source.sprite == null) return;
        string selected = asset;
        if (door != null) selected = door.IsOpen ? "door-open" : "door-closed";
        if (checkpoint != null) selected = checkpoint.IsActive ? "checkpoint-active" : "checkpoint";
        if (portal != null)
            selected = source.color == portal.activeColor ? "portal-active"
                : portal.IsMarked ? "portal-marked" : "portal";
        var state = new State
        {
            selected = selected,
            color = source.color,
            enabled = source.enabled,
            flipX = source.flipX,
            flipY = source.flipY,
            sortingLayerID = source.sortingLayerID,
            sortingOrder = source.sortingOrder,
            material = source.sharedMaterial,
            scale = transform.localScale,
            sprite = source.sprite,
        };
        if (hasLastState && state.Equals(lastState)) return;
        var sprite = LabArt.Load(selected);
        if (sprite == null) return;
        lastState = state;
        hasLastState = true;
        source.forceRenderingOff = true;
        display.sprite = sprite;
        display.enabled = source.enabled;
        display.color = new Color(1f, 1f, 1f, source.color.a);
        if (checkpoint != null && checkpoint.IsActive)
            display.color = new Color(0.5f, 1f, 0.65f, source.color.a);
        display.flipX = source.flipX;
        display.flipY = source.flipY;
        display.sortingLayerID = source.sortingLayerID;
        display.sortingOrder = source.sortingOrder;
        display.sharedMaterial = source.sharedMaterial;
        var originalSize = source.sprite.bounds.size;
        if (tiled)
        {
            // Cancel the rectangle's scale, then tile in world-sized cells (never stretch pixels).
            Vector3 scale = transform.localScale;
            display.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 1f);
            display.drawMode = SpriteDrawMode.Tiled;
            display.size = new Vector2(originalSize.x * Mathf.Abs(scale.x), originalSize.y * Mathf.Abs(scale.y));
        }
        else
        {
            display.drawMode = SpriteDrawMode.Simple;
            display.transform.localScale = new Vector3(originalSize.x / sprite.bounds.size.x,
                originalSize.y / sprite.bounds.size.y, 1f);
        }
    }

    void OnDisable()
    {
        // forceRenderingOff and display.enabled were just changed outside Refresh; force a rewrite on re-enable.
        hasLastState = false;
        if (source != null) source.forceRenderingOff = previousForceOff;
        if (display != null) display.enabled = false;
    }

    void OnDestroy()
    {
        if (source != null) source.forceRenderingOff = previousForceOff;
        if (display == null) return;
        if (Application.isPlaying) Destroy(display.gameObject);
        else DestroyImmediate(display.gameObject);
    }
}
