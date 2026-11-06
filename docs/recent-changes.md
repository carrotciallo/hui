# 最近更改

记录近期合并进 `main` 的工程改动。当前开发入口和规则基准见 [current-baseline.md](current-baseline.md)；设计草稿见 [策划案](策划案/)。

## 2026-11-06

### 机制与表现层

- 隐藏方块（#59）：`LabHiddenBlock` + `LabLevelData.hiddenBlocks` + 关卡编辑器工具 + 演示关；无实体、按组永久半透明、`MapReset` 不恢复。
- 音效（#60）：`LabAudio` 运行时挂载，12 个 Cue；素材在 `Assets/Sounds/Resources/`，不依赖场景引用。
- 速度/死亡反馈（#62）：`LabSpeedFeedback` 的 V3/V4 残影与拖尾，`TuningLab` 的死亡边框闪红。
- 灵体态（#64）修订：四条边全碎后不受任何力，按输入四向匀速移动（斜向同速、松手即停），冲刺禁用；离开灵体恢复重力（[ADR-0007](adr/0007-spirit-intangible-timer.md)）。
- 召回与幻影（#69）：`LabRecallPickup` / `LabPhantom`；A 按 `Q`、B 按右 `Shift`，头顶 1 格有空间时把另一名玩家拉到头顶并在旧位置留下幻影；最多 3 个 FIFO。
- 判据修复：双人撞击补齐锻造门槛（#65）；A 碎边后仍可拾取钥匙（#67）。
- 传送门进出场动画收尾；相机 `SnapFocus` 与阶段机接入。
- 冰块运行时保持默认冰蓝填充，外圈显示 2 像素组色边框，和编辑器手柄共用 `LabIce.GroupColor`；前 1024 个组号不重复。
- 设置界面（#71）：`LabSettingsUI` + `LabSettingsStore`，Esc 打开时 `Time.timeScale = 0`，再按 Esc 或点关闭图标恢复；纯图标行覆盖主/音乐/音效音量、分辨率、全屏，并保留亮度和按键绑定占位。设置写入 `persistentDataPath/settings.json`，临时文件替换 + 备份 + 坏档回退。图标在 `Assets/TuningLab/Resources/SettingsIcons/`，同名 PNG 可替换。

### 验证与文档

- SelfCheck 新增隐藏方块和召回道具数据段；PlayCheck 覆盖召回道具、幻影和 `MapReset`。
- PlayMode 规格覆盖 Forge 23、Cannon 14、Ice 22、Magma 18、Rope 15、Respawn 14、Teleport 11、Spike 9、Recall 9、Phantom 8、KeyDoor 5、Settings 9、Spirit 4，共 161 条。
- 文档按当前架构重写：README、基准入口、编辑器文档、现行规则、术语表、ADR 注记和历史复盘。

## 2026-11-02

### Tuning Lab 关卡编辑器

- 新增 `Assets/TuningLab/Level/`：`LabLevelData`（格 / 横竖绳 / 出生点 / 相机范围）、`LabLevel`（数据 → 合并 `BoxCollider2D` / 横竖绳组件，编辑器预览与 Play 共用）
- 新增 **GameJam → New Level From Tuning Lab**：复制 `TuningLab.unity`，挂 `LabLevel` 并用 `TuningLab.CaptureBaseline()` 写入基准房间；关卡场景必须从调试台派生
- 新增 **GameJam → Tuning Lab Level Editor**：1 格网格铺/擦地形、矩形填充、放横/竖绳、设出生点、改相机范围，支持撤销、校验和从 Tuning Lab 重置
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