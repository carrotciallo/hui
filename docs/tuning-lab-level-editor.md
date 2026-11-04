# Tuning Lab 关卡编辑器：先做出地图，再看原理

本文按实际工作顺序写。第一次接触项目时，先看前面的“快速开始”；需要扩展编辑器或排查底层问题时，再看后面的“原理和代码地图”。

## 快速开始

### 1. 打开并编译工程

1. 用 **Unity Hub** 打开 `Project/nameless`。
2. 使用项目要求的 Unity 版本（当前验证版本：`6000.6.4f1`）。
3. 等 Unity 完成导入和脚本编译。项目里的 `.cs` 文件会由 Unity 自动编译，不需要单独运行 C# 编译器。
4. 如果找不到基准场景，先在 Unity 菜单执行：

   `GameJam → Build Tuning Lab`

   这会生成 `Assets/Scenes/TuningLab.unity`。

需要在命令行验证“能否编译并通过数据检查”时，在仓库根目录执行：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelSelfCheck.RunBatch ^
  -logFile Logs\selfcheck.log
```

退出码为 `0` 表示通过；失败时先看 `Logs/selfcheck.log`。Unity 的 `pipe not found` 偶发是批处理环境问题，重试一次即可。

### 2. 先调试基准规则

打开：

`GameJam → Open Tuning Lab Scene`

然后点击 **Play**。这里是调试台，不是正式关卡。HUD 可以直接看到速度、格数、等级、充能和撞击读数：

- `R`：重置
- `P`：像素网格
- `Tab`：格线
- `timeScale`、重力、跳高、冲刺等参数可在 Inspector 中调

先在这里确认玩家手感、相机和碰撞符合预期。所有正式关卡都会从这个场景派生，不要在正式关卡里另做一套玩家或相机规则。

### 3. 新建自己的地图

1. 执行 `GameJam → New Level From Tuning Lab`。
2. 在 `Assets/Scenes/Levels` 下输入关卡文件名，例如 `Level_02`。
3. 打开 `GameJam → Tuning Lab Level Editor`。
4. 在 Scene 视图中编辑地图。编辑器会自动显示预览，修改后点击面板里的 **保存场景**。
5. 点击 **Play**，直接测试这张地图。

新地图已经包含 Tuning Lab 的玩家、相机和规则。不要复制旧 World 场景，也不要直接修改 `Assets/Scenes/TuningLab.unity`。

### 4. 在编辑器里摆东西

编辑器工具快捷键：

| 按键 | 工具 |
| --- | --- |
| `1` | 铺地形 |
| `2` | 擦地形 |
| `3` | 矩形地形 |
| `4` | 竖绳 |
| `5` | 出生点 A |
| `6` | 出生点 B |
| `7` | 关卡分区 |
| `8` | 钥匙 |
| `9` | 门 |
| `0` | 地刺 |
| `-` | 冰块 |

常用操作：

- 在格子上点击或拖动即可编辑。
- 门从最下面那一格开始，向上占 `3` 格。
- 地刺面板选择朝上、朝下、朝左或朝右；同一格再次放置会改方向，不会生成第二根。
- 冰块按 `groupId` 编组；同一格再次放置同组会删除，换组号再点会改组。`0` 也是普通组，「新建组」会取当前最大组号 + 1。
- 分区工具在空白处拖出分区，在分区内部拖动可移动分区；重叠分区会标红并且不能创建。
- 面板中的 **钥匙 / 门**、**地刺**、**冰块**、**分区**可以逐条删除或清空。
- 所有编辑操作都支持撤销/重做。

地图数据保存在 `LabLevelData` 中，不要直接修改预览生成出来的碰撞体。预览和 Play 使用同一条生成路径，所以编辑器里看到的几何就是运行时实际碰撞的几何。

### 5. 调试自己的地图

推荐按下面的顺序排查：

1. **只放一个新东西**：先建一张小地图，只测试这个机关或组件。
2. **看 Scene 预览**：确认位置、尺寸、朝向和相机分区正确。
3. **进 Play 测碰撞**：不要只看画面，实际走过去、跳、撞、拿钥匙、开门或触发死亡。
4. **检查分区**：B 的中心位置决定当前分区和相机范围；B 不在任何分区时，相机不应被错误夹到原点。
5. **需要日志时**：使用 `Debug.Log(..., this)`；编辑器日志在 `Logs/Editor.log`。

常见问题：

| 现象 | 先检查 |
| --- | --- |
| Scene 里有，Play 里没有 | `autoBuild`、`TuningLab.level`、`LabLevel.BuildRuntime` |
| Play 里有，Scene 里没有 | `RebuildPreview`、`OnEnable`、预览物体是否使用了 `HideFlags.DontSaveInEditor` |
| 移动分区后物件没动 | 是否接入 `MovePartition/TranslatePartition` |
| B 出界时相机被拉向原点 | `GetCameraFrame` 是否走了 `NoCameraBounds` 分支 |
| 地刺第一次碰到就删掉玩家 | 正式关卡的 `deathWhenNoCheckpoint` 是否设为 `RespawnAtSpawn` |

### 6. 提交前验证

先跑纯数据检查：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelSelfCheck.RunBatch ^
  -logFile Logs\selfcheck.log
```

