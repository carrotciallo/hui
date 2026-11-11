# 最近更改

记录近期合并进 `main` 的工程改动。当前开发入口和规则基准见 [current-baseline.md](current-baseline.md)。

> 按主题归类，不按提交顺序。单条改动看 `git log`。

---

## 发布与构建（2026-11-10 ~ 11-11）

### 建立了完整的 CI / Release 流水线

从零搭起 GitHub Actions + GameCI：

- 打 tag 自动构建并发 GitHub Release，产物命名 `Hui-<版本>-<系统>-x64.zip`
- 支持预发布 tag（`1.0.0-rc1` 这类带连字符的自动标 prerelease）
- 手动触发带 `dry_run` 开关，可只构建不发版
- Unity 许可证通过 Secrets 配置，流程见 [ci-secrets.md](ci-secrets.md)

### 踩过的坑（都记在这里，别再踩）

| 尝试 | 结果 |
| --- | --- |
| 脚本写死输出路径 | 打包步骤对着空目录 `zip error: Nothing to do` |
| 让 unity-builder 自己管路径 | 还是空 —— 脚本写在容器内 `Project/nameless/Builds/`，没同步回宿主机 |
| `BUILD_PATH` 当普通相对路径拼 | 拼成 `Project/nameless/build/...`，宿主机上是仓库根的 `build/` |
| Windows/macOS 换成 `base` 镜像 | Linux 可以（平台支持内置），**Windows 不行**，报 `build target was unsupported` |
| `linux-il2cpp` 镜像 | 加载 `LinuxStandaloneSupport` 后 0.6 秒被 Killed，稳定 OOM |
| `dockerMemoryLimit: 14g` + `dockerCpuLimit: 4` | **反效果**，默认是 15.2G，砍到 14G 反而更容易 OOM |
| `max-parallel: 1` 串行 | 更糟 —— 让多个 job 挤在同一台 runner 上依次跑 |
| Windows 跑双份 | 两份在同一次 run 里同时 137，冗余扛不住 |

