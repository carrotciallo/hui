#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 材质测试场 2.0：把调试台的场地（可数格子的台阶 + 我们的玩家）
/// 和仓库里的全部场景物件放在同一个场景里，方便对着新物理逐个验。
///
/// 菜单：GameJam → Build Material Lab
/// 参数全部来自 Resources/GameTuning.asset，改一处这里也跟着变。
/// </summary>
public static class MaterialLabBuilder
{
    const string ScenePath = "Assets/Scenes/MaterialLab.unity";

    static Sprite white;
    static PhysicsMaterial2D slick;

    [MenuItem("GameJam/Build Material Lab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        white = GreyboxAssets.EnsureWhiteSprite();
        slick = GreyboxAssets.EnsurePhysicsMaterial(
            "Assets/Settings/MaterialLabFrictionless.physicsMaterial2D", "MaterialLabFrictionless", 0f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("MaterialLab").transform;

        BuildFloor(root);
        BuildDropStairs(root);
        BuildMaterials(root);
        var cam = BuildCamera();

        Vector2 spawnY = Vector2.up * (GameConstants.PlayerSize * 0.5f + 0.05f);
        const int PlayerLayerIndex = 7;
        GreyboxAssets.EnsureLayer(PlayerLayerIndex, "Player");
        GreyboxAssets.CreatePlayer("A", new Vector2(-9f, spawnY.y), new Color(0.36f, 0.62f, 1f),
                                   PlayerMotor.Scheme.WASD, white, slick, white, PlayerLayerIndex);
        GreyboxAssets.CreatePlayer("B", new Vector2(-8f, spawnY.y), new Color(1f, 0.45f, 0.4f),
                                   PlayerMotor.Scheme.Arrows, white, slick, white, PlayerLayerIndex);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        AssetDatabase.Refresh();
        Debug.Log("[MaterialLab] 已生成 " + ScenePath + "（A: WASD + 空格 + 左Shift，B: 方向键 + / + .）");
    }

    // ------------------------------------------------------------------
    // 场地
    // ------------------------------------------------------------------

    static void BuildFloor(Transform root)
    {
        Box(root, "Floor", new Vector2(9f, -0.5f), new Vector2(40f, 1f), new Color(0.24f, 0.26f, 0.32f));
    }

    /// <summary>可数格子的台阶：往左走从每一级直接掉到地面，落差 = 该级高度。</summary>
    static void BuildDropStairs(Transform root)
    {
        for (int i = 1; i <= 5; i++)
        {
            float top = i * 2f;
            Box(root, "Step_" + top, new Vector2(-14f + i * 2f, top - 0.5f),
                new Vector2(2f, 1f), new Color(0.30f, 0.33f, 0.40f));
        }
    }

    // ------------------------------------------------------------------
    // 物件
    // ------------------------------------------------------------------

    static void BuildMaterials(Transform root)
    {
        // 竖绳：v2 穿过，v3 以上弹回
        var rope = Box(root, "VerticalRope", new Vector2(2f, 2f), new Vector2(0.4f, 4f),
                       new Color(0.85f, 0.75f, 0.45f));
        rope.AddComponent<VerticalRope>();

        // 地面绳：单向平台，v2 从上踩穿，v3 弹起
        var ground = Box(root, "GroundRope", new Vector2(5f, 1.2f), new Vector2(3f, 0.25f),
                         new Color(0.85f, 0.75f, 0.45f));
        ground.AddComponent<GroundRope>();

        // 冰面：低摩擦地面 + 标记
        var ice = Box(root, "IceSurface", new Vector2(9f, 0.5f), new Vector2(4f, 0.4f),
                      new Color(0.6f, 0.85f, 1f));
        ice.AddComponent<IceSurface>();

        // 冰面板：v2 撞碎
        Box(root, "IcePanel", new Vector2(12f, 1f), new Vector2(1f, 1f),
            new Color(0.6f, 0.9f, 1f)).AddComponent<IcePanel>();

        // 可破坏墙：v2 撞碎
        Box(root, "BreakableWall", new Vector2(14f, 1f), new Vector2(1f, 2f),
            new Color(0.75f, 0.45f, 0.35f)).AddComponent<BreakableWall>();

        // 钥匙 + 过关门
        var key = Box(root, "KeyPickup", new Vector2(16f, 1.4f), new Vector2(0.5f, 0.5f),
                      new Color(1f, 0.9f, 0.3f), trigger: true);
        key.AddComponent<KeyPickup>();

        Box(root, "KeyGate", new Vector2(18f, 1f), new Vector2(0.3f, 2f),
            new Color(0.32f, 0.33f, 0.36f)).AddComponent<KeyGate>();

        // 岩浆
        Box(root, "Lava", new Vector2(21f, -0.1f), new Vector2(3f, 0.6f),
            new Color(0.95f, 0.35f, 0.15f), trigger: true).AddComponent<Lava>();

        // 致死区
        Box(root, "KillZone", new Vector2(25f, 1f), new Vector2(1f, 1f),
            new Color(0.9f, 0.2f, 0.2f), trigger: true).AddComponent<KillZone>();

        // 重生点
        Box(root, "RespawnPoint", new Vector2(27f, 1.2f), new Vector2(1f, 1f),
            new Color(0.4f, 0.9f, 0.5f), trigger: true).AddComponent<RespawnPoint>();

        // 传送门
        Box(root, "Portal", new Vector2(30f, 1.2f), new Vector2(1.6f, 2f),
            new Color(0.7f, 0.5f, 1f), trigger: true).AddComponent<Portal>();
    }

    // ------------------------------------------------------------------

    static GameObject Box(Transform root, string name, Vector2 center, Vector2 size, Color color,
                          bool trigger = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.color = color;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.sharedMaterial = slick;
        col.isTrigger = trigger;

        return go;
    }

    static Camera BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = new Vector3(6f, 4f, -10f);

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.75f;                 // 13.5 格
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        go.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData();

        var hud = go.AddComponent<DebugHud>();
        return cam;
    }
}
#endif
