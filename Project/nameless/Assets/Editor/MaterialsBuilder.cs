using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// 生成 P4 材质 prefab 和材质测试场景：每种物件一排，按 1–5 强制挡位后走进/跳上去验收。
public static class MaterialsBuilder
{
    const string ScenePath = "Assets/Scenes/Materials.unity";
    const string PrefabDir = "Assets/Prefabs/Materials";
    const string PlayerMaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";
    const string IceMaterialPath = "Assets/Settings/IceSlick.physicsMaterial2D";

    const int GroundLayerIndex = 6;
    const int PlayerLayerIndex = 7;
    const int HazardLayerIndex = 8;
    const int InteractableLayerIndex = 9;

    const float RowWidth = 26f;
    const int RowCount = 8;
    const float SpawnY = GameConstants.PlayerSize * 0.5f + 0.02f;

    static readonly Color FloorColor = new Color(0.30f, 0.33f, 0.40f);
    static readonly Color RopeColor = new Color(0.3f, 0.85f, 0.4f, 0.85f);
    static readonly Color IceColor = new Color(0.6f, 0.85f, 1f, 0.9f);
    static readonly Color KeyColor = new Color(1f, 0.8f, 0.2f, 1f);
    static readonly Color GateColor = new Color(0.62f, 0.62f, 0.66f, 1f);
    static readonly Color SpikeColor = new Color(1f, 0.25f, 0.2f, 0.9f);
    static readonly Color PortalColor = new Color(0.65f, 0.35f, 0.9f, 0.55f);

    struct MaterialPrefabs
    {
        public GameObject VerticalRope;
        public GameObject GroundRope;
        public GameObject IceSurface;
        public GameObject IcePanel;
        public GameObject BreakableWall;
        public GameObject Spikes;
        public GameObject Key;
        public GameObject Gate;
        public GameObject Portal;
    }

    [MenuItem("GameJam/Build Materials Test Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GreyboxAssets.EnsureLayer(GroundLayerIndex, "Ground");
        GreyboxAssets.EnsureLayer(PlayerLayerIndex, "Player");
        GreyboxAssets.EnsureLayer(HazardLayerIndex, "Hazard");
        GreyboxAssets.EnsureLayer(InteractableLayerIndex, "Interactable");

        var white = GreyboxAssets.EnsureWhiteSprite();
        var playerMat = GreyboxAssets.EnsurePhysicsMaterial(PlayerMaterialPath,
                                                            "PlayerFrictionless", 0f);
        var iceMat = GreyboxAssets.EnsurePhysicsMaterial(IceMaterialPath, "IceSlick", 0f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Materials").transform;

        var prefabs = CreatePrefabs(white, iceMat);

        for (int row = 0; row < RowCount; row++)
        {
            float start = row * RowWidth;

            if (row == 2)
            {
                CreateGround(root, white, start - 2f, start + 8f, null);
                CreateGround(root, white, start + 8f, start + 16f, iceMat, ice: true);
                CreateGround(root, white, start + 16f, start + 24f, null);
            }
            else
            {
                CreateGround(root, white, start - 2f, start + 24f, null);
            }

            PlaceRow(root, prefabs, row, start);
        }

        var host = new GameObject("RunStateHost");
        host.AddComponent<RunStateHost>();

        var camGo = CreateCamera(new Vector3(6f, 2f, -10f));
        var p1 = GreyboxAssets.CreatePlayer("Player1", new Vector2(1f, SpawnY),
                                            new Color(0.36f, 0.62f, 1f),
                                            PlayerMotor.Scheme.WASD, white, playerMat, white,
                                            PlayerLayerIndex);
        var p2 = GreyboxAssets.CreatePlayer("Player2", new Vector2(2.5f, SpawnY),
                                            new Color(1f, 0.45f, 0.4f),
                                            PlayerMotor.Scheme.Arrows, white, playerMat, white,
                                            PlayerLayerIndex);

        var life1 = p1.AddComponent<PlayerLife>();
        var life2 = p2.AddComponent<PlayerLife>();
        life1.Configure(0, life2);
        life2.Configure(1, life1);

        var bounds = new Rect(-6f, -6f, RowCount * RowWidth + 10f, 20f);
        camGo.GetComponent<CoopCamera>().Configure(p1.transform, p2.transform, life1, life2, bounds);

        var hud = camGo.AddComponent<DebugHud>();
        hud.Configure(p1.GetComponent<PlayerMotor>(), p2.GetComponent<PlayerMotor>(), null);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[Materials] 材质测试场景已生成：" +
                  "绳 / 地绳 / 冰面 / 冰面板 / 破墙 / 钥匙+门 / 刺 / 传送门各一排；" +
                  "按 F1–F5 强制挡位，K 发钥匙。");
    }

    static MaterialPrefabs CreatePrefabs(Sprite white, PhysicsMaterial2D iceMat)
    {
        return new MaterialPrefabs
        {
            VerticalRope = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/VerticalRope.prefab", white, new Vector2(0.5f, 3f),
                RopeColor, 4, InteractableLayerIndex,
                go =>
                {
                    AddTrigger(go);
                    go.AddComponent<VerticalRope>();
                }),

            GroundRope = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/GroundRope.prefab", white, new Vector2(4f, 0.25f),
                RopeColor, 4, GroundLayerIndex,
                go =>
                {
                    go.AddComponent<BoxCollider2D>();
                    var rb = go.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Static;
                    var effector = go.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 170f;
                    effector.useSideBounce = false;
                    effector.useSideFriction = false;
                    go.AddComponent<GroundRope>();
                }),

            IceSurface = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/IceSurface.prefab", white, new Vector2(8f, 2f),
                IceColor, 1, GroundLayerIndex,
                go =>
                {
                    go.AddComponent<BoxCollider2D>().sharedMaterial = iceMat;
                    go.AddComponent<IceSurface>();
                }),