**根本结论**（[unity-builder#540](https://github.com/game-ci/unity-builder/issues/540)）：Docker 的 `--memory` 是硬限制，触到就 SIGKILL，限制容器内存只会让 OOM 更早发生；Unity 也没有限制自身内存的设置。GitHub 托管 runner 上的 OOM 只能靠重试。

### 当前 CI 覆盖

| 平台 | 状态 |
| --- | --- |
| macOS x64 | ✅ 偶发 OOM，重跑即可 |
| Linux x64 | ✅ |
| Windows x64 | 本地构建（`GameJam → Build Players → Windows`） |

---

## 运行时问题修复（2026-11-10 ~ 11-11）

### 打包版点开始游戏进不去（#148 / `7399179`）

打包后 Build Settings 里存的是**完整路径**（`Assets/Scenes/Levels/REAL FINAL MAP.unity`），而 `HomeScreen` 和存档用的是**短名**（`REAL FINAL MAP`）。编辑器里短名能命中，打包后不能。

修法：`ResolveSceneName` 遍历 `SceneUtility.GetScenePathByBuildIndex`，短名 / 短名+扩展名 / 完整路径三种写法都能解析。

### 主界面按钮点不动（#151 / #154）

两个层面：

- **#151**：release 改为窗口化启动，避免开机全屏协商错位
- **#154**：进游戏后在设置里切全屏，只要所选分辨率不是显示器原生分辨率，按钮仍然全灭 —— 改成全屏时表面恒为显示器原生尺寸

根因是 Unity Linux player 对非原生全屏只做缩放视口，渲染面与指针坐标系错位，IMGUI 命中整体失效。

### 碎冰卡顿（#150 / `69b1915`）

`LabLevel.BuildSteps` 最后把 composite 切成 `Synchronous`，于是**碎一块冰就自动重建整张合并几何**。关卡两千多格地形 + 812 块冰，撞碎一组（最大 75 块）就是几十到上百毫秒停顿。

修法：composite 保持 `Manual`，碎冰只标脏，`LabCompositeRebuild` 在本帧 `LateUpdate` 统一重建一次。

### 发布包显示调试台（`5a8d6ac`）

`LabHud` 被无条件挂上且没有开关。加 `showTuningPanel`，默认取 `Debug.isDebugBuild` —— 编辑器显示、正式包隐藏。

> 注意不能在字段初始化器里读 `isDebugBuild`，Unity 禁止在 MonoBehaviour 构造阶段访问，会抛 `UnityException`。

---

## 设置界面（2026-11 上旬）

### 三页结构（#131）

从单页扩展成三页，左侧竖排图标按钮翻页：

| 页 | 内容 |
| --- | --- |
| 基本设置 | 三档音量、分辨率、全屏（亮度预留） |
| 操控设置 | 两个玩家各 9 个动作的按键重映射 |
| 关于 | 项目名、制作组、CC BY 4.0 署名、第三方素材授权 |

### 图标对比度（#131）

选中的翻页按钮底色是青色、图标却是白色，对比度只有 1.57:1。用 PIL 生成两套图标变体（`_Dark` / `_Light`），按底色选用，青色底上达到 **6.31:1**。

### 配色回归深蓝主题

图标牌从米白改成深蓝 `(0.13,0.17,0.25)`。期间踩过一个坑：`ChipColor` 同时被当底色和前景色用，改深后第 2 页文字看不见 —— 已拆成 `ChipColor`（底）和 `InkColor`（前景）两组。

### 按键重映射（#94）

- 两个玩家各一套绑定，默认值见 [README 操作表](../README.md#操作)
- 冲突**允许存在**（同一个键绑两个动作时两个都响应），只标红不阻止保存
- 按键按**名字**序列化，Input System 改枚举顺序不会让存档错位

### 传送门按键（#152）

原来三处硬编码：进选择读 `LeftShift`/`.`（借用冲刺键）、移光标读 `A`/`D`（**和大块移动撞车**）、确认读 `Enter`。

现在全部走绑定：`Interact` / `Cancel` 两个新动作，默认 `E`/`Q`（大块）、`U`/`Backspace`（小块）。

---

## 光桥与幻影（2026-11 上旬）

### 光桥（#76 / #114 / #122 / #128）

电梯形（‖ / =）幻影沿平行线方向铺光桥。经历了几轮调整：

- **#114**：方块从"1/3 薄片"改成填满整格
- **#114**：普通地形和红块不再截断光桥；冰、岩浆、修复方块、门仍然停
- **#114**：横向走 `PlatformEffector2D` 单向平台，纵向走竖绳的二级穿过逻辑
- **#122**：竖光桥左右两面可攀附（贴面爬、跳跃脱离）；竖直光桥允许压过一格红块
- **#128**：单向平台参数收紧（`surfaceArc` 160、关掉侧向摩擦/弹跳）

### 2048 幻影（#118）

矛形（剩一条边）幻影改成会自己走的滑块：朝剩余边方向滑到撞墙，速度固定为二级速度最小值，**不与任何玩家相碰**，按格推进。

### L 形与玻璃大炮（#122）

- L 形入口门槛从 `V0Speed` 降到 `converterEntrySpeed`(12)，原来必须冲刺才进得去
- 玻璃大炮增益从"一个等级间距"抽成独立参数 `cannonBoostCells`（默认 8）
- 幻影装填/出膛补了音效

---

## 关卡编辑器（2026-11 上旬）

### 关卡数据可独立成资源（#109）

新增 `LabLevelAsset`（ScriptableObject）。`LabLevel` 绑了资源就读资源，没绑保持旧行为。这样各分区可以在不同场景里分开搭，最后合并。

### 合并（#109）

`LabLevelData.MergeFrom`：地形取并集、物件按身份去重（冰/岩浆/隐藏方块按格+分组）、`bounds` 取并集、出生点保持接收方。

### 拖动不再重建预览（#109）

`Move` 工具拖动时每帧调 `MarkDirty(true)` → `RebuildPreview()`，等于每帧把场景拆了重搭。改成拖动中只标脏，松手时重建一次。

### 矩形工具填充目标（#109）

可选填实心地形或隐藏方块（带分组 id）。未新增快捷键。

### 像素画摆放

关卡编辑器支持摆放纯视觉像素画装饰。

---

## 主页与存档（2026-11 上旬）

- **主页（#70 / #144）**：`Home.unity` 作为 Build Settings 第 0 项；纯图标入口、边框蛇动画、电视关闭式转场。
- **重生点自动存档（#144）**：`LabCheckpointStore` 把进度写到 `checkpoint.json`，主页支持读档续玩，新游戏时如有存档会弹覆盖确认。
- **渐进式加载（#147）**：主页点击后才异步加载关卡，加载期间黑屏 + Loading 动画。

---

## 2026-11-07 及更早

主要节点（详细记录在 git 历史里）：

- Tuning Lab 单一基准建立（[ADR-0006](adr/0006-lab-single-baseline.md)）
- 红热锻造（[ADR-0005](adr/0005-red-hot-forge.md)）、灵体（[ADR-0007](adr/0007-spirit-intangible-timer.md)）
- 修复方块（#63）、隐藏方块（#59）、音效（#60）、速度/死亡反馈（#62）
- 召回与幻影（#69）、纯图标设置界面第一版（#71）、主页（#70）
- 冰块、竖绳/横绳、地刺进入基准
- 判据修复：双人撞击补齐锻造门槛（#65）；A 碎边后仍可拾取钥匙（#67）
