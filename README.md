# 学院路 · 双人 2D 横板闯关

Game Jam 原型仓库（主题：碰撞 / 质感）。当前阶段：玩法验证。

> **2026-11 状态**：Unity 工程已清理为 Tuning Lab 专用，旧 World / 测试房 / 旧脚本已删除；下面旧章节是历史记录。当前架构、扩展和验证见 [tuning-lab-level-editor.md](docs/tuning-lab-level-editor.md)。

> 项目名暂未定，定下来后改这里和仓库名。

## 方向

- **美术**：几何图形，可爱的画风
- **分工**：去中心化，不强制指派，谁有想法谁做，走 PR 合入

## 环境

- Unity **6000.6.4f1**
- 渲染管线 URP 2D（Universal 2D 模板）
- 输入：Input System 1.20（`Active Input Handling = Input System Package (New)`，旧版 `Input.` API 不可用）

## 打开

1. Unity Hub 添加 `Project/nameless`
2. 用 6000.6.4f1 打开
3. 菜单 **GameJam → Build World Scene**，生成正式世界 `Assets/Scenes/World.unity`（L0 灰盒 + L1/L2 正式内容 + L3–L10 占位，Build Settings 只保留它）
4. 需要单测手感时，再跑 **GameJam → Build Prototype Scene** 生成玩具房 `Assets/Scenes/Prototype.unity`（不进构建）
5. 验材质判定时，跑 **GameJam → Build Materials Test Scene** 生成材质测试房 `Assets/Scenes/Materials.unity`（不进构建）
6. 验 A 的形态时，跑 **GameJam → Build Forms Test Scene** 生成形态测试房 `Assets/Scenes/Forms.unity`（不进构建）
7. Play

场景由脚本生成，所以第一次打开仓库时没有现成场景是正常的；生成一次后正常提交即可。

## 关卡编辑器

规则基准在 `Assets/TuningLab`，所以关卡场景也必须从 Tuning Lab 派生，不碰 `World.unity` 那一套还没对齐的规范。

1. 菜单 **GameJam → New Level From Tuning Lab**：复制 `Assets/Scenes/TuningLab.unity`（继承调参值/相机/玩家），清掉生成物，挂上 `LabLevel` 并写入基准房间数据，存到 `Assets/Scenes/Levels/`。
2. 菜单 **GameJam → Tuning Lab Level Editor**：在 Scene 视图里编辑这份 `LabLevel`。
3. 工具：按 1–7 切铺地形 / 擦地形 / 矩形 / 竖绳 / 出生点 A / 出生点 B / 关卡分区；`G` 网格、`Home` 取景、`[` `]` 调绳长；左键拖动绘制，支持撤销。
4. 左侧面板可改相机范围、相机跟随尺寸（最小/最大 size）、出生点、绳长和关卡分区；**校验摆放** 会查出生点是否在地形里、绳是否越界、分区是否重叠；**从 Tuning Lab 重置** 用基准房间覆盖当前关卡。

如果已经有一份 Tuning Lab 副本（例如 `Bake Tuning Lab To Editable Scene` 生成的场景），用 **GameJam → Convert Current Scene To Level** 就地转成关卡；`TuningLab.unity` 基准场景本身禁止转换。

**连续关卡分区**：关卡编辑器（GameJam → Tuning Lab Level Editor）里有「关卡分区」面板：

- 新建 / 命名 / 调大小 / 删除 / 在 Scene 视图拖动移动；移动时可带上分区里的地形、绳和出生点。
- 分区之间**不能重叠**：新建、移动、改大小都会被拒绝，贴边相邻可以。
- 分区的 bounds **就是相机范围**：B 进入哪个分区，相机 clamp 就用哪个分区的大小；缩放仍用关卡的相机跟随尺寸。
- B 不在任何分区时**没有相机范围约束**，只按两人的相对位置和关卡的相机跟随尺寸缩放。
- 新关卡默认带一个覆盖编辑范围的 `Level 1` 分区；旧关卡没有分区时仍按单关相机设置走。

Play 时 `TuningLab` 按 `LabLevel` 数据生成合并后的 `BoxCollider2D` 和 `LabVerticalRope`，和调试台走同一条 `BuildRoom` 路径。编辑器里看到的预览也是这条路径生成的，不写进场景文件。

> 旧的 **GameJam → Level Editor** 改的是 `World.unity`，规则/尺度还没和 TuningLab 对齐，先别用。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| P1 | A / D | W / S | 空格 | 左 Shift |
| P2 | ← / → | ↑ / ↓ | 右 Alt | `.`（句号） |