再跑真实物理 Play 检查：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelPlayCheck.RunFromCommandLine ^
  -logFile Logs\playcheck.log
```

最后手工回归：

- 编辑器预览和 Play 一致；
- 摆放、撤销、重做、保存、重新打开都正常；
- 分区新建、移动、改大小、删除正常，移动时内容跟随；
- 钥匙、门、地刺的尺寸、方向、碰撞和重置正常；
- 冰块的 1×1 碰撞、组号、红热融一块、v2 碎整组和地图重置恢复正常；
- B 在分区内和分区外时，相机范围正确；
- 没有新字段或分区的旧关卡仍能打开。

## 关卡规则速查

- **唯一规则基准**：`Assets/TuningLab/`。
- **1 格 = 1 世界单位 = 16 px**；原生分辨率 `384×216`。
- 正式关卡必须从 `Assets/Scenes/TuningLab.unity` 派生。
- 多个连续关卡放在同一个 scene 中，用 `LabLevelPartition` 分区，不要为每一关复制一套规则。
- 相机跟随 A/B 中点，但只用 **B 的中心**判断当前分区。
- 分区不能重叠，贴边相邻允许。
- 钥匙和门是共享的：任意钥匙可以打开任意门，门的颜色只用于区分。
- 地刺按 `{ cell, dir }` 保存；每格最多一根，碰到即死。
- 冰块按 `{ cell, groupId }` 保存；1×1 实体，可站立/滑行。红热边（Hot1/Hot2）接触只融一块并把边回普通；组内任意一块被 v2 碰撞则整组碎。冰面按住方向按 `a = g` 加速，4 格升一级，封顶 v4；松手保留滑行。
- 预览生成物不保存到场景；运行时重新从数据生成。

### 场景用途

| 场景 | 用途 |
| --- | --- |
| `Assets/Scenes/TuningLab.unity` | 规则基准和调试台，禁止手改 |
| `Assets/Scenes/Levels/Level_01.unity` | 正式关卡 |
| `Assets/Scenes/Levels/ddebug room.unity` | 临时调试房，可随意验证组件 |

先在 `ddebug room` 或新建的小关卡里验证组件，再放进正式地图。

## 实装一个新组件

如果策划需要在编辑器里摆放、撤销、保存、随分区移动，并在 Play 时重新生成，这个组件就应该成为 `LabLevelData` 的关卡数据：

1. **定义数据**：在 `LabLevelData.cs` 添加 `[Serializable]` spec、`List<>`、`Normalize()`、`CopyFrom()` 和增删改方法。
2. **接入分区操作**：在 `MovePartition`、`TranslatePartition`、`DeletePartition` 中处理这个组件。
3. **实现运行时组件**：放在 `Assets/TuningLab/Level/`，只使用现有 `LabPlayer` 接口和碰撞回调，不另起一套速度规则。
4. **接入生成**：在 `LabLevel.Build` 中生成真实物体；预览和 Play 必须走同一条路径。
5. **保持三者一致**：尺寸、碰撞和视觉都由同一份数据决定。
6. **接入编辑器**：在 `LabLevelEditorWindow` 增加工具、面板、Undo、Scene Handles 和校验。
7. **放对默认值**：规则参数放 `TuningLab` Inspector；地图内容放 `LabLevelData`。
8. **补齐资源文件**：每个 `.cs`、`.prefab`、`.png` 和目录都要有对应 `.meta`；C# 文件使用 LF 换行。

类型选择：

| 类型 | 示例 | 位置 | 是否进 `LabLevelData` |
| --- | --- | --- | --- |
| 全局系统 | 相机、输入、管理器 | `Assets/TuningLab/` | 否 |
| 单场景物件 | 只手摆一次的触发器 | `Assets/TuningLab/Level/` | 否 |
| 关卡内容 | 弹簧、移动平台、新机关 | `Assets/TuningLab/Level/` | 是，推荐 |

## 原理和代码地图

### 数据如何变成地图

`LabLevelData` 保存：

- `solids`：实心格；
- `ropes`：竖绳；
- `keys`、`doors`、`spikes`、`ices`：机关；
- `spawnA/B`：出生点脚底位置；
- `cameraMinSize/cameraMaxSize`：关卡级相机尺寸；
- `bounds`：编辑范围和无分区时的相机范围；
- `partitions`：关卡分区。

`Normalize()` 负责排序、去重、旧字段回落、相机尺寸限制和分区尺寸修正；`CopyFrom()` 做深拷贝，撤销、重置和派生都依赖它。

`LabLevel.Build` 会把实心格合并成尽量少的矩形 `BoxCollider2D`，再生成绳、钥匙、门、地刺和冰块。钥匙、门、地刺、冰块的 `ApplyShape()` 同时用于预览和 Play，因此不会出现“看起来能走、实际撞不到”的两套几何。

地图重置时，`LabLevel` 会重新生成被拾取的钥匙、关回门并恢复所有冰块。运行时订阅 `TuningLab.MapReset`，清理生成物时退订。

### 相机如何选择范围

`TuningLab.GetCameraFrame(B.position, ...)` 的优先级：

| 情况 | bounds | min/max |
| --- | --- | --- |
| B 在分区内 | 当前分区 bounds | 关卡尺寸 |
| 有分区但 B 不在任何分区 | `NoCameraBounds`，不夹 | 关卡尺寸 |
| 没有分区但有 `LabLevel` | 关卡 bounds | 关卡尺寸 |
| 基准场景 | `defaultCameraBounds` | TuningLab 默认值 |

`LabCamera` 跟随 A/B 中点，并对范围和尺寸做 `cameraFrameBlend` 混合。无边界时使用超大 `NoCameraBounds`，不能拿 `Rect.zero` 直接插值，否则镜头会被拉向原点。

### 玩家死亡和重生

地刺和塌框最终都调用 `LabPlayer.Kill()`：

| 条件 | 行为 |
| --- | --- |
| 有激活检查点 | 两人回检查点，清钥匙并重置地图 |
| 无检查点，`DestroyPlayer` | 保留旧实验台行为，销毁死亡玩家 |
| 无检查点，`RespawnAtSpawn` | 两人回出生点并重置地图 |

正式关卡如果使用地刺，通常应把 `deathWhenNoCheckpoint` 改为 `RespawnAtSpawn`。

### 主要代码文件

| 文件 | 职责 |
| --- | --- |
| `Assets/TuningLab/TuningLab.cs` | 规则参数、基准房间、HUD、网格和相机范围 |
| `Assets/TuningLab/LabPlayer.cs` | 玩家移动、跳、冲刺、撞击和死亡 |
| `Assets/TuningLab/LabCamera.cs` | A/B 中点跟随和分区相机范围 |
| `Assets/TuningLab/LabVerticalRope.cs` | 竖绳及其形状应用 |
| `Assets/TuningLab/LabKey.cs` | 钥匙 |
| `Assets/TuningLab/LabDoor.cs` | 门 |
| `Assets/TuningLab/LabSpike.cs` | 地刺 |
| `Assets/TuningLab/LabIce.cs` | 冰块：编组、单块融化、整组破坏和恢复 |
| `Assets/TuningLab/Level/LabLevelData.cs` | 关卡数据和分区 |
| `Assets/TuningLab/Level/LabLevel.cs` | 数据到碰撞体和运行时物体的生成 |
| `Assets/TuningLab/Level/Editor/LabLevelScene.cs` | 从 Tuning Lab 派生关卡 scene |
| `Assets/TuningLab/Level/Editor/LabLevelEditorWindow.cs` | 编辑器工具、面板和 Scene 预览 |
| `Assets/TuningLab/Level/Editor/LabLevelSelfCheck.cs` | 纯数据自检 |
| `Assets/TuningLab/Level/Editor/LabLevelPlayCheck.cs` | 临时关卡的真实物理检查 |

## 设计决策和踩坑

1. **不要复用旧 World 场景。** Tilemap/脚本和 Tuning Lab 的 1 格实体不是同一套规范。
2. **不要用一个全局相机矩形管理连续关卡。** 用分区，并由 B 判定当前分区。
3. **预览物体不能存盘。** 使用 `HideFlags.DontSaveInEditor`，并在清理时移除生成物。
4. **运行时 Sprite 和 PhysicsMaterial 不是资产。** 场景烘焙可能丢引用，数据驱动重建更可靠。
5. **分区移动必须带上内容。** 只移动 bounds 会让地形、机关和出生点留在原地。
6. **重叠检查必须在数据层拒绝。** 不能只在编辑器里显示警告。
7. **不要把 ProjectSettings、用户生成的关卡和 packages-lock 的无关改动带进提交。**
8. **冰块组号 0 是普通组。** 编辑器默认从 1 开始分配新组；手动把多块冰设成 0 会让它们共享整组破坏。冰块不并入 `MergedRects`，必须逐格生成才能单块融化。

## 当前限制

- 分区尺寸目前通过面板数值修改，Scene 视图只支持拖动，不支持边角拖拽。
- 相机 min/max 是关卡级，不能为每个分区单独设置。
- 鼠标交互不能在无头批处理模式模拟，仍需要在 Unity 编辑器内手测。
- 红热边融冰只在碰撞进入（`OnCollisionEnter`）时结算；贴冰站立后边才变红不会融脚下这块。
- 冰面加速按 `a = g` 持续加速，封顶 v4；已超速的玩家在冰面上不会被强制减速。
- README 和 `docs/策划案` 仍可能包含清理前的旧游戏描述；关卡编辑以本文和 `Assets/TuningLab` 代码为准。
