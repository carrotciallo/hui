#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tuning Lab 关卡编辑器。照着 World 的 LevelEditorWindow 做，但编辑对象换成了
/// 从 Tuning Lab 派生的关卡场景（TuningLab + LabLevel），规则、相机、玩家全部跑调试台同一套脚本。
///
/// 地形直接写 <see cref="LabLevelData"/> 的格集合，渲染/碰撞由 LabLevel 按矩形合并生成。
/// 不碰 Tilemap，也不依赖 World.unity 那一套还没对齐的规范。
/// </summary>
public sealed class LabLevelEditorWindow : EditorWindow
{
    enum Tool
    {
        Paint = 0,
        Erase = 1,
        Rect = 2,
        Rope = 3,
        HorizontalRope = 4,
        SpawnA = 5,
        SpawnB = 6,
        Partition = 7,
        Key = 8,
        Door = 9,
        Spike = 10,
        Ice = 11,
        Magma = 12,
        RespawnPoint = 13,
        TeleportPoint = 14,
        Marquee = 15,
        Move = 16,
        Recall = 17
    }

    /// <summary>下一次拉选区时怎么处理当前选区。</summary>
    enum MarqueeMode
    {
        Replace = 0,
        Add = 1,
        Subtract = 2
    }

    /// <summary>选择工具点中的东西。</summary>
    enum PickKind
    {
        None = 0,
        Solid = 1,
        Rope = 2,
        Key = 3,
        Door = 4,
        Spike = 5,
        Ice = 6,
        Magma = 7,
        RespawnPoint = 8,
        TeleportPoint = 9,
        Recall = 10
    }

    struct Pick
    {
        public PickKind kind;
        public int index;
    }

    const int EditMargin = 8;                 // 相机范围外还能画多少格
    const int MaxRopeLength = 32;
    const float RopeStripWidth = 6f / 16f;    // 和 LabVerticalRope 一致

    static readonly string[] ToolLabels =
    {
        "铺地形", "擦地形", "矩形", "竖绳", "横绳", "出生点 A", "出生点 B", "关卡分区", "钥匙", "门", "地刺", "冰块", "岩浆块", "重生点", "传送点",
        "选区", "选择", "召回道具"
    };

    static readonly string[] MarqueeModeLabels = { "重新选择", "增加选区", "删除选区" };

    static readonly string[] SpikeDirLabels = { "朝上", "朝下", "朝左", "朝右" };
    static readonly LabSpikeDir[] SpikeDirs =
    {
        LabSpikeDir.Up, LabSpikeDir.Down, LabSpikeDir.Left, LabSpikeDir.Right
    };

    LabLevel level;
    TuningLab lab;
    Scene cachedScene;

    Tool tool = Tool.Paint;
    int ropeLength = 4;
    int ropeChainId;
    Color newDoorColor = LabDoor.DefaultClosedColor;
    LabSpikeDir newSpikeDir = LabSpikeDir.Up;
    int iceGroupId = 1;
    int magmaGroupId = 1;
    bool showGrid = true;
    bool showSpawns = true;
    bool showCameraFrames = true;
    Vector2 windowScroll;
    Vector2 ropeScroll;
    Vector2 keyDoorScroll;
    Vector2 spikeScroll;
    Vector2 iceScroll;
    Vector2 magmaScroll;
    Vector2 respawnScroll;
    Vector2 teleportScroll;
    Vector2 recallScroll;

    bool gesture;
    Vector2Int rectStart;
    Vector2Int rectEnd;
    Vector2Int lastCell;

    // 连续摆放：拖动时每个经过的格子都按第一次的"放/删"语义执行。
    bool dragAdds;

    // 选区：同一时间只有一个，用互不重叠的矩形块拼出任意形状；增加选区走并集。
    MarqueeMode marqueeMode = MarqueeMode.Replace;
    readonly List<RectInt> selection = new List<RectInt>();
    bool marqueeDragging;
    Vector2Int marqueeStart;
    Vector2Int marqueeEnd;

    // 选择 / 拖动
    Pick picked;
    bool movingContent;
    bool movingSelection;
    LabLevelData.ContentSelection moveCapture;
    Vector2Int moveLastCell;

    int partitionIndex = -1;
    bool movePartitionContents = true;
    string partitionMessage;
    bool partitionCreating;
    int partitionDragIndex = -1;
    Vector2Int partitionDragLast;
    Vector2Int partitionCreateStart;
    Vector2Int partitionCreateEnd;

    bool validated;
    readonly List<string> issues = new List<string>();

    [MenuItem("GameJam/Tuning Lab Level Editor")]
    public static void Open()
    {
        var window = GetWindow<LabLevelEditorWindow>("Tuning Lab Level Editor");
        window.minSize = new Vector2(340f, 500f);
        window.Show();
        window.Refresh();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        Refresh();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnHierarchyChanged()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        RefreshIfNeeded();
    }

    void OnUndoRedo()
    {
        if (level != null && !Application.isPlaying)
        {
            level.RebuildPreview();
            validated = false;
        }
        Repaint();
    }

    // ------------------------------------------------------------------
    // 场景绑定
    // ------------------------------------------------------------------

    void Refresh()
    {
        LabLevel previous = level;
        cachedScene = SceneManager.GetActiveScene();
        level = FindInScene<LabLevel>(cachedScene);
        lab = FindInScene<TuningLab>(cachedScene);

        if (level == null && lab != null)
            level = lab.GetComponent<LabLevel>();

        if (level != null && lab == null)
            lab = level.GetComponent<TuningLab>();

        // 手工搭的关卡场景可能忘了把引用连上；这里补一次，Play 时 TuningLab 也会自己找。
        if (level != null && lab != null && lab.level == null)
        {
            lab.level = level;
            EditorUtility.SetDirty(lab);
        }

        // 打开/切到关卡场景时把预览补回来（场景创建后预览是故意清掉的，不写进文件）。
        if (level != null && level != previous && !Application.isPlaying)
            level.RebuildPreview();

        int count = level != null && level.Data.partitions != null ? level.Data.partitions.Count : 0;
        if (count == 0) partitionIndex = -1;
        else if (partitionIndex >= count) partitionIndex = count - 1;

        Repaint();
    }

