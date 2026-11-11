# 构建与发布（CI）

工作流：[`.github/workflows/release.yml`](../.github/workflows/release.yml)

---

## 触发方式

| 方式 | 命令 | 结果 |
| --- | --- | --- |
| 打 tag | `git tag 1.0.0 && git push origin 1.0.0` | 构建 + 自动发 Release |
| 手动 | Actions 页 → Release → Run workflow | 填版本号；`dry_run` 默认开，只构建不发版 |

tag 格式支持两种：

- 正式版：`1.0.0`
- 预发布：`1.0.1-rc1`，版本号含连字符时自动标成 prerelease，不会顶到"最新正式版"

> 手动触发时**必须填 version**。不能直接拿 `github.ref_name` 当版本 —— 手动触发时那是分支名，产物会变成 `Hui-main-windows-x64.zip`。

---

## 产物命名

```
Hui-<版本>-<系统>-x64.zip
```

当前 CI 产出两个：

- `Hui-1.0.0-macos-x64.zip`
- `Hui-1.0.0-linux-x64.zip`

解压后是 `Hui.app` / `Hui`。

---

## 平台覆盖

| 平台 | CI | 说明 |
| --- | --- | --- |
| macOS x64 | ✅ | 偶发 OOM（约 1/4 概率），失败后 `gh run rerun <id> --failed` 重跑 |
| Linux x64 | ✅ | 稳定 |
| Windows x64 | ❌ | **本地构建**，见下 |

### Windows 为什么不在 CI 里

GitHub 托管 runner 上，Unity 的 Windows 容器会在加载 `WindowsStandaloneSupport` 之后约 0.6 秒被 OOM killer 杀掉（`exit code 137`）。

试过且**全部无效**：

| 尝试 | 结果 |
| --- | --- |
| 换 `base` 镜像 | 报 `build target was unsupported` —— base 不含 Windows 平台支持 |
| `dockerMemoryLimit: 14g` | 默认是 15.2G，砍到 14G 反而更容易 OOM |
| `dockerCpuLimit: 4` | 编译队列积压，内存峰值更高 |
| 同一 run 跑双份 | 两份同时 137 |

根因见 [game-ci/unity-builder#540](https://github.com/game-ci/unity-builder/issues/540)：Docker 的 `--memory` 是硬限制，触到就 SIGKILL；Unity 也没有限制自身内存的设置。**限制容器内存只会让 OOM 更早发生。**

`--shm-size=1025m` 是硬编码在 action 的 `dist/index.js` 里的，没有暴露成参数，改不了。

Windows 包请本地构建：

```
GameJam → Build Players → Windows
```

产物在 `Project/nameless/Builds/Windows/`，手动传到 Release 即可。

---

## 需要配置的 Secrets

仓库 Settings → Secrets and variables → Actions：

| Secret | 内容 |
| --- | --- |
| `UNITY_LICENSE` | `.ulf` 文件的完整内容 |
| `UNITY_EMAIL` | Unity 账号邮箱 |
| `UNITY_PASSWORD` | Unity 账号密码 |

三个都要，GameCI 即使 Personal 授权也要求齐全。获取 `.ulf` 的步骤见 [ci-secrets.md](ci-secrets.md)。

---

## 场景

发布包只含两个场景，顺序固定：

1. `Assets/Scenes/Home.unity` —— **主界面，必须是第一个**
2. `Assets/Scenes/Levels/REAL FINAL MAP.unity`

`TuningLab.unity` 是调试场景，不进发布包。列表写在 `LabPlayerBuild.ReleaseScenes` 里，主界面缺失会直接抛异常。

---

## 构建脚本

`Assets/TuningLab/Editor/LabPlayerBuild.cs`

| 入口 | 用途 |
| --- | --- |
| `RunFromCi()` | CI 用。正常返回 = 成功，抛异常 = 失败；**不能自己调 `EditorApplication.Exit`**，否则和 unity-builder 的收尾流程打架 |
| `Run()` | 本地命令行用，自己设退出码 |

两个入口都读 unity-builder 设的环境变量：

- `BUILD_TARGET` —— 目标平台
- `BUILD_PATH` —— 输出目录（**相对仓库根**，不是相对 Unity 工程目录）

> `BUILD_PATH` 是相对仓库根的（如 `build/StandaloneWindows64`），而 Unity 的工作目录是 `Project/nameless`。直接用 `Path.GetFullPath` 会拼成 `Project/nameless/build/...`，和宿主机上的 `build/` 对不上，打包步骤就会对着空目录报 `zip error: Nothing to do`。

本地命令行也能用：

```bash
Unity -batchmode -quit -projectPath Project/nameless \
      -executeMethod LabPlayerBuild.Run \
      -buildTarget linux -outPath /abs/out
```

---

## 工作流结构

```text
setup     解析版本号（tag 名或手动输入）
   ↓
build     矩阵：macos-x64 / linux-x64（各拿独立 runner）
   ├─ 缓存 Library（按平台隔离）
   ├─ unity-builder 构建
   ├─ 打包成 Hui-<版本>-<平台>-x64.zip
   ├─ 上传 Unity 日志（always，失败也能看到停在哪一步）
   └─ 上传产物
   ↓
release   Verify 三平台产物齐全 → 创建 Release
```

`Verify` 步骤会检查所需的 zip 都在，缺任何一个就让 Release 失败，**不发残缺版本**。

---

## 已知限制

| 限制 | 说明 |
| --- | --- |
| macOS 偶发 OOM | 重跑即可，不是稳定失败 |
| macOS 未签名 | 没有 Apple 证书，Gatekeeper 会拦，用户需右键 → 打开 |
| Linux glibc | 需要目标机 glibc 2.31+ |
| 首次构建慢 | 每平台要完整导入一遍，15–25 分钟；之后 Library 缓存命中会快很多 |

---

## 排查

**构建失败但不知道卡在哪**
下载 `unity-log-<平台>` artifact，里面有完整的 Unity 日志。

**`zip error: Nothing to do`**
说明构建目录是空的。检查 `LabPlayerBuild` 是否真的把产物写到了 `BUILD_PATH`。

**`build target was unsupported`**
镜像不含对应平台的 Build Support。Windows 必须是 `windows-mono`，macOS 必须是 `mac-mono`，Linux 可以用 `base`。

**`already_exists field: tag_name`**
同一个 tag 下已经有一个 Release。删掉旧的（`gh release delete <tag> --yes --cleanup-tag`）再重跑，或者直接编辑那个 Release。
