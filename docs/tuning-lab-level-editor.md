# Tuning Lab 关卡编辑器：架构、扩展与验证

> 2026-11 这一轮从"World 场景关卡编辑器规范不对齐、不能用"开始，最后收敛成：
> **Tuning Lab 是唯一基准，所有关卡从一个 Tuning Lab 派生场景里用「关卡分区」组织。**
> 本文记录这套东西的边界、代码地图、扩展方法、调试/验证流程和踩过的坑。

## 现有场景

| 场景 | 用途 |
| --- | --- |
| `Assets/Scenes/TuningLab.unity` | 规则基准场景，禁止手改；改规则改 `Assets/TuningLab/` 代码 |
| `Assets/Scenes/Levels/Level_01.unity` | **正式关卡**，将来的游戏地图，内容往上堆 |
| `Assets/Scenes/Levels/ddebug room.unity` | **调试房**：给程序验证新组件用，可以随意改坏，不按关卡内容维护 |

新增正式关卡走 `GameJam → New Level From Tuning Lab`。
验新组件先在 `ddebug room` 里试通，再往 `Level_01` 上放，避免把没定稿的东西混进正式地图。

## 0. 一句话

规则、玩家、相机、碰撞全部只跑 `Assets/TuningLab` 这一套；关卡场景必须从 `TuningLab.unity` 派生；
连续关卡不再新建 scene，而是在同一个 scene 里用 `LabLevelData.partitions` 切分，**B 的位置**决定当前分区和相机范围。

## 1. 边界

- **唯一规则基准**：`Assets/TuningLab/`。速度等级、跳/冲/撞、玩家尺寸、相机都以这里为准。
- **1 格 = 1 世界单位 = 16px**；原生分辨率 384×216；所有位置、尺寸、速度都用格描述。
- **关卡场景必须从 Tuning Lab 派生**：复制 `TuningLab.unity`，挂 `LabLevel`，用 `TuningLab.CaptureBaseline()` 写入基准房间。
- **一个 scene 放全部关卡**：`LabLevelData` 里一份地形/绳/出生点 + 多个 `LabLevelPartition`。
- **不再维护旧 World/Prototype/Materials/Forms 场景和脚本**；`Assets` 只保留 TuningLab、TuningLab 场景/Levels、URP Settings。
- 旧文档（README/策划案等）仍保留，但描述的是清理前的旧游戏；以本文和 `Assets/TuningLab` 代码为准。

## 2. 代码地图

| 文件 | 职责 |
| --- | --- |
| `Assets/TuningLab/TuningLab.cs` | 参数、内置房间描述 `DescribeRoom`、`CaptureBaseline`、相机 `GetCameraFrame`、HUD/网格 |
| `Assets/TuningLab/LabPlayer.cs` | 方块玩家：移动、跳、四向冲刺、撞击、驮带 |
| `Assets/TuningLab/LabCamera.cs` | 跟随 A/B 中点；按 B 查当前分区的 bounds/min-max，并做混合 |
| `Assets/TuningLab/LabVerticalRope.cs` | 竖绳；`ApplyShape` 同时服务预览和 Play |
| `Assets/TuningLab/LabKey.cs` | 钥匙；1×1 触发体，`ApplyShape` 同时服务预览和 Play |
| `Assets/TuningLab/LabDoor.cs` | 门；1×3，任意一把钥匙开门，`ApplyShape` 同时服务预览和 Play |
| `Assets/TuningLab/Level/LabLevelData.cs` | 关卡数据：实心格、绳、**钥匙、门**、出生点、相机尺寸、**关卡分区** |
| `Assets/TuningLab/Level/LabLevel.cs` | 数据 → 合并 `BoxCollider2D` / `LabVerticalRope` / `LabKey` / `LabDoor`；编辑器预览和 Play 共用 |
| `Assets/TuningLab/Level/Editor/LabLevelScene.cs` | 从 `TuningLab.unity` 派生关卡场景；默认 `Level 1` 分区 |
| `Assets/TuningLab/Level/Editor/LabLevelEditorWindow.cs` | 关卡编辑器：地形/绳/出生点/分区/相机面板 |
| `Assets/TuningLab/Level/Editor/LabLevelSelfCheck.cs` | 纯数据自检（基准 73 格 ↔ 8 块、分区移动/删除、相机分区内外） |
| `Assets/TuningLab/Level/Editor/LabLevelPlayCheck.cs` | 临时派关卡 → 进 Play → 真实物理检查 → 退出/删场景 |

