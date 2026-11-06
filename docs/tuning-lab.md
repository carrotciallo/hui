# Tuning Lab 使用手册

Tuning Lab 是当前项目唯一的规则调试台，用来验证玩家移动、冲刺、速度读数、碰撞和相机行为。正式关卡必须从它派生，不能另起一套玩家或数值规则。

## 打开

1. 用 Unity 6000.6.4f1 打开 `Project/nameless/`。
2. 执行 **GameJam → Build Tuning Lab**。
3. 打开或运行 `Assets/Scenes/TuningLab.unity`。
4. 按 Play，使用 HUD 和 Scene 视图检查读数。

场地、玩家和相机由 `Assets/TuningLab/` 代码生成；不要手改生成物。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 | 召回 |
| --- | --- | --- | --- | --- | --- |
| A | A / D | W / S | 空格 | 左 Shift | `Q` |
| B | ← / → | ↑ / ↓ | `/` | `.` | 右 `Shift` |

- `R`：重置位置和调试读数。
- `P`：像素网格。
- `Tab`：格线。
- `F1`：手碎一条边，顺序固定 上 → 下 → 左 → 右；`F2`：恢复四条边框。用来逐个看碎边组合出的形态，不用真去砸。

B 被装进 u 形大炮的膛里时（A 是 u 形且 B 以冲刺态撞在炮口上），B 不可视、移动无效、不算在地面、充能不回；
B 的冲刺键 `.` 变成**开炮**，把 B 按进入方向的反向打出去，`v' = √(Vb² + 一个等级间距的 v²)`，A 这边一切照常。
规则细节与朝向门控见 [冲刺与对撞-现行规则](冲刺与对撞-现行规则.md) §3.6。

召回需要先在关卡里放召回道具（基准室内没有）。拾取后 HUD 显示“召回 已解锁”：A 按 `Q`、B 按右 `Shift`，
把另一名玩家拉到施法者头顶并在旧位置留下幻影；规则见 §3.10。

V3 起会留下残影和拖尾（顶档 V6 更多更亮）；任一玩家死亡时屏幕边缘闪红。音效由运行时 `LabAudio` 挂载，不依赖场景对象。

具体规则以当前代码和 HUD 读数为准；历史速度规格不再作为实现依据。

## 传送门

两人均存活、未进膛且同时位于同一传送门 3×3 区域时，任一人按自己的冲刺键启动。已标记传送门必须多于一个；不足时不会冻结角色或移动相机。

- 起点先播放 `0.35s` 高亮与粒子，再由蓝、红光柱以 `48 格/s` 向上离屏；双人离屏后才开放选择。
- 选择阶段用 `A/D` 切换、`Enter` 确认、`Esc` 取消，候选相机移动为 `0.25s`。
- 确认后先完成终点取景，再播放 `0.15s` 淡入、`0.20s` 高亮停留和 `0.15s` 爆开；最后恢复操作、进入前形态和完整二维速度。
- 取消会在起点播放同样的返场动画，恢复零速度，不吞钥匙、不重置地图。
- 动画期间移动、跳跃、`R` 和重复触发无效；`Esc` 只在选择阶段有效。

## 关卡验证

正式关卡工作流：

1. **GameJam → New Level From Tuning Lab** 创建关卡。
2. **GameJam → Tuning Lab Level Editor** 编辑 `LabLevelData`。
3. 使用 **Run Level Baseline Self Check** 验证数据和相机规则。
4. 使用 **Run Level Play Check** 验证真实物理。
5. 需要单独看隐藏方块时，执行 **GameJam → Build HiddenBlock Demo Level** 生成演示关。

编辑器预览与 Play 共用 `LabLevel.Build`。分区 bounds 是相机范围；B 的位置决定当前分区；分区不能重叠，贴边相邻允许。

## 代码地图

```text
Assets/TuningLab/
├── TuningLab.cs          规则参数、基准房间、HUD/网格、音效/表现层挂载
├── LabPlayer.cs          移动、跳跃、冲刺、碰撞、形态、召回输入
├── LabCamera.cs          双人跟随、缩放和分区相机范围
├── LabVisual.cs          玩家边框/眼睛
├── LabSpeedFeedback.cs   V3 起 / 顶档 V6 残影与拖尾
├── LabAudio.cs           运行时音效（12 个 Cue）
├── LabVerticalRope.cs    竖绳行为
├── LabHorizontalRope.cs  横绳平台行为
├── LabKey.cs / LabDoor.cs
├── LabSpike.cs / LabIce.cs / LabMagma.cs / LabHiddenBlock.cs
├── LabRecallPickup.cs / LabPhantom.cs
├── LabRespawnPoint.cs / LabTeleportPoint.cs
├── TierMath.cs           速度与等级数学
└── Level/
    ├── LabLevelData.cs       关卡数据
    ├── LabLevel.cs            数据到碰撞/机关的生成
    └── Editor/                派生、编辑、SelfCheck、PlayCheck、隐藏方块演示关
```

扩展关卡机制时，先阅读 [tuning-lab-level-editor.md](tuning-lab-level-editor.md) 第 5 节。需要摆放、撤销、分区移动和保存的内容必须进入 `LabLevelData`。

## 验证命令

Windows Unity CLI 示例见 [tuning-lab-level-editor.md](tuning-lab-level-editor.md) 第 7 节。命令无法运行时必须记录原因，不要伪造通过结果。
