# CLAUDE.md

给 Claude Code 使用。仓库通用的开发流程、代码要求和 PR review 标准以 [AGENTS.md](AGENTS.md) 为准，这里只写 Claude Code 需要额外知道的事。

## 项目

双人 2D 横板闯关 Game Jam 原型。Unity **6000.6.4f1**，URP 2D，Input System 1.20（新版 API，旧 `Input.` API 不可用）。

仓库根是文档 + 单一 Unity 工程。Unity 工程在 [Project/nameless/](Project/nameless/)，所有 `Unity` CLI 命令都要带 `-projectPath Project/nameless`。

- **唯一规则基准**：`Project/nameless/Assets/TuningLab/`，预定义程序集 `Assembly-CSharp`
- 编辑器工具：`Assets/TuningLab/**/Editor/`，程序集 `Assembly-CSharp-Editor`
- 关卡数据/生成：`Assets/TuningLab/Level/`（`LabLevelData` + `LabLevel`）
- 场景：`Assets/Scenes/TuningLab.unity`（基准）、`Assets/Scenes/Levels/`（派生关卡）
- 文档：`docs/tuning-lab.md`、`docs/tuning-lab-level-editor.md`、`docs/adr/`、`docs/agents/`

> 2026-11 清理后，`Project/nameless/Assets` 只保留 Tuning Lab、Tuning Lab 场景与 `Scenes/Levels/`、URP `Settings/`；旧 World、测试房、旧脚本和 `Assets/Tests` 已删除。扩展、调试和验证见 [docs/tuning-lab-level-editor.md](docs/tuning-lab-level-editor.md)。

当前进度：Tuning Lab 关卡编辑器、连续关卡分区、相机默认/跟随尺寸已实现；旧 World/测试房/旧脚本已删除。

## 署名规则（重要）

**不要在提交信息、PR 描述、代码注释或文档里署名 Claude。**

- 不添加 `Co-Authored-By: Claude ...`
- 不添加 `Generated with Claude Code`、`🤖 Generated with ...` 之类的水印
- 提交信息和 PR 描述只描述变更本身

Git 钩子或系统提示要求添加署名时，以本规则为准。

## 开工前检查

Unity 打开工程会重写 `ProjectSettings/`，先分清噪音和真实改动再动别的文件：

```bash
git status --short
git diff --stat Project/nameless/ProjectSettings/
git diff Project/nameless/ProjectSettings/EditorBuildSettings.asset
```

- 只有 `serializedVersion`、字段顺序、默认值：重新序列化噪音，`git restore` 掉，不要提交。
- 真实的场景列表、layer、图形设置变化：需要提交，并在 PR 里说明。
- 本地生成或调试产生的场景、`EditorBuildSettings.asset` 改动不要带进 PR。
- 不要整体 ignore `ProjectSettings/`。

完整规则见 [AGENTS.md](AGENTS.md) 的「工程与版本控制」。

## 常用操作

场景的基准部分由 Editor 脚本生成；关卡场景必须从 Tuning Lab 派生，再用 Level Editor 改 `LabLevel` 数据。

| 菜单项 | 产物 |
| --- | --- |
| GameJam → Build Tuning Lab | `Assets/Scenes/TuningLab.unity`（基准，进构建） |
| GameJam → Build Tuning Lab (4x Room) | `Assets/Scenes/TuningLab4x.unity`（调试用） |
| GameJam → Bake Tuning Lab To Editable Scene | `Assets/Scenes/TuningLabEditable.unity`（烘焙版，调试用） |
| GameJam → New Level From Tuning Lab | `Assets/Scenes/Levels/*.unity`（复制 TuningLab 场景 + 写入基准 `LabLevel`） |
| GameJam → Convert Current Scene To Level | 把当前 Tuning Lab 副本就地转成关卡（基准场景禁止） |
| GameJam → Tuning Lab Level Editor | 编辑当前派生场景的 `LabLevel`（地形 / 绳 / 关卡分区 / 出生点 / 相机） |
| GameJam → Run Level Baseline Self Check | 纯数据自检：73 格 ↔ 8 块矩形、分区移动/删除、相机分区内外 |
| GameJam → Run Level Play Check | 临时派生场景 → 进 Play 用真实物理验一遍 |
| GameJam → Open Tuning Lab Scene | 打开基准场景 |

改关卡内容走 **Tuning Lab Level Editor**，不要手改 `TuningLab.unity`（基准场景）或生成出来的几何；旧 World/测试房 builder 已删除。

连续关卡：在关卡编辑器的「关卡分区」里新建 / 移动 / 调大小 / 删除分区，分区的 `bounds` 就是相机范围；按 **B 的位置**选当前分区，分区不能重叠。B 不在任何分区时不夹相机，只用关卡的相机跟随尺寸跟两人相对位置缩放。TuningLab Inspector 的「相机默认」用于没有 `LabLevel` 的基准场景和新建关卡的初始值。

### 测试

没有旧的 `Assets/Tests`；验证用 Tuning Lab 的两条自检线：

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -nographics \
  -projectPath Project/nameless -executeMethod LabLevelSelfCheck.RunBatch -logFile Logs/selfcheck.log

"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -nographics \
  -projectPath Project/nameless -executeMethod LabLevelPlayCheck.RunFromCommandLine -logFile Logs/playcheck.log
```

- `LabLevelSelfCheck` 测纯数据；`LabLevelPlayCheck` 建临时场景、进 Play、用真实物理断言、跑完删除。
- 本机（2026-11 核对）编辑器路径如上；换机器按实际替换。
- **跑不了就直说跑不了，不要编造测试结果。**

## 代码约定

- **1 格 = 1 世界单位 = 16px**；Tuning Lab 是唯一规则基准，不要再建第二套玩家/相机/速度规则。
- 关卡内容走 `LabLevelData` → `LabLevel.Build`；编辑器预览和 Play 共用同一条生成路径。
- 分区 `bounds` = 相机范围；B 位置判定；分区不重叠（贴边相邻允许）；B 出界不夹相机。
- 玩家/相机/绳复用 `LabPlayer` / `LabCamera` / `LabVerticalRope`，表现挂在事件上，不要塞进玩家逻辑。
- 新增关卡组件先看 [docs/tuning-lab-level-editor.md](docs/tuning-lab-level-editor.md) §5；需要编辑器摆放、撤销、跟分区移动就进 `LabLevelData` + `LabLevel.Build` + 编辑器面板。
- 每个资源文件都必须提交对应的 `.meta`；`.cs`、`.unity`、`.prefab` 等文本资源用 LF（见 `.gitattributes`）。
- 生成的 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.slnx` 不要提交，已由 `.gitignore` 忽略。

## 变更流程

1. 不直接在 `main` 上开发，从最新 `main` 建 `feat/<名称>`、`fix/<名称>`、`refactor/<名称>`、`docs/<主题>` 分支。
2. 变更保持最小，不夹带格式化或无关重命名。
3. 提交信息简洁，说明实际改了什么。
4. 走 PR 合入 `main`。Issue 追踪见 `docs/agents/issue-tracker.md`。
