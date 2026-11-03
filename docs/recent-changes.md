# 最近更改

记录近期合并进 `main` 的工程改动。当前开发入口和规则基准见 [current-baseline.md](current-baseline.md)；设计草稿见 [策划案](策划案/)。

## 2026-11-02

### Tuning Lab 关卡编辑器

- 新增 `Assets/TuningLab/Level/`：`LabLevelData`（格 / 绳 / 出生点 / 相机范围）、`LabLevel`（数据 → 合并 `BoxCollider2D` / `LabVerticalRope`，编辑器预览与 Play 共用）
- 新增 **GameJam → New Level From Tuning Lab**：复制 `TuningLab.unity`，挂 `LabLevel` 并用 `TuningLab.CaptureBaseline()` 写入基准房间；关卡场景必须从调试台派生
- 新增 **GameJam → Tuning Lab Level Editor**：1 格网格铺/擦地形、矩形填充、放竖绳、设出生点、改相机范围，支持撤销、校验和从 Tuning Lab 重置
- 关卡编辑器增加相机跟随尺寸调节、关卡分区、分区移动带物件和不重叠校验
- 新增 **GameJam → Run Level Baseline Self Check** 与 **GameJam → Run Level Play Check**（纯数据 / 真实物理两条验证）
- `TuningLab` 的场地描述抽成 `DescribeRoom`，基准场景与 `CaptureBaseline` 读同一段代码，避免两套规格
- 旧的 **GameJam → Level Editor**（编辑 `World.unity`）规则未对齐，不再使用

### 经验总结

- 新增 [tuning-lab-level-editor.md](tuning-lab-level-editor.md)：Tuning Lab 的架构、代码地图、加组件流程、调试/验证、踩坑与当前限制

## 2026-11-01

### 维护记录

- 接入 Unity MCP 包；本机服务地址为 `127.0.0.1:8088`（仅开发环境）
- 补全 Unity 生成物 `.gitignore`
- 新增 `CLAUDE.md`，约定 Claude Code 的工作方式
- 明确 `ProjectSettings/` 的处理规则：只丢弃重新序列化噪音，真实改动照常提交，见 [AGENTS.md](../AGENTS.md)

### 说明

本节保留历史记录；其中旧 World 编辑器、旧测试房和旧规则实现已经移除，不再作为当前工作流。