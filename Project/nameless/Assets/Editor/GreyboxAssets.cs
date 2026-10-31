using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// 场景构建器共用的灰盒资源：白块、物理材质、prefab 和玩家。
public static class GreyboxAssets
{
    public const string WhiteSpritePath = "Assets/Art/Greybox/White.png";

    public static Sprite EnsureWhiteSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        var importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WhiteSpritePath);

        bool valid = existing != null && importer != null && texture != null
                     && texture.width == 16 && texture.height == 16
                     && Mathf.Approximately(importer.spritePixelsPerUnit, GameConstants.PixelsPerUnit);

        if (valid) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath));

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceUpdate);

        importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = GameConstants.PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
    }

    public static PhysicsMaterial2D EnsurePhysicsMaterial(string path, string materialName, float friction)
    {
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(path));

        var material = new PhysicsMaterial2D(materialName)
        {
            friction = friction,
            bounciness = 0f
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static GameObject BuildPrefab(string path, Sprite sprite, Vector2 size, Color color,
                                         int sortingOrder, int layer, Action<GameObject> configure)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.Refresh();

        var go = new GameObject(Path.GetFileNameWithoutExtension(path));
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.layer = layer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        configure?.Invoke(go);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    public static GameObject CreatePlayer(string name, Vector2 position, Color color,
                                          PlayerMotor.Scheme scheme, Sprite sprite,
                                          PhysicsMaterial2D bodyMaterial, Sprite keySprite,
                                          int playerLayer)
    {
        var go = new GameObject(name);
        go.layer = playerLayer;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * GameConstants.PlayerSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 10;

        var aura = new GameObject("TierOutline");
        aura.transform.SetParent(go.transform, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localScale = Vector3.one * 1.35f;
        var auraSr = aura.AddComponent<SpriteRenderer>();
        auraSr.sprite = sprite;
        auraSr.color = new Color(1f, 1f, 1f, 0f);
        auraSr.sortingOrder = 9;
        aura.AddComponent<TierOutline>();

        var box = go.AddComponent<BoxCollider2D>();
        box.sharedMaterial = bodyMaterial;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = GameConstants.RiseGravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<SpeedTierProvider>();
        var motor = go.AddComponent<PlayerMotor>();

        var so = new SerializedObject(motor);
        so.FindProperty("scheme").enumValueIndex = (int)scheme;
        so.ApplyModifiedPropertiesWithoutUndo();

        var key = new GameObject("CarriedKey");
        key.transform.SetParent(go.transform, false);
        key.transform.localPosition = Vector3.up * (GameConstants.KeyCarryHeight / GameConstants.PlayerSize);
        key.transform.localScale = Vector3.one * (0.5f / GameConstants.PlayerSize);
        var keySr = key.AddComponent<SpriteRenderer>();
        keySr.sprite = keySprite;
        keySr.color = new Color(1f, 0.8f, 0.2f, 1f);
        keySr.sortingOrder = 11;

        var carrier = go.AddComponent<KeyCarrier>();
        carrier.Configure(key.transform);

        return go;
    }

    public static void EnsureLayer(int index, string layerName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        var entry = layers.GetArrayElementAtIndex(index);
        if (entry.stringValue == layerName) return;

        entry.stringValue = layerName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