            IcePanel = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/IcePanel.prefab", white, new Vector2(1f, 1f),
                IceColor, 2, GroundLayerIndex,
                go =>
                {
                    go.AddComponent<BoxCollider2D>().sharedMaterial = iceMat;
                    var panel = go.AddComponent<IcePanel>();
                    panel.Configure(GameConstants.IceBreakTier);
                }),

            BreakableWall = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/BreakableWall.prefab", white, new Vector2(1f, 3f),
                new Color(0.55f, 0.55f, 0.62f), 2, GroundLayerIndex,
                go =>
                {
                    go.AddComponent<BoxCollider2D>();
                    var wall = go.AddComponent<BreakableWall>();
                    wall.Configure(GameConstants.WallBreakTier);
                }),

            Spikes = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/Spikes.prefab", white, new Vector2(1f, 0.4f),
                SpikeColor, 5, HazardLayerIndex,
                go =>
                {
                    AddTrigger(go);
                    go.AddComponent<KillZone>();
                }),

            Key = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/KeyPickup.prefab", white, new Vector2(0.5f, 0.5f),
                KeyColor, 6, InteractableLayerIndex,
                go =>
                {
                    AddTrigger(go);
                    go.AddComponent<KeyPickup>();
                }),

            Gate = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/KeyGate.prefab", white, new Vector2(1f, 3f),
                GateColor, 2, GroundLayerIndex,
                go =>
                {
                    go.AddComponent<BoxCollider2D>();
                    go.AddComponent<KeyGate>();
                }),

            Portal = GreyboxAssets.BuildPrefab(
                $"{PrefabDir}/Portal.prefab", white, new Vector2(2f, 3f),
                PortalColor, 3, InteractableLayerIndex,
                go =>
                {
                    AddTrigger(go);
                    go.AddComponent<Portal>();
                })
        };
    }

    static void PlaceRow(Transform root, MaterialPrefabs prefabs, int row, float start)
    {
        switch (row)
        {
            case 0:
                Place(prefabs.VerticalRope, root, new Vector2(start + 13f, 1.5f), "VerticalRope");
                break;

            case 1:
                Place(prefabs.GroundRope, root, new Vector2(start + 13f, 2f), "GroundRope");
                break;

            case 2:
                // 冰地板由 CreateGround 铺好，直接走过去就能感到滑行
                break;

            case 3:
                Place(prefabs.IcePanel, root, new Vector2(start + 13f, 0.5f), "IcePanel");
                break;

            case 4:
                Place(prefabs.BreakableWall, root, new Vector2(start + 13f, 1.5f), "BreakableWall");
                break;

            case 5:
                Place(prefabs.Key, root, new Vector2(start + 2f, 0.8f), "KeyPickup");
                ConfigureGate(Place(prefabs.Gate, root, new Vector2(start + 13f, 1.5f),
                                    "KeyGate A"), 0);
                ConfigureGate(Place(prefabs.Gate, root, new Vector2(start + 18f, 1.5f),
                                    "KeyGate B"), 1);
                break;

            case 6:
                for (int i = 0; i < 3; i++)
                {
                    Place(prefabs.Spikes, root, new Vector2(start + 10f + i, 0.2f),
                          $"Spikes {i}");
                }
                break;

            case 7:
                Place(prefabs.Portal, root, new Vector2(start + 12f, 1.5f), "Portal");
                break;
        }
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector2 position, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = position;
        go.name = name;
        return go;
    }

    static void AddTrigger(GameObject go)
    {
        go.AddComponent<BoxCollider2D>().isTrigger = true;
    }

    static void ConfigureGate(GameObject gate, int id)
    {
        var so = new SerializedObject(gate.GetComponent<KeyGate>());
        so.FindProperty("id").intValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject CreateGround(Transform parent, Sprite sprite, float xMin, float xMax,
                                   PhysicsMaterial2D material, bool ice = false)
    {
        var go = new GameObject(ice ? "IceFloor" : "Floor");
        go.transform.SetParent(parent, false);
        go.layer = GroundLayerIndex;
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, -1f, 0f);
        go.transform.localScale = new Vector3(xMax - xMin, 2f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = ice ? IceColor : FloorColor;
        sr.sortingOrder = 0;

        var box = go.AddComponent<BoxCollider2D>();
        if (material != null) box.sharedMaterial = material;
        if (ice) go.AddComponent<IceSurface>();

        return go;
    }

    static GameObject CreateCamera(Vector3 position)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = position;

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
}