## 3. 数据与生成

### 3.1 `LabLevelData`

- `solids`：实心格；`ropes`：竖绳；`keys`：钥匙（1×1）；`doors`：门（1×3）；`spawnA/B`：脚底落点。
- 钥匙和门都是**共享**的：任意一把钥匙能开任意一扇门，门色只用于区分，不参与配对。
- 门的位置是中心，`LabDoorSpec.Cells()` 给出它盖住的 1×3 格；钥匙同理给 1×1。
- `cameraMinSize / cameraMaxSize`：**关卡级**相机跟随尺寸（正交 size，半高）。
- `bounds`：编辑范围；没有分区时也作为单关相机 clamp 范围。
- `partitions`：关卡分区列表。
- `Normalize()`：排序/去重、旧字段回落、相机尺寸 clamp、分区正尺寸。
- `CopyFrom()`：深拷贝，撤销/重置/派生都靠它。

### 3.2 `LabLevel.Build`

- 把 `solids` 合并成尽量少的矩形，每个矩形一个 `BoxCollider2D`；绳生成 `LabVerticalRope`。
- 钥匙生成 `LabKey`、门生成 `LabDoor`，尺寸/颜色/贴图由各自的 `ApplyShape()` 应用——预览里也走同一个方法，所以编辑器看得到的就是 Play 撞得到的。
- 地图重置（复活/传送触发的 `TuningLab.MapReset`）会把门关回去、把捡走的钥匙按数据重新生成；`LabLevel` 在 `BuildRuntime` 时订阅、`ClearGenerated` 时退订。
- **预览和 Play 走同一条 `Build`**：编辑器里看到的几何就是 Play 撞到的几何。
- 预览物体带 `HideFlags.DontSaveInEditor`，不写进场景；`OnEnable` 重建，`ClearGenerated` 清理。
- 运行时用 `TuningLab.WhiteSprite` / 运行时材质；不依赖外部 Sprite 资产，避免烘焙场景丢引用。

## 4. 关卡分区

### 4.1 规则

- `LabLevelPartition { name, RectInt bounds }`；`bounds` **同时是相机范围**。
- 判定点固定是 **B 的中心位置**；B 进入哪个分区，相机 clamp 就用哪个分区。
- **分区不能重叠**：`AddPartition / MovePartition / ResizePartition` 在数据层直接拒绝；贴边相邻允许。
- 移动分区时可以带内容：地形按格中心、绳按中心、钥匙/门按中心、出生点按点；`TranslatePartition` 负责平移。
- 删除分区可以选择只删分区，或连里面的地形/绳/钥匙/门一起删；出生点保留。
- 新关卡默认生成一个覆盖 `bounds` 的 `Level 1` 分区。

### 4.2 编辑器操作

- `1–9`：铺地形 / 擦地形 / 矩形 / 竖绳 / 出生点 A / 出生点 B / 关卡分区 / 钥匙（`8`）/ 门（`9`）。
- 「钥匙 / 门」面板：列出所有钥匙和门、改门色、逐条删除、清空。
- 门按"点最下面那一格、向上占 3 格"摆放；放不进相机范围时会在 Scene 视图提示。
- 分区面板：新建、命名、X/Y/宽/高、删除、移动时带物件。
- 分区工具（`7`）：空白处拖出新分区；分区内部拖动移动；重叠时预览变红、不能创建。
- 所有修改走 `Undo.RecordObject`，改完 `AfterStructuralEdit()` / `RebuildPreview()`。

