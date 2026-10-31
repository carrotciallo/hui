using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PrototypeSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Prototype.unity";
    const string SpritePath = "Assets/Art/Greybox/White.png";
    const string MaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";

    const int GroundLayerIndex = 6;
    const int PlayerLayerIndex = 7;

    static readonly Color FloorColor = new Color(0.24f, 0.26f, 0.32f);
    static readonly Color PlatformColor = new Color(0.30f, 0.34f, 0.42f);
    static readonly Color MarkerColor = new Color(0.55f, 0.58f, 0.66f);
    static readonly Color MajorMarkerColor = new Color(1f, 0.85f, 0.25f);

    [MenuItem("GameJam/Build Prototype Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureLayer(GroundLayerIndex, "Ground");
        EnsureLayer(PlayerLayerIndex, "Player");

        Sprite white = EnsureWhiteSprite();
        PhysicsMaterial2D slick = EnsureFrictionlessMaterial();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = CreateCamera();
        CreateTestRoom(white);

        float standY = GameConstants.PlayerSize * 0.5f + 0.01f;
        var p1 = CreatePlayer("Player1", new Vector2(-4f, standY), new Color(0.36f, 0.62f, 1f),
                              PlayerMotor.Scheme.WASD, white, slick);
        var p2 = CreatePlayer("Player2", new Vector2(-2.5f, standY), new Color(1f, 0.45f, 0.4f),
                              PlayerMotor.Scheme.Arrows, white, slick);

        int groundMask = (1 << GroundLayerIndex) | (1 << PlayerLayerIndex);
        foreach (var player in new[] { p1, p2 })
        {
            var so = new SerializedObject(player.GetComponent<PlayerMotor>());
            so.FindProperty("groundMask").intValue = groundMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var follow = camGo.GetComponent<CoopCamera>();
        var camSo = new SerializedObject(follow);
        camSo.FindProperty("a").objectReferenceValue = p1.transform;
        camSo.FindProperty("b").objectReferenceValue = p2.transform;
        camSo.FindProperty("minSize").floatValue = 6f;
        camSo.FindProperty("maxSize").floatValue = 11f;
        camSo.ApplyModifiedPropertiesWithoutUndo();

        var hud = camGo.AddComponent<DebugHud>();
        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("p1").objectReferenceValue = p1.GetComponent<PlayerMotor>();
        hudSo.FindProperty("p2").objectReferenceValue = p2.GetComponent<PlayerMotor>();
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();

        Debug.Log("[Prototype] 场景已生成：P1 = WASD / Space 跳 / LeftShift 冲刺；" +
                  "P2 = 方向键 / 右Alt 跳 / . 冲刺；R 重置。");
    }

    static GameObject CreateCamera()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(3f, 2f, -10f);

        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData();
        camGo.AddComponent<CoopCamera>();

        return camGo;
    }

    static void CreateTestRoom(Sprite white)
    {
        CreateBlock("Floor A", new Vector2(-1f, -0.5f), new Vector2(10f, 1f), FloorColor, white);
        CreateBlock("Floor B", new Vector2(9.75f, -0.5f), new Vector2(6.5f, 1f), FloorColor, white);
        CreateBlock("High Platform", new Vector2(1.5f, 1.5f), new Vector2(3f, 1f), PlatformColor, white);

        for (int x = -6; x <= 13; x++)
        {
            bool major = x % 5 == 0;
            float height = major ? 0.5f : 0.2f;
            float width = major ? 0.05f : 0.03f;
            CreateVisual($"Marker {x}", new Vector2(x, height * 0.5f), new Vector2(width, height),
                         major ? MajorMarkerColor : MarkerColor, white, 5);
        }

        CreateVisual("Height Ruler", new Vector2(-5.5f, 1.5f), new Vector2(0.04f, 3f),
                     MarkerColor, white, 5);

        for (int y = 1; y <= 3; y++)
        {
            bool major = y == 2;
            CreateVisual($"Height Tick {y}", new Vector2(-5.35f, y),
                         new Vector2(0.3f, major ? 0.04f : 0.025f),
                         major ? MajorMarkerColor : MarkerColor, white, 5);
        }
    }

    static GameObject CreateBlock(string name, Vector2 center, Vector2 size, Color color, Sprite sprite)
    {
        var go = CreateVisual(name, center, size, color, sprite, 0);
        go.layer = GroundLayerIndex;
        go.AddComponent<BoxCollider2D>();
        return go;
    }

    static GameObject CreateVisual(string name, Vector2 center, Vector2 size, Color color,
                                   Sprite sprite, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return go;
    }

    static GameObject CreatePlayer(string name, Vector2 position, Color color, PlayerMotor.Scheme scheme,
                                   Sprite sprite, PhysicsMaterial2D material)
    {
        var go = new GameObject(name);
        go.layer = PlayerLayerIndex;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * GameConstants.PlayerSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 10;

        var box = go.AddComponent<BoxCollider2D>();
        box.sharedMaterial = material;

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

        return go;
    }

    static Sprite EnsureWhiteSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);

        bool valid = existing != null && importer != null && texture != null
                     && texture.width == 16 && texture.height == 16
                     && Mathf.Approximately(importer.spritePixelsPerUnit, GameConstants.PixelsPerUnit);

        if (valid) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

        importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = GameConstants.PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static PhysicsMaterial2D EnsureFrictionlessMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (existing != null) return existing;

        var material = new PhysicsMaterial2D("PlayerFrictionless")
        {
            friction = 0f,
            bounciness = 0f
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static void EnsureLayer(int index, string layerName)
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
