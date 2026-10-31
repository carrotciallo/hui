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
3. 菜单 **GameJam → Build Prototype Scene**，生成 `Assets/Scenes/Prototype.unity`
4. Play

场景由脚本生成，所以第一次打开仓库时没有现成场景是正常的；生成一次后正常提交即可。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| P1 | A / D | W / S | 空格 | 左 Shift |
| P2 | ← / → | ↑ / ↓ | 右 Alt | `.`（句号） |

两人共用一个键盘。测试房里按 `R` 重置位置和测量数据。冲刺不可斜向；上下方向优先于左右，空中才能下冲。之后接手柄时再换成 Input System 的 control scheme。

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
│       │   ├── CoopCamera.cs           双人中点跟随 + 按距离缩放
│       │   └── DebugHud.cs             挡位、速度、冷却、跳跃/冲刺测距
│       └── Editor/
│           └── PrototypeSceneBuilder.cs 一键生成 16px 网格测试房
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
- 撞击会抛 `ImpactInfo` 事件，并预留 `IImpactReceiver` 表面契约给 P4 的绳、冰、门使用
- 玩家描边随挡位变色（v1 黄 / v2 红 / v3 蓝 / v4 紫），HUD 同步显示挡位与目标速度
- 地面检测同时认 `Ground` 和 `Player` 两层，所以可以踩在队友头上起跳
- 被踩的一方同样可以起跳，不做压制（已与策划确认，暂时作为特性保留，不是 bug）
- `MotionState` 状态机：`Idle` / `Move` / `Jump` / `Fall` / `Dash` / `Hit`，通过 `StateChanged` 事件外抛，供动画和音效挂接
- 测试房带 1 格刻度、2 格高度标记、2.5 格断崖（跳距按 3 格调）和调试 HUD；`R` 重置

还没做：绳子/冰块等材质判定、A 的形态、Tilemap 正式关卡、顿帧与震屏、手柄、Cinemachine 相机。

## 协作

分支 + PR，规则见 `AGENTS.md`。Issue 在 GitHub Issues 上追踪，见 `docs/agents/issue-tracker.md`。
