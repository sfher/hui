# 配置 Unity CI 授权（Secrets）

工作流 `.github/workflows/release.yml` 跑之前，仓库里必须先配好三个 Secrets。
没配的话构建会在「激活 Unity」这一步直接失败——已经实测过，报错是：

```
##[error]Missing Unity License File and no Serial was found.
```

## 先弄清你的授权类型

打开 Unity Hub → 右上角头像 → **Manage licenses**：

| 类型 | 判断 | 走哪节 |
| --- | --- | --- |
| **Personal**（个人版） | 免费的、显示 Personal | A |
| **Plus / Pro**（付费） | 有订阅 | B |

---

## A. Personal 授权

需要三样东西：一份 `.ulf` 许可证文件、账号邮箱、账号密码。

### 第一步：拿到 `.ulf`

`.ulf` 是 Unity 授权凭证。本机是 Hub 登录授权的，凭证在 Hub 里，
磁盘上没有现成文件，所以要**手动激活**流程导出一份给 CI 用：

1. 关闭 Unity 编辑器
2. Unity Hub 里**登出**账号（先记下账号密码）
3. 用命令行启动，让它生成 `.alf`（激活请求文件）：

```bash
cd /home/wmjhhd/Projects/hui

~/Unity/Hub/Editor/6000.6.4f1/Editor/Unity \
  -batchmode -quit -nographics \
  -createManualActivationFile \
  -logFile /tmp/alf.log

# 生成在当前目录，名字类似 Unity_v6000.6.4f1.alf
ls *.alf
```

4. 打开 <https://license.unity3d.com/manual>，登录，上传那个 `.alf`
5. 选 **Unity Personal** → 用途选「个人」→ 下载得到 `.ulf`
6. 把 `.ulf` 内容贴到下面的 `UNITY_LICENSE`

> 注意：这一步会把你本机 Unity 的授权切换成"手动激活"状态。
> 导出完成后，在 Hub 里重新登录一次就能恢复日常使用。

### 第二步：加进 GitHub Secrets

仓库 **Settings → Secrets and variables → Actions → New repository secret**：

| Name | Value |
| --- | --- |
| `UNITY_LICENSE` | `.ulf` 文件的**全文**（从 `<?xml` 到 `</root>`，原样粘贴，不转义） |
| `UNITY_EMAIL` | Unity 账号邮箱 |
| `UNITY_PASSWORD` | Unity 账号密码 |

三个都要配。GameCI 即使拿 Personal 授权也要求邮箱密码齐全。

---

## B. Plus / Pro 授权

| Name | Value |
| --- | --- |
| `UNITY_SERIAL` | `XX-XXXX-XXXX-XXXX-XXXX-XXXX` |
| `UNITY_EMAIL` | 账号邮箱 |
| `UNITY_PASSWORD` | 账号密码 |

用序列号的话要跟我说一声，我把工作流里的 `UNITY_LICENSE` 换成 `UNITY_SERIAL`。

---

## 配好之后怎么验证

```bash
# 1. 确认三个 secret 都在
gh secret list --repo sfher/hui

# 2. 手动触发一次（dry_run=true 只构建、不发 Release）
gh workflow run Release --repo sfher/hui -f version=1.0.0 -f dry_run=true

# 3. 看进度
gh run list --repo sfher/hui --workflow Release --limit 3
gh run watch --repo sfher/hui
```

跑通之后打正式 tag 发版：

```bash
git tag 1.0.0
git push origin 1.0.0
```

---

## 常见失败对照

| 报错 | 原因 | 处理 |
| --- | --- | --- |
| `Missing Unity License File` | `UNITY_LICENSE` 没配或内容空 | 重配，注意是文件全文 |
| `Access token is unavailable` | 邮箱/密码错 | 检查 `UNITY_EMAIL` / `UNITY_PASSWORD` |
| `No valid Unity Editor license found` | `.ulf` 不完整或过期 | 重新走一遍手动激活 |
| `license is not valid for this version` | 许可证版本与 `6000.6.4f1` 不符 | 用同版本编辑器生成 `.alf` |
| 卡在 `Activating Unity` 超过 10 分钟 | Personal 授权的并发限制 | 等前一个跑完 |

---

## 一个现实提醒

**Personal 授权在 CI 上有并发限制**：同一账号同时只能跑一个 Unity 实例。
这个工作流是三平台并行，用 Personal 时会变成串行排队，
总耗时可能 40 分钟以上（Pro 大约 15–25 分钟）。

如果嫌慢，可以先只保留一个平台跑通（改 `release.yml` 里 `matrix.include` 只留一项），
确认链路没问题后再放开三个。

---

## 实测状态（2026-11-09）

已经触发过一次 dry run（run `42608944911`），结果与预期一致：

| Job | 结果 |
| --- | --- |
| Resolve version | **success** |
| Build windows-x64 | failure（缺 license） |
| Build macos-x64 | failure（缺 license） |
| Build linux-x64 | failure（缺 license） |
| Publish release | **skipped**（dry_run 生效） |

说明工作流结构、版本号解析、dry_run 开关都正常，只差许可证。