    void RefreshIfNeeded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active != cachedScene || level == null) Refresh();
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // 窗口 UI
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Play 模式下不能编辑关卡。退出 Play 再改。", MessageType.Info);
            return;
        }

        RefreshIfNeeded();

        if (level == null)
        {
            DrawOnboarding();
            return;
        }

        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        DrawHeader();
        EditorGUILayout.Space();
        DrawTools();
        EditorGUILayout.Space();
        DrawSelection();
        EditorGUILayout.Space();
        DrawTerrain();
        EditorGUILayout.Space();
        DrawPartitions();
        EditorGUILayout.Space();
        DrawRopes();
        EditorGUILayout.Space();
        DrawKeyDoors();
        EditorGUILayout.Space();
        DrawRecalls();
        EditorGUILayout.Space();
        DrawSpikes();
        EditorGUILayout.Space();
        DrawIces();
        EditorGUILayout.Space();
        DrawMagmas();
        EditorGUILayout.Space();
        DrawRespawnPoints();
        EditorGUILayout.Space();
        DrawTeleports();
        EditorGUILayout.Space();
        DrawSpawns();
        EditorGUILayout.Space();
        DrawBounds();
        EditorGUILayout.Space();
        DrawCamera();
        EditorGUILayout.Space();
        DrawActions();
        DrawValidation();

        EditorGUILayout.EndScrollView();
    }

    void DrawOnboarding()
    {
        EditorGUILayout.LabelField("Tuning Lab 关卡编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "当前场景没有 LabLevel 数据。\n" +
            "关卡场景必须从 Tuning Lab 派生，不能直接改 TuningLab.unity（那是基准场景）。",
            MessageType.Warning);

        if (GUILayout.Button("从 Tuning Lab 新建关卡场景", GUILayout.Height(32f)))
        {
            LabLevelScene.CreateInteractive();
            Refresh();
        }

        if (GUILayout.Button("把当前场景转成关卡（仅限 Tuning Lab 副本）"))
        {
            LabLevelScene.ConvertCurrentInteractive();
            Refresh();
        }

        if (GUILayout.Button("打开 Tuning Lab 基准场景"))
            LabLevelScene.OpenLab();

        EditorGUILayout.Space();
        DrawExistingLevels();
    }

    void DrawExistingLevels()
    {
        if (!AssetDatabase.IsValidFolder(LabLevelScene.LevelFolder)) return;

        var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { LabLevelScene.LevelFolder });
        if (guids.Length == 0) return;

        var paths = new List<string>();
        foreach (var guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal);

        EditorGUILayout.LabelField("已有派生关卡", EditorStyles.boldLabel);
        foreach (var path in paths)
        {
            if (GUILayout.Button(Path.GetFileNameWithoutExtension(path)))
                LabLevelScene.OpenScene(path);
        }
    }

    void DrawHeader()
    {
        EditorGUILayout.LabelField(SceneManager.GetActiveScene().name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("关卡数据来自 Tuning Lab 基准（CaptureBaseline）", EditorStyles.miniLabel);

        if (lab == null)
        {
            EditorGUILayout.HelpBox("场景里没有 TuningLab，Play 时不会生成场地。", MessageType.Error);
            return;
        }

        if (lab.level != level)
        {
            EditorGUILayout.HelpBox("TuningLab 没有指向这个 LabLevel。", MessageType.Warning);
            if (GUILayout.Button("绑定到当前 LabLevel"))
            {
                Undo.RecordObject(lab, "绑定关卡数据");
                lab.level = level;
                EditorUtility.SetDirty(lab);
                MarkSceneDirty();
            }
        }

        if (!lab.autoBuild)
        {
            EditorGUILayout.HelpBox("TuningLab.autoBuild 关着，Play 不会生成场地（这是烘焙场景的遗留状态）。",
                                    MessageType.Warning);
            if (GUILayout.Button("打开 autoBuild"))
            {
                Undo.RecordObject(lab, "打开 autoBuild");
                lab.autoBuild = true;
                EditorUtility.SetDirty(lab);
                MarkSceneDirty();
            }
        }

        if (!Mathf.Approximately(lab.roomScale, 1f))
        {
            EditorGUILayout.HelpBox("TuningLab.roomScale ≠ 1。有 LabLevel 时场地完全按关卡数据生成，roomScale 不生效。",
                                    MessageType.Info);
        }
    }

    void DrawTools()
    {
        EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
        tool = (Tool)GUILayout.SelectionGrid((int)tool, ToolLabels, 3);

        switch (tool)
        {
            case Tool.Paint:
                EditorGUILayout.HelpBox("左键拖动铺 1 格实心地形。", MessageType.None);
                break;
            case Tool.Erase:
                EditorGUILayout.HelpBox("左键拖动擦掉实心格。", MessageType.None);
                break;
            case Tool.Rect:
                EditorGUILayout.HelpBox("左键拖出一个矩形，松手后填充实心格。", MessageType.None);
                break;
            case Tool.Rope:
                EditorGUILayout.HelpBox("左键在格子底部放一根竖绳，向上长。", MessageType.None);
                break;
            case Tool.HorizontalRope:
                EditorGUILayout.HelpBox("左键沿格子顶部放一根横绳，向右长。", MessageType.None);
                break;
            case Tool.SpawnA:
                EditorGUILayout.HelpBox("左键设置 A 的落点（脚底）。必须在相机范围内。", MessageType.None);
                break;
            case Tool.SpawnB:
                EditorGUILayout.HelpBox("左键设置 B 的落点（脚底）。必须在相机范围内。", MessageType.None);
                break;
            case Tool.Partition:
                EditorGUILayout.HelpBox(
                    "左键空白处拖出新分区；在分区内部拖动可移动分区（默认带上里面的地形/绳/出生点）。\n" +
                    "改名 / 调大小 / 删除在下面的「关卡分区」面板里。",
                    MessageType.None);
                break;
            case Tool.Key:
                EditorGUILayout.HelpBox("左键在一个格子上放一把钥匙（1×1，拾取后消失）。", MessageType.None);
                break;
            case Tool.Recall:
                EditorGUILayout.HelpBox(
                    "左键放一个 1×1 召回道具；同一格再点删除。任一玩家拾取后全队永久解锁召回，\n" +
                    "地图重置不会刷新道具。A 按 Q、B 按右 Shift，把另一名玩家召回到头顶并留下幻影。",
                    MessageType.None);
                break;
            case Tool.Door:
                EditorGUILayout.HelpBox(
                    "左键点门最下面那一格，门向上占 3 格（1×3）。任意一把钥匙能开任意一扇门，\n" +
                    "颜色只用于区分，在下面的「钥匙 / 门」面板里调。",
                    MessageType.None);
                break;
            case Tool.Spike:
                EditorGUILayout.HelpBox(
                    "左键点刺所在的那一格（不能是实心格）；换个方向再点 = 改向，同方向再点 = 删掉这根刺。" +
                    "「擦地形」把该格的地形和刺一起擦掉。\n" +
                    "朝上 = 立在地板顶上的地刺；朝下 = 吊在天花板下的倒刺；朝左 / 朝右 = 贴墙的侧刺。" +
                    "刺的底座必须有实心格，悬空的会在「校验」里列出来。",
                    MessageType.None);
                int spikeDirIndex = System.Array.IndexOf(SpikeDirs, newSpikeDir);
                spikeDirIndex = GUILayout.SelectionGrid(spikeDirIndex, SpikeDirLabels, 4);
                newSpikeDir = SpikeDirs[Mathf.Clamp(spikeDirIndex, 0, SpikeDirs.Length - 1)];
                break;
            case Tool.Ice:
                EditorGUILayout.HelpBox(
                    "左键放一块 1×1 冰块，按住拖可以连续铺。同组冰块共享 v2 整组破坏；\n" +
                    "冰面滑行（含 v2+）不算撞击，不会碎冰；冰面已并入地形合并碰撞，滑行不卡脚。\n" +
                    "红热边接触只融一块。同格再点 = 删除，换组号再点 = 改组。\n" +
                    "0 也是普通组；「新建组」会给一个当前最大组号 + 1 的新组。",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    iceGroupId = EditorGUILayout.IntField("组号", iceGroupId);
                    if (GUILayout.Button("新建组", GUILayout.Width(64f)))
                        iceGroupId = level.Data.NextIceGroupId();
                }
                break;
            case Tool.Magma:
                EditorGUILayout.HelpBox(
                    "左键放一块 1×1 岩浆块，按住拖可以连续铺。每个接触面只在第一次接触时强制 A 的对应边热二；\n" +
                    "已经热二的边再接触直接死亡。v2 及以上撞上来时，撞击热 + 强制热也会死亡；v3 撞击整组破坏。\n" +
                    "在上面走动和普通地面相同。同格再点 = 删除，换组号再点 = 改组。\n" +
                    "0 也是普通组；「新建组」会给一个当前最大组号 + 1 的新组。",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    magmaGroupId = EditorGUILayout.IntField("组号", magmaGroupId);
                    if (GUILayout.Button("新建组", GUILayout.Width(64f)))
                        magmaGroupId = level.Data.NextMagmaGroupId();
                }
                break;
            case Tool.RespawnPoint:
                EditorGUILayout.HelpBox(
                    "左键放置 3×3 重生点；同一格再次点击删除。重生点由 LabLevel 数据生成，" +
                    "不直接编辑场景生成物。",
                    MessageType.None);
                break;
            case Tool.TeleportPoint:
                EditorGUILayout.HelpBox(
                    "左键放置 3×3 传送点；同一格再次点击删除。运行时双人进入后标记，已标记点互为终点：\n" +
                    "Play 中两人同时进入后按冲刺键，A/D 选终点、Enter 确认。",
                    MessageType.None);
                break;

            case Tool.Marquee:
                EditorGUILayout.HelpBox(
                    "左键拖出一个矩形选区，起点和对角就是它的两个顶点。同一时间只有一个选区。\n" +
                    "下一次拉选区怎么处理它：重新选择（默认，换掉旧的）/ 增加选区（并进去，可以拼出不规则形状）/ " +
                    "删除选区（从选区里挖掉这块）。",
                    MessageType.None);
                marqueeMode = (MarqueeMode)GUILayout.SelectionGrid((int)marqueeMode, MarqueeModeLabels, 3);
                break;

            case Tool.Move:
                EditorGUILayout.HelpBox(
                    "左键点中物件后拖动 = 移动那个物件。只要场景里有选区，不管鼠标从哪里开始拖，" +
                    "都改成拖动选区框和里面的全部内容；框扫过、但原本不在选区里的物件不会被带走。\n" +
                    "删除用下面「选区 / 选择」面板里的按钮。移动和删除都能撤销。",
                    MessageType.None);
                break;

        }

        if (GUILayout.Button("跳到关卡")) FrameLevel();
        showGrid = EditorGUILayout.ToggleLeft("显示网格", showGrid);
        EditorGUILayout.LabelField("快捷键：1 铺地形 / 3 擦地形 / 4 选区 / 5 选择（禁止新增）",
            EditorStyles.miniLabel);
    }

    void DrawSelection()
    {
        EditorGUILayout.LabelField("选区 / 选择", EditorStyles.boldLabel);

        marqueeMode = (MarqueeMode)GUILayout.SelectionGrid((int)marqueeMode, MarqueeModeLabels, 3);

        if (selection.Count == 0)
        {
            EditorGUILayout.LabelField("（没有选区）");
        }
        else
        {
            RectInt bounds = SelectionBounds(selection);
            EditorGUILayout.LabelField(
                string.Format("当前选区：{0} 格，包围盒 {1}×{2} @ ({3}, {4})",
                    SelectionArea(selection), bounds.width, bounds.height, bounds.x, bounds.y));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                if (GUILayout.Button("清空选区")) selection.Clear();
                if (GUILayout.Button("删除选区内容")) DeleteSelection();
            }
        }

        EditorGUILayout.LabelField("选中", picked.kind == PickKind.None ? "（无）" : PickLabel(picked));

        EditorGUILayout.HelpBox(
            "同一时间只有一个选区，它只是编辑器的状态，不存进关卡数据。\n" +
            "拖动时选区框和里面的物件一起移动；删除作用在选区覆盖的物件上。\n" +
            "增加选区会把新拖出的矩形并进当前选区，所以选区可以是不规则形状。\n" +
            "地形格、绳、钥匙、门、地刺、冰块、岩浆块、重生点、传送点都算物件；出生点和关卡分区不算。",
            MessageType.None);
    }

    void DrawTerrain()
    {
        EditorGUILayout.LabelField("地形", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("实心格", level.SolidCellCount.ToString());
        EditorGUILayout.LabelField("合并后碰撞矩形", level.MergedRectCount.ToString());
        EditorGUILayout.LabelField("可选范围", "相机范围外扩 " + EditMargin + " 格");

        if (GUILayout.Button("清空地形"))
        {
            if (!EditorUtility.DisplayDialog("清空地形", "删掉所有实心格？绳和出生点保留。", "清空", "取消")) return;
            Record("清空地形");
            level.Data.ClearCells();
            AfterStructuralEdit();
        }
    }

    void DrawPartitions()
    {
        EditorGUILayout.LabelField("关卡分区", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.partitions == null) data.partitions = new List<LabLevelPartition>();

        EditorGUILayout.HelpBox(
            "分区的 bounds 就是相机范围：B 进入哪个分区，相机就用哪个分区。\n" +
            "移动分区时可选择把里面的地形/绳/出生点一起带走。",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("新建分区")) CreatePartitionAtView();
            using (new EditorGUI.DisabledScope(partitionIndex < 0 || partitionIndex >= data.partitions.Count))
            {
                if (GUILayout.Button("删除选中")) DeleteSelectedPartition();
            }
        }

        for (int i = 0; i < data.partitions.Count; i++)
        {
            var partition = data.partitions[i];
            if (partition == null) continue;
            string label = (i == partitionIndex ? "▶ " : "") + partition.name + "   " + partition.bounds;
            if (GUILayout.Button(label)) partitionIndex = i;
        }

        if (data.partitions.Count == 0)
            EditorGUILayout.LabelField("（没有分区：相机用单关范围/尺寸）");

        if (!string.IsNullOrEmpty(partitionMessage))
            EditorGUILayout.HelpBox(partitionMessage, MessageType.Warning);

        if (partitionIndex >= 0 && partitionIndex < data.partitions.Count)
        {
            var partition = data.partitions[partitionIndex];
            if (partition != null)
            {
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("名称", partition.name);
                int x = EditorGUILayout.IntField("X", partition.bounds.x);
                int y = EditorGUILayout.IntField("Y", partition.bounds.y);
                int width = EditorGUILayout.IntField("宽", partition.bounds.width);
                int height = EditorGUILayout.IntField("高", partition.bounds.height);
                movePartitionContents = EditorGUILayout.ToggleLeft("移动时带上里面的物件", movePartitionContents);

                if (EditorGUI.EndChangeCheck())
                {
                    var oldBounds = partition.bounds;
                    var next = new RectInt(x, y, Mathf.Max(1, width), Mathf.Max(1, height));
                    var delta = new Vector2Int(next.x - oldBounds.x, next.y - oldBounds.y);
                    bool boundsChanged = next.x != oldBounds.x || next.y != oldBounds.y ||
                                         next.width != oldBounds.width || next.height != oldBounds.height;

                    if (boundsChanged && data.OverlapsAny(next, partitionIndex))
                    {
                        partitionMessage = "新位置/大小和其它关卡分区重叠，未应用。";
                        Repaint();
                        return;
                    }

                    Record("改关卡分区");
                    if (delta != Vector2Int.zero && movePartitionContents)
                        data.TranslatePartition(partitionIndex, delta, true);

                    partition.name = name;
                    partition.bounds = next;
                    partitionMessage = null;
                    data.Normalize();
                    AfterStructuralEdit();
                }
            }
        }

        EditorGUILayout.HelpBox(
            "分区之间不能重叠。B 不在任何分区里时，相机不加范围，只用关卡的相机跟随尺寸跟两人相对位置缩放。",
            MessageType.None);
    }

    void CreatePartitionAtView()
    {
        var view = SceneView.lastActiveSceneView;
        Vector2 center = view != null
            ? new Vector2(view.pivot.x, view.pivot.y)
            : new Vector2(
                level.Data.bounds.x + level.Data.bounds.width * 0.5f,
                level.Data.bounds.y + level.Data.bounds.height * 0.5f);

        var bounds = new RectInt(
            Mathf.FloorToInt(center.x) - 8,
            Mathf.FloorToInt(center.y) - 6,
            16, 12);

        if (level.Data.OverlapsAny(bounds))
        {
            partitionMessage = "视图中心的新分区会和现有分区重叠，先移动/缩小已有分区，或用分区工具在空白处拖。";
            Repaint();
            return;
        }

        Record("新建关卡分区");
        if (level.Data.AddPartition(bounds) == null)
        {
            partitionMessage = "新建分区失败：和其它分区重叠。";
            Repaint();
            return;
        }

        partitionIndex = level.Data.partitions.Count - 1;
        partitionMessage = null;
        AfterStructuralEdit();
    }

    void DeleteSelectedPartition()
    {
        var data = level.Data;
        if (partitionIndex < 0 || partitionIndex >= data.partitions.Count) return;

        int choice = EditorUtility.DisplayDialogComplex(
            "删除关卡分区",
            "同时删除分区里的地形和绳？",
            "删除分区和内容",
            "只删分区",
            "取消");
        if (choice == 2) return;

        Record("删除关卡分区");
        data.DeletePartition(partitionIndex, choice == 0);
        partitionIndex = Mathf.Min(partitionIndex, data.partitions.Count - 1);
        partitionMessage = null;
        AfterStructuralEdit();
    }

    void DrawRopes()
    {
        EditorGUILayout.LabelField("绳", EditorStyles.boldLabel);
        ropeLength = Mathf.Clamp(EditorGUILayout.IntField("新绳长度（格）", ropeLength), 1, MaxRopeLength);
        ropeChainId = EditorGUILayout.IntField("chainId", ropeChainId);

        var ropes = level.Data.ropes;
        if (ropes == null || ropes.Count == 0)
        {
            EditorGUILayout.LabelField("（没有绳）");
        }
        else
        {
            ropeScroll = EditorGUILayout.BeginScrollView(ropeScroll, GUILayout.MaxHeight(110f));
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("{0} ({1:0.##}, {2:0.##})  长 {3}  链 {4}",
                            rope.orientation == LabRopeOrientation.Horizontal ? "横" : "竖",
                            rope.center.x, rope.center.y, rope.cells, rope.chainId),
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删绳");
                        ropes.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        if (GUILayout.Button("清空绳"))
        {
            if (!EditorUtility.DisplayDialog("清空绳", "删掉所有绳？", "清空", "取消")) return;
            Record("清空绳");
            level.Data.ClearRopes();
            AfterStructuralEdit();
        }
    }

    void DrawKeyDoors()
    {
        EditorGUILayout.LabelField("钥匙 / 门", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUILayout.LabelField("钥匙", level.KeyCount + "（1×1，拾取后消失）");
        EditorGUILayout.LabelField("门", level.DoorCount + "（1×3，任意一把钥匙开门）");
        newDoorColor = EditorGUILayout.ColorField("新门颜色", newDoorColor);

        keyDoorScroll = EditorGUILayout.BeginScrollView(keyDoorScroll, GUILayout.MaxHeight(120f));
        if (data.keys != null)
        {
            for (int i = 0; i < data.keys.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("钥匙 ({0:0.##}, {1:0.##})",
                            data.keys[i].position.x, data.keys[i].position.y),
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删钥匙");
                        data.keys.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var door = data.doors[i];
                    EditorGUILayout.LabelField(
                        string.Format("门 ({0:0.##}, {1:0.##})", door.position.x, door.position.y),
                        EditorStyles.miniLabel);
                    EditorGUI.BeginChangeCheck();
                    Color color = EditorGUILayout.ColorField(GUIContent.none, door.color, false, false, false,
                        GUILayout.Width(40f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改门颜色");
                        door.color = color;
                        data.doors[i] = door;
                        MarkDirty(true);
                    }
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删门");
                        data.doors.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(level.KeyCount == 0))
            {
                if (GUILayout.Button("清空钥匙"))
                {
                    if (!EditorUtility.DisplayDialog("清空钥匙", "删掉所有钥匙？", "清空", "取消")) return;
                    Record("清空钥匙");
                    level.Data.ClearKeys();
                    AfterStructuralEdit();
                }
            }
            using (new EditorGUI.DisabledScope(level.DoorCount == 0))
            {
                if (GUILayout.Button("清空门"))
                {
                    if (!EditorUtility.DisplayDialog("清空门", "删掉所有门？", "清空", "取消")) return;
                    Record("清空门");
                    level.Data.ClearDoors();
                    AfterStructuralEdit();
                }
            }
        }
    }

    void DrawSpikes()
    {
        EditorGUILayout.LabelField("地刺", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("刺", level.SpikeCount + "（16×6 像素，贴格边，碰到即死）");

        spikeScroll = EditorGUILayout.BeginScrollView(spikeScroll, GUILayout.MaxHeight(120f));
        if (data.spikes != null)
        {
            for (int i = 0; i < data.spikes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var spike = data.spikes[i];
                    bool floating = !data.HasCell(spike.BaseCell);
                    EditorGUILayout.LabelField(
                        string.Format("刺 ({0}, {1}){2}",
                            spike.cell.x, spike.cell.y, floating ? "  悬空" : ""),
                        EditorStyles.miniLabel, GUILayout.Width(120f));

                    int dirIndex = Mathf.Clamp(System.Array.IndexOf(SpikeDirs, spike.dir), 0, SpikeDirs.Length - 1);
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.Popup(dirIndex, SpikeDirLabels, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改地刺方向");
                        spike.dir = SpikeDirs[next];
                        data.spikes[i] = spike;
                        MarkDirty(true);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删地刺");
                        data.spikes.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.SpikeCount == 0))
        {
            if (GUILayout.Button("清空地刺"))
            {
                if (!EditorUtility.DisplayDialog("清空地刺", "删掉所有地刺？", "清空", "取消")) return;
                Record("清空地刺");
                level.Data.ClearSpikes();
                AfterStructuralEdit();
            }
        }
    }

    void DrawIces()
    {
        EditorGUILayout.LabelField("冰块", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("冰块", level.IceCount + "（1×1，同组共享 v2 整组破坏）");

        iceScroll = EditorGUILayout.BeginScrollView(iceScroll, GUILayout.MaxHeight(120f));
        if (data.ices != null)
        {
            for (int i = 0; i < data.ices.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var ice = data.ices[i];
                    EditorGUILayout.LabelField(
                        string.Format("冰 ({0}, {1})", ice.cell.x, ice.cell.y),
                        EditorStyles.miniLabel, GUILayout.Width(110f));

                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntField(ice.groupId, GUILayout.Width(48f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改冰块组号");
                        ice.groupId = next;
                        data.ices[i] = ice;
                        MarkDirty(true);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删冰块");
                        data.ices.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.IceCount == 0))
        {
            if (GUILayout.Button("清空冰块"))
            {
                if (!EditorUtility.DisplayDialog("清空冰块", "删掉所有冰块？", "清空", "取消")) return;
                Record("清空冰块");
                level.Data.ClearIces();
                AfterStructuralEdit();
            }
        }
    }

    void DrawRecalls()
    {
        EditorGUILayout.LabelField("召回道具", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("道具", level.RecallCount + "（1×1，拾取后全队永久解锁召回）");

        recallScroll = EditorGUILayout.BeginScrollView(recallScroll, GUILayout.MaxHeight(100f));
        if (data.recalls != null)
        {
            for (int i = 0; i < data.recalls.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var recall = data.recalls[i];
                    EditorGUILayout.LabelField(
                        string.Format("召回 ({0}, {1})", recall.cell.x, recall.cell.y),
                        EditorStyles.miniLabel, GUILayout.Width(120f));

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删召回道具");
                        data.recalls.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.RecallCount == 0))
        {
            if (GUILayout.Button("清空召回道具"))
            {
                if (!EditorUtility.DisplayDialog("清空召回道具", "删掉所有召回道具？", "清空", "取消")) return;
                Record("清空召回道具");
                level.Data.ClearRecalls();
                AfterStructuralEdit();
            }
        }
    }

    void DrawMagmas()
    {
        EditorGUILayout.LabelField("岩浆块", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.magmas == null) data.magmas = new List<LabMagmaSpec>();
        EditorGUILayout.LabelField("岩浆块", level.MagmaCount + "（1×1，首次接触强制红热 / 热二接触死亡 / v2+ 撞击死亡 / v3 整组破坏）");

        magmaScroll = EditorGUILayout.BeginScrollView(magmaScroll, GUILayout.MaxHeight(120f));
        for (int i = 0; i < data.magmas.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var magma = data.magmas[i];
                EditorGUILayout.LabelField(
                    string.Format("岩 ({0}, {1})", magma.cell.x, magma.cell.y),
                    EditorStyles.miniLabel, GUILayout.Width(110f));

                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(magma.groupId, GUILayout.Width(48f));
                if (EditorGUI.EndChangeCheck())
                {
                    Record("改岩浆块组号");
                    magma.groupId = next;
                    data.magmas[i] = magma;
                    MarkDirty(true);
                }

                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删岩浆块");
                    data.magmas.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.MagmaCount == 0))
        {
            if (GUILayout.Button("清空岩浆块"))
            {
                if (!EditorUtility.DisplayDialog("清空岩浆块", "删掉所有岩浆块？", "清空", "取消")) return;
                Record("清空岩浆块");
                level.Data.ClearMagmas();
                AfterStructuralEdit();
            }
        }
    }

    void DrawSpawns()
    {
        EditorGUILayout.LabelField("出生点（脚底）", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUI.BeginChangeCheck();
        Vector2 a = EditorGUILayout.Vector2Field("A", data.spawnA);
        Vector2 b = EditorGUILayout.Vector2Field("B", data.spawnB);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改出生点");
            data.spawnA = a;
            data.spawnB = b;
            MarkDirty();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("A 回基准（8, 0）"))
            {
                Record("A 回基准出生点");
                data.spawnA = TuningLab.SpawnAFeet;
                MarkDirty();
            }
            if (GUILayout.Button("B 回基准（10, 0）"))
            {
                Record("B 回基准出生点");
                data.spawnB = TuningLab.SpawnBFeet;
                MarkDirty();
            }
        }
    }

    void DrawRespawnPoints()
    {
        EditorGUILayout.LabelField("重生点", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("重生点", level.RespawnPointCount + "（3×3，任一玩家接触触发）");
        if (data.respawnPoints == null) data.respawnPoints = new List<LabRespawnPointSpec>();

        respawnScroll = EditorGUILayout.BeginScrollView(respawnScroll, GUILayout.MaxHeight(100f));
        for (int i = 0; i < data.respawnPoints.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var point = data.respawnPoints[i];
                EditorGUILayout.LabelField(
                    string.Format("重生点 ({0:0.##}, {1:0.##})", point.position.x, point.position.y),
                    EditorStyles.miniLabel);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删重生点");
                    data.respawnPoints.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.RespawnPointCount == 0))
        {
            if (GUILayout.Button("清空重生点"))
            {
                if (!EditorUtility.DisplayDialog("清空重生点", "删掉所有重生点？", "清空", "取消")) return;
                Record("清空重生点");
                data.respawnPoints.Clear();
                AfterStructuralEdit();
            }
        }
    }

    void DrawTeleports()
    {
        EditorGUILayout.LabelField("传送点", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("传送点", level.TeleportCount + "（3×3，运行时标记后互为终点）");
        if (data.teleports == null) data.teleports = new List<LabTeleportSpec>();

        if (data.teleports.Count == 1)
        {
            EditorGUILayout.HelpBox(
                "只有一个传送点：运行时最多只能标记一个，进入后没有可选终点。至少放置两个并让双人进入标记。",
                MessageType.Warning);
        }

        teleportScroll = EditorGUILayout.BeginScrollView(teleportScroll, GUILayout.MaxHeight(100f));
        for (int i = 0; i < data.teleports.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var point = data.teleports[i];
                EditorGUILayout.LabelField(
                    string.Format("传送点 ({0:0.##}, {1:0.##})", point.position.x, point.position.y),
                    EditorStyles.miniLabel);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删传送点");
                    data.teleports.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.TeleportCount == 0))
        {
            if (GUILayout.Button("清空传送点"))
            {
                if (!EditorUtility.DisplayDialog("清空传送点", "删掉所有传送点？", "清空", "取消")) return;
                Record("清空传送点");
                data.teleports.Clear();
                AfterStructuralEdit();
            }
        }
    }

    void DrawBounds()
    {
        EditorGUILayout.LabelField("编辑范围 / 单关相机范围（clamp）", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUI.BeginChangeCheck();
        int x = EditorGUILayout.IntField("X", data.bounds.x);
        int y = EditorGUILayout.IntField("Y", data.bounds.y);
        int width = EditorGUILayout.IntField("宽", data.bounds.width);
        int height = EditorGUILayout.IntField("高", data.bounds.height);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改相机范围");
            data.bounds = new RectInt(x, y, Mathf.Max(1, width), Mathf.Max(1, height));
            MarkDirty();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("回 Tuning Lab 基准范围"))
            {
                Record("回基准相机范围");
                Rect defaults = TuningLab.BaselineBounds;
                if (lab != null)
                {
                    Rect custom = lab.defaultCameraBounds;
                    if (custom.width > 0f && custom.height > 0f) defaults = custom;
                }
                data.bounds = ToRectInt(defaults);
                MarkDirty();
            }
            if (GUILayout.Button("按地形计算"))
            {
                Record("按地形算相机范围");
                data.bounds = data.ContentBounds();
                MarkDirty();
            }
        }

        EditorGUILayout.HelpBox(
            "Tuning Lab 基准的两面墙在相机范围外 1 格，这是基准行为；范围管编辑/绘制，不限制画地形。\n" +
            "有关卡分区时，分区的 bounds 才是相机范围；这里的范围只作为编辑范围和没有分区时的单关相机范围。",
            MessageType.None);
    }

    void DrawCamera()
    {
        EditorGUILayout.LabelField("相机跟随尺寸（全关卡）", EditorStyles.boldLabel);
        var data = level.Data;

        int partitionCount = data.partitions != null ? data.partitions.Count : 0;
        if (partitionCount > 0)
        {
            EditorGUILayout.HelpBox(
                string.Format(
                    "已定义 {0} 个关卡分区：相机 clamp 用 B 所在分区的 bounds；下面的跟随尺寸对所有分区生效。\n" +
                    "B 不在任何分区时不夹相机，仍用关卡的相机跟随尺寸跟两人相对位置缩放。",
                    partitionCount),
                MessageType.Info);
            if (GUILayout.Button("选中 TuningLab 编辑相机默认"))
                Selection.activeGameObject = lab != null ? lab.gameObject : null;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "没有分区：相机用下面的单关范围/尺寸。\n" +
                "连续关卡请在「关卡分区」里新建分区。",
                MessageType.None);
        }

        float defaultMin = lab != null ? lab.defaultCameraMinSize : TuningLab.DefaultCameraMinSize;
        float defaultMax = lab != null ? lab.defaultCameraMaxSize : TuningLab.DefaultCameraMaxSize;
        if (defaultMin <= 0f) defaultMin = TuningLab.DefaultCameraMinSize;
        if (defaultMax < defaultMin) defaultMax = defaultMin;

        float min = data.cameraMinSize;
        float max = data.cameraMaxSize;

        EditorGUI.BeginChangeCheck();
        min = EditorGUILayout.FloatField("最小 size（格）", min);
        max = EditorGUILayout.FloatField("最大 size（格）", max);
        EditorGUILayout.MinMaxSlider("跟随范围", ref min, ref max, 0.5f, 128f);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改相机尺寸");
            data.cameraMinSize = Mathf.Clamp(min, 0.5f, 128f);
            data.cameraMaxSize = Mathf.Clamp(max, data.cameraMinSize, 128f);
            MarkDirty();
        }

        const float aspect = 16f / 9f;
        EditorGUILayout.LabelField("最小视野",
            string.Format("{0:0.##} 格高 × {1:0.#} 格宽（16:9）",
                data.cameraMinSize * 2f, data.cameraMinSize * 2f * aspect));
        EditorGUILayout.LabelField("最大视野",
            string.Format("{0:0.##} 格高 × {1:0.#} 格宽（16:9）",
                data.cameraMaxSize * 2f, data.cameraMaxSize * 2f * aspect));
        EditorGUILayout.LabelField("1080p 像素缩放",
            string.Format("{0:0.##}× – {1:0.##}×",
                1080f / (data.cameraMinSize * 2f * TuningLab.CellPixels),
                1080f / (data.cameraMaxSize * 2f * TuningLab.CellPixels)));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tuning Lab 默认"))
            {
                Record("相机尺寸回基准");
                data.cameraMinSize = defaultMin;
                data.cameraMaxSize = defaultMax;
                MarkDirty();
            }
            if (GUILayout.Button("固定为最小"))
            {
                Record("相机固定为最小尺寸");
                data.cameraMaxSize = data.cameraMinSize;
                MarkDirty();
            }
        }

        showCameraFrames = EditorGUILayout.ToggleLeft("在 Scene 视图画出最小/最大视野（以出生点中心示意）", showCameraFrames);

        if (lab != null && !lab.freeCamera)
        {
            EditorGUILayout.HelpBox(
                "TuningLab.freeCamera = false 时用的是固定相机，这里的尺寸范围不生效。",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "size 是正交相机的半高（格），视野高度 = 2 × size；这是跟随范围，不是相机夹具。\n" +
            "只有整数像素缩放才逐像素对齐；非整数倍只适合概览或调试。",
            MessageType.None);
    }

    void DrawActions()
    {
        EditorGUILayout.LabelField("关卡", EditorStyles.boldLabel);

        if (GUILayout.Button("从 Tuning Lab 重置（地形 + 绳 + 出生点 + 相机 + 分区）", GUILayout.Height(28f)))
        {
            if (!EditorUtility.DisplayDialog(
                    "重置关卡",
                    "用 Tuning Lab 基准房间覆盖当前地形、绳、出生点、相机范围/尺寸和关卡分区？",
                    "重置", "取消")) return;

            Record("从 Tuning Lab 重置关卡");
            level.ApplyBaseline();
            if (lab != null) LabLevelScene.CopyCameraDefaults(lab, level.Data);
            LabLevelScene.EnsureDefaultPartition(level.Data);
            AfterStructuralEdit();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("校验摆放")) Validate();
            if (GUILayout.Button("保存场景")) SaveScene();
            if (GUILayout.Button("进入 Play")) EditorApplication.isPlaying = true;
        }
    }

    void DrawValidation()
    {
        EditorGUILayout.Space();
        if (!validated)
        {
            EditorGUILayout.LabelField("校验：还没有跑。", EditorStyles.miniLabel);
            return;
        }

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
            return;
        }

        foreach (var issue in issues)
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
    }

    // ------------------------------------------------------------------
    // 操作
    // ------------------------------------------------------------------

    void Record(string name)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(name);
        Undo.RecordObject(level, name);
    }

    void MarkDirty(bool rebuildPreview = false)
    {
        if (rebuildPreview && !Application.isPlaying) level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void MarkSceneDirty()
    {
        if (level == null) return;
        Scene scene = level.gameObject.scene;
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
    }

    void AfterStructuralEdit()
    {
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void SaveScene()
    {
        Scene scene = level.gameObject.scene;
        if (!scene.IsValid()) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[LabLevelEditor] 已保存 " + scene.path);
    }

    void Validate()
    {
        issues.Clear();
        var data = level.Data;

        if (data.bounds.width <= 0 || data.bounds.height <= 0)
            issues.Add("编辑范围的宽/高必须大于 0。");

        if (data.cameraMinSize <= 0f)
            issues.Add("相机最小 size 必须大于 0。");
        if (data.cameraMaxSize < data.cameraMinSize)
            issues.Add("相机最大 size 不能小于最小 size。");
        if (data.cameraMaxSize > 128f)
            issues.Add("相机最大 size 超过 128 格，画面会小到看不清。");

        if (data.partitions != null)
        {
            var partitionRects = new List<Rect>();
            for (int i = 0; i < data.partitions.Count; i++)
            {
                var partition = data.partitions[i];
                if (partition == null)
                {
                    issues.Add("第 " + i + " 个关卡分区是空引用。");
                    continue;
                }

                if (partition.bounds.width <= 0 || partition.bounds.height <= 0)
                    issues.Add("关卡分区「" + partition.name + "」宽/高必须大于 0。");

                Rect rect = LabLevelData.ToRect(partition.bounds);
                for (int j = 0; j < partitionRects.Count; j++)
                {
                    if (!partitionRects[j].Overlaps(rect)) continue;
                    issues.Add("关卡分区 " + j + " 和 " + i + " 重叠；B 会命中列表里靠前的那个。");
                }
                partitionRects.Add(rect);
            }

            if (data.partitions.Count > 0)
            {
                Vector2 probe = data.spawnB;
                probe.y += TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
                if (data.FindPartition(probe) < 0)
                    issues.Add("B 出生点不在任何关卡分区里，开局相机会用 TuningLab 默认范围。");
            }
        }

        if (data.solids == null || data.solids.Count == 0)
            issues.Add("关卡没有任何地形。");
        else
        {
            var seen = new HashSet<Vector2Int>();
            foreach (var cell in data.solids)
                if (!seen.Add(cell)) issues.Add("地形有重复格：" + cell);
        }

        CheckSpawn("A", data.spawnA, data);
        CheckSpawn("B", data.spawnB, data);

        if (Vector2.Distance(data.spawnA, data.spawnB) < 1f)
            issues.Add("A / B 出生点距离小于 1 格。");

        if (data.ropes != null)
        {
            for (int i = 0; i < data.ropes.Count; i++)
            {
                var rope = data.ropes[i];
                if (rope.cells < 1)
                {
                    issues.Add("第 " + i + " 根绳长度小于 1 格。");
                    continue;
                }

                if (rope.center.x < data.bounds.xMin || rope.center.x > data.bounds.xMax)
                    issues.Add("第 " + i + " 根绳的 x 不在编辑范围内。");
                if (rope.Bottom < data.bounds.yMin || rope.Top > data.bounds.yMax)
                    issues.Add("第 " + i + " 根绳的上下端不在编辑范围内。");
            }
        }

        if (data.keys != null)
        {
            var seenKeys = new HashSet<Vector2>();
            for (int i = 0; i < data.keys.Count; i++)
            {
                var key = data.keys[i];
                if (!data.ContainsPoint(key.position))
                    issues.Add("第 " + i + " 把钥匙不在编辑范围内：" + key.position);
                if (!seenKeys.Add(key.position))
                    issues.Add("第 " + i + " 把钥匙和前面的钥匙重合：" + key.position);

                RectInt cell = key.Cells();
                if (data.HasCell(new Vector2Int(cell.x, cell.y)))
                    issues.Add("第 " + i + " 把钥匙在地形里：" + new Vector2Int(cell.x, cell.y));
            }
        }

        if (data.recalls != null)
        {
            var seenRecalls = new HashSet<Vector2Int>();
            for (int i = 0; i < data.recalls.Count; i++)
            {
                var recall = data.recalls[i];
                if (!data.ContainsCell(recall.cell))
                    issues.Add("第 " + i + " 个召回道具不在编辑范围内：" + recall.cell);
                if (!seenRecalls.Add(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和前面的重合：" + recall.cell);
                if (data.HasCell(recall.cell))
                    issues.Add("第 " + i + " 个召回道具在地形里：" + recall.cell);
                if (data.HasSpike(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和地刺在同一格：" + recall.cell);
                if (data.HasIce(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和冰块在同一格：" + recall.cell);
                if (data.HasMagma(recall.cell))
                    issues.Add("第 " + i + " 个召回道具和岩浆块在同一格：" + recall.cell);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, recall.cell))
                    issues.Add("第 " + i + " 个召回道具压在 A 的出生点上：" + recall.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, recall.cell))
                    issues.Add("第 " + i + " 个召回道具压在 B 的出生点上：" + recall.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具和第 " + k + " 把钥匙在同一格：" + recall.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + d + " 扇门里：" + recall.cell);

                if (data.respawnPoints != null)
                    for (int r = 0; r < data.respawnPoints.Count; r++)
                        if (data.respawnPoints[r].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + r + " 个重生点里：" + recall.cell);

                if (data.teleports != null)
                    for (int t = 0; t < data.teleports.Count; t++)
                        if (data.teleports[t].Cells().Contains(recall.cell))
                            issues.Add("第 " + i + " 个召回道具在第 " + t + " 个传送点里：" + recall.cell);
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                var door = data.doors[i];
                if (!data.ContainsPoint(door.position))
                    issues.Add("第 " + i + " 扇门不在编辑范围内：" + door.position);

                RectInt cells = door.Cells();
                for (int y = cells.y; y < cells.y + cells.height; y++)
                {
                    var cell = new Vector2Int(cells.x, y);
                    if (data.HasCell(cell)) issues.Add("第 " + i + " 扇门和地形重叠：" + cell);
                }
            }
        }

        if (data.spikes != null)
        {
            for (int i = 0; i < data.spikes.Count; i++)
            {
                var spike = data.spikes[i];
                if (!data.ContainsCell(spike.cell))
                    issues.Add("第 " + i + " 根刺不在编辑范围内：" + spike.cell);
                if (data.HasCell(spike.cell))
                    issues.Add("第 " + i + " 根刺和地形重叠：" + spike.cell);
                if (!data.HasCell(spike.BaseCell))
                    issues.Add("第 " + i + " 根刺悬空，底座那格不是实心：" + spike.cell + " 需要 " + spike.BaseCell);

                if (SpawnTouchesSpike(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, spike))
                    issues.Add("第 " + i + " 根刺压在 A 的出生点上：" + spike.cell);
                if (SpawnTouchesSpike(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, spike))
                    issues.Add("第 " + i + " 根刺压在 B 的出生点上：" + spike.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().x == spike.cell.x && data.keys[k].Cells().y == spike.cell.y)
                            issues.Add("第 " + i + " 根刺和第 " + k + " 把钥匙在同一格：" + spike.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(spike.cell))
                            issues.Add("第 " + i + " 根刺在第 " + d + " 扇门里：" + spike.cell);
            }
        }

        if (data.ices != null)
        {
            for (int i = 0; i < data.ices.Count; i++)
            {
                var ice = data.ices[i];
                if (!data.ContainsCell(ice.cell))
                    issues.Add("第 " + i + " 块冰不在编辑范围内：" + ice.cell);
                if (data.HasCell(ice.cell))
                    issues.Add("第 " + i + " 块冰和地形重叠：" + ice.cell);
                if (data.HasSpike(ice.cell))
                    issues.Add("第 " + i + " 块冰和地刺在同一格：" + ice.cell);
                if (data.HasMagma(ice.cell))
                    issues.Add("第 " + i + " 块冰和岩浆块在同一格：" + ice.cell);
                if (ice.groupId < 0)
                    issues.Add("第 " + i + " 块冰的组号不能是负数：" + ice.groupId);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, ice.cell))
                    issues.Add("第 " + i + " 块冰压在 A 的出生点上：" + ice.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, ice.cell))
                    issues.Add("第 " + i + " 块冰压在 B 的出生点上：" + ice.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(ice.cell))
                            issues.Add("第 " + i + " 块冰和第 " + k + " 把钥匙在同一格：" + ice.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(ice.cell))
                            issues.Add("第 " + i + " 块冰在第 " + d + " 扇门里：" + ice.cell);
            }
        }

        if (data.magmas != null)
        {
            for (int i = 0; i < data.magmas.Count; i++)
            {
                var magma = data.magmas[i];
                if (!data.ContainsCell(magma.cell))
                    issues.Add("第 " + i + " 块岩浆不在编辑范围内：" + magma.cell);
                if (data.HasCell(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和地形重叠：" + magma.cell);
                if (data.HasSpike(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和地刺在同一格：" + magma.cell);
                if (data.HasIce(magma.cell))
                    issues.Add("第 " + i + " 块岩浆和冰块在同一格：" + magma.cell);
                if (magma.groupId < 0)
                    issues.Add("第 " + i + " 块岩浆的组号不能是负数：" + magma.groupId);

                if (SpawnTouchesCell(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, magma.cell))
                    issues.Add("第 " + i + " 块岩浆压在 A 的出生点上：" + magma.cell);
                if (SpawnTouchesCell(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, magma.cell))
                    issues.Add("第 " + i + " 块岩浆压在 B 的出生点上：" + magma.cell);

                if (data.keys != null)
                    for (int k = 0; k < data.keys.Count; k++)
                        if (data.keys[k].Cells().Contains(magma.cell))
                            issues.Add("第 " + i + " 块岩浆和第 " + k + " 把钥匙在同一格：" + magma.cell);

                if (data.doors != null)
                    for (int d = 0; d < data.doors.Count; d++)
                        if (data.doors[d].Cells().Contains(magma.cell))
                            issues.Add("第 " + i + " 块岩浆在第 " + d + " 扇门里：" + magma.cell);
            }
        }

        if (data.respawnPoints != null)
        {
            for (int i = 0; i < data.respawnPoints.Count; i++)
            {
                var point = data.respawnPoints[i];
                if (!data.ContainsPoint(point.position))
                    issues.Add("第 " + i + " 个重生点不在编辑范围内：" + point.position);

                var cell = new Vector2Int(
                    Mathf.FloorToInt(point.position.x), Mathf.FloorToInt(point.position.y));
                if (data.HasCell(cell))
                    issues.Add("第 " + i + " 个重生点在地形里：" + cell);

                Rect rect = RespawnPointRect(point.position);
                for (int j = 0; j < i; j++)
                {
                    if (rect.Overlaps(RespawnPointRect(data.respawnPoints[j].position)))
                        issues.Add("第 " + i + " 个重生点和第 " + j + " 个重叠，接触时会抢同一个触发区。");
                }
            }
        }

        if (data.teleports != null)
        {
            var seenTeleports = new HashSet<Vector2>();
            for (int i = 0; i < data.teleports.Count; i++)
            {
                var point = data.teleports[i];
                if (!data.ContainsPoint(point.position))
                    issues.Add("第 " + i + " 个传送点不在编辑范围内：" + point.position);

                var cell = new Vector2Int(
                    Mathf.FloorToInt(point.position.x), Mathf.FloorToInt(point.position.y));
                if (data.HasCell(cell))
                    issues.Add("第 " + i + " 个传送点在地形里：" + cell);

                if (!seenTeleports.Add(point.position))
                    issues.Add("第 " + i + " 个传送点和前面的传送点重合：" + point.position);

                Rect rect = RespawnPointRect(point.position);
                for (int j = 0; j < i; j++)
                {
                    if (data.teleports[j].position == point.position) continue;
                    if (rect.Overlaps(RespawnPointRect(data.teleports[j].position)))
                        issues.Add("第 " + i + " 个传送点和第 " + j + " 个触发区重叠，玩家会同时踩进两个点。");
                }
            }
        }

        if (data.teleports != null && data.teleports.Count == 1)
            issues.Add("只有一个传送点：运行时最多只能标记一个，无法打开选择界面。至少放置两个并让双人进入标记。");

        validated = true;

        if (issues.Count == 0)
        {
            Debug.Log(string.Format("[LabLevelEditor] 校验通过：{0} 个实心格，{1} 个碰撞矩形，{2} 根绳，{3} 把钥匙，{4} 个召回道具，{5} 扇门，{6} 根刺，{7} 块冰，{8} 块岩浆，{9} 个重生点，{10} 个传送点，{11} 个关卡分区。",
                level.SolidCellCount, level.MergedRectCount, level.RopeCount, level.KeyCount, level.RecallCount,
                level.DoorCount, level.SpikeCount, level.IceCount, level.MagmaCount, level.RespawnPointCount,
                level.TeleportCount, data.partitions != null ? data.partitions.Count : 0), level);
        }
        else
        {
            foreach (var issue in issues) Debug.LogWarning("[LabLevelEditor] " + issue, level);
        }

        Repaint();
    }

    /// <summary>出生点上的玩家方框会不会一开场就蹭到这根刺（按刺带自己的矩形算，留 1 像素不算）。</summary>
    static bool SpawnTouchesSpike(Vector2 feet, float size, LabSpikeSpec spike)
    {
        var player = new Rect(feet.x - size * 0.5f, feet.y, size, size);
        Vector2 center = spike.StripCenter;
        Vector2 strip = spike.StripSize;
        return player.Overlaps(new Rect(center.x - strip.x * 0.5f, center.y - strip.y * 0.5f, strip.x, strip.y));
    }

    /// <summary>出生点上的玩家方框会不会一开场就压在冰块上。</summary>
    static bool SpawnTouchesCell(Vector2 feet, float size, Vector2Int cell)
    {
        var player = new Rect(feet.x - size * 0.5f, feet.y, size, size);
        return player.Overlaps(new Rect(cell.x, cell.y, 1f, 1f));
    }

    void CheckSpawn(string name, Vector2 feet, LabLevelData data)
    {
        if (!data.ContainsPoint(feet))
            issues.Add(name + " 出生点不在编辑范围内：" + feet);

        var cell = new Vector2Int(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
        if (data.HasCell(cell))
            issues.Add(name + " 出生点在地形里：" + cell);
        if (data.HasCell(cell + Vector2Int.up))
            issues.Add(name + " 出生点头顶被地形堵住：" + (cell + Vector2Int.up));
    }

    void FrameLevel()
    {
        var data = level.Data;
        RectInt content = data.ContentBounds();
        RectInt bounds = data.bounds;

        int x0 = Mathf.Min(content.x, bounds.x);
        int y0 = Mathf.Min(content.y, bounds.y);
        int x1 = Mathf.Max(content.x + content.width, bounds.x + bounds.width);
        int y1 = Mathf.Max(content.y + content.height, bounds.y + bounds.height);

        var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        var size = new Vector3(Mathf.Max(8f, x1 - x0 + 4f), Mathf.Max(8f, y1 - y0 + 4f), 1f);

        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        view.Frame(new Bounds(center, size), false);
        view.Repaint();
    }

    static RectInt ToRectInt(Rect rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.xMin),
            Mathf.RoundToInt(rect.yMin),
            Mathf.RoundToInt(rect.width),
            Mathf.RoundToInt(rect.height));
    }

    /// <summary>重生点的 3×3 触发区（中心坐标 → 世界矩形）。</summary>
    static Rect RespawnPointRect(Vector2 center)
        => new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f);

    // ------------------------------------------------------------------
    // Scene 视图
    // ------------------------------------------------------------------

    void OnSceneGUI(SceneView view)
    {
        if (level == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Event.current == null) return;

        var data = level.Data;
        if (showGrid) DrawGrid(data);
        DrawBoundsHandle(data);
        DrawPartitionHandles(data);
        DrawRopeHandles(data);
        DrawKeyDoorHandles(data);
        DrawRecallHandles(data);
        DrawSpikeHandles(data);
        DrawIceHandles(data);
        DrawMagmaHandles(data);
        DrawRespawnPointHandles(data);
        DrawTeleportHandles(data);
        DrawSelectionRegion();
        DrawPickedHighlight();
        if (showSpawns) DrawSpawnHandles(data);
        if (showCameraFrames) DrawCameraFrames(data);

        Event e = Event.current;
        Vector3 point = ScenePoint(e.mousePosition);
        Vector2Int cell = CellOf(point);
        bool inArea = InEditArea(cell, data);
        bool inBounds = data.ContainsCell(cell);

        if (e.type == EventType.KeyDown && !e.control && !e.alt)
        {
            if (HandleKey(e)) return;
        }

        DrawHover(cell, inArea, inBounds, data);

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0 && !e.alt:
                // 有选区时，移动工具在场景任意位置起手都拖整个选区；其它工具仍限制在编辑范围内。
                bool movesSelection = tool == Tool.Move && selection.Count > 0;
                if (tool != Tool.Partition && !inArea && !movesSelection) return;
                BeginGesture(cell, inBounds, view);
                e.Use();
                break;

            case EventType.MouseDrag when e.button == 0 && gesture:
                ContinueGesture(cell);
                e.Use();
                break;

            case EventType.MouseUp when e.button == 0 && gesture:
                EndGesture();
                e.Use();
                break;
        }
    }

    void BeginGesture(Vector2Int cell, bool inBounds, SceneView view)
    {
        if (IsPlaceTool()) dragAdds = ShouldAddAtStart(cell);

        switch (tool)
        {
            case Tool.Paint:
            case Tool.Erase:
                Record(tool == Tool.Paint ? "铺地形" : "擦地形");
                gesture = true;
                lastCell = cell;
                ApplyCell(cell, tool == Tool.Erase);
                MarkDirty(true);
                break;

            case Tool.Rect:
                Record("矩形地形");
                gesture = true;
                rectStart = cell;
                rectEnd = cell;
                Repaint();
                break;

            case Tool.Rope:
            case Tool.HorizontalRope:
                if (!inBounds)
                {
                    Notify(view, "绳必须放在相机范围里。");
                    return;
                }
                LabRopeOrientation orientation = tool == Tool.HorizontalRope
                    ? LabRopeOrientation.Horizontal
                    : LabRopeOrientation.Vertical;
                if (orientation == LabRopeOrientation.Vertical &&
                    cell.y + ropeLength > level.Data.bounds.yMax)
                {
                    Notify(view, "绳太长，超出相机范围上沿。");
                    return;
                }
                if (orientation == LabRopeOrientation.Horizontal &&
                    cell.x + ropeLength > level.Data.bounds.xMax)
                {
                    Notify(view, "绳太长，超出相机范围右沿。");
                    return;
                }
                if (orientation == LabRopeOrientation.Horizontal &&
                    cell.y + 1 > level.Data.bounds.yMax)
                {
                    Notify(view, "横绳超出相机范围上沿。");
                    return;
                }
                Record(orientation == LabRopeOrientation.Horizontal ? "放横绳" : "放竖绳");
                Vector2 center = orientation == LabRopeOrientation.Horizontal
                    ? new Vector2(cell.x + ropeLength * 0.5f, cell.y + 1.125f)
                    : new Vector2(cell.x + 0.5f, cell.y + ropeLength * 0.5f);
                level.Data.ropes.Add(new LabRopeSpec(
                    center, ropeLength, ropeChainId, orientation));
                AfterStructuralEdit();
                break;

            case Tool.SpawnA:
            case Tool.SpawnB:
                if (!inBounds)
                {
                    Notify(view, "出生点必须在相机范围里。");
                    return;
                }
                Record(tool == Tool.SpawnA ? "放出生点 A" : "放出生点 B");
                {
                    var feet = new Vector2(cell.x + 0.5f, cell.y);
                    if (tool == Tool.SpawnA) level.Data.spawnA = feet;
                    else level.Data.spawnB = feet;
                }
                MarkDirty();
                break;

            case Tool.Partition:
                BeginPartition(cell);
                break;

            case Tool.Key:
                if (!inBounds)
                {
                    Notify(view, "钥匙必须放在相机范围里。");
                    return;
                }
                Record("放钥匙");
                level.Data.keys.Add(new LabKeySpec(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
                AfterStructuralEdit();
                break;

            case Tool.Recall:
                if (!inBounds)
                {
                    Notify(view, "召回道具必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "召回道具不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasRecall(cell))
                {
                    Record("删召回道具");
                    level.Data.RemoveRecall(cell);
                }
                else
                {
                    Record("放召回道具");
                    level.Data.PlaceRecall(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.Door:
                if (!inBounds)
                {
                    Notify(view, "门必须放在相机范围里。");
                    return;
                }
                if (cell.y + LabDoorSpec.CellHeight > level.Data.bounds.yMax)
                {
                    Notify(view, "门有 3 格高，超出相机范围上沿。");
                    return;
                }
                Record("放门");
                level.Data.doors.Add(new LabDoorSpec(
                    new Vector2(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f),
                    newDoorColor));
                AfterStructuralEdit();
                break;

            case Tool.Spike:
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "刺不能放在实心格里：" + cell);
                    return;
                }

                var existing = level.Data.FindSpike(cell);
                if (existing.HasValue && existing.Value.dir == newSpikeDir)
                {
                    Record("删地刺");
                    level.Data.RemoveSpike(cell);
                }
                else
                {
                    Record(existing.HasValue ? "改地刺方向" : "放地刺");
                    level.Data.PlaceSpike(cell, newSpikeDir);
                }
                AfterStructuralEdit();
                break;

            case Tool.Ice:
                if (!inBounds)
                {
                    Notify(view, "冰块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "冰块不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasMagma(cell))
                {
                    Notify(view, "冰块不能和岩浆块放在同一格：" + cell);
                    return;
                }

                var existingIce = level.Data.FindIce(cell);
                if (existingIce.HasValue && existingIce.Value.groupId == iceGroupId)
                {
                    Record("删冰块");
                    level.Data.RemoveIce(cell);
                }
                else
                {
                    Record(existingIce.HasValue ? "改冰块组号" : "放冰块");
                    level.Data.PlaceIce(cell, iceGroupId);
                }
                AfterStructuralEdit();
                break;

            case Tool.Magma:
                if (!inBounds)
                {
                    Notify(view, "岩浆块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "岩浆块不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasIce(cell))
                {
                    Notify(view, "岩浆块不能和冰块放在同一格：" + cell);
                    return;
                }

                var existingMagma = level.Data.FindMagma(cell);
                if (existingMagma.HasValue && existingMagma.Value.groupId == magmaGroupId)
                {
                    Record("删岩浆块");
                    level.Data.RemoveMagma(cell);
                }
                else
                {
                    Record(existingMagma.HasValue ? "改岩浆块组号" : "放岩浆块");
                    level.Data.PlaceMagma(cell, magmaGroupId);
                }
                AfterStructuralEdit();
                break;

            case Tool.RespawnPoint:
                if (!inBounds)
                {
                    Notify(view, "重生点必须在相机范围里。");
                    return;
                }
                if (level.Data.HasRespawnPoint(cell))
                {
                    Record("删重生点");
                    level.Data.RemoveRespawnPoint(cell);
                }
                else
                {
                    Record("放重生点");
                    level.Data.PlaceRespawnPoint(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.TeleportPoint:
                if (!inBounds)
                {
                    Notify(view, "传送点必须在相机范围里。");
                    return;
                }
                if (level.Data.HasTeleport(cell))
                {
                    Record("删传送点");
                    level.Data.RemoveTeleport(cell);
                }
                else
                {
                    Record("放传送点");
                    level.Data.PlaceTeleport(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.Marquee:
                gesture = true;
                marqueeDragging = true;
                marqueeStart = cell;
                marqueeEnd = cell;
                Repaint();
                break;

            case Tool.Move:
                if (selection.Count > 0)
                {
                    // 锁住拖动开始时选区里的条目：框和内容一起走，沿途扫过的物件不会被带走。
                    Record("移动选区内容");
                    movingContent = true;
                    movingSelection = true;
                    moveCapture = level.Data.CaptureContents(selection);
                    moveLastCell = cell;
                    gesture = true;
                    Repaint();
                    break;
                }

                movingSelection = false;
                moveCapture = null;
                picked = PickAt(cell);
                if (picked.kind == PickKind.None)
                {
                    Repaint();
                    break;
                }

                Record("拖动物件");
                movingContent = true;
                moveLastCell = cell;
                gesture = true;
                Repaint();
                break;
        }

        // 摆放类工具：第一次已经落笔，后续拖动按同一个"放/删"语义连续执行。
        if (IsPlaceTool())
        {
            gesture = true;
            lastCell = cell;
        }
    }

    void ContinueGesture(Vector2Int cell)
    {
        if (tool == Tool.Partition)
        {
            ContinuePartition(cell);
            return;
        }

        if (tool == Tool.Marquee)
        {
            if (marqueeEnd == cell) return;
            marqueeEnd = cell;
            Repaint();
            return;
        }

        if (tool == Tool.Move)
        {
            if (!movingContent) return;
            var delta = cell - moveLastCell;
            if (delta == Vector2Int.zero) return;

            var areas = CurrentMoveAreas();
            if (areas == null) return;

            moveLastCell = cell;
            if (movingSelection)
            {
                level.Data.MoveCapturedContents(moveCapture, delta);
                TranslateRects(selection, delta);
            }
            else
            {
                level.Data.MoveContents(areas, delta);
            }
            MarkDirty(true);
            return;
        }

        if (IsPlaceTool())
        {
            if (cell == lastCell) return;
            // 沿直线补齐，快速拖动也不会漏格；起点那格已经落过笔，跳过。
            foreach (var lineCell in LineCells(lastCell, cell))
            {
                if (lineCell == lastCell) continue;
                ApplyToolAtCell(lineCell, dragAdds);
            }
            lastCell = cell;
            MarkDirty(true);
            return;
        }

        switch (tool)
        {
            case Tool.Paint:
            case Tool.Erase:
                if (cell == lastCell) return;
                PaintLine(lastCell, cell, tool == Tool.Erase);
                lastCell = cell;
                MarkDirty(true);
                break;

            case Tool.Rect:
                rectEnd = cell;
                Repaint();
                break;
        }
    }

    void EndGesture()
    {
        if (tool == Tool.Partition)
        {
            EndPartition();
            return;
        }

        if (tool == Tool.Marquee)
        {
            CommitMarquee();
            marqueeDragging = false;
            gesture = false;
            Repaint();
            return;
        }

        if (tool == Tool.Move)
        {
            movingContent = false;
            movingSelection = false;
            moveCapture = null;
            gesture = false;
            level.Data.Normalize();
            level.RebuildPreview();
            EditorUtility.SetDirty(level);
            MarkSceneDirty();
            validated = false;
            Repaint();
            return;
        }

        if (tool == Tool.Rect)
        {
            var data = level.Data;
            int x0 = Mathf.Min(rectStart.x, rectEnd.x);
            int y0 = Mathf.Min(rectStart.y, rectEnd.y);
            int x1 = Mathf.Max(rectStart.x, rectEnd.x);
            int y1 = Mathf.Max(rectStart.y, rectEnd.y);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (InEditArea(cell, data)) data.AddCell(cell);
                }
        }

        gesture = false;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void BeginPartition(Vector2Int cell)
    {
        var data = level.Data;
        int index = data.FindPartition(new Vector2(cell.x + 0.5f, cell.y + 0.5f));

        if (index >= 0)
        {
            partitionIndex = index;
            partitionCreating = false;
            partitionDragIndex = index;
            partitionDragLast = cell;
            Record("移动关卡分区");
        }
        else
        {
            partitionIndex = -1;
            partitionCreating = true;
            partitionDragIndex = -1;
            partitionCreateStart = cell;
            partitionCreateEnd = cell;
        }

        gesture = true;
        Repaint();
    }

    void ContinuePartition(Vector2Int cell)
    {
        if (partitionCreating)
        {
            if (partitionCreateEnd == cell) return;
            partitionCreateEnd = cell;
            Repaint();
            return;
        }

        if (partitionDragIndex < 0) return;

        var delta = cell - partitionDragLast;
        if (delta == Vector2Int.zero) return;

        if (!level.Data.MovePartition(partitionDragIndex, delta, movePartitionContents))
        {
            partitionMessage = "移动被拒绝：分区不能和其它分区重叠。";
            Repaint();
            return;
        }

        partitionMessage = null;
        partitionDragLast = cell;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    RectInt PreviewPartitionBounds()
    {
        int x0 = Mathf.Min(partitionCreateStart.x, partitionCreateEnd.x);
        int y0 = Mathf.Min(partitionCreateStart.y, partitionCreateEnd.y);
        int width = Mathf.Abs(partitionCreateEnd.x - partitionCreateStart.x) + 1;
        int height = Mathf.Abs(partitionCreateEnd.y - partitionCreateStart.y) + 1;

        if (width <= 1 && height <= 1)
        {
            width = 16;
            height = 12;
        }

        return new RectInt(x0, y0, width, height);
    }

    void EndPartition()
    {
        if (partitionCreating)
        {
            var bounds = PreviewPartitionBounds();
            if (level.Data.OverlapsAny(bounds))
            {
                partitionMessage = "新分区会和已有分区重叠，未创建。";
            }
            else
            {
                Record("新建关卡分区");
                if (level.Data.AddPartition(bounds) != null)
                {
                    partitionIndex = level.Data.partitions.Count - 1;
                    partitionMessage = null;
                }
                else
                {
                    partitionMessage = "新建分区失败：和其它分区重叠。";
                }
            }

            partitionCreating = false;
        }

        partitionDragIndex = -1;
        gesture = false;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void ApplyCell(Vector2Int cell, bool erase)
    {
        var data = level.Data;
        if (!InEditArea(cell, data)) return;
        if (erase)
        {
            data.RemoveCell(cell);
            EraseObjectsAt(data, cell);  // 擦地形 = 这一格摆的东西一起擦掉
        }
        else data.AddCell(cell);
    }

    /// <summary>擦掉落在这一格上的所有摆放物件（地形格另算）。</summary>
    static void EraseObjectsAt(LabLevelData data, Vector2Int cell)
    {
        if (data.keys != null) data.keys.RemoveAll(key => key.Cells().Contains(cell));
        if (data.recalls != null) data.recalls.RemoveAll(recall => recall.cell == cell);
        if (data.doors != null) data.doors.RemoveAll(door => door.Cells().Contains(cell));
        if (data.spikes != null) data.spikes.RemoveAll(spike => spike.cell == cell);
        if (data.ices != null) data.ices.RemoveAll(ice => ice.cell == cell);
        if (data.magmas != null) data.magmas.RemoveAll(magma => magma.cell == cell);
        if (data.respawnPoints != null)
            data.respawnPoints.RemoveAll(point => point.Cells().Contains(cell));
        if (data.teleports != null)
            data.teleports.RemoveAll(point => point.Cells().Contains(cell));
        if (data.ropes != null) data.ropes.RemoveAll(rope => RopeCoversCell(rope, cell));
    }

    /// <summary>这根绳（竖或横）是否盖住这一格。</summary>
    static bool RopeCoversCell(LabRopeSpec rope, Vector2Int cell)
    {
        if (rope.orientation == LabRopeOrientation.Horizontal)
        {
            int row = Mathf.FloorToInt(rope.center.y - 0.125f);
            if (cell.y != row) return false;
            float half = rope.cells * 0.5f;
            return cell.x + 1 > rope.center.x - half && cell.x < rope.center.x + half;
        }

        if (cell.x != Mathf.FloorToInt(rope.center.x)) return false;
        float bottom = rope.center.y - rope.cells * 0.5f;
        float top = rope.center.y + rope.cells * 0.5f;
        return cell.y + 1 > bottom && cell.y < top;
    }

    void PaintLine(Vector2Int from, Vector2Int to, bool erase)
    {
        int x = from.x;
        int y = from.y;
        int dx = Mathf.Abs(to.x - from.x);
        int dy = -Mathf.Abs(to.y - from.y);
        int sx = from.x < to.x ? 1 : -1;
        int sy = from.y < to.y ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            ApplyCell(new Vector2Int(x, y), erase);
            if (x == to.x && y == to.y) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    // ------------------------------------------------------------------
    // 摆放 / 擦除：同一个手势里按格连续执行
    // ------------------------------------------------------------------

    bool IsPlaceTool()
    {
        switch (tool)
        {
            case Tool.Key:
            case Tool.Recall:
            case Tool.Door:
            case Tool.Rope:
            case Tool.HorizontalRope:
            case Tool.Spike:
            case Tool.Ice:
            case Tool.Magma:
            case Tool.RespawnPoint:
            case Tool.TeleportPoint:
                return true;
            default:
                return false;
        }
    }

    /// <summary>按下第一格时决定这个手势是"放"还是"删"：开关型工具点在已存在的同类上就是删。</summary>
    bool ShouldAddAtStart(Vector2Int cell)
    {
        var data = level.Data;
        switch (tool)
        {
            case Tool.Spike:
            {
                var existing = data.FindSpike(cell);
                return !(existing.HasValue && existing.Value.dir == newSpikeDir);
            }
            case Tool.Ice:
            {
                var existing = data.FindIce(cell);
                return !(existing.HasValue && existing.Value.groupId == iceGroupId);
            }
            case Tool.Magma:
            {
                var existing = data.FindMagma(cell);
                return !(existing.HasValue && existing.Value.groupId == magmaGroupId);
            }
            case Tool.RespawnPoint:
                return !data.HasRespawnPoint(cell);
            case Tool.TeleportPoint:
                return !data.HasTeleport(cell);
            case Tool.Recall:
                return !data.HasRecall(cell);
            default:
                return true;
        }
    }

    /// <summary>对单个格子执行当前摆放工具的"放"或"删"。不 Record，由手势开始时统一记一次。</summary>
    bool ApplyToolAtCell(Vector2Int cell, bool add)
    {
        var data = level.Data;
        switch (tool)
        {
            case Tool.Key:
                if (!add) return data.keys.RemoveAll(key => key.Cells().Contains(cell)) > 0;
                if (!data.ContainsCell(cell)) return false;
                data.keys.Add(new LabKeySpec(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
                return true;

            case Tool.Recall:
                if (!add) return data.RemoveRecall(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell)) return false;
                return data.PlaceRecall(cell);

            case Tool.Door:
                if (!add) return data.doors.RemoveAll(door => door.Cells().Contains(cell)) > 0;
                if (!data.ContainsCell(cell)) return false;
                if (cell.y + LabDoorSpec.CellHeight > data.bounds.yMax) return false;
                data.doors.Add(new LabDoorSpec(
                    new Vector2(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f), newDoorColor));
                return true;

            case Tool.Rope:
            case Tool.HorizontalRope:
            {
                if (!add) return data.ropes.RemoveAll(rope => RopeCoversCell(rope, cell)) > 0;

                var orientation = tool == Tool.HorizontalRope
                    ? LabRopeOrientation.Horizontal
                    : LabRopeOrientation.Vertical;
                if (orientation == LabRopeOrientation.Vertical && cell.y + ropeLength > data.bounds.yMax)
                    return false;
                if (orientation == LabRopeOrientation.Horizontal &&
                    (cell.x + ropeLength > data.bounds.xMax || cell.y + 1 > data.bounds.yMax))
                    return false;

                Vector2 center = orientation == LabRopeOrientation.Horizontal
                    ? new Vector2(cell.x + ropeLength * 0.5f, cell.y + 1.125f)
                    : new Vector2(cell.x + 0.5f, cell.y + ropeLength * 0.5f);
                data.ropes.Add(new LabRopeSpec(center, ropeLength, ropeChainId, orientation));
                return true;
            }

            case Tool.Spike:
                if (!add) return data.RemoveSpike(cell);
                if (data.HasCell(cell)) return false;
                return data.PlaceSpike(cell, newSpikeDir);

            case Tool.Ice:
                if (!add) return data.RemoveIce(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell) || data.HasMagma(cell)) return false;
                return data.PlaceIce(cell, iceGroupId);

            case Tool.Magma:
                if (!add) return data.RemoveMagma(cell);
                if (!data.ContainsCell(cell) || data.HasCell(cell) || data.HasIce(cell)) return false;
                return data.PlaceMagma(cell, magmaGroupId);

            case Tool.RespawnPoint:
                if (!add) return data.RemoveRespawnPoint(cell);
                if (!data.ContainsCell(cell)) return false;
                return data.PlaceRespawnPoint(cell);

            case Tool.TeleportPoint:
                if (!add) return data.RemoveTeleport(cell);
                if (!data.ContainsCell(cell)) return false;
                return data.PlaceTeleport(cell);
        }
        return false;
    }

    /// <summary>Bresenham 走过的格子，拖动时用来补齐，免得快速拖动漏格。</summary>
    static IEnumerable<Vector2Int> LineCells(Vector2Int from, Vector2Int to)
    {
        int x = from.x;
        int y = from.y;
        int dx = Mathf.Abs(to.x - from.x);
        int dy = -Mathf.Abs(to.y - from.y);
        int sx = from.x < to.x ? 1 : -1;
        int sy = from.y < to.y ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            yield return new Vector2Int(x, y);
            if (x == to.x && y == to.y) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    // ------------------------------------------------------------------
    // 选择 / 选区
    // ------------------------------------------------------------------

    static RectInt CellRect(Vector2 point)
        => new RectInt(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), 1, 1);

    /// <summary>这一格上最上层的物件。优先级：传送点 &gt; 重生点 &gt; 钥匙 &gt; 召回道具 &gt; 冰块/岩浆块 &gt; 地刺 &gt; 门 &gt; 绳 &gt; 地形。</summary>
    Pick PickAt(Vector2Int cell)
    {
        var data = level.Data;

        if (data.teleports != null)
            for (int i = 0; i < data.teleports.Count; i++)
                if (data.teleports[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.TeleportPoint, index = i };

        if (data.respawnPoints != null)
            for (int i = 0; i < data.respawnPoints.Count; i++)
                if (data.respawnPoints[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.RespawnPoint, index = i };

        if (data.keys != null)
            for (int i = 0; i < data.keys.Count; i++)
                if (data.keys[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.Key, index = i };

        if (data.recalls != null)
            for (int i = 0; i < data.recalls.Count; i++)
                if (data.recalls[i].cell == cell)
                    return new Pick { kind = PickKind.Recall, index = i };

        if (data.ices != null)
            for (int i = 0; i < data.ices.Count; i++)
                if (data.ices[i].cell == cell)
                    return new Pick { kind = PickKind.Ice, index = i };

        if (data.magmas != null)
            for (int i = 0; i < data.magmas.Count; i++)
                if (data.magmas[i].cell == cell)
                    return new Pick { kind = PickKind.Magma, index = i };

        if (data.spikes != null)
            for (int i = 0; i < data.spikes.Count; i++)
                if (data.spikes[i].cell == cell)
                    return new Pick { kind = PickKind.Spike, index = i };

        if (data.doors != null)
            for (int i = 0; i < data.doors.Count; i++)
                if (data.doors[i].Cells().Contains(cell))
                    return new Pick { kind = PickKind.Door, index = i };

        if (data.ropes != null)
            for (int i = 0; i < data.ropes.Count; i++)
                if (RopeCoversCell(data.ropes[i], cell))
                    return new Pick { kind = PickKind.Rope, index = i };

        if (data.solids != null)
            for (int i = 0; i < data.solids.Count; i++)
                if (data.solids[i] == cell)
                    return new Pick { kind = PickKind.Solid, index = i };

        return new Pick { kind = PickKind.None, index = -1 };
    }

    /// <summary>撤销之后索引可能失效，用之前先验一遍。</summary>
    bool ValidPick(Pick pick)
    {
        var data = level.Data;
        switch (pick.kind)
        {
            case PickKind.Solid: return pick.index >= 0 && pick.index < data.solids.Count;
            case PickKind.Rope: return pick.index >= 0 && pick.index < data.ropes.Count;
            case PickKind.Key: return pick.index >= 0 && pick.index < data.keys.Count;
            case PickKind.Recall: return pick.index >= 0 && pick.index < data.recalls.Count;
            case PickKind.Door: return pick.index >= 0 && pick.index < data.doors.Count;
            case PickKind.Spike: return pick.index >= 0 && pick.index < data.spikes.Count;
            case PickKind.Ice: return pick.index >= 0 && pick.index < data.ices.Count;
            case PickKind.Magma: return pick.index >= 0 && pick.index < data.magmas.Count;
            case PickKind.RespawnPoint: return pick.index >= 0 && pick.index < data.respawnPoints.Count;
            case PickKind.TeleportPoint: return pick.index >= 0 && pick.index < data.teleports.Count;
            default: return false;
        }
    }

    RectInt PickArea(Pick pick)
    {
        var data = level.Data;
        switch (pick.kind)
        {
            case PickKind.Solid: return new RectInt(data.solids[pick.index], Vector2Int.one);
            case PickKind.Rope: return CellRect(data.ropes[pick.index].center);
            case PickKind.Key: return CellRect(data.keys[pick.index].position);
            case PickKind.Recall: return new RectInt(data.recalls[pick.index].cell, Vector2Int.one);
            case PickKind.Door: return CellRect(data.doors[pick.index].position);
            case PickKind.Spike: return new RectInt(data.spikes[pick.index].cell, Vector2Int.one);
            case PickKind.Ice: return new RectInt(data.ices[pick.index].cell, Vector2Int.one);
            case PickKind.Magma: return new RectInt(data.magmas[pick.index].cell, Vector2Int.one);
            case PickKind.RespawnPoint: return CellRect(data.respawnPoints[pick.index].position);
            case PickKind.TeleportPoint: return CellRect(data.teleports[pick.index].position);
            default: return new RectInt(0, 0, 0, 0);
        }
    }

    string PickLabel(Pick pick)
    {
        switch (pick.kind)
        {
            case PickKind.Solid: return "地形格 " + level.Data.solids[pick.index];
            case PickKind.Rope: return "绳 #" + pick.index;
            case PickKind.Key: return "钥匙 #" + pick.index;
            case PickKind.Recall: return "召回道具 #" + pick.index;
            case PickKind.Door: return "门 #" + pick.index;
            case PickKind.Spike: return "地刺 #" + pick.index;
            case PickKind.Ice: return "冰块 #" + pick.index;
            case PickKind.Magma: return "岩浆块 #" + pick.index;
            case PickKind.RespawnPoint: return "重生点 #" + pick.index;
            case PickKind.TeleportPoint: return "传送点 #" + pick.index;
            default: return "（无）";
        }
    }

    /// <summary>拖动时作用到的范围：有选区就是选区，否则是点中的那个物件。</summary>
    IList<RectInt> CurrentMoveAreas()
    {
        if (selection.Count > 0) return selection;
        if (!ValidPick(picked)) return null;
        return new List<RectInt> { PickArea(picked) };
    }

    void DeleteSelection()
    {
        bool hasSelection = selection.Count > 0;
        if (!hasSelection && !ValidPick(picked)) return;

        Record("删除选区内容");

        bool did;
        if (hasSelection)
        {
            did = level.Data.DeleteContents(selection, true) > 0;
        }
        else
        {
            var area = new List<RectInt> { PickArea(picked) };
            did = level.Data.DeleteContents(area, picked.kind == PickKind.Solid) > 0;
        }

        picked = default;
        if (!did)
        {
            Repaint();
            return;
        }
        AfterStructuralEdit();
    }

    static RectInt MarqueeRect(Vector2Int a, Vector2Int b)
    {
        int x0 = Mathf.Min(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y);
        return new RectInt(x0, y0, Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
    }

    void CommitMarquee()
    {
        RectInt rect = MarqueeRect(marqueeStart, marqueeEnd);
        bool empty = rect.width <= 1 && rect.height <= 1;

        if (empty)
        {
            // 单击不产生选区；重新选择模式下单击 = 清掉当前选区。
            if (marqueeMode == MarqueeMode.Replace) selection.Clear();
            return;
        }

        switch (marqueeMode)
        {
            case MarqueeMode.Replace:
                selection.Clear();
                AddRect(selection, rect);
                break;
            case MarqueeMode.Add:
                UnionRect(selection, rect);
                break;
            case MarqueeMode.Subtract:
                SubtractRect(selection, rect);
                break;
        }
    }

    static void AddRect(List<RectInt> list, RectInt rect)
    {
        if (rect.width > 0 && rect.height > 0) list.Add(rect);
    }

    /// <summary>
    /// 把一块矩形并进选区。选区用一组互不重叠的矩形表示：先把新矩形从旧矩形里挖掉，
    /// 再把新矩形加回去，结果正好是并集，且不会留下重叠。
    /// </summary>
    internal static void UnionRect(List<RectInt> list, RectInt rect)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        SubtractRect(list, rect);
        list.Add(rect);
    }

    /// <summary>从一组选区里挖掉一块：被切开的部分按 左/右/上/下 拆成最多四块。</summary>
    internal static void SubtractRect(List<RectInt> list, RectInt cut)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            RectInt rect = list[i];
            if (rect.x >= cut.x + cut.width || cut.x >= rect.x + rect.width ||
                rect.y >= cut.y + cut.height || cut.y >= rect.y + rect.height)
                continue;

            list.RemoveAt(i);

            int x0 = rect.x, x1 = rect.x + rect.width;
            int y0 = rect.y, y1 = rect.y + rect.height;
            int cx0 = cut.x, cx1 = cut.x + cut.width;
            int cy0 = cut.y, cy1 = cut.y + cut.height;

            AddRect(list, new RectInt(x0, y0, cx0 - x0, y1 - y0));           // 左
            AddRect(list, new RectInt(cx1, y0, x1 - cx1, y1 - y0));          // 右
            int midX0 = Mathf.Max(x0, cx0);
            int midX1 = Mathf.Min(x1, cx1);
            AddRect(list, new RectInt(midX0, y0, midX1 - midX0, cy0 - y0));  // 下
            AddRect(list, new RectInt(midX0, cy1, midX1 - midX0, y1 - cy1)); // 上
        }
    }

    /// <summary>选区的格数。矩形互不重叠，所以直接累加面积。</summary>
    static int SelectionArea(IList<RectInt> rects)
    {
        int area = 0;
        for (int i = 0; i < rects.Count; i++) area += rects[i].width * rects[i].height;
        return area;
    }

    /// <summary>选区的包围盒，只用于面板显示和标签定位。</summary>
    static RectInt SelectionBounds(IList<RectInt> rects)
    {
        if (rects.Count == 0) return new RectInt();
        int xMin = int.MaxValue, yMin = int.MaxValue;
        int xMax = int.MinValue, yMax = int.MinValue;
        for (int i = 0; i < rects.Count; i++)
        {
            RectInt rect = rects[i];
            xMin = Mathf.Min(xMin, rect.x);
            yMin = Mathf.Min(yMin, rect.y);
            xMax = Mathf.Max(xMax, rect.x + rect.width);
            yMax = Mathf.Max(yMax, rect.y + rect.height);
        }
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    /// <summary>平移选区矩形。拖动选区时框和内容保持相对位置一起走。</summary>
    internal static void TranslateRects(List<RectInt> rects, Vector2Int delta)
    {
        if (delta == Vector2Int.zero) return;
        for (int i = 0; i < rects.Count; i++)
        {
            RectInt rect = rects[i];
            rect.x += delta.x;
            rect.y += delta.y;
            rects[i] = rect;
        }
    }

    /// <summary>
    /// 只保留四个快捷键：1 铺地形 / 3 擦地形 / 4 选区 / 5 选择。
    /// 不要新增或恢复其它快捷键；其它工具一律从工具面板选择。
    /// </summary>
    bool HandleKey(Event e)
    {
        // 拖动过程中不让工具键改工具，否则一个手势会跨到别的工具上。
        if (gesture) return false;

        switch (e.keyCode)
        {
            case KeyCode.Alpha1: tool = Tool.Paint; break;
            case KeyCode.Alpha3: tool = Tool.Erase; break;
            case KeyCode.Alpha4: tool = Tool.Marquee; break;
            case KeyCode.Alpha5: tool = Tool.Move; break;
            default: return false;
        }

        Repaint();
        e.Use();
        return true;
    }

    // ------------------------------------------------------------------
    // 绘制
    // ------------------------------------------------------------------

    void DrawGrid(LabLevelData data)
    {
        RectInt area = EditArea(data);
        // 范围特别大时抽稀网格，避免一次画几千条线把 Scene 视图拖死。
        int stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(area.width, area.height) / 400f));

        Handles.color = new Color(1f, 1f, 1f, 0.055f);
        for (int x = area.x; x <= area.x + area.width; x += stride)
            Handles.DrawLine(new Vector3(x, area.y, 0f), new Vector3(x, area.y + area.height, 0f));
        for (int y = area.y; y <= area.y + area.height; y += stride)
            Handles.DrawLine(new Vector3(area.x, y, 0f), new Vector3(area.x + area.width, y, 0f));

        // 每 4 格一条亮线，方便读关卡尺寸
        int major = 4 * stride;
        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        for (int x = Mathf.CeilToInt(area.x / (float)major) * major; x <= area.x + area.width; x += major)
            Handles.DrawLine(new Vector3(x, area.y, 0f), new Vector3(x, area.y + area.height, 0f));
        for (int y = Mathf.CeilToInt(area.y / (float)major) * major; y <= area.y + area.height; y += major)
            Handles.DrawLine(new Vector3(area.x, y, 0f), new Vector3(area.x + area.width, y, 0f));
    }

    void DrawBoundsHandle(LabLevelData data)
    {
        RectInt b = data.bounds;
        var p0 = new Vector3(b.x, b.y, 0f);
        var p1 = new Vector3(b.x + b.width, b.y, 0f);
        var p2 = new Vector3(b.x + b.width, b.y + b.height, 0f);
        var p3 = new Vector3(b.x, b.y + b.height, 0f);

        Handles.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Handles.DrawLine(p0, p1);
        Handles.DrawLine(p1, p2);
        Handles.DrawLine(p2, p3);
        Handles.DrawLine(p3, p0);
        Handles.Label(new Vector3(b.x + 0.5f, b.y + b.height - 0.5f, 0f), "编辑范围");
    }

    void DrawRopeHandles(LabLevelData data)
    {
        if (data.ropes == null) return;
        Handles.color = new Color(0.85f, 0.65f, 0.35f, 0.9f);
        for (int i = 0; i < data.ropes.Count; i++)
        {
            var rope = data.ropes[i];
            Vector3 size = rope.orientation == LabRopeOrientation.Horizontal
                ? new Vector3(rope.cells, 0.25f, 0.01f)
                : new Vector3(RopeStripWidth, rope.cells, 0.01f);
            Handles.DrawWireCube(new Vector3(rope.center.x, rope.center.y, 0f), size);
        }
    }

    void DrawKeyDoorHandles(LabLevelData data)
    {
        if (data.keys != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            for (int i = 0; i < data.keys.Count; i++)
            {
                var key = data.keys[i];
                Handles.DrawWireCube(new Vector3(key.position.x, key.position.y, 0f), Vector3.one);
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                var door = data.doors[i];
                Handles.color = new Color(door.color.r, door.color.g, door.color.b, 0.9f);
                Handles.DrawWireCube(
                    new Vector3(door.position.x, door.position.y, 0f),
                    new Vector3(1f, LabDoorSpec.CellHeight, 0.01f));
            }
        }
    }

    void DrawRecallHandles(LabLevelData data)
    {
        if (data.recalls == null || data.recalls.Count == 0) return;
        Handles.color = new Color(0.2f, 0.9f, 0.8f, 0.95f);
        for (int i = 0; i < data.recalls.Count; i++)
        {
            var recall = data.recalls[i];
            Handles.DrawWireCube(CellCenter(recall.cell), Vector3.one);
            Handles.Label(CellCenter(recall.cell) + Vector3.up * 0.6f, "召回");
        }
    }

    void DrawSelectionRegion()
    {
        if (selection.Count > 0)
        {
            Color fill = new Color(0.35f, 0.9f, 1f, 0.10f);
            for (int i = 0; i < selection.Count; i++)
            {
                RectInt rect = selection[i];
                // 只铺底色，不画每块自己的边；外轮廓统一在下面按并集画。
                Handles.DrawSolidRectangleWithOutline(
                    RectVerts(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height),
                    fill, Color.clear);
            }

            Handles.color = new Color(0.35f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < selection.Count; i++)
            {
                RectInt rect = selection[i];
                DrawRegionEdge(selection, i, true, rect.y, false);              // 下边界
                DrawRegionEdge(selection, i, true, rect.y + rect.height, true); // 上边界
                DrawRegionEdge(selection, i, false, rect.x, false);             // 左边界
                DrawRegionEdge(selection, i, false, rect.x + rect.width, true); // 右边界
            }

            RectInt bounds = SelectionBounds(selection);
            Handles.Label(new Vector3(bounds.x + 0.5f, bounds.y + bounds.height - 0.5f, 0f),
                "选区 " + SelectionArea(selection) + " 格");
        }

        if (!marqueeDragging) return;

        RectInt preview = MarqueeRect(marqueeStart, marqueeEnd);
        DrawFilledRect(preview.x, preview.y, preview.x + preview.width - 1, preview.y + preview.height - 1,
            new Color(1f, 0.9f, 0.35f, 0.16f), new Color(1f, 0.9f, 0.35f, 0.95f));
        Handles.Label(new Vector3(preview.x + 0.5f, preview.y + preview.height - 0.5f, 0f),
            preview.width + "×" + preview.height);
    }

    /// <summary>
    /// 画选区的一块外边界：挖掉同一条边上相邻矩形覆盖的区间，只画真正露在外面的线段。
    /// 这样多块矩形拼出来的选区看起来仍是一个整体，不会出现内部接缝。
    /// towardPositive 表示相邻矩形在边的正方向一侧：上边界 / 右边界为 true。
    /// </summary>
    static void DrawRegionEdge(List<RectInt> region, int index, bool horizontal, int edge, bool towardPositive)
    {
        RectInt rect = region[index];
        int start = horizontal ? rect.x : rect.y;
        int end = horizontal ? rect.x + rect.width : rect.y + rect.height;

        var covered = new List<Vector2Int>();
        for (int i = 0; i < region.Count; i++)
        {
            if (i == index) continue;
            RectInt other = region[i];
            if (horizontal)
            {
                int otherEdge = towardPositive ? other.y : other.y + other.height;
                if (otherEdge != edge) continue;
                int from = Mathf.Max(start, other.x);
                int to = Mathf.Min(end, other.x + other.width);
                if (from < to) covered.Add(new Vector2Int(from, to));
            }
            else
            {
                int otherEdge = towardPositive ? other.x : other.x + other.width;
                if (otherEdge != edge) continue;
                int from = Mathf.Max(start, other.y);
                int to = Mathf.Min(end, other.y + other.height);
                if (from < to) covered.Add(new Vector2Int(from, to));
            }
        }

        covered.Sort((a, b) => a.x.CompareTo(b.x));
        int cursor = start;
        for (int i = 0; i < covered.Count && cursor < end; i++)
        {
            Vector2Int span = covered[i];
            if (span.x > cursor) DrawRegionLine(horizontal, edge, cursor, span.x);
            cursor = Mathf.Max(cursor, span.y);
        }
        if (cursor < end) DrawRegionLine(horizontal, edge, cursor, end);
    }

    static void DrawRegionLine(bool horizontal, int edge, int from, int to)
    {
        if (from >= to) return;
        if (horizontal)
            Handles.DrawLine(new Vector3(from, edge, 0f), new Vector3(to, edge, 0f));
        else
            Handles.DrawLine(new Vector3(edge, from, 0f), new Vector3(edge, to, 0f));
    }

    void DrawPickedHighlight()
    {
        if (!ValidPick(picked)) return;
        RectInt area = PickArea(picked);
        DrawFilledRect(area.x, area.y, area.x + area.width - 1, area.y + area.height - 1,
            new Color(1f, 0.55f, 0.2f, 0.10f), new Color(1f, 0.55f, 0.2f, 0.95f));
        Handles.Label(new Vector3(area.x + 0.5f, area.y + area.height - 0.5f, 0f), PickLabel(picked));
    }

    void DrawSpawnHandles(LabLevelData data)
    {
        DrawPlayerMarker(data.spawnA, TuningLab.A_Pixels / (float)TuningLab.CellPixels, "A", TuningLab.ColorA);
        DrawPlayerMarker(data.spawnB, TuningLab.B_Pixels / (float)TuningLab.CellPixels, "B", TuningLab.ColorB);
    }

    void DrawPartitionHandles(LabLevelData data)
    {
        if (data.partitions == null) return;

        for (int i = 0; i < data.partitions.Count; i++)
        {
            var partition = data.partitions[i];
            if (partition == null) continue;

            Rect rect = LabLevelData.ToRect(partition.bounds);
            bool selected = i == partitionIndex;
            Handles.color = selected
                ? new Color(1f, 0.85f, 0.3f, 0.95f)
                : new Color(1f, 0.75f, 0.2f, 0.35f);
            Handles.DrawWireCube(rect.center, new Vector3(rect.width, rect.height, 0.01f));

            string label = string.IsNullOrEmpty(partition.name) ? "Level" : partition.name;
            Handles.Label(new Vector3(rect.xMin + 0.5f, rect.yMax - 0.5f, 0f),
                (selected ? "▶ " : "") + label);
        }

        if (!partitionCreating) return;

        RectInt bounds = PreviewPartitionBounds();
        bool overlaps = data.OverlapsAny(bounds);
        Color fill = overlaps ? new Color(1f, 0.25f, 0.2f, 0.2f) : new Color(1f, 0.85f, 0.3f, 0.12f);
        Color line = overlaps ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
        DrawFilledRect(bounds.x, bounds.y, bounds.x + bounds.width - 1, bounds.y + bounds.height - 1, fill, line);
        Handles.Label(new Vector3(bounds.x + 0.5f, bounds.y + bounds.height - 0.5f, 0f),
            overlaps ? "重叠，不能创建" : "新分区 " + bounds.width + "×" + bounds.height);
    }

    void DrawPartitionHover(Vector2Int cell)
    {
        var data = level.Data;

        if (partitionCreating)
        {
            RectInt bounds = PreviewPartitionBounds();
            bool overlaps = data.OverlapsAny(bounds);
            Color fill = overlaps ? new Color(1f, 0.25f, 0.2f, 0.25f) : new Color(1f, 0.85f, 0.3f, 0.15f);
            Color line = overlaps ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            DrawFilledRect(bounds.x, bounds.y, bounds.x + bounds.width - 1, bounds.y + bounds.height - 1, fill, line);
            return;
        }

        int index = data.FindPartition(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
        Handles.color = index >= 0
            ? new Color(1f, 0.85f, 0.3f, 0.9f)
            : new Color(0.4f, 0.9f, 1f, 0.7f);
        Handles.DrawWireCube(CellCenter(cell), Vector3.one);
        if (index >= 0)
            Handles.Label(CellCenter(cell) + Vector3.up * 0.8f, "拖动移动");
    }

    void DrawCameraFrames(LabLevelData data)
    {
        // 以出生点估算相机起点：脚底 + 两名玩家半高的平均 + LabCamera.offset(0, 1)
        Vector2 mid = (data.spawnA + data.spawnB) * 0.5f;
        mid.y += (TuningLab.A_Pixels + TuningLab.B_Pixels) / (4f * TuningLab.CellPixels);
        mid.y += 1f;
        var center = new Vector3(mid.x, mid.y, 0f);

        float min = data.cameraMinSize;
        float max = data.cameraMaxSize;

        // B 的位置决定开局用哪一关；视野框要按那个分区画，不能只看 LabLevelData。
        if (lab != null)
        {
            Vector2 probe = data.spawnB;
            probe.y += TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
            lab.GetCameraFrame(probe, out _, out min, out max);
        }

        DrawCameraFrame(center, min, new Color(0.3f, 0.9f, 1f, 0.55f), "最小视野");
        if (max > min + 0.01f)
            DrawCameraFrame(center, max, new Color(1f, 0.6f, 0.2f, 0.3f), "最大视野");
    }

    static void DrawCameraFrame(Vector3 center, float size, Color color, string label)
    {
        float height = size * 2f;
        float width = height * (16f / 9f);
        Handles.color = color;
        Handles.DrawWireCube(center, new Vector3(width, height, 0.01f));
        Handles.Label(new Vector3(center.x - width * 0.5f + 0.5f, center.y + height * 0.5f - 0.5f, 0f), label);
    }

    void DrawPlayerMarker(Vector2 feet, float size, string label, Color color)
    {
        float half = size * 0.5f;
        var center = new Vector3(feet.x, feet.y + half, 0f);
        Handles.color = color;
        Handles.DrawWireCube(center, new Vector3(size, size, 0.01f));
        Handles.Label(center + Vector3.up * (half + 0.2f), label + " 落点");
        Handles.DrawLine(center, new Vector3(feet.x, feet.y, 0f));
    }

    void DrawSpikeHandles(LabLevelData data)
    {
        if (data.spikes == null) return;
        for (int i = 0; i < data.spikes.Count; i++)
        {
            var spike = data.spikes[i];
            // 底座没有实心支撑的刺画红，有支撑的画钢灰。
            Handles.color = data.HasCell(spike.BaseCell)
                ? new Color(0.75f, 0.82f, 0.92f, 0.95f)
                : new Color(1f, 0.35f, 0.3f, 0.95f);
            var center = spike.StripCenter;
            var size = spike.StripSize;
            Handles.DrawWireCube(new Vector3(center.x, center.y, 0f),
                new Vector3(size.x, size.y, 0.01f));
        }
    }

    void DrawIceHandles(LabLevelData data)
    {
        if (data.ices == null) return;
        for (int i = 0; i < data.ices.Count; i++)
        {
            var ice = data.ices[i];
            bool blocked = data.HasCell(ice.cell) || data.HasMagma(ice.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : IceGroupColor(ice.groupId);
            Handles.DrawWireCube(CellCenter(ice.cell), Vector3.one);
        }

    }

    void DrawMagmaHandles(LabLevelData data)
    {
        if (data.magmas == null) return;
        for (int i = 0; i < data.magmas.Count; i++)
        {
            var magma = data.magmas[i];
            bool blocked = data.HasCell(magma.cell) || data.HasIce(magma.cell);
            Handles.color = blocked ? new Color(1f, 0.35f, 0.3f, 0.95f) : MagmaGroupColor(magma.groupId);
            Handles.DrawWireCube(CellCenter(magma.cell), Vector3.one);
        }
    }

    void DrawRespawnPointHandles(LabLevelData data)
    {
        if (data.respawnPoints == null) return;
        Handles.color = new Color(0.35f, 1f, 0.65f, 0.95f);
        for (int i = 0; i < data.respawnPoints.Count; i++)
        {
            var point = data.respawnPoints[i];
            Handles.DrawWireCube(new Vector3(point.position.x, point.position.y, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(point.position.x - 1.4f, point.position.y + 1.6f, 0f), "重生点");
        }
    }

    void DrawTeleportHandles(LabLevelData data)
    {
        if (data.teleports == null || data.teleports.Count == 0) return;

        // 全互联：每两个点之间连一条线，说明进入任意一个都能选其它点作终点。
        Handles.color = new Color(0.75f, 0.55f, 1f, 0.28f);
        for (int i = 0; i < data.teleports.Count; i++)
            for (int j = i + 1; j < data.teleports.Count; j++)
                Handles.DrawLine(
                    new Vector3(data.teleports[i].position.x, data.teleports[i].position.y, 0f),
                    new Vector3(data.teleports[j].position.x, data.teleports[j].position.y, 0f));

        Handles.color = new Color(0.8f, 0.5f, 1f, 0.95f);
        for (int i = 0; i < data.teleports.Count; i++)
        {
            var point = data.teleports[i];
            Handles.DrawWireCube(new Vector3(point.position.x, point.position.y, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(point.position.x - 1.4f, point.position.y + 1.6f, 0f), "传送点");
        }
    }

    static Color IceGroupColor(int groupId)
    {
        float hue = Mathf.Repeat(groupId * 0.37f, 1f);
        return Color.HSVToRGB(hue, 0.55f, 1f);
    }

    /// <summary>岩浆块用暖色区分组号，和冰块的冷色调分开。</summary>
    static Color MagmaGroupColor(int groupId)
    {
        float hue = Mathf.Lerp(0f, 0.14f, Mathf.Repeat(groupId * 0.37f, 1f));
        float value = 0.72f + 0.28f * Mathf.Repeat(groupId * 0.53f, 1f);
        return Color.HSVToRGB(hue, 0.85f, value);
    }

    void DrawHover(Vector2Int cell, bool inArea, bool inBounds, LabLevelData data)
    {
        if (tool == Tool.Partition)
        {
            DrawPartitionHover(cell);
            return;
        }

        if (tool == Tool.Marquee)
        {
            RectInt preview = marqueeDragging
                ? MarqueeRect(marqueeStart, marqueeEnd)
                : new RectInt(cell.x, cell.y, 1, 1);
            DrawFilledRect(preview.x, preview.y, preview.x + preview.width - 1, preview.y + preview.height - 1,
                new Color(0.35f, 0.9f, 1f, 0.12f), new Color(0.35f, 0.9f, 1f, 0.9f));
            return;
        }

        if (tool == Tool.Move)
        {
            Handles.color = new Color(1f, 0.55f, 0.2f, 0.9f);

            // 有选区时移动工具不按鼠标位置挑物件，任何位置起手都是拖整个选区。
            if (selection.Count > 0)
            {
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "拖动整个选区");
                return;
            }

            var hover = PickAt(cell);
            if (hover.kind == PickKind.None)
            {
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            RectInt area = PickArea(hover);
            Handles.DrawWireCube(
                new Vector3(area.x + area.width * 0.5f, area.y + area.height * 0.5f, 0f),
                new Vector3(area.width, area.height, 0.01f));
            return;
        }

        if (!inArea) return;

        Color color = ToolColor();
        Handles.color = color;

        if (tool == Tool.Rect && gesture)
        {
            int x0 = Mathf.Min(rectStart.x, rectEnd.x);
            int y0 = Mathf.Min(rectStart.y, rectEnd.y);
            int x1 = Mathf.Max(rectStart.x, rectEnd.x);
            int y1 = Mathf.Max(rectStart.y, rectEnd.y);
            DrawFilledRect(x0, y0, x1, y1, new Color(color.r, color.g, color.b, 0.18f), color);
            return;
        }

        if (tool == Tool.Rope || tool == Tool.HorizontalRope)
        {
            if (!inBounds)
            {
                Handles.color = new Color(1f, 0.3f, 0.25f, 0.9f);
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            bool horizontal = tool == Tool.HorizontalRope;
            var center = horizontal
                ? new Vector3(cell.x + ropeLength * 0.5f, cell.y + 1.125f, 0f)
                : new Vector3(cell.x + 0.5f, cell.y + ropeLength * 0.5f, 0f);
            var size = horizontal
                ? new Vector3(ropeLength, 0.25f, 0.01f)
                : new Vector3(RopeStripWidth, ropeLength, 0.01f);
            Handles.DrawWireCube(center, size);
            Handles.Label(center + (horizontal ? Vector3.up * 0.3f : Vector3.up * (ropeLength * 0.5f + 0.3f)),
                (horizontal ? "横绳 " : "竖绳 ") + ropeLength + " 格");
            return;
        }

        if (tool == Tool.SpawnA || tool == Tool.SpawnB)
        {
            if (!inBounds)
            {
                Handles.color = new Color(1f, 0.3f, 0.25f, 0.9f);
                Handles.DrawWireCube(CellCenter(cell), Vector3.one);
                return;
            }

            float size = tool == Tool.SpawnA
                ? TuningLab.A_Pixels / (float)TuningLab.CellPixels
                : TuningLab.B_Pixels / (float)TuningLab.CellPixels;
            var center = new Vector3(cell.x + 0.5f, cell.y + size * 0.5f, 0f);
            Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            Handles.DrawWireCube(center, new Vector3(size, size, 0.01f));
            return;
        }

        if (tool == Tool.Key)
        {
            Handles.color = inBounds
                ? new Color(color.r, color.g, color.b, 0.6f)
                : new Color(1f, 0.3f, 0.25f, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            return;
        }

        if (tool == Tool.Recall)
        {
            bool blocked = !inBounds || data.HasCell(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.75f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f,
                data.HasRecall(cell) ? "召回道具（再点删除）" : "召回道具");
            return;
        }

        if (tool == Tool.Door)
        {
            Handles.color = inBounds
                ? new Color(color.r, color.g, color.b, 0.7f)
                : new Color(1f, 0.3f, 0.25f, 0.9f);
            Handles.DrawWireCube(
                new Vector3(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f, 0f),
                new Vector3(1f, LabDoorSpec.CellHeight, 0.01f));
            return;
        }

        if (tool == Tool.Spike)
        {
            var spec = new LabSpikeSpec(cell, newSpikeDir);
            bool blocked = data.HasCell(cell);
            bool floating = !data.HasCell(spec.BaseCell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, floating ? 0.45f : 0.9f);

            var center = spec.StripCenter;
            var size = spec.StripSize;
            Handles.DrawWireCube(new Vector3(center.x, center.y, 0f),
                new Vector3(size.x, size.y, 0.01f));
            // 整格描一圈：点下去的是这一格，刺只占靠边的那 6 像素。
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            if (blocked) Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "实心格里放不下");
            else if (floating) Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "底座悬空");
            return;
        }

        if (tool == Tool.Ice)
        {
            bool blocked = !inBounds || data.HasCell(cell) || data.HasSpike(cell) || data.HasMagma(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(IceGroupColor(iceGroupId).r, IceGroupColor(iceGroupId).g,
                            IceGroupColor(iceGroupId).b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "冰 组 " + iceGroupId);
            return;
        }

        if (tool == Tool.Magma)
        {
            bool blocked = !inBounds || data.HasCell(cell) || data.HasSpike(cell) || data.HasIce(cell);
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(MagmaGroupColor(magmaGroupId).r, MagmaGroupColor(magmaGroupId).g,
                            MagmaGroupColor(magmaGroupId).b, 0.9f);
            Handles.DrawWireCube(CellCenter(cell), Vector3.one);
            Handles.Label(CellCenter(cell) + Vector3.up * 0.6f, "岩浆 组 " + magmaGroupId);
            return;
        }

        if (tool == Tool.RespawnPoint)
        {
            bool blocked = !inBounds;
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.75f);
            Handles.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(cell.x + 0.5f, cell.y + 2.1f, 0f), "重生点");
            return;
        }

        if (tool == Tool.TeleportPoint)
        {
            bool blocked = !inBounds;
            Handles.color = blocked
                ? new Color(1f, 0.3f, 0.25f, 0.9f)
                : new Color(color.r, color.g, color.b, 0.8f);
            Handles.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                new Vector3(3f, 3f, 0.01f));
            Handles.Label(new Vector3(cell.x + 0.5f, cell.y + 2.1f, 0f),
                data.HasTeleport(cell) ? "传送点（再点删除）" : "传送点");
            return;
        }

        Handles.color = new Color(color.r, color.g, color.b, 0.18f);
        Handles.DrawSolidRectangleWithOutline(
            RectVerts(cell.x, cell.y, cell.x + 1, cell.y + 1),
            new Color(color.r, color.g, color.b, 0.16f),
            color);
    }

    void DrawFilledRect(int x0, int y0, int x1, int y1, Color fill, Color outline)
    {
        Handles.DrawSolidRectangleWithOutline(RectVerts(x0, y0, x1 + 1, y1 + 1), fill, outline);
    }

    static Vector3[] RectVerts(int x0, int y0, int x1, int y1)
    {
        return new[]
        {
            new Vector3(x0, y0, 0f),
            new Vector3(x1, y0, 0f),
            new Vector3(x1, y1, 0f),
            new Vector3(x0, y1, 0f)
        };
    }

    Color ToolColor()
    {
        switch (tool)
        {
            case Tool.Erase: return new Color(1f, 0.35f, 0.3f, 0.95f);
            case Tool.Rect: return new Color(1f, 0.85f, 0.3f, 0.95f);
            case Tool.Rope:
            case Tool.HorizontalRope: return new Color(0.85f, 0.65f, 0.35f, 0.95f);
            case Tool.SpawnA: return TuningLab.ColorA;
            case Tool.SpawnB: return TuningLab.ColorB;
            case Tool.Partition: return new Color(1f, 0.85f, 0.3f, 0.95f);
            case Tool.Key: return new Color(1f, 0.85f, 0.1f, 0.95f);
            case Tool.Recall: return new Color(0.2f, 0.9f, 0.8f, 0.95f);
            case Tool.Door: return new Color(newDoorColor.r, newDoorColor.g, newDoorColor.b, 0.95f);
            case Tool.Spike: return new Color(0.75f, 0.82f, 0.92f, 0.95f);
            case Tool.Ice: return new Color(0.42f, 0.75f, 1f, 0.95f);
            case Tool.Magma: return new Color(1f, 0.34f, 0.12f, 0.95f);
            case Tool.RespawnPoint: return new Color(0.35f, 1f, 0.65f, 0.95f);
            case Tool.TeleportPoint: return new Color(0.8f, 0.5f, 1f, 0.95f);
            case Tool.Marquee: return new Color(0.35f, 0.9f, 1f, 0.95f);
            case Tool.Move: return new Color(1f, 0.55f, 0.2f, 0.95f);
            default: return new Color(1f, 0.85f, 0.3f, 0.95f);
        }
    }

    static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

    static Vector3 ScenePoint(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    static Vector2Int CellOf(Vector3 point)
        => new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

    static bool InEditArea(Vector2Int cell, LabLevelData data)
    {
        RectInt area = EditArea(data);
        return cell.x >= area.x && cell.x < area.x + area.width
            && cell.y >= area.y && cell.y < area.y + area.height;
    }

    static RectInt EditArea(LabLevelData data)
    {
        return new RectInt(
            data.bounds.x - EditMargin,
            data.bounds.y - EditMargin,
            data.bounds.width + EditMargin * 2,
            data.bounds.height + EditMargin * 2);
    }

    static void Notify(SceneView view, string message)
    {
        if (view == null) return;
        view.ShowNotification(new GUIContent(message));
    }
}
#endif
