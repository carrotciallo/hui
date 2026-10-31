# 学院路 · 双人 2D 横板闯关

Game Jam 原型仓库（主题：碰撞 / 质感）。当前阶段：玩法验证。

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
3. 菜单 **GameJam → Build World Scene**，生成正式世界 `Assets/Scenes/World.unity`（L0 灰盒 + L1–L10 占位，Build Settings 只保留它）
4. 需要单测手感时，再跑 **GameJam → Build Prototype Scene** 生成玩具房 `Assets/Scenes/Prototype.unity`（不进构建）
5. 验材质判定时，跑 **GameJam → Build Materials Test Scene** 生成材质测试房 `Assets/Scenes/Materials.unity`（不进构建）
6. Play

场景由脚本生成，所以第一次打开仓库时没有现成场景是正常的；生成一次后正常提交即可。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| P1 | A / D | W / S | 空格 | 左 Shift |
| P2 | ← / → | ↑ / ↓ | 右 Alt | `.`（句号） |

两人共用一个键盘。测试房里按 `R` 重置位置和测量数据。冲刺不可斜向；上下方向优先于左右，空中才能下冲。之后接手柄时再换成 Input System 的 control scheme。

正式世界额外调试键：`R` 回各自重生点，`PageUp` / `PageDown` 选区域，`Home` 传送到选中区域，`K` 给两人各发一把钥匙，`1`–`5` 强制两人挡位 v0–v4 持续 3 秒，`F3` 收起/展开运行状态（钥匙、已开门、传送门、B 质量、重生点、生死）。

## 结构

```
学院路/
├── Project/nameless/               Unity 工程
│   └── Assets/
│       ├── Scripts/
│       │   ├── GameConstants.cs        尺度、跳距、冲刺距离等规格常量
│       │   ├── PlayerMotor.cs          移动、跳跃、四向冲刺、地面检测、被撞击飞
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
│       │   └── MaterialsBuilder.cs      一键生成材质 prefab 与 Materials.unity
│       └── Tests/
│           ├── EditMode/               RunState 规则测试（含开门幂等）
│           └── PlayMode/               出生、掉落、KillZone、复活、相机、传送、材质判定
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
- 地面检测同时认 `Ground` 和 `Player` 两层，所以可以踩在队友头上起跳
- 被踩的一方同样可以起跳，不做压制（已与策划确认，暂时作为特性保留，不是 bug）
- `MotionState` 状态机：`Idle` / `Move` / `Jump` / `Fall` / `Dash` / `Hit`，通过 `StateChanged` 事件外抛，供动画和音效挂接
- 测试房带 1 格刻度、2 格高度标记、2.5 格断崖（跳距按 3 格调）和调试 HUD；`R` 重置
- 单场景连续世界：Tilemap 灰盒 + `Region` / `World` 区域划分，L0 灰盒（2 格跳高、3 格跳远、4 格横冲缺口）+ L1–L10 占位
- 死亡与重生：掉落线、`KillZone` 触发死亡，0.5s 后在最近 `RespawnPoint` 复活；没有记录时回当前区域出生点；队友触碰重生点可立即复活
- `RunState` 跨区域保留钥匙、已点亮传送门、B 质量和两人重生点，死亡不重置
- 相机按世界外框 clamp、只框活着的玩家，留了 `SetViewRestriction` 给 L10 视野揭示
- 程序集拆分为 `GameJam.Runtime` / `GameJam.Editor` / 两个测试程序集；EditMode 测 `RunState`，PlayMode 测关卡流程与材质判定

还没做：玉米糊规则、A 的形态、L1–L10 实际关卡内容、传送效果、顿帧与震屏、手柄、Cinemachine 相机。

## 协作

分支 + PR，规则见 `AGENTS.md`。Issue 在 GitHub Issues 上追踪，见 `docs/agents/issue-tracker.md`。
