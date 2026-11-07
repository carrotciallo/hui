# 当前项目基准

本文是开发入口。除非另有明确的 ADR，代码、关卡和文档都必须遵循这里的约定。

## 唯一基准

- Unity：6000.6.4f1，工程目录：`Project/nameless/`
- 规则、玩家、相机、机关和表现层：`Project/nameless/Assets/TuningLab/`
- 关卡数据：`Assets/TuningLab/Level/LabLevelData.cs`
- 关卡生成：`Assets/TuningLab/Level/LabLevel.cs`
- 音效素材：`Assets/Sounds/Resources/`（运行时 `LabAudio` 挂载，不依赖场景引用）
- 设置界面：`Assets/TuningLab/LabSettingsUI.cs`，图标在 `Assets/TuningLab/Resources/SettingsIcons/`
- 设置存档：`Assets/TuningLab/LabSettingsStore.cs` → `Application.persistentDataPath/settings.json`
- 基准场景：`Assets/Scenes/TuningLab.unity`
- 正式关卡：`Assets/Scenes/Levels/`
- 1 格 = 1 世界单位 = 16px

## 开发边界

- 规则改动进入 Tuning Lab；不要恢复旧 `Assets/Scripts` 或另建第二套玩家逻辑。
- 关卡内容通过 `LabLevelData` 和 Level Editor 修改；不要手改生成的碰撞体。
- 编辑器预览和 Play 共用 `LabLevel.Build`。
- 可摆放、可撤销、需保存或需随分区移动的机制必须进入关卡数据模型。
- 调试房用于单机制验证，正式关卡用于组合体验。

## 当前系统

- 玩家：移动、跳跃、四向冲刺、速度等级、双人动量互换、红热锻造、碎边形态、u 大炮、L 转向器、灵体态（限时穿透，耗尽即死并掉临时钥匙）。
- 关卡物件：竖绳/横绳、钥匙/门、地刺、冰块、岩浆块、隐藏方块、重生点、传送点、召回道具、幻影。
- 表现层：V3/V4 残影与拖尾、冲刺就绪轮廓闪烁与头顶粒子、死亡边框闪红、运行时音效（12 个 Cue，无场景引用）。
- 设置：纯图标 IMGUI；Esc 打开/关闭，打开时 `Time.timeScale = 0`，关闭恢复暂停前的值；主音量、音乐/音效音量、分辨率、全屏可用，亮度与按键绑定为预留位。
- 数据/生成：`LabLevelData` → `LabLevel.Build`，编辑器预览与 Play 共用同一条生成路径。

## 场景与菜单

| 目的 | 入口 |
| --- | --- |
| 生成基准场景 | GameJam → Build Tuning Lab |
| 新建关卡 | GameJam → New Level From Tuning Lab |
| 转换已有副本 | GameJam → Convert Current Scene To Level |
| 编辑关卡 | GameJam → Tuning Lab Level Editor |
| 生成隐藏方块演示关卡 | GameJam → Build HiddenBlock Demo Level |
| 纯数据验证 | GameJam → Run Level Baseline Self Check |
| 真实物理验证 | GameJam → Run Level Play Check |

## 验证

验证分四层：

1. 纯数据与数学：SelfCheck。
2. 真实物理：PlayCheck。
3. PlayMode 自动化规格：`Assets/TuningLab/Tests/`（`GameJam.Lab.Tests.PlayMode`）。
4. 编辑器交互：手测撤销/重做、保存重开、分区移动/调整/删除、预览与 Play 一致。

CLI 示例和常见问题见 [tuning-lab-level-editor.md](tuning-lab-level-editor.md)。

## 文档层级

1. 本文：当前工程事实和入口。
2. `tuning-lab-level-editor.md`：架构、关卡数据、扩展和验证细节。
3. `tuning-lab.md`：调试台操作说明。
4. `冲刺与对撞-现行规则.md`：冲刺、对撞、锻造、形态（含灵体态与临时钥匙）、大炮、冰块、岩浆、召回/幻影和隐藏方块的现行规则，按代码写；§6 是文档与代码的冲突清单。
5. `GLOSSARY.md`：跨关卡复用的领域术语。
6. Issue / ADR：具体规则的拍板记录；ADR 是历史决策，若与现行代码冲突，以本文和规则文档为准。
7. `docs/策划案/`：设计草稿；未同步前不能覆盖代码基准。
8. `速度等级-基准与教训.md`、`recent-changes.md`：历史复盘。

旧 ADR 和旧速度规格已移除；如果规则重新发生不可逆变化，应新增 ADR，并在本文更新入口。
