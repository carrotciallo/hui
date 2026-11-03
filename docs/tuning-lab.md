# Tuning Lab 使用手册

Tuning Lab 是当前项目唯一的规则调试台，用来验证玩家移动、冲刺、速度读数、碰撞和相机行为。正式关卡必须从它派生，不能另起一套玩家或数值规则。

## 打开

1. 用 Unity 6000.6.4f1 打开 `Project/nameless/`。
2. 执行 **GameJam → Build Tuning Lab**。
3. 打开或运行 `Assets/Scenes/TuningLab.unity`。
4. 按 Play，使用 HUD 和 Scene 视图检查读数。

场地、玩家和相机由 `Assets/TuningLab/` 代码生成；不要手改生成物。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| A | A / D | W / S | 空格 | 左 Shift |
| B | ← / → | ↑ / ↓ | `/` | `.` |

- `R`：重置位置和调试读数。
- `P`：像素网格。
- `Tab`：格线。

具体规则以当前代码和 HUD 读数为准；历史速度规格不再作为实现依据。

## 关卡验证

正式关卡工作流：

1. **GameJam → New Level From Tuning Lab** 创建关卡。
2. **GameJam → Tuning Lab Level Editor** 编辑 `LabLevelData`。
3. 使用 **Run Level Baseline Self Check** 验证数据和相机规则。
4. 使用 **Run Level Play Check** 验证真实物理。

编辑器预览与 Play 共用 `LabLevel.Build`。分区 bounds 是相机范围；B 的位置决定当前分区；分区不能重叠，贴边相邻允许。

## 代码地图

```text
Assets/TuningLab/
├── TuningLab.cs       规则参数、基准房间、HUD/网格与场景生成
├── LabPlayer.cs       移动、跳跃、冲刺、碰撞
├── LabCamera.cs       双人跟随、缩放和分区相机范围
├── LabVerticalRope.cs 竖绳行为
├── TierMath.cs        速度与等级数学
└── Level/
    ├── LabLevelData.cs       关卡数据
    ├── LabLevel.cs            数据到碰撞/机关的生成
    └── Editor/                派生、编辑、SelfCheck、PlayCheck
```

扩展关卡机制时，先阅读 [tuning-lab-level-editor.md](tuning-lab-level-editor.md) 第 5 节。需要摆放、撤销、分区移动和保存的内容必须进入 `LabLevelData`。

## 验证命令

Windows Unity CLI 示例见 [tuning-lab-level-editor.md](tuning-lab-level-editor.md) 第 7 节。命令无法运行时必须记录原因，不要伪造通过结果。