### 4.3 相机

`TuningLab.GetCameraFrame(B.position, ...)` 的优先级：

| 情况 | bounds | min/max |
| --- | --- | --- |
| B 在某个分区里 | 该分区的 `bounds` | 关卡 `cameraMinSize/MaxSize` |
| B 不在任何分区（存在分区） | `NoCameraBounds`，等价于不夹 | 关卡 `cameraMinSize/MaxSize` |
| 没有分区、有 `LabLevel` | 关卡 `bounds` | 关卡尺寸 |
| 没有 `LabLevel`（基准场景） | `defaultCameraBounds` | TuningLab 默认 |

- 相机跟随 A/B 中点；只有"当前在哪个关卡"看 B。
- `LabCamera` 对 bounds/size 做 `cameraFrameBlend` 混合；出界时用超大矩形代替 `Rect.zero`，避免 rect 插值把镜头拽向原点。
- 没有分区时仍按单关相机设置；新关卡默认有分区，所以旧行为不变。

## 5. 加一个新组件

先选类型，选错落点后面全是返工：

| 类型 | 例子 | 放哪 | 进 `LabLevelData`？ |
| --- | --- | --- | --- |
| 全局系统 | 相机、输入、管理器 | `Assets/TuningLab/` | 否 |
| 一次性场景物件 | 只在一个场景手摆的触发器 | `Assets/TuningLab/Level/` | 否 |
| 关卡内容 | 弹簧、移动平台、新机关 | `Assets/TuningLab/Level/` | **是（推荐）** |

只要策划要在编辑器里摆、撤销、跟着分区移动、存盘后重新生成，就走第三条：

1. **数据**：在 `LabLevelData.cs` 加 `[Serializable]` spec、`List<>`、`Normalize`、`CopyFrom`、`Add/Move/Delete`；空间物件要接进 `MovePartition/TranslatePartition/DeletePartition`。
2. **生成**：在 `LabLevel.Build` 里生成真实物体，预览/Play 同路径；尺寸 = 碰撞 = 视觉；不要依赖已删的 `Assets/Art`。
3. **运行时**：自己的 `MonoBehaviour` 放 `Assets/TuningLab/Level/`；和玩家交互用 `LabPlayer` 公开接口/碰撞回调；不要往 `LabPlayer` 塞表现逻辑，也不要另起一套速度规则。
4. **编辑器**：`LabLevelEditorWindow` 加工具/面板、`Record`、`AfterStructuralEdit`、Handles 预览、校验。
5. **默认值**：规则/基准放 `TuningLab` Inspector；关卡内容放 `LabLevelData`。
6. **资源**：每个 `.cs/.prefab/.png` 和文件夹都要 `.meta`；`.cs` 用 LF。

## 6. 调试

- **先 Play**：TuningLab HUD 直接看速度、折算格数、等级、充能、撞击读数；`R` 重置，`P` 像素网格，`Tab` 格线；timeScale/重力/跳高/冲刺都能拖。
- **Scene 视图**：预览就是 Play 的几何；分区、相机框、编辑范围都有 Handles/Gizmos。新组件自己加 `OnDrawGizmosSelected`。
- **分而治之**：`New Level From Tuning Lab` 建小场景，只放这个组件；用分区把 B 限制在一小段单独看相机/碰撞。
- **日志**：`Debug.Log(..., this)`；`LabProbe`（`Assets/TuningLab/Diagnostics`）可落盘到 `Logs/TuningLabDiagnostics.txt`；看 `Logs/Editor.log`。
- **常见症状**：
  - 编辑器看得到、Play 没有：`autoBuild`、`TuningLab.level`、`ClearGenerated`。
  - Play 有、编辑器没有：`RebuildPreview`、`OnEnable`、预览 `HideFlags`。
  - 分区移动组件不跟着走：`MovePartition/TranslatePartition` 没接。
  - B 出界相机被夹：`GetCameraFrame` 没走 `NoCameraBounds` 分支。

