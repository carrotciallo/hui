# 回（Hui）· 双人 2D 横板闯关

Unity 双人协作平台解谜原型。当前仓库的**唯一开发基准是 Tuning Lab**，正式关卡从它派生，旧 `World` 架构、旧编辑器和旧测试房不再作为开发依据。

> **当前进度（2026-11-07）**：核心玩家、速度/冲刺、红热锻造、A 形态、大炮、L 形转向器、钥匙与门、重生点、传送点、冰块、绳子、地刺、隐藏方块、召回/幻影、修复方块、灵体、主页、音效、速度/死亡反馈和纯图标设置界面已经进入 Tuning Lab；关卡编辑器、SelfCheck/PlayCheck 和 PlayMode 规格已建立。美术资源已开始入库（`LabArt`），完整主线关卡仍在进行。

## 先看这里：参考索引

### 开发基准（代码和关卡有冲突时优先看这里）

1. [当前项目基准](docs/current-baseline.md)：唯一架构、场景边界、菜单入口、验证层级。
2. [Tuning Lab 关卡编辑器](docs/tuning-lab-level-editor.md)：关卡数据模型、代码地图、扩展和验证方式。
3. [冲刺与对撞：现行规则](docs/冲刺与对撞-现行规则.md)：按当前代码整理的冲刺、对撞、锻造、形态、大炮、冰块、岩浆、召回/幻影和隐藏方块规则。
4. [Tuning Lab 使用手册](docs/tuning-lab.md)：调试台操作与常用快捷键。
5. [领域术语](GLOSSARY.md)：隐藏方块、死亡反馈、残影、拖尾等跨关卡术语。

### 设计参考（用于规划，不得直接覆盖当前代码基准）

- [策划案总览](docs/策划案/回-概述.md)
- [机制策划案](docs/策划案/回-机制.md)
- [关卡策划案](docs/策划案/回-关卡%28未完成%29.md)
- [剧情策划案](docs/策划案/回-剧情.md)
- [美学策划案](docs/策划案/回-美学.md)

### 协作与历史

- [Issue 流程](docs/agents/issue-tracker.md)：需求、验收和状态约定。
- [策划 → 程序交接](docs/agents/策划-程序交接.md)：设计变更如何落到代码和关卡。
- [最近更改](docs/recent-changes.md)：近期合并记录。
- [速度等级：基准与教训](docs/速度等级-基准与教训.md)：历史复盘，不是当前规范。

## 当前架构

```text
Project/nameless/
├─ Assets/
│  ├─ Scenes/
│  │  ├─ TuningLab.unity          # 规则基准场景，禁止手改生成物
│  │  └─ Levels/                  # 正式关卡和调试房
│  ├─ Sounds/Resources/           # 12 类运行时音效（LabAudio 按目录加载）
│  └─ TuningLab/
│     ├─ TuningLab.cs             # 参数、基准房间、HUD、音效/表现层挂载、死亡闪红
│     ├─ LabPlayer.cs             # 玩家、形态、撞击、召回输入
│     ├─ LabVisual.cs             # 边框/眼睛
│     ├─ LabSpeedFeedback.cs      # V3/V4 残影与拖尾
│     ├─ LabAudio.cs              # 运行时音效
│     ├─ LabSettingsUI.cs         # 纯图标设置界面、Esc 暂停/恢复
│     ├─ LabSettingsStore.cs      # 设置 JSON、备份与坏档恢复
│     ├─ Resources/SettingsIcons/ # 16×16 像素图标（同名 PNG 可替换）
│     ├─ Lab*.cs                  # 相机、绳、钥匙/门、刺、冰、岩浆、隐藏方块、召回/幻影等
│     ├─ Level/
│     │  ├─ LabLevelData.cs       # 格子、绳、机关、出生点、相机和分区数据
│     │  └─ LabLevel.cs           # 数据 → 场景生成
│     ├─ Editor/                  # Tuning Lab 菜单和自检
│     ├─ Level/Editor/            # 派生、关卡编辑器、SelfCheck、PlayCheck、演示关
│     └─ Tests/                   # PlayMode 规格
└─ ProjectSettings/               # Unity 项目配置
```

