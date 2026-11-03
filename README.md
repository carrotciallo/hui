# 学院路 · 双人 2D 横板闯关

Unity 双人协作平台解谜原型。当前开发基准是 **Tuning Lab + LabLevel 关卡数据**；旧 World、旧测试房和旧挡位实现已移除。

## 当前基准

- Unity **6000.6.4f1**，URP 2D，Input System 1.20
- 工程：`Project/nameless/`
- 规则与玩家：`Project/nameless/Assets/TuningLab/`
- 关卡数据：`Assets/TuningLab/Level/` 下的 `LabLevelData` / `LabLevel`
- 基准场景：`Assets/Scenes/TuningLab.unity`
- 正式关卡：`Assets/Scenes/Levels/`
- 详细导航：[docs/current-baseline.md](docs/current-baseline.md)

## 打开工程

1. 用 Unity Hub 添加 `Project/nameless`。
2. 使用 Unity 6000.6.4f1 打开。
3. 菜单执行 **GameJam → Build Tuning Lab**，生成或更新基准场景。
4. 按 Play 验证调试台；正式关卡从 Tuning Lab 派生，不要手改基准场景的生成物。

Unity 打开工程可能重写 `ProjectSettings/`。提交前先检查 `git status`，只丢弃纯序列化噪音；真实的图层、输入、构建场景等改动必须保留。

## 关卡工作流

- **GameJam → New Level From Tuning Lab**：创建正式关卡。
- **GameJam → Convert Current Scene To Level**：把 Tuning Lab 副本转为关卡。
- **GameJam → Tuning Lab Level Editor**：编辑地形、绳、出生点、相机和连续关卡分区。
- **GameJam → Run Level Baseline Self Check**：验证数据归一化、矩形合并、分区和相机规则。
- **GameJam → Run Level Play Check**：创建临时关卡并用真实物理验证。

预览和 Play 共用 `LabLevel.Build`；编辑器生成物不写入场景。正式关卡和调试房都必须复用 Tuning Lab 的玩家、相机和规则。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| P1 / A | A / D | W / S | 空格 | 左 Shift |
| P2 / B | ← / → | ↑ / ↓ | `/` | `.` |

调试台常用：`R` 重置，`P` 像素网格，`Tab` 格线。关卡编辑器工具和验证方式见 [docs/tuning-lab-level-editor.md](docs/tuning-lab-level-editor.md)。

## 文档

- [当前基准导航](docs/current-baseline.md)
- [Tuning Lab 使用手册](docs/tuning-lab.md)
- [冲刺与对撞：现行规则](docs/冲刺与对撞-现行规则.md)（按代码写的冲刺 / 对撞 / 锻造 / 形态 / 大炮规则）
- [Tuning Lab 关卡编辑器](docs/tuning-lab-level-editor.md)
- [策划 → 程序交接](docs/agents/策划-程序交接.md)
- [Issue 流程](docs/agents/issue-tracker.md)
- [速度等级：基准与教训](docs/速度等级-基准与教训.md)（历史复盘，不是当前规范）
- [最近更改](docs/recent-changes.md)

旧规则、旧 ADR 和已删除 World 架构不再作为开发依据。