两人共用一个键盘。测试房里按 `R` 重置位置和测量数据。冲刺不可斜向；上下方向优先于左右，空中才能下冲。之后接手柄时再换成 Input System 的 control scheme。

正式世界额外调试键：`R` 回各自重生点，`PageUp` / `PageDown` 选区域，`Home` 传送到选中区域，`K` 给两人各发一把钥匙，`F1`–`F5` 强制两人挡位 v0–v4 持续 3 秒，`F6` 收起/展开运行状态（钥匙、已开门、传送门、B 质量、重生点、生死）。

A（P1）的形态：`1`–`6` 依次切普通 / `u` 大炮 / `L` 转换器 / `|` 攀附 / `_` 木筏 / 空。`u` 形态下 B 站上来按「下」进仓，B 再按冲刺射出（挡位 +1）；`|` 形态在竖绳旁按 W 附着、W/S 升降；空形态可穿墙拿钥匙，2.5 秒后死亡。

## 结构

```
学院路/
├── Project/nameless/               Unity 工程
│   └── Assets/
│       ├── Scripts/
│       │   ├── GameConstants.cs        尺度、跳距、冲刺距离等规格常量
│       │   ├── PlayerMotor.cs          移动、跳跃、四向冲刺、地面检测、被撞击飞、驮带/攀附/漂浮/空形态
│       │   ├── FormController.cs       A 的六形态：切换、大炮收人、转换区、攀绳、木筏、空形态
│       │   ├── ConverterZone.cs        A 的 L 形态转换区（子物体触发器）
│       │   ├── Lava.cs                 岩浆：木筏漂浮、普通玩家即死
│       │   ├── SpeedTierProvider.cs    v0–v4 挡位状态 → 目标速度；可替换为速度推导挡位
│       │   ├── ImpactInfo.cs           撞击上下文 + 表面响应契约（P4 材质消费）
│       │   ├── TierOutline.cs          玩家挡位描边（黄/红/蓝/紫）
│       │   ├── VerticalRope.cs         竖绳：v2 穿过、v3/v4 反弹 +1
│       │   ├── GroundRope.cs           地面绳：单向平台、v2 下落、v3/v4 上弹
│       │   ├── IceSurface.cs           冰面标记（配合低摩擦材质）
│       │   ├── BreakableWall.cs        可破坏墙：tier / 质量阈值
│       │   ├── IcePanel.cs             冰面板：可破坏墙的冰版本
│       │   ├── KeyCarrier.cs           头顶钥匙：拾取、消耗、视觉
│       │   ├── KeyPickup.cs            钥匙拾取触发器
│       │   ├── KeyGate.cs              过关门：吞钥匙或按质量破门
│       │   ├── Portal.cs               传送门：点亮、双人下键、吞钥匙计数
│       │   ├── RunState.cs             钥匙、已开门、传送门、B 质量、两槽位重生点（纯 C#）
│       │   ├── RunStateHost.cs         场景内持有 RunState 的宿主
│       │   ├── Region.cs / World.cs    区域、出生点、世界外框、掉落线与区域判定
│       │   ├── PlayerLife.cs           死亡、延迟重生、队友复活接口与事件
│       │   ├── KillZone.cs             通用死亡触发器（刺也用它）
│       │   ├── RespawnPoint.cs         记录重生点 + 立即复活已死亡的队友
│       │   ├── CoopCamera.cs           双人中点跟随 + 缩放 + 世界边界 clamp
│       │   └── DebugHud.cs             挡位、速度、RunState、调试传送/发钥匙
│       ├── Editor/
│       │   ├── GreyboxAssets.cs        共享灰盒资源：精灵、材质、prefab、玩家构建
│       │   ├── PrototypeSceneBuilder.cs 一键生成 16px 网格测试房
│       │   ├── WorldBuilder.cs          一键生成 World.unity（Tilemap + marker prefab）
│       │   ├── MaterialsBuilder.cs      一键生成材质 prefab 与 Materials.unity
│       │   └── FormsBuilder.cs          一键生成 Forms.unity（五种形态 + 岩浆各一排）
│       └── Tests/
│           ├── EditMode/               RunState 规则测试（含开门幂等）
│           └── PlayMode/               出生、掉落、KillZone、复活、相机、传送、材质判定、A 形态
├── docs/agents/                    工程约定（issue / 域模型）
└── AGENTS.md
```

## 当前实现的范围

