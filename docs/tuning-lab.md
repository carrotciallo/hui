# Tuning Lab 调试台手册

Tuning Lab 是本项目的**唯一规则基准**。所有玩法规则的调整都在这里做，正式关卡从它派生。

场景：`Assets/Scenes/TuningLab.unity`（由 `GameJam → Build Tuning Lab` 生成，**不要手改生成物**）

---

## 打开与运行

1. 菜单 **GameJam → Build Tuning Lab** 重新生成基准场景
2. 打开 `Assets/Scenes/TuningLab.unity`
3. 按 Play

场景里有 `TuningLab` 和 `Main Camera` 两个物体。`TuningLab` 挂着 `LabLevel`，Play 时由 `TuningLab.BuildAll()` 现场搭出整个场地。

> `TuningLab.autoBuild` 关掉时不会自动搭场地，PlayMode 规格用这个开关。正常玩保持勾选。

---

## 操作

默认键位。可在设置界面（Esc）里改。

### 两个玩家

| 动作 | 大块 A | 小块 B |
| --- | --- | --- |
| 上下左右 | `W` `A` `S` `D` | `I` `J` `K` `L` |
| 跳跃 | `Space` | `'` |
| 冲刺 | `LeftShift` | `;` |
| 召回 | `LeftCtrl` | `Enter` |

### 调试用

| 键 | 作用 |
| --- | --- |
| `R` | 重置两人 |
| `P` | 像素网格 |
| `Tab` | 格线 |
| `F1` | 打碎 A 的下一条边（快速演示形态） |
| `F2` | 恢复 A 的全部边 |
| `Esc` | 设置界面（全局暂停） |

---

## HUD 面板

Play 时左上角会出现**手感调试台**，里面有实时可调的参数。右侧是读数，底部有标尺。

> **发布包里不显示这个面板。** `LabHud.showTuningPanel` 默认取 `Debug.isDebugBuild`——编辑器里显示，正式构建隐藏。

### 可调参数

| 分组 | 参数 |
| --- | --- |
| 时间 | 时间缩放（0.05×–2×） |
| 重力与跳跃 | 重力、跳高、走路速度、起步加速度、空中制动、地面制动 |
| 冲刺 | 冲刺距离、冷却、锁定时间 |
| 等级 | 首级落差、每级间距、等级数 |
| 大炮 | 增益格数、L 形进膛门槛 |
| 冰块 / 岩浆 | 破碎所需档位 |
| 地图重置 | 重置半径 |

改完立刻作用于物理，方便边调边试。

---

## 速度等级

一切判定都用"相当于从多少格落下"表达，不是绝对速度。`TierMath` 负责换算：

```
速度 = √(2 · g · 格数)
格数 = 速度² / (2 · g)
```

等级边界默认 `tierFirstCells = 4`、`tierSpacingCells = 4`，即 **4 / 8 / 12 / 16 格**（v0 顶端到 v4）。

调等级参数会同时影响：撞击门槛、红热门槛、冰块破碎、岩浆破坏、大炮增益。**这是个牵一发动全身的参数**，改之前先看 [速度等级：基准与教训](速度等级-基准与教训.md)。

---

## 灰盒场地

基准场地是一片带地板、台阶和几处机关的空旷房间，用来单独验证机制。它不是正式关卡。

组合验证请在 `Assets/Scenes/Levels/` 下的正式关卡里做。

---

## 验证一次改动

```bash
# 1. 纯数据自检（秒级）
GameJam → Run Level Baseline Self Check

# 2. 真实物理自检（十几秒）
GameJam → Run Level Play Check

# 3. 全量规格（约 80 秒）
Unity Test Runner → PlayMode
```

三层都过再提交。

---

## 加一个新机关

1. 在 `Assets/TuningLab/` 写组件（参考 `LabSpike.cs` 这类简单的）
2. 在 `LabLevelData` 加对应的 spec 列表和 `Place*` 方法
3. 在 `LabLevel.BuildSteps` 里加一个 `Add*` 步骤
4. 在 `LabLevelEditorWindow` 里加工具和绘制
5. 补 PlayMode 规格
6. 更新 [冲刺与对撞-现行规则](冲刺与对撞-现行规则.md) 和 [关卡编辑器文档](tuning-lab-level-editor.md)

---

## 代码地图

| 文件 | 职责 |
| --- | --- |
| `TuningLab.cs` | 参数、场地生成、HUD/音效挂载、全局输入、死亡闪红 |
| `LabPlayer.cs` | 移动、跳跃、冲刺、撞击结算、形态、输入读取 |
| `LabBindings.cs` | 按键绑定模型、默认值、冲突检测、按键显示名 |
| `LabForms.cs` | 碎边掩码 → 形态映射 |
| `TierMath.cs` | 速度 ↔ 格数换算与等级映射 |
| `LabPhantom.cs` | 召回幻影（光桥、2048 滑块、玻璃大炮、L 转向） |
| `LabLevel.cs` | 数据 → 场景几何 |
| `LabLevelData.cs` | 关卡数据模型 |

---

## 常见问题

**场地没生成**
检查 `TuningLab.autoBuild` 是否勾选，或手动执行 `GameJam → Build Tuning Lab`。

**改了参数没生效**
等级相关参数有缓存（`cachedBounds`），需要让场景重建一次。

**Play 时画面卡住**
设置界面打开会 `Time.timeScale = 0`，按 `Esc` 关掉即可。

**F1 没反应**
`DebugShatterNextEdge` 只对 `canBeRedHot` 的玩家生效（目前只有 A）。