## 7. 验证

工程里没有旧的 `Assets/Tests`；验证靠三条线。

### 7.1 纯数据/数学自检

在 `Assets/TuningLab/Level/Editor/` 加 `[MenuItem]` 或扩 `LabLevelSelfCheck`，提供 `RunBatch()`，
失败时 `EditorApplication.Exit(1)`：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelSelfCheck.RunBatch ^
  -logFile Logs\selfcheck.log
```

### 7.2 真实物理 Play 校验

照 `LabLevelPlayCheck`：`LabLevelScene.CreateSilent` 建临时场景 → `EditorApplication.EnterPlaymode` →
等 0.8s → 断言 → 退出并删临时场景。断言必须走真实物理结果，不要直接调私有方法绕过物理。

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelPlayCheck.RunFromCommandLine ^
  -logFile Logs\playcheck.log
```

### 7.3 手工回归

- 编辑器预览 vs Play 一致
- 摆放/撤销/重做/保存/重新打开
- 分区新建/移动/改大小/删除，移动时内容跟着走
- 钥匙/门：摆放、改门色、删除、跟着分区走；门 3 格高放得下、钥匙不在实心格里
- 分区不重叠；贴边可以
- B 在分区内 / 出界，相机 bounds 和 min/max 正确
- 旧关卡（没有新字段/没有分区）能打开
- 性能：生成物体数、Collider 数、重建频率

## 8. 踩过的坑

1. **规范不对齐时不要复用旧场景。** World 的 Tilemap/脚本和 TuningLab 的 1 格实体不是一套；先定唯一基准。
2. **一个全局相机矩形管不了连续关卡。** 用分区 + B 判定；没有分区时才退回单关范围。
3. **"无夹具"不要用 `Rect.zero` 直接插值。** rect 会缩向原点；用超大 `NoCameraBounds` 或显式 `hasBounds`。
4. **预览物体必须不存盘。** `HideFlags.DontSaveInEditor` + 保存前 `ClearGenerated`，否则场景里会多一份生成物。
5. **运行时生成的 Sprite/PhysicsMaterial 不是资产。** 烘焙场景会丢引用；数据驱动重建最稳。
6. **分区移动要包括里面的东西。** 只移动 bounds 会让内容留在原地；判定按格中心/绳中心/出生点。
7. **重叠只靠校验警告不够。** 在 `AddPartition/MovePartition/ResizePartition` 数据层直接拒绝，贴边相邻允许。
8. **Unity 批处理/ILPP 会偶发 `pipe not found`。** 这是环境问题，不是代码错误；重试一次。
9. **工作区噪音不要带进 PR。** ProjectSettings 重序列化、用户生成的 Levels/场景、packages-lock 改动先分清。
10. **旧文档会过期。** README 的旧章节和 `docs/策划案` 等仍在描述旧游戏；本轮只加了状态说明，后续单独收敛。

## 9. 当前限制 / 下一步

- 分区大小调节在面板数值；Scene 视图只有拖动移动，没有边角拖拽手柄。
- 相机 min/max 是关卡级，不是每个分区独立；要按分区调需再加字段。
- 没有 Unity Test Framework 程序集；验证靠 TuningLab 的 self-check / PlayCheck。
- README 的旧章节和 `docs/策划案` 等仍包含清理前旧游戏内容；CLAUDE 已切到 Tuning Lab-only，旧文档还需要一次收敛。
- 用户生成的 `Assets/Scenes/Levels/` 目前是 untracked，提交前要决定是否入库。

## 10. 验证记录（2026-11-02）

- `LabLevelSelfCheck.RunBatch`：通过（73 格 ↔ 8 块、分区移动/删除、重叠拒绝、B 出界无夹具）
- `LabLevelPlayCheck.RunFromCommandLine`：通过（分区内相机 bounds/尺寸、真实物理）
- 清理旧 Assets 后重跑上述两项：仍通过
- 鼠标交互未在无头模式模拟，需要 Unity 内手测
