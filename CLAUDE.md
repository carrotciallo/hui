# CLAUDE.md

给 Claude Code 使用。仓库通用的开发流程、代码要求和 PR review 标准以 [AGENTS.md](AGENTS.md) 为准，这里只写 Claude Code 需要额外知道的事。

## 项目

双人 2D 横板闯关 Game Jam 原型。Unity **6000.6.4f1**，URP 2D，Input System 1.20（新版 API，旧 `Input.` API 不可用）。

仓库根是文档 + 单一 Unity 工程。Unity 工程在 [Project/nameless/](Project/nameless/)，所有 `Unity` CLI 命令都要带 `-projectPath Project/nameless`。

- 运行时代码：`Project/nameless/Assets/Scripts/`，程序集 `GameJam.Runtime`
- 编辑器工具：`Project/nameless/Assets/Editor/`，程序集 `GameJam.Editor`
- 调试台 + 关卡编辑器：`Project/nameless/Assets/TuningLab/`，走预定义程序集 `Assembly-CSharp` / `Assembly-CSharp-Editor`（没有 asmdef，不能被 `GameJam.Runtime` 引用）
- 测试：`Assets/Tests/EditMode` 与 `Assets/Tests/PlayMode`
- 策划案：`docs/策划案/`；架构决策：`docs/adr/`；域约定：`docs/agents/`

当前进度：L0 灰盒、L1、L2 已完成，L3–L10 是占位。已实现范围见 README。

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
| GameJam → Build World Scene | `Assets/Scenes/World.unity`（旧世界，规则未对齐，先别用） |
| GameJam → Build Prototype Scene | `Assets/Scenes/Prototype.unity`（不进构建） |
| GameJam → Build Materials Test Scene | `Assets/Scenes/Materials.unity` |
| GameJam → Build Forms Test Scene | `Assets/Scenes/Forms.unity` |
| GameJam → New Level From Tuning Lab | `Assets/Scenes/Levels/*.unity`（复制 TuningLab 场景 + 写入基准 `LabLevel`） |
| GameJam → Convert Current Scene To Level | 把当前 Tuning Lab 副本就地转成关卡（基准场景禁止） |
| GameJam → Tuning Lab Level Editor | 编辑当前派生场景的 `LabLevel`（地形 / 绳 / 关卡分区 / 出生点 / 相机范围与跟随尺寸） |
| GameJam → Run Level Baseline Self Check | 纯数据自检：73 格 ↔ 8 块矩形往返 |
| GameJam → Run Level Play Check | 临时派生场景 → 进 Play 用真实物理验一遍 |

改关卡内容走 **Tuning Lab Level Editor**，不要手改 `TuningLab.unity`（基准场景）或 `World.unity`；旧测试房仍然改对应 builder 再重新生成。

连续关卡：在关卡编辑器的「关卡分区」里新建 / 移动 / 调大小 / 删除分区，分区的 `bounds` 就是相机范围；按 **B 的位置**选当前分区，分区不能重叠。B 不在任何分区时不夹相机，只用关卡的相机跟随尺寸跟两人相对位置缩放。TuningLab Inspector 的「相机默认」用于没有 `LabLevel` 的基准场景和新建关卡的初始值。

### 测试

在 Unity 的 **Window → General → Test Runner** 里跑。命令行模板（编辑器路径按本机实际情况替换）：

```bash
<Unity可执行文件> -batchmode -quit -projectPath Project/nameless \
  -runTests -testPlatform EditMode -testResults /tmp/editmode.xml

<Unity可执行文件> -batchmode -quit -projectPath Project/nameless \
  -runTests -testPlatform PlayMode -testResults /tmp/playmode.xml
```

注意：本机（2026-11 核对）已装 6000.6.4f1，编辑器二进制在 `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`；换机器按实际路径替换。**跑不了就直说跑不了，不要编造测试结果。**

## 代码约定

- 尺寸、跳距、冲刺距离等规格数值集中在 `GameConstants.cs`，不要散落魔数。
- 世界尺度、速度挡位、关卡拓扑、撞击传递这四件事有 ADR 定案（`docs/adr/0001`–`0004`）。改动与之冲突时先指出冲突，不要默默改掉。
- `MotionState` 用状态机 + `StateChanged` 事件外抛，动画和音效挂在事件上，不要往 `PlayerMotor` 里塞表现逻辑。
- 表面响应走 `IImpactReceiver`（`Pass / Bounce / Break / Accelerate / Stick / Die`），新增材质对象实现接口，不要加到撞击方。
- PlayMode 测试用真实物理和输入驱动；EditMode 测试只测纯 C#（如 `RunState`）。不要为了测试直接调 `ReceiveImpact` 这类绕过物理的私有路径。
- 每个资源文件都必须提交对应的 `.meta`；`.cs`、`.unity`、`.prefab` 等文本资源用 LF（见 `.gitattributes`）。
- 生成的 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.slnx` 不要提交，已由 `.gitignore` 忽略。

## 变更流程

1. 不直接在 `main` 上开发，从最新 `main` 建 `feat/<名称>`、`fix/<名称>`、`refactor/<名称>`、`docs/<主题>` 分支。
2. 变更保持最小，不夹带格式化或无关重命名。
3. 提交信息简洁，说明实际改了什么。
4. 走 PR 合入 `main`。Issue 追踪见 `docs/agents/issue-tracker.md`。
