using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// World.unity 的关卡编辑器：按 1 格网格刷地形、摆 prefab。
/// 手工修改会被 GameJam → Build World Scene 覆盖，改完记得保存并提交场景。
public sealed class LevelEditorWindow : EditorWindow
{
    const string WorldScenePath = "Assets/Scenes/World.unity";
    const string WorldTilePath = "Assets/Art/Greybox/WorldTile.asset";
    static readonly string[] PrefabDirs = { "Assets/Prefabs/Materials", "Assets/Prefabs/Markers" };

    enum Brush
    {
        Paint,
        Erase,
        Place
    }

    World world;
    Region[] regions = System.Array.Empty<Region>();
    Tilemap tilemap;
    Tile tile;
    readonly List<GameObject> prefabs = new List<GameObject>();

    int regionSlot;
    Brush brush = Brush.Paint;
    int prefabSlot;
    float yOffset;
    bool drawInScene = true;
    bool limitToRegion = true;
    Vector2 prefabScroll;

    bool gestureActive;
    Vector3Int lastCell = new Vector3Int(int.MinValue, int.MinValue, 0);

    [MenuItem("GameJam/Level Editor")]
    static void Open()
    {
        var window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(320f, 360f);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyChanged += Refresh;
        Undo.undoRedoPerformed += Repaint;
        Refresh();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.hierarchyChanged -= Refresh;
        Undo.undoRedoPerformed -= Repaint;
    }

    void Refresh()
    {
        world = Object.FindAnyObjectByType<World>();
        tilemap = FindWorldTilemap();
        tile = AssetDatabase.LoadAssetAtPath<Tile>(WorldTilePath);

        var found = new List<Region>();
        if (world != null)
        {
            foreach (var region in world.Regions)
            {
                if (region != null) found.Add(region);
            }
        }

        found.Sort((a, b) => a.Index.CompareTo(b.Index));
        regions = found.ToArray();
        regionSlot = Mathf.Clamp(regionSlot, 0, Mathf.Max(0, regions.Length - 1));

        EnsurePrefabs();
        Repaint();
    }

