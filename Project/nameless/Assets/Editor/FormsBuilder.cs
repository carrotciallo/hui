using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// 生成 P5 形态测试场景：每行一个形态（大炮 / 转换器 / 绳 / 岩浆 / 空 + 墙中钥匙）。
public static class FormsBuilder
{
    const string ScenePath = "Assets/Scenes/Forms.unity";
    const string PlayerMaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";

    const int GroundLayerIndex = 6;
    const int PlayerLayerIndex = 7;
    const int HazardLayerIndex = 8;
    const int InteractableLayerIndex = 9;

    const float RowWidth = 26f;
    const int RowCount = 5;
    const float SpawnY = GameConstants.PlayerSize * 0.5f + 0.02f;

    static readonly Color FloorColor = new Color(0.30f, 0.33f, 0.40f);
    static readonly Color RopeColor = new Color(0.3f, 0.85f, 0.4f, 0.85f);
    static readonly Color LavaColor = new Color(1f, 0.3f, 0.1f, 0.55f);
    static readonly Color WallColor = new Color(0.55f, 0.55f, 0.62f);
    static readonly Color KeyColor = new Color(1f, 0.8f, 0.2f, 1f);

    [MenuItem("GameJam/Build Forms Test Scene")]
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

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Forms").transform;

        for (int row = 0; row < RowCount; row++)
        {
            float start = row * RowWidth;

            if (row == 3)
            {
                // 岩浆行：地面留 6 格缺口，木筏形态浮过去
                CreateGround(root, white, start - 2f, start + 8f);
                CreateGround(root, white, start + 14f, start + 24f);
                CreateLava(root, white, start + 8f, start + 14f);
            }
            else
            {
                CreateGround(root, white, start - 2f, start + 24f);
            }

            PlaceRow(root, white, row, start);
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

        Debug.Log("[Forms] 形态测试场景已生成：" +
                  "大炮 / 转换器 / 绳 / 岩浆 / 空形态各一排。" +
                  "A 按 1–6 选形态；B 站在 A 上按「下」进大炮，按冲刺射出。");
    }

    static void PlaceRow(Transform root, Sprite white, int row, float start)
    {
        switch (row)
        {
            case 0:
                // 大炮：B 跳上 A，按「下」进入，再按冲刺射出
                break;

            case 1:
                // 转换器：B 以 v1+ 撞向 A 的侧面，速度转 90°
                break;

            case 2:
                CreateRope(root, white, new Vector2(start + 13f, 1.5f));
                break;

            case 3:
                // 岩浆缺口在 CreateGround / CreateLava 里铺好
                break;

            case 4:
                CreateWall(root, white, new Vector2(start + 13f, 2f));
                CreateKey(root, white, new Vector2(start + 15.5f, 1f));
                break;
        }
    }

    static GameObject CreateGround(Transform parent, Sprite sprite, float xMin, float xMax)
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(parent, false);
        go.layer = GroundLayerIndex;
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, -1f, 0f);
        go.transform.localScale = new Vector3(xMax - xMin, 2f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = FloorColor;
        sr.sortingOrder = 0;

        go.AddComponent<BoxCollider2D>();
        return go;
    }

    static void CreateLava(Transform parent, Sprite sprite, float xMin, float xMax)
    {
        var go = new GameObject("Lava");
        go.transform.SetParent(parent, false);
        go.layer = HazardLayerIndex;
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, -1f, 0f);
        go.transform.localScale = new Vector3(xMax - xMin, 2f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = LavaColor;
        sr.sortingOrder = 1;

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<Lava>();
    }

    static void CreateRope(Transform parent, Sprite sprite, Vector2 center)
    {
        var go = new GameObject("VerticalRope");
        go.transform.SetParent(parent, false);
        go.layer = InteractableLayerIndex;
        go.transform.position = center;
        go.transform.localScale = new Vector3(0.5f, 3f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = RopeColor;
        sr.sortingOrder = 4;

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<VerticalRope>();
    }

    static void CreateWall(Transform parent, Sprite sprite, Vector2 center)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.layer = GroundLayerIndex;
        go.transform.position = center;
        go.transform.localScale = new Vector3(1f, 4f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = WallColor;
        sr.sortingOrder = 2;

        go.AddComponent<BoxCollider2D>();
    }

    static void CreateKey(Transform parent, Sprite sprite, Vector2 position)
    {
        var go = new GameObject("KeyPickup");
        go.transform.SetParent(parent, false);
        go.layer = InteractableLayerIndex;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = KeyColor;
        sr.sortingOrder = 6;

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<KeyPickup>();
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