架构约束：

- 规则改动进入 `Assets/TuningLab/`，不要恢复 `Assets/Scripts` 或另建第二套玩家逻辑。
- 可编辑、可保存、可随分区移动的关卡内容进入 `LabLevelData`，不要手改生成的碰撞体。
- 编辑器预览和 Play 共用 `LabLevel.Build`，避免两套关卡结果。
- `TuningLab.unity` 是规则基准；正式关卡通过菜单从它派生。
- 工程目录仍然是 `Project/nameless/`。仓库名虽然是 Hui，暂不改目录名，避免破坏 Unity 项目引用。

## 当前进度

### 已完成并进入基准

| 范围 | 当前状态 | 依据 |
| --- | --- | --- |
| Tuning Lab 单一基准、场景派生和关卡编辑器 | 已完成 | [Issue #16](https://github.com/sfher/hui/issues/16)、[Issue #3](https://github.com/sfher/hui/issues/3) |
| 玩家移动、跳跃、冲刺、五档速度和相机 | 已完成基础版本 | [机制策划案](docs/策划案/回-机制.md) |
| 红热锻造、A 形态、L 形转向器和大炮 | 已完成基础版本 | [Issue #14](https://github.com/sfher/hui/issues/14)、[#15](https://github.com/sfher/hui/issues/15)、[#21](https://github.com/sfher/hui/issues/21) |
| 钥匙、门、重生点、传送点（含进出场动画） | 已完成基础版本 | [Issue #18](https://github.com/sfher/hui/issues/18)、[#19](https://github.com/sfher/hui/issues/19)、[#20](https://github.com/sfher/hui/issues/20) |
| 冰块（#29，含运行时组色边框）、竖绳/横绳（#27）、地刺（#34） | 已进入基准 | 现行规则 §3.8 / §3.9 与编辑器文档 |
| 隐藏方块（#59） | 已进入基准 | `LabHiddenBlock` + 关卡数据 + 编辑器工具 |
| 召回与幻影（#69） | 已进入基准 | `LabRecallPickup` / `LabPhantom` + 编辑器工具 + 契约测试 |
| 音效（#60）、速度/死亡反馈（#62） | 已进入基准 | `LabAudio` / `LabSpeedFeedback` / 死亡闪红 |
| 纯图标设置界面（#71） | 已进入基准 | `LabSettingsUI` / `LabSettingsStore`，Esc 打开并暂停；音量、分辨率、全屏、亮度/按键预留位 |
| 修复方块（#63） | 已进入基准 | `LabRepairBlock` + 关卡数据 + 编辑器工具；热二/灵体边贴面回普通，不消耗 |
| 灵体（#64，ADR-0007 定名） | 已进入基准 | 全碎边限时穿透、耗尽即死并掉临时钥匙；`LabPlayer.EnterSpirit` 一线 |
| 主页（#70） | 已完成基础版本 | `Home.unity`（Build Settings 第 0 项）+ `HomeScreen`：纯图标三入口、边框蛇、关闭转场 |
| 判据修复：双人锻造门槛（#65）、形态不拦拾取（#67） | 已修 | 规则文档 §3.4 / §3.5 |
| 纯数据 Self Check、真实物理 Play Check、PlayMode 测试 | 已建立 | [当前项目基准](docs/current-baseline.md) |

### 当前进行中或待处理

| Issue | 主题 | 当前判断 |
| --- | --- | --- |
| [#66](https://github.com/sfher/hui/issues/66) | 传送门相对位置保留 | 未实现；传送后相对站位保持。 |
| [#72](https://github.com/sfher/hui/issues/72) | 游戏内图标 UI | 未开始；主页已按 #70 落地，游戏内 HUD 仍是调试用 IMGUI。 |
| [#26](https://github.com/sfher/hui/issues/26) | 像素美术资源 | 首批资源已入库并经 `LabArt` 接入（角色边框、材质、道具）；清单未齐，仍在补。音效已按 #60 入库。 |
| 主线关卡 | 组合关卡和最终流程 | 机制大多具备，完整主线尚未制作。 |

### 按策划案的主线覆盖情况

这是“机制是否具备”的进度，不代表对应关卡已经制作完成。

| 区段 | 设计目标 | 当前状态 |
| --- | --- | --- |
| L0 | 移动、跳跃、冲刺入门 | 基础能力已具备 |
| L1 | 横碰、上冲、重生点 | 机制具备 |
| L2 | 钥匙、门、伤害物、绳子初见 | 钥匙/门、地刺、绳子均已具备；关卡内容待补 |
| L3 | 冰块破坏与滑行 | 机制具备（#29），关卡内容待补 |
| L4 / L4+ | L 转向、传送点、冰面组合 | L 转向、传送点、冰面组合机制具备；完整关卡待补 |
| L5 / L6 | 滑道、绳子、转换器组合 | 部分机制具备，组合关卡未完成 |
| L7 | 大炮 | 基础功能具备 |
| L8 | 竖绳与 `\|` 形态 | 绳子基础存在；`\|` 形态的完整关卡用途待补 |
| L9 | 地刺交互 | 机制具备（#34） |
| L10 | 全部传送门、最终门、最大速度、结局/Boss | 仍属策划阶段，未形成完整关卡 |

## 打开和验证工程

1. 用 Unity Hub 添加 `Project/nameless`。
2. 使用 Unity **6000.6.4f1** 打开（URP 2D，Input System 1.20）。
3. 菜单执行 **GameJam → Build Tuning Lab**，生成或更新基准场景。
4. 按 Play 验证调试台；正式关卡从 Tuning Lab 派生，不要手改基准场景生成物。
5. 使用 **GameJam → Run Level Baseline Self Check** 做纯数据验证，使用 **GameJam → Run Level Play Check** 做真实物理验证。
6. PlayMode 规格在 `Assets/TuningLab/Tests/`，用 Unity Test Runner 的 PlayMode 运行。

Unity 打开工程可能重写 `ProjectSettings/`。提交前检查 `git status`：只丢弃纯序列化噪音，真实的图层、输入和构建场景改动必须保留。

## 关卡工作流

- **GameJam → New Level From Tuning Lab**：创建正式关卡。
- **GameJam → Convert Current Scene To Level**：把 Tuning Lab 副本转为关卡。
- **GameJam → Tuning Lab Level Editor**：编辑地形、绳、机关、隐藏方块、召回道具、出生点、相机和连续关卡分区。
- **GameJam → Build HiddenBlock Demo Level**：生成隐藏方块演示关，快速验证整组揭示。
- **GameJam → Run Level Baseline Self Check**：验证数据归一化、矩形合并、分区和相机规则。
- **GameJam → Run Level Play Check**：创建临时关卡并用真实物理验证。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 | 召回 |
| --- | --- | --- | --- | --- | --- |
| P1 / A | A / D | W / S | 空格 | 左 Shift | `Q` |
| P2 / B | ← / → | ↑ / ↓ | `/` | `.` | 右 `Shift` |

调试台常用：`R` 重置，`P` 像素网格，`Tab` 格线，`Esc` 打开/关闭设置界面（打开时全部玩法暂停，再按 `Esc` 或点关闭图标恢复）。传送门选择阶段 `Esc` 仍然只取消选择。召回需要先在关卡里拾取召回道具。关卡编辑器工具和验证方式见 [编辑器手册](docs/tuning-lab-level-editor.md)。

## 目录速查

- 规则、玩家、相机、机关和表现层：`Project/nameless/Assets/TuningLab/`
- 关卡数据与生成：`Project/nameless/Assets/TuningLab/Level/`
- 音效素材：`Project/nameless/Assets/Sounds/Resources/`
- 设置图标：`Project/nameless/Assets/TuningLab/Resources/SettingsIcons/`
- 基准场景：`Project/nameless/Assets/Scenes/TuningLab.unity`
- 正式关卡：`Project/nameless/Assets/Scenes/Levels/`
- 设计草稿：`docs/策划案/`
- 当前事实与入口：[docs/current-baseline.md](docs/current-baseline.md)