    void EnsurePrefabs()
    {
        if (prefabs.Count > 0) return;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabDirs))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
        }

        prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        prefabSlot = Mathf.Clamp(prefabSlot, 0, Mathf.Max(0, prefabs.Count - 1));
    }

    static Tilemap FindWorldTilemap()
    {
        var terrain = GameObject.Find("Terrain");
        if (terrain != null)
        {
            var ground = terrain.transform.Find("Ground");
            if (ground != null)
            {
                var map = ground.GetComponent<Tilemap>();
                if (map != null) return map;
            }
        }

        return Object.FindAnyObjectByType<Tilemap>();
    }

    void OnGUI()
    {
        if (world == null)
        {
            EditorGUILayout.HelpBox($"没有打开 World 场景，或场景里没有 World 组件。\n{WorldScenePath}",
                                    MessageType.Warning);
            if (GUILayout.Button("打开 World 场景")) OpenWorldScene();
            return;
        }

        EditorGUILayout.HelpBox("手工修改会被 GameJam → Build World Scene 覆盖。改完保存场景再提交。",
                                MessageType.Info);

        DrawRegionSelector();
        EditorGUILayout.Space();

        DrawBrushSelector();
        EditorGUILayout.Space();

        if (brush == Brush.Place) DrawPalette();

        EditorGUILayout.Space();
        drawInScene = EditorGUILayout.ToggleLeft("在 Scene 视图绘制", drawInScene);
        limitToRegion = EditorGUILayout.ToggleLeft("限制在当前区域", limitToRegion);

        if (drawInScene)
        {
            EditorGUILayout.HelpBox("左键绘制/摆放，按住左键拖动可连续刷地形。", MessageType.None);
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("跳转区域")) JumpToRegion();
            if (GUILayout.Button("校验摆放")) Validate();
        }

        if (tilemap == null)
        {
            EditorGUILayout.HelpBox("场景里找不到 Ground Tilemap，地形笔刷不可用。", MessageType.Warning);
        }
        else if (tile == null)
        {
            EditorGUILayout.HelpBox($"缺少 {WorldTilePath}，先跑一次 Build World Scene。", MessageType.Warning);
        }
    }

    void DrawRegionSelector()
    {
        if (regions.Length == 0)
        {
            EditorGUILayout.HelpBox("世界还没有区域（Region）。", MessageType.Warning);
            return;
        }

        var labels = new string[regions.Length];
        for (int i = 0; i < regions.Length; i++) labels[i] = regions[i].Label;

        regionSlot = EditorGUILayout.Popup("区域", regionSlot, labels);
    }

    void DrawBrushSelector()
    {
        brush = (Brush)GUILayout.Toolbar((int)brush, new[] { "铺地形", "擦地形", "摆物件" });
    }

    void DrawPalette()
    {
        if (prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("没有可用 prefab，先跑 GameJam → Build Materials Test Scene。",
                                    MessageType.Warning);
            return;
        }

        var labels = new string[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
        {
            labels[i] = prefabs[i].name;
            if (AssetDatabase.GetAssetPath(prefabs[i]).Contains("/Markers/")) labels[i] += "（标记）";
        }

        prefabScroll = EditorGUILayout.BeginScrollView(prefabScroll, GUILayout.Height(140f));
        prefabSlot = GUILayout.SelectionGrid(prefabSlot, labels, 1);
        EditorGUILayout.EndScrollView();

        yOffset = EditorGUILayout.FloatField("Y 偏移", yOffset);
    }

    void OpenWorldScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath) == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(WorldScenePath);
        Refresh();
    }

    Region CurrentRegion =>
        regions.Length > 0 ? regions[Mathf.Clamp(regionSlot, 0, regions.Length - 1)] : null;

    void JumpToRegion()
    {
        var region = CurrentRegion;
        var view = SceneView.lastActiveSceneView;
        if (region == null || view == null) return;

        var area = region.Area;
        var center = new Vector3(area.center.x, area.center.y, 0f);
        var size = new Vector3(Mathf.Max(area.width, 8f), Mathf.Max(area.height, 12f), 10f);
        view.Frame(new Bounds(center, size), false);
        view.Repaint();
    }

    void OnSceneGUI(SceneView view)
    {
        if (!drawInScene || world == null || tilemap == null) return;

        var event_ = Event.current;
        Vector2 guiPosition = event_.mousePosition;
        Vector3 point = ScenePoint(guiPosition);
        Vector3Int cell = tilemap.WorldToCell(point);
        Vector3 center = tilemap.GetCellCenterWorld(cell);
        bool inRegion = !limitToRegion || IsInsideCurrentRegion(center);

        Handles.color = !inRegion
            ? new Color(1f, 1f, 1f, 0.25f)
            : brush == Brush.Erase ? new Color(1f, 0.35f, 0.3f, 0.9f)
            : brush == Brush.Place ? new Color(0.4f, 0.9f, 1f, 0.9f)
            : new Color(1f, 0.85f, 0.3f, 0.9f);
        Handles.DrawWireCube(center, Vector3.one);

        switch (event_.type)
        {
            case EventType.MouseDown when event_.button == 0:
                if (!inRegion) return;

                if (brush != Brush.Place)
                {
                    Undo.RegisterCompleteObjectUndo(tilemap, "Level Editor 地形");
                    lastCell = new Vector3Int(int.MinValue, int.MinValue, 0);
                }

                Apply(cell, center);
                gestureActive = brush != Brush.Place;
                event_.Use();
                break;

            case EventType.MouseDrag when event_.button == 0 && gestureActive:
                if (!inRegion) return;
                Apply(cell, center);
                event_.Use();
                break;

            case EventType.MouseUp when event_.button == 0:
                if (gestureActive)
                {
                    gestureActive = false;
                    MarkSceneDirty();
                }
                break;
        }
    }

    static Vector3 ScenePoint(Vector2 guiPosition)
    {
        var ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    bool IsInsideCurrentRegion(Vector3 position)
    {
        var region = CurrentRegion;
        return region == null || region.ContainsX(position.x);
    }

    void Apply(Vector3Int cell, Vector3 center)
    {
        if (brush == Brush.Place)
        {
            PlacePrefab(center);
            return;
        }

        if (cell == lastCell) return;
        lastCell = cell;

        tilemap.SetTile(cell, brush == Brush.Paint ? tile : null);
        EditorUtility.SetDirty(tilemap);
        MarkSceneDirty();
    }

    void PlacePrefab(Vector3 center)
    {
        if (prefabs.Count == 0) return;

        var prefab = prefabs[Mathf.Clamp(prefabSlot, 0, prefabs.Count - 1)];
        var region = CurrentRegion;
        Transform parent = region != null ? region.transform : world.transform;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = new Vector3(center.x, center.y + yOffset, 0f);
        instance.name = prefab.name;

        Undo.RegisterCreatedObjectUndo(instance, "Level Editor 放置 " + prefab.name);
        Selection.activeGameObject = instance;
        MarkSceneDirty();
    }

    void MarkSceneDirty()
    {
        if (tilemap != null) EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        Repaint();
    }

    void Validate()
    {
        var issues = new List<string>();

        if (regions.Length == 0)
        {
            issues.Add("世界没有任何区域。");
        }

        foreach (var region in regions)
        {
            if (region.Spawn == null)
            {
                issues.Add($"{region.Label} 缺少 Spawn。");
                continue;
            }

            if (!region.ContainsX(region.SpawnPosition.x))
            {
                issues.Add($"{region.Label} 的 Spawn 在区域范围外：x = {region.SpawnPosition.x:0.##}。");
            }

            foreach (var child in region.GetComponentsInChildren<Transform>(true))
            {
                if (child == region.transform) continue;

                Vector3 position = child.position;
                if (position.x < world.Bounds.xMin || position.x > world.Bounds.xMax ||
                    position.y < world.Bounds.yMin || position.y > world.Bounds.yMax)
                {
                    issues.Add($"{region.Label} 的 {child.name} 在世界边界外：{position}。");
                }
            }
        }

        if (issues.Count == 0)
        {
            Debug.Log($"[Level Editor] 校验通过：{regions.Length} 个区域，摆放都在世界边界内。", world);
            return;
        }

        foreach (var issue in issues) Debug.LogWarning($"[Level Editor] {issue}", world);
    }
}
