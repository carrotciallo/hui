# 当前项目基准

本文是开发入口。除非另有明确的 ADR，代码、关卡和文档都必须遵循这里的约定。

## 唯一基准

- Unity：6000.6.4f1，工程目录：`Project/nameless/`
- 规则、玩家、相机：`Project/nameless/Assets/TuningLab/`
- 关卡数据：`Assets/TuningLab/Level/LabLevelData.cs`
- 关卡生成：`Assets/TuningLab/Level/LabLevel.cs`
- 基准场景：`Assets/Scenes/TuningLab.unity`
- 正式关卡：`Assets/Scenes/Levels/`
- 1 格 = 1 世界单位 = 16px

## 开发边界

- 规则改动进入 Tuning Lab；不要恢复旧 `Assets/Scripts` 或另建第二套玩家逻辑。
- 关卡内容通过 `LabLevelData` 和 Level Editor 修改；不要手改生成的碰撞体。
- 编辑器预览和 Play 共用 `LabLevel.Build`。
- 可摆放、可撤销、需保存或需随分区移动的机制必须进入关卡数据模型。
- 调试房用于单机制验证，正式关卡用于组合体验。

## 场景与菜单

| 目的 | 入口 |
| --- | --- |
| 生成基准场景 | GameJam → Build Tuning Lab |
| 新建关卡 | GameJam → New Level From Tuning Lab |
| 转换已有副本 | GameJam → Convert Current Scene To Level |
| 编辑关卡 | GameJam → Tuning Lab Level Editor |
| 纯数据验证 | GameJam → Run Level Baseline Self Check |
| 真实物理验证 | GameJam → Run Level Play Check |

## 验证

验证分三层：

1. 纯数据与数学：SelfCheck。
2. 真实物理：PlayCheck。
3. 编辑器交互：手测撤销/重做、保存重开、分区移动/调整/删除、预览与 Play 一致。

CLI 示例和常见问题见 [tuning-lab-level-editor.md](tuning-lab-level-editor.md)。

## 文档层级

1. 本文：当前工程事实和入口。
2. `tuning-lab-level-editor.md`：架构、扩展和验证细节。
3. `tuning-lab.md`：调试台使用说明。
4. `冲刺与对撞-现行规则.md`：冲刺、对撞、锻造、形态（含灵体态与临时钥匙）、大炮的现行规则，按代码写；§6 是文档与代码的冲突清单。
5. Issue / ADR：具体规则的拍板记录。
6. `docs/策划案/`：设计草稿；未同步前不能覆盖代码基准。
7. `速度等级-基准与教训.md`、`recent-changes.md`：历史复盘。

旧 ADR 和旧速度规格已移除；如果规则重新发生不可逆变化，应新增 ADR，并在本文更新入口。