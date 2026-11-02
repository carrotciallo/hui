# 最近更改

记录近期合并进 `main` 的工程改动，便于回看当前仓库状态。详尽的关卡与机制设计见 [策划案](策划案/)，数值规格见 [speed-tier-spec.md](speed-tier-spec.md)。

## 2026-11-02

### Tuning Lab 关卡编辑器

- 新增 `Assets/TuningLab/Level/`：`LabLevelData`（格 / 绳 / 出生点 / 相机范围）、`LabLevel`（数据 → 合并 `BoxCollider2D` / `LabVerticalRope`，编辑器预览与 Play 共用）
- 新增 **GameJam → New Level From Tuning Lab**：复制 `TuningLab.unity`（继承调参值/相机/玩家），挂 `LabLevel` 并用 `TuningLab.CaptureBaseline()` 写入基准房间；关卡场景必须从调试台派生
- 新增 **GameJam → Tuning Lab Level Editor**：1 格网格铺/擦地形、矩形填充、放竖绳、设出生点、改相机范围，支持撤销、校验和"从 Tuning Lab 重置"
- 关卡编辑器增加**相机跟随尺寸**调节：每个 `LabLevel` 独立保存最小/最大正交 size，面板显示 16:9 视野格数与 1080p 像素缩放，Scene 视图可画出最小/最大视野框
- `TuningLab` Inspector 增加**相机默认**（min/max/bounds）和**关卡相机分区**：按 B 的位置选当前关卡，切换 bounds/size；没有分区时仍用 `LabLevelData` 的单关相机设置
- 新增 **GameJam → Run Level Baseline Self Check** 与 **GameJam → Run Level Play Check**（纯数据 / 真实物理两条验证）
- `TuningLab` 的场地描述抽成 `DescribeRoom`，基准场景与 `CaptureBaseline` 读同一段代码，避免两套规格
- 旧的 `GameJam → Level Editor`（编辑 `World.unity`）规则未对齐，暂不使用

## 2026-11-01

### 关卡编辑器

- 新增 `Assets/Editor/LevelEditorWindow.cs`，菜单 **GameJam → Level Editor**
- 直接编辑 `Assets/Scenes/World.unity`：按 1 格网格铺 / 擦地形，从 `Assets/Prefabs/Materials` 与 `Assets/Prefabs/Markers` 调色板摆放物件
- 支持撤销、L0–L10 区域跳转与取景、默认限制在当前区域
- 摆放校验：缺 Spawn、Spawn 越界、物件超出世界边界
- 注意：**GameJam → Build World Scene** 会重建场景，覆盖手工修改

### Unity MCP 接入

- `Packages/manifest.json` 加入 `com.coplaydev.unity-mcp`，锁文件同步其传递依赖
- 本机 MCP for Unity 服务运行在 `127.0.0.1:8088`（HTTP transport），供编辑器内自动化调用

### 手感调试台 TuningLab

- 代码在 `Assets/TuningLab/`，菜单 **GameJam → Build Tuning Lab** 生成 `Assets/Scenes/TuningLab.unity`
- 规则与数值见 [speed-tier-spec.md](speed-tier-spec.md)，操作与调参说明见 [tuning-lab.md](tuning-lab.md)
- 附带 **GameJam → Run Speed Tier Self Check** 与 **GameJam → Dump Tuning Values**

### 操作调整

- B 改为方向键移动、`/` 跳跃、`.` 冲刺（提交 `2b8c696`），TuningLab 的操作说明同步更新

### 工程维护

- 补全 `.gitignore`：Unity 生成目录大小写不敏感，补充崩溃转储、Addressables、Burst 导出、Rider 本地文件等规则
- 新增 `CLAUDE.md`，约定 Claude Code 的工作方式
- 明确 `ProjectSettings/` 的处理规则：只丢弃重新序列化噪音，真实改动照常提交，见 [AGENTS.md](../AGENTS.md) 的「工程与版本控制」

## 相关 PR

| PR | 内容 |
| --- | --- |
| #7 | 补全 Unity 生成物的 gitignore、添加 CLAUDE.md |
| #8 | 速度等级调试台 TuningLab、B 键位调整 |
| #9 | 接入 Unity MCP 包 |
| #10 | World 场景关卡编辑器 |
