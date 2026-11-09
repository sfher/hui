# 构建与发布（CI）

工作流：[`.github/workflows/release.yml`](../.github/workflows/release.yml)

## 触发方式

| 方式 | 触发 | 结果 |
| --- | --- | --- |
| 打 tag | `git tag 1.0.0 && git push origin 1.0.0` | 三平台构建 + 自动发 Release |
| 手动 | Actions 页 → Release → Run workflow | 填版本号；`dry_run` 默认开，只构建不发版 |

## 产物命名

```
Hui-<版本>-<系统>-<架构>.zip
```

三个 zip：

- `Hui-1.0.0-windows-x64.zip`
- `Hui-1.0.0-macos-x64.zip`
- `Hui-1.0.0-linux-x64.zip`

解压后是三平台的可执行文件（`Hui.exe` / `Hui.app` / `Hui`）。

## 需要先配的 Secrets

仓库 Settings → Secrets and variables → Actions，加三项：

| Secret | 内容 |
| --- | --- |
| `UNITY_LICENSE` | Unity 许可证内容（`.ulf` 文件全文，或 Personal 的 XML） |
| `UNITY_EMAIL` | Unity 账号邮箱 |
| `UNITY_PASSWORD` | Unity 账号密码 |

**没配这三项，构建会在 Unity 激活阶段失败**，这是预期行为——GameCI 必须激活许可才能跑编辑器。

获取 `.ulf` 的方法见 [GameCI 文档](https://game.ci/docs/github/activation)。

## 版本号

构建前有一个 `setup` job 专门解析版本：

- tag 触发 → 用 tag 名（`1.0.0`）
- 手动触发 → 用 `version` 输入

**不能直接用 `github.ref_name`**：手动触发时那是分支名（如 `main`），产物会变成 `Hui-main-windows-x64.zip`。

## 场景

发布包只含两个场景，顺序固定：

1. `Assets/Scenes/Home.unity` —— **主界面，必须是第一个**
2. `Assets/Scenes/Levels/REAL FINAL MAP.unity`

`TuningLab.unity` 是调试场景，不进发布包。这个列表写在 `LabPlayerBuild.ReleaseScenes` 里，构建时如果主界面缺失会直接报错。

## 构建脚本

`Assets/TuningLab/Editor/LabPlayerBuild.cs`

| 入口 | 用途 |
| --- | --- |
| `RunFromCi()` | CI 用。正常返回=成功，抛异常=失败；**不能自己调 Exit**，否则和 unity-builder 的收尾打架 |
| `Run()` | 本地命令行用，自己设退出码 |

本地也能从菜单构建：`GameJam → Build Players → Windows / macOS / Linux`，输出到 `Project/nameless/Builds/`。

## 平台说明

| 平台 | 镜像 | 备注 |
| --- | --- | --- |
| Windows x64 | `ubuntu-*-windows-mono` | 从 Linux 交叉编译，产物可用 |
| macOS x64 | `ubuntu-*-mac-mono` | **未签名、未公证**，用户首次打开需右键→打开 |
| Linux x64 | `ubuntu-*-linux-il2cpp` | 需要目标机有 glibc 2.31+ |

全部按 x64 构建（架构名固定写在产物名里）。Unity 6000.6.4f1 的 IL2CPP 交叉编译在 GameCI 镜像上可行，但 Mac 的签名只能在 macOS 上做——这一点无法在 CI 里解决。

## 已知限制

- **Mac 包未签名**：没有 Apple Developer 证书和 notarization，Gatekeeper 会拦。分发时要写清楚。
- **本机无法验证 workflow**：本地 Unity 没有 `.ulf` 许可证，`-batchmode` 起不来，只能在配好 Secrets 的 CI 上验证。
- **Library 缓存**：按平台分别缓存，首次构建较慢（约 15–25 分钟），之后有缓存会快很多。
