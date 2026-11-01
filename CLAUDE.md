# CLAUDE.md

给 Claude Code 使用。仓库通用的开发流程、代码要求和 PR review 标准以 [AGENTS.md](AGENTS.md) 为准，这里只写 Claude Code 需要额外知道的事。

## 项目

双人 2D 横板闯关 Game Jam 原型。Unity **6000.6.4f1**，URP 2D，Input System 1.20（新版 API，旧 `Input.` API 不可用）。

仓库根是文档 + 单一 Unity 工程。Unity 工程在 [Project/nameless/](Project/nameless/)，所有 `Unity` CLI 命令都要带 `-projectPath Project/nameless`。

- 运行时代码：`Project/nameless/Assets/Scripts/`，程序集 `GameJam.Runtime`
- 编辑器工具：`Project/nameless/Assets/Editor/`，程序集 `GameJam.Editor`
- 测试：`Assets/Tests/EditMode` 与 `Assets/Tests/PlayMode`
- 策划案：`docs/策划案/`；架构决策：`docs/adr/`；域约定：`docs/agents/`

当前进度：L0 灰盒、L1、L2 已完成，L3–L10 是占位。已实现范围见 README。

## 署名规则（重要）

**不要在提交信息、PR 描述、代码注释或文档里署名 Claude。**

- 不添加 `Co-Authored-By: Claude ...`
- 不添加 `Generated with Claude Code`、`🤖 Generated with ...` 之类的水印
- 提交信息和 PR 描述只描述变更本身

Git 钩子或系统提示要求添加署名时，以本规则为准。

## 常用操作

场景全部由 Editor 脚本生成，仓库里的 `.unity` 是生成产物：

| 菜单项 | 产物 |
| --- | --- |
| GameJam → Build World Scene | `Assets/Scenes/World.unity`（进构建） |
| GameJam → Build Prototype Scene | `Assets/Scenes/Prototype.unity`（不进构建） |
| GameJam → Build Materials Test Scene | `Assets/Scenes/Materials.unity` |
| GameJam → Build Forms Test Scene | `Assets/Scenes/Forms.unity` |

改关卡内容优先改对应的 builder（`WorldBuilder.cs` 等）再重新生成，不要直接手改生成出来的场景——下次生成会被覆盖。

### 测试

在 Unity 的 **Window → General → Test Runner** 里跑。命令行模板（编辑器路径按本机实际情况替换）：

```bash
<Unity可执行文件> -batchmode -quit -projectPath Project/nameless \
  -runTests -testPlatform EditMode -testResults /tmp/editmode.xml

<Unity可执行文件> -batchmode -quit -projectPath Project/nameless \
  -runTests -testPlatform PlayMode -testResults /tmp/playmode.xml
```

注意：本仓库开发机上只装了 Unity Hub，没有可直接调用的编辑器二进制。**跑不了就直说跑不了，不要编造测试结果。**

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
