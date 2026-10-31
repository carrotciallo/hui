using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public static class WorldBuilder
{
    const string ScenePath = "Assets/Scenes/World.unity";
    const string SpritePath = "Assets/Art/Greybox/White.png";
    const string TilePath = "Assets/Art/Greybox/WorldTile.asset";
    const string MaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";
    const string MarkerPrefabDir = "Assets/Prefabs/Markers";

    const int GroundLayerIndex = 6;
    const int PlayerLayerIndex = 7;
    const int HazardLayerIndex = 8;
    const int InteractableLayerIndex = 9;

    const float RegionSpacing = 40f;
    const float SpawnY = GameConstants.PlayerSize * 0.5f + 0.02f;
    const float FallFloor = -8f;

    static readonly Vector2 L0Spawn = new Vector2(-17f, SpawnY);
    static readonly Rect WorldBounds = new Rect(-24f, -12f, 424f, 30f);

    static readonly Color FloorColor = new Color(0.30f, 0.33f, 0.40f);
    static readonly Color SpawnColor = new Color(0.3f, 0.9f, 1f, 0.9f);
    static readonly Color RespawnColor = new Color(0.35f, 0.9f, 0.5f, 0.9f);
    static readonly Color HazardColor = new Color(1f, 0.25f, 0.2f, 0.7f);
    static readonly Color RegionColor = new Color(0.9f, 0.8f, 0.3f, 0.5f);

    [MenuItem("GameJam/Build World Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureLayer(GroundLayerIndex, "Ground");
        EnsureLayer(PlayerLayerIndex, "Player");
        EnsureLayer(HazardLayerIndex, "Hazard");
        EnsureLayer(InteractableLayerIndex, "Interactable");

        Sprite white = EnsureWhiteSprite();
        PhysicsMaterial2D slick = EnsureFrictionlessMaterial();
        Tile tile = EnsureWorldTile(white);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var markers = CreateMarkerPrefabs(white);

        var camGo = CreateCamera();
        var worldGo = new GameObject("World");
        worldGo.AddComponent<RunStateHost>();

        var tilemap = CreateTilemap(worldGo.transform);
        PaintL0(tilemap, tile);

        var regions = new List<Region>();
        var w = CreateRegion(worldGo.transform, tilemap, tile, 0, "L0", L0Spawn,
                             new Rect(WorldBounds.xMin, WorldBounds.yMin, 44f, WorldBounds.height),
                             markers, paintGround: false);
        regions.Add(w);
        AddL0Markers(w, markers);

        for (int i = 1; i <= 10; i++)
        {
            float x = 24f + RegionSpacing * (i - 1);
            var area = new Rect(20f + RegionSpacing * (i - 1), WorldBounds.yMin,
                                RegionSpacing, WorldBounds.height);
            var region = CreateRegion(worldGo.transform, tilemap, tile, i, $"L{i}",
                                      new Vector2(x, SpawnY), area, markers, paintGround: true);
            regions.Add(region);
        }

        var world = worldGo.AddComponent<World>();
        world.Configure(regions.ToArray(), WorldBounds, FallFloor);

        var p1 = CreatePlayer("Player1", w.SpawnPositionFor(0), new Color(0.36f, 0.62f, 1f),
                              PlayerMotor.Scheme.WASD, white, slick);
        var p2 = CreatePlayer("Player2", w.SpawnPositionFor(1), new Color(1f, 0.45f, 0.4f),
                              PlayerMotor.Scheme.Arrows, white, slick);

        var life1 = p1.AddComponent<PlayerLife>();
        var life2 = p2.AddComponent<PlayerLife>();
        life1.Configure(0, life2);
        life2.Configure(1, life1);

        int groundMask = (1 << GroundLayerIndex) | (1 << PlayerLayerIndex);
        foreach (var player in new[] { p1, p2 })
        {
            var so = new SerializedObject(player.GetComponent<PlayerMotor>());
            so.FindProperty("groundMask").intValue = groundMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var follow = camGo.GetComponent<CoopCamera>();
        follow.Configure(p1.transform, p2.transform, life1, life2, WorldBounds);

        var hud = camGo.AddComponent<DebugHud>();
        hud.Configure(p1.GetComponent<PlayerMotor>(), p2.GetComponent<PlayerMotor>(), world);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();

        Debug.Log("[World] 场景已生成：L0 灰盒 + L1–L10 占位；" +
                  "PageUp/PageDown 选区域，Home 传送，R 回重生点。");
    }

    struct MarkerPrefabs
    {
        public GameObject Spawn;
        public GameObject Respawn;
        public GameObject KillZone;
        public GameObject RegionMarker;
    }

    static MarkerPrefabs CreateMarkerPrefabs(Sprite white)
    {
        Directory.CreateDirectory(MarkerPrefabDir);
        AssetDatabase.Refresh();

        return new MarkerPrefabs
        {
            Spawn = BuildPrefab($"{MarkerPrefabDir}/SpawnPoint.prefab", white,
                                new Vector2(0.5f, 1.6f), SpawnColor, 6, InteractableLayerIndex,
                                null),
            Respawn = BuildPrefab($"{MarkerPrefabDir}/RespawnPoint.prefab", white,
                                  new Vector2(1.2f, 1.8f), RespawnColor, 6, InteractableLayerIndex,
                                  go =>
                                  {
                                      var box = go.AddComponent<BoxCollider2D>();
                                      box.isTrigger = true;
                                      go.AddComponent<RespawnPoint>();
                                  }),
            KillZone = BuildPrefab($"{MarkerPrefabDir}/KillZone.prefab", white,
                                   new Vector2(4f, 1f), HazardColor, 6, HazardLayerIndex,
                                   go =>
                                   {
                                       var box = go.AddComponent<BoxCollider2D>();
                                       box.isTrigger = true;
                                       go.AddComponent<KillZone>();
                                   }),
            RegionMarker = BuildPrefab($"{MarkerPrefabDir}/RegionMarker.prefab", white,
                                       new Vector2(0.12f, 3f), RegionColor, 5, 0, null)
        };
    }

    static GameObject BuildPrefab(string path, Sprite sprite, Vector2 size, Color color,
                                  int sortingOrder, int layer, System.Action<GameObject> configure)
    {
        var go = new GameObject(Path.GetFileNameWithoutExtension(path));
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.layer = layer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        configure?.Invoke(go);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Tilemap CreateTilemap(Transform parent)
    {
        var terrain = new GameObject("Terrain");
        terrain.transform.SetParent(parent, false);

        var grid = terrain.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        var tilemapGo = new GameObject("Ground");
        tilemapGo.transform.SetParent(terrain.transform, false);
        tilemapGo.layer = GroundLayerIndex;

        var tilemap = tilemapGo.AddComponent<Tilemap>();
        var renderer = tilemapGo.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        var rb = tilemapGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var composite = tilemapGo.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        var tileCollider = tilemapGo.AddComponent<TilemapCollider2D>();
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        return tilemap;
    }

    static void PaintL0(Tilemap tilemap, Tile tile)
    {
        // 顶面 y = 0；左地面 / 4 格横冲缺口 / 右侧 3 格跳远坑 / 2 格台阶
        PaintRect(tilemap, tile, -21, -5, -4, -1);
        PaintRect(tilemap, tile, 0, 9, -4, -1);
        PaintRect(tilemap, tile, 13, 15, -4, -1);
        PaintRect(tilemap, tile, 3, 7, 0, 1);
    }

    static Region CreateRegion(Transform parent, Tilemap tilemap, Tile tile, int index, string label,
                               Vector2 spawnPosition, Rect area, MarkerPrefabs markers,
                               bool paintGround)
    {
        var go = new GameObject($"L{index}");
        go.transform.SetParent(parent, false);

        var spawn = InstantiateMarker(markers.Spawn, go.transform, spawnPosition, Vector2.one);
        spawn.name = "Spawn";

        var marker = InstantiateMarker(markers.RegionMarker, go.transform,
                                       spawnPosition + new Vector2(-2.5f, 1.5f), Vector2.one);
        marker.name = "RegionMarker";

        var region = go.AddComponent<Region>();
        region.Configure(index, label, spawn.transform, area);

        if (paintGround)
        {
            int center = Mathf.RoundToInt(spawnPosition.x);
            PaintRect(tilemap, tile, center - 4, center + 3, -2, -1);
        }

        return region;
    }

    static void AddL0Markers(Region l0, MarkerPrefabs markers)
    {
        var dashGap = InstantiateMarker(markers.KillZone, l0.transform, new Vector2(-2f, -5.5f),
                                        new Vector2(1f, 1.5f));
        dashGap.name = "KillZone DashGap";

        var jumpPit = InstantiateMarker(markers.KillZone, l0.transform, new Vector2(11.5f, -5.5f),
                                        new Vector2(0.75f, 1.5f));
        jumpPit.name = "KillZone JumpPit";

        var respawn = InstantiateMarker(markers.Respawn, l0.transform, new Vector2(14f, 1f),
                                        Vector2.one);
        respawn.name = "RespawnPoint";
    }

    static GameObject InstantiateMarker(GameObject prefab, Transform parent, Vector2 position,
                                        Vector2 scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = position;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        return go;
    }

    static void PaintRect(Tilemap tilemap, Tile tile, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
            for (int y = yMin; y <= yMax; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    static GameObject CreateCamera()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(L0Spawn.x, 2f, -10f);

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

    static Tile EnsureWorldTile(Sprite sprite)
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }

        tile.sprite = sprite;
        tile.color = FloorColor;
        tile.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();

        return tile;
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