- 世界尺度：1 单位 = 1 格 = 16px，人物 13px；取景和测量都按格
- 速度驱动的动态刚体（`Rigidbody2D`），水平用 `Mathf.MoveTowards` 逼近目标速度，不是直接赋值，这样物理推挤才有机会生效
- 跳跃：coyote time、跳跃缓冲、可变跳跃高度、上升/下落不同重力；按规格调到跳高 2 格、跳远 3 格；短按也有 0.1s 的最小起跳时间，不会一按就半高
- 四向冲刺：水平 / 上 / 下，不可斜冲；下冲仅限空中；冲刺期间无重力；落地且不在冲刺时开始 0.5s 冷却
- 挡位状态机：行为申请 v0–v4，挡位给出目标速度；冲刺申请 v1，被撞进入 v2，v3/v4 留给绳索/重力/大炮
- 双人碰撞：v1 及以上横向撞击把对方送进 v2，按“v2 走 3 格 → v1 走 2 格 → 自然减速约 1 格”击飞；慢速推挤仍是 0.12s 短硬直
- 撞击会抛 `ImpactInfo` 事件，表面物件实现 `IImpactReceiver`，返回 `Pass / Bounce / Break / Accelerate / Stick / Die`
- 玩家描边随挡位变色（v1 黄 / v2 红 / v3 蓝 / v4 紫），HUD 同步显示挡位与目标速度
- 竖绳：v2 穿过、v3/v4 反弹并升一挡（封顶 v4）；地面绳：可上不可下，v2 强行下落 0.2s，v3/v4 向上反弹
- 冰面：低摩擦滑行，速度驱动下用 0.3 加速系数保留滑行惯性；可破坏墙 / 冰面板按 tier 阈值（v2）破碎，墙还支持质量阈值
- 钥匙头顶携带（最多 1 把）；过关门有钥匙就吞钥匙开门，否则 B 质量 ≥ 2 且 v2 破门；开门状态写入 `RunState` 跨区域保留
- 传送门：靠近点亮，双人同时站入并按「下」、消耗 1 把钥匙后激活计数；刺复用 `KillZone`
- 材质测试房 8 行逐物件验证，PlayMode 用真实物理驱动（不直接调 `ReceiveImpact`）
- A 的形态：`1`–`6` 切普通 / `u` / `L` / `|` / `_` / 空；`u` 收 B 后按 B 的冲刺方向射出、挡位 +1；`L` 用触发器子物体把横向命中转向上、纵向命中转向远离 A，挡位不变；`|` 贴绳按 W 吸附并能 W/S 升降；`_` 在岩浆上漂浮并承载 B；空形态身体变触发器、无重力、2.5s 后死亡
- 驮带：站在队友头上时把队友的水平速度叠加进目标速度，跟着走不会被甩掉
- 岩浆：普通玩家接触即死，木筏形态和骑在木筏上的 B 安全
- 形态测试房 5 排（大炮 / 转换器 / 竖绳 / 岩浆 / 空形态），PlayMode 用真实物理与输入驱动
- 地面检测同时认 `Ground` 和 `Player` 两层，所以可以踩在队友头上起跳
- 被踩的一方同样可以起跳，不做压制（已与策划确认，暂时作为特性保留，不是 bug）
- `MotionState` 状态机：`Idle` / `Move` / `Jump` / `Fall` / `Dash` / `Hit` / `Climb` / `Swim`，通过 `StateChanged` 事件外抛，供动画和音效挂接
- 测试房带 1 格刻度、2 格高度标记、2.5 格断崖（跳距按 3 格调）和调试 HUD；`R` 重置
- 单场景连续世界：Tilemap 灰盒 + `Region` / `World` 区域划分，L0 灰盒（2 格跳高、3 格跳远、4 格横冲缺口）+ L1 正式灰盒（横碰破墙、上冲高台、两次重生点）+ L2 正式灰盒（4 格冲刺缺口、钥匙与过关门、地绳桥）+ L3–L10 占位
- 死亡与重生：掉落线、`KillZone` 触发死亡，0.5s 后在最近 `RespawnPoint` 复活；没有记录时回当前区域出生点；队友触碰重生点可立即复活
- `RunState` 跨区域保留钥匙、已点亮传送门、B 质量和两人重生点，死亡不重置
- 相机按世界外框 clamp、只框活着的玩家，留了 `SetViewRestriction` 给 L10 视野揭示
- 程序集拆分为 `GameJam.Runtime` / `GameJam.Editor` / 两个测试程序集；EditMode 测 `RunState`，PlayMode 测关卡流程、材质判定与 A 形态

还没做：玉米糊规则、热锻、L3–L10 实际关卡内容（L1、L2 已完成）、传送效果、顿帧与震屏、手柄、Cinemachine 相机。

## 协作

分支 + PR，规则见 `AGENTS.md`。Issue 在 GitHub Issues 上追踪，见 `docs/agents/issue-tracker.md`。
