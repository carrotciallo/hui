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
    const string TilePath = "Assets/Art/Greybox/WorldTile.asset";
    const string MaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";
    const string MarkerPrefabDir = "Assets/Prefabs/Markers";
    const string MaterialPrefabDir = "Assets/Prefabs/Materials";

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
    static readonly Color WallColor = new Color(0.55f, 0.55f, 0.62f);
    static readonly Color SpawnColor = new Color(0.3f, 0.9f, 1f, 0.9f);
    static readonly Color RespawnColor = new Color(0.35f, 0.9f, 0.5f, 0.9f);
    static readonly Color HazardColor = new Color(1f, 0.25f, 0.2f, 0.7f);
    static readonly Color RegionColor = new Color(0.9f, 0.8f, 0.3f, 0.5f);

    [MenuItem("GameJam/Build World Scene")]
    public static void Build()
    {
        // SaveCurrentModifiedScenesIfUserWantsTo 和 NewScene 在 Play 模式下会直接抛异常
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[World] 请先退出 Play 模式，再生成正式世界。");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GreyboxAssets.EnsureLayer(GroundLayerIndex, "Ground");
        GreyboxAssets.EnsureLayer(PlayerLayerIndex, "Player");
        GreyboxAssets.EnsureLayer(HazardLayerIndex, "Hazard");
        GreyboxAssets.EnsureLayer(InteractableLayerIndex, "Interactable");

        Sprite white = GreyboxAssets.EnsureWhiteSprite();
        PhysicsMaterial2D slick = GreyboxAssets.EnsurePhysicsMaterial(
            MaterialPath, "PlayerFrictionless", 0f);
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
                                      new Vector2(x, SpawnY), area, markers,
                                      paintGround: i != 1 && i != 2);

            if (i == 1)
            {
                PaintL1(tilemap, tile);
                AddL1Markers(region, markers, white);
            }
            else if (i == 2)
            {
                PaintL2(tilemap, tile);
                AddL2Markers(region, markers);
            }

            regions.Add(region);
        }

        var world = worldGo.AddComponent<World>();
        world.Configure(regions.ToArray(), WorldBounds, FallFloor);

        var p1 = GreyboxAssets.CreatePlayer("Player1", w.SpawnPositionFor(0),
                                            new Color(0.36f, 0.62f, 1f),
                                            PlayerMotor.Scheme.WASD, white, slick, white,
                                            PlayerLayerIndex);
        var p2 = GreyboxAssets.CreatePlayer("Player2", w.SpawnPositionFor(1),
                                            new Color(1f, 0.45f, 0.4f),
                                            PlayerMotor.Scheme.Arrows, white, slick, white,
                                            PlayerLayerIndex);

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

        Debug.Log("[World] 场景已生成：L0 灰盒 + L1 正式内容（横碰撞墙 / 上冲高台 / 重生点） + " +
                  "L2 正式内容（冲刺复习 / 钥匙与过关门 / 地绳桥） + L3–L10 占位；" +
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
            Spawn = GreyboxAssets.BuildPrefab($"{MarkerPrefabDir}/SpawnPoint.prefab", white,
                                              new Vector2(0.5f, 1.6f), SpawnColor, 6,
                                              InteractableLayerIndex, null),
            Respawn = GreyboxAssets.BuildPrefab($"{MarkerPrefabDir}/RespawnPoint.prefab", white,
                                                new Vector2(1.2f, 1.8f), RespawnColor, 6,
                                                InteractableLayerIndex,
                                                go =>
                                                {
                                                    var box = go.AddComponent<BoxCollider2D>();
                                                    box.isTrigger = true;
                                                    go.AddComponent<RespawnPoint>();
                                                }),
            KillZone = GreyboxAssets.BuildPrefab($"{MarkerPrefabDir}/KillZone.prefab", white,
                                                 new Vector2(4f, 1f), HazardColor, 6,
                                                 HazardLayerIndex,
                                                 go =>
                                                 {
                                                     var box = go.AddComponent<BoxCollider2D>();
                                                     box.isTrigger = true;
                                                     go.AddComponent<KillZone>();
                                                 }),
            RegionMarker = GreyboxAssets.BuildPrefab($"{MarkerPrefabDir}/RegionMarker.prefab", white,
                                                     new Vector2(0.12f, 3f), RegionColor, 5, 0,
                                                     null)
        };
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

    static void PaintL1(Tilemap tilemap, Tile tile)
    {
        // 入口地面 [20,26) / 2 格坑 / 主地面 [28,45)
        PaintRect(tilemap, tile, 20, 25, -2, -1);
        PaintRect(tilemap, tile, 28, 44, -2, -1);

        // 破墙隧道：上方厚顶 [32,36)×[3,8)，墙体是单独的可破坏物件
        PaintRect(tilemap, tile, 32, 35, 3, 7);

        // 上冲高台 [38,45)×[0,4)，再往右是 [45,46) 和 [48,60) 的顶面，中间 2 格缺口；
        // 出口平台补到 x=60 与 L2 地面接上，避免台边漏出 1 格无标记的死亡缝
        PaintRect(tilemap, tile, 38, 44, 0, 3);
        PaintRect(tilemap, tile, 45, 45, 3, 3);
        PaintRect(tilemap, tile, 48, 59, 3, 3);
    }

    static void AddL1Markers(Region l1, MarkerPrefabs markers, Sprite white)
    {
        // L1 考察点：横碰破墙 / 上冲上高台 / 重生点
        CreateBreakableWall(l1.transform, white, new Vector2(33.5f, 1.5f), new Vector2(1f, 3f));

        var entryRespawn = InstantiateMarker(markers.Respawn, l1.transform,
                                             new Vector2(28.9f, 0.9f), Vector2.one);
        entryRespawn.name = "RespawnPoint AfterPit";

        var ledgeRespawn = InstantiateMarker(markers.Respawn, l1.transform,
                                             new Vector2(41f, 4.9f), Vector2.one);
        ledgeRespawn.name = "RespawnPoint Ledge";

        var entryPit = InstantiateMarker(markers.KillZone, l1.transform,
                                         new Vector2(27f, -2.5f), new Vector2(2f, 2f));
        entryPit.name = "KillZone EntryPit";

        var topGap = InstantiateMarker(markers.KillZone, l1.transform,
                                       new Vector2(47f, -4f), new Vector2(3f, 2f));
        topGap.name = "KillZone TopGap";
    }

    static void CreateBreakableWall(Transform parent, Sprite sprite, Vector2 center, Vector2 size)
    {
        var go = new GameObject("BreakableWall");
        go.transform.SetParent(parent, false);
        go.layer = GroundLayerIndex;
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = WallColor;
        sr.sortingOrder = 2;

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<BreakableWall>();
    }

    static void PaintL2(Tilemap tilemap, Tile tile)
    {
        // 入口 [60,70) / 4 格冲刺缺口 (70–74) / 钥匙与门走廊 [74,90) / 地绳桥 (90–94) / 出口 [94,100)
        PaintRect(tilemap, tile, 60, 69, -2, -1);
        PaintRect(tilemap, tile, 74, 89, -2, -1);
        PaintRect(tilemap, tile, 94, 99, -2, -1);

        // 钥匙柱 [79,80)×[0,1) 只有 1 格高，跳上去拿头顶的钥匙
        PaintRect(tilemap, tile, 79, 79, 0, 0);

        // 门洞上方压顶 [83,86)×[3,8)：门顶 y=3 与顶板齐平，上冲翻不过门
        PaintRect(tilemap, tile, 83, 85, 3, 7);
    }

    static void AddL2Markers(Region l2, MarkerPrefabs markers)
    {
        // L2 考察点：4 格冲刺缺口 / 钥匙 + 过关门 / 地绳桥
        var entryRespawn = InstantiateMarker(markers.Respawn, l2.transform,
                                             new Vector2(66f, 0.9f), Vector2.one);
        entryRespawn.name = "RespawnPoint Entry";

        var ropeRespawn = InstantiateMarker(markers.Respawn, l2.transform,
                                            new Vector2(88.5f, 0.9f), Vector2.one);
        ropeRespawn.name = "RespawnPoint BeforeRope";

        var dashGap = InstantiateMarker(markers.KillZone, l2.transform,
                                        new Vector2(72f, -2.5f), new Vector2(4f, 2f));
        dashGap.name = "KillZone DashGap";

        var ropePit = InstantiateMarker(markers.KillZone, l2.transform,
                                        new Vector2(92f, -3.5f), new Vector2(4f, 2f));
        ropePit.name = "KillZone RopePit";

        PlaceMaterial(l2.transform, "KeyPickup", new Vector2(79.5f, 1.8f), "KeyPickup L2");

        var gate = PlaceMaterial(l2.transform, "KeyGate", new Vector2(84.5f, 1.5f), "KeyGate L2");
        if (gate != null)
        {
            var so = new SerializedObject(gate.GetComponent<KeyGate>());
            so.FindProperty("id").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 地绳顶面齐地面 y=0，直接走过去；下落穿过留给 v2 之后的机制
        PlaceMaterial(l2.transform, "GroundRope", new Vector2(92f, -0.125f), "GroundRope L2");
    }

    static GameObject PlaceMaterial(Transform parent, string prefabName, Vector2 position,
                                    string name)
    {
        var path = $"{MaterialPrefabDir}/{prefabName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[World] 缺少 {path}，先跑 GameJam → Build Materials Test Scene。");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = position;
        go.name = name;
        return go;
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
}
