# 回（Hui）

**双人协作 2D 平台解谜** · 学院路 GameJam 参赛作品

两个方块、一个世界。大块（A）与小块（B）各自只有四条边，边会碎、会红热、会变成别的形状；两块撞在一起时动量守恒地交换速度。所有解法都建立在"如何让两块互相把对方甩出去"上。

Unity **6000.6.4f1** · URP 2D · Input System · 全部 x64

---

## 目录

- [玩法概览](#玩法概览)
- [快速开始](#快速开始)
- [操作](#操作)
- [工程结构](#工程结构)
- [关卡制作](#关卡制作)
- [验证体系](#验证体系)
- [构建与发布](#构建与发布)
- [文档索引](#文档索引)
- [制作者](#制作者)
- [许可](#许可)

---

## 玩法概览

### 两个角色

| | 大块 A（方形，带边框） | 小块 B（实心红块） |
| --- | --- | --- |
| 碰撞体 | 比格子略小，四条边围成框 | 满格实心 |
| 特殊能力 | 可以**红热锻造**、碎边改变形态 | 可以进入 A 的炮口被发射 |
| 在玩法中的角色 | 更多是"被摆布的一方"——它的形状由碎边决定 | 更多是"动力来源"——靠冲刺和高速度撞击 |

### 核心机制

**速度等级** —— 一切判定用"相当于从多少格高落下"来表达。速度折算成格数后分档（v0–v4），撞击门槛、红热门槛、碎冰门槛都按档位比较，而不是按绝对速度。这样数值调起来很直观："差两格"就是差两档。

**碎边与形态** —— A 的四条边可以被打碎，变成"灵体边"。碎哪几条边决定 A 变成什么形态：

| A 的形态 | 碎边数 | 能力 |
| --- | --- | --- |
| 普通 | 0 | — |
| u 大炮 | 1 | 把进来的 B 打出去 |
| L 转向器 | 2（相邻） | 把从底面进来的 B 转向水平射出 |
| ‖ / = 电梯 | 2（相对） | 挡人，同时也是竖绳/横绳的延伸 |
| 矛（"2048"） | 3 | 只剩一条边，会自己滑到撞墙 |
| 灵体 | 4 | 限时穿透，时间耗尽即死 |

**红热锻造** —— A 红热状态下撞击方块，可以把目标"锻造"成更高级别的速度载体；红热边接触岩浆会被强制红热，接触修复方块会回普通。

**召回与幻影** —— 拾取召回道具后，可以把队友传送到自己头顶，并在队友原来的位置留下一个**幻影**。幻影复制队友当时的形态，能做真实玩家做不到的事：铺光桥、当滑块、当玻璃大炮。

**双人动量** —— 两块相撞时速度互换（本质是能量守恒 + 冲刺态参与计算）。这是所有"把队友打上去"的解法基础。

### 关卡机关

地形、竖绳、横绳、地刺、钥匙与门、冰块（可编组，整组一起碎）、岩浆（接触强制红热，热二再接触即死）、隐藏方块（组内揭示）、修复方块、重生点、传送点、像素画装饰。

---

## 快速开始

### 环境

- Unity Hub + **Unity 6000.6.4f1**（带 Linux/Windows/macOS Build Support 模块）
- 打开 `Project/nameless/`（**注意不是仓库根目录**）

### 第一次运行

1. Unity Hub 添加 `Project/nameless`
2. 菜单 **GameJam → Build Tuning Lab** —— 生成规则基准场景
3. 打开 `Assets/Scenes/TuningLab.unity`，按 Play

想直接玩正式关卡，打开 `Assets/Scenes/Levels/REAL FINAL MAP.unity` 或从主界面进。

### 从发布版运行

从 [Releases](https://github.com/sfher/hui/releases) 下载对应平台的压缩包，解压后运行 `Hui` / `Hui.exe` / `Hui.app`。启动会停在主界面。

---

## 操作

默认键位。**所有键都可以在设置界面里改**（Esc → 操控页）。

### 游戏内

| 动作 | 大块 A | 小块 B |
| --- | --- | --- |
| 上下左右 | `W` `A` `S` `D` | `I` `J` `K` `L` |
| 跳跃 | `Space` | `'`（引号键） |
| 冲刺 | `LeftShift` | `;` |
| 召回 | `LeftCtrl` | `Enter` |

### 传送门（两人同时在传送点内时）

| 动作 | 大块 A | 小块 B |
| --- | --- | --- |
| 进入选择 / 确认 | `E` | `U` |
| 取消 | `Q` | `Backspace` |

选择界面里左右用各自的移动键。`Esc` 任何时候都能取消。

### 调试与全局

| 键 | 作用 |
| --- | --- |
| `Esc` | 打开/关闭设置界面（打开时全局暂停） |
| `R` | 重置两人到最近的激活重生点 |
| `P` | 切换像素网格 |
| `Tab` | 切换格线 |
| `F1` | 打碎 A 的下一条边（演示形态用） |
| `F2` | 恢复 A 的全部边 |

> 按键绑定存在 `settings.json`，按**键名**序列化，Input System 改枚举顺序不会让存档错位。冲突时界面会标红，但**不阻止保存**——同一个键绑两个动作时两个都会响应。

---

## 工程结构

```text
Project/nameless/
├─ Assets/
│  ├─ Scenes/
│  │  ├─ Home.unity              # 主界面，Build Settings 第 0 项
│  │  ├─ TuningLab.unity         # 规则基准场景（生成物，不手改）
│  │  └─ Levels/                 # 正式关卡 + 各系统测试房
│  ├─ Sounds/Resources/          # 音效素材（LabAudio 按目录加载）
│  └─ TuningLab/                 # 唯一规则基准
│     ├─ TuningLab.cs            # 参数、场地生成、HUD、音效挂载
│     ├─ LabPlayer.cs            # 玩家、形态、撞击、输入
│     ├─ LabBindings.cs          # 按键绑定模型与冲突检测
│     ├─ LabPhantom.cs           # 召回幻影（光桥、2048、玻璃大炮、L 转向）
│     ├─ LabLightBridgeBlock.cs  # 光桥方块
│     ├─ LabIce.cs / LabMagma.cs / LabRepairBlock.cs
│     ├─ LabVerticalRope.cs / LabHorizontalRope.cs
│     ├─ LabDoor.cs / LabKey.cs / LabSpike.cs / LabHiddenBlock.cs
│     ├─ LabRespawnPoint.cs / LabTeleportPoint.cs / LabRecallPickup.cs
│     ├─ LabSettingsUI.cs        # 设置界面（三页：基本 / 操控 / 关于）
│     ├─ LabSettingsStore.cs     # 设置 JSON 持久化
│     ├─ LabCheckpointStore.cs   # 存档（重生点续玩）
│     ├─ LabArt.cs / LabVisual.cs
│     ├─ LabAudio.cs / LabSpeedFeedback.cs / LabChargeFeedback.cs
│     ├─ LabCamera.cs / LabBackground.cs / LabHud.cs
│     ├─ LabCompositeRebuild.cs  # 合并碰撞体的帧末统一重建
│     ├─ LabPixelFont.cs         # 像素字体（Fusion Pixel 子集）
│     ├─ TierMath.cs / LabForms.cs
│     ├─ Level/
│     │  ├─ LabLevelData.cs      # 关卡数据（地形、机关、分区、相机）
│     │  ├─ LabLevel.cs          # 数据 → 场景生成
│     │  └─ LabLevelAsset.cs     # 数据独立成 ScriptableObject
│     ├─ Editor/                 # 菜单与自检
│     ├─ Level/Editor/           # 关卡编辑器、SelfCheck、PlayCheck
│     ├─ Resources/
│     │  ├─ SettingsIcons/       # 16×16 图标（含 _Dark/_Light 变体）
│     │  ├─ Credits/             # 制作者头像
│     │  └─ Fonts/               # 像素字体 + OFL 授权
│     └─ Tests/                  # PlayMode 规格（30 个文件）
└─ ProjectSettings/
```

### 三条硬约束

1. **规则改动只进 `Assets/TuningLab/`** —— 不要恢复已删除的 `Assets/Scripts` 或另建第二套玩家逻辑。
2. **可编辑的关卡内容进 `LabLevelData`** —— 不要手改生成的碰撞体，它们在 Play 时重建。
3. **编辑器预览和 Play 共用 `LabLevel.Build`** —— 保证"看到的"和"撞到的"是同一份几何。

> 完整的开发流程、分支规范、提交要求、验证标准和 PR review 清单见 **[开发准则](docs/development-guidelines.md)**。
>
> 工程目录仍叫 `Project/nameless/`。仓库名叫 Hui，但改目录名会破坏所有 Unity 引用，不值得。

---

## 关卡制作

### 菜单入口

| 菜单 | 用途 |
| --- | --- |
| **GameJam → Build Tuning Lab** | 生成/更新规则基准场景 |
| **GameJam → New Level From Tuning Lab** | 从基准派生正式关卡 |
| **GameJam → Convert Current Scene To Level** | 把 Tuning Lab 副本转为关卡 |
| **GameJam → Tuning Lab Level Editor** | 关卡编辑器（主入口） |
| **GameJam → Build HiddenBlock Demo Level** | 隐藏方块演示关 |
| **GameJam → Run Level Baseline Self Check** | 纯数据自检 |
| **GameJam → Run Level Play Check** | 真实物理自检 |
| **GameJam → Build Players → Windows/macOS/Linux** | 本地构建发布包 |

### 关卡编辑器

可放：地形、竖绳、横绳、钥匙、门、地刺、冰块（含分组）、岩浆（含分组）、隐藏方块（含分组）、修复方块、召回道具、像素画、重生点、传送点、出生点和相机分区。

**快捷键只有四个**（其余从工具面板进）：

| 键 | 工具 |
| --- | --- |
| `1` | 铺地形 |
| `3` | 擦地形 |
| `4` | 选区 |
| `5` | 选择 |

**移动选区**：拖动时不会重建整张预览——早期版本每帧重建会导致"整个场景破裂"，现在只在松手时重建一次。

**分区**（partitions）—— 大关卡按房间切分，每个分区的 bounds 同时是相机范围。分区不能重叠，贴边相邻可以。

**合并** —— 关卡数据可以独立成 `.asset`，把多个分区场景的数据合并成一份。合并规则：地形取并集、物件按格+分组去重、相机范围取并集，出生点保持接收方的值。

### 数据模型

`LabLevelData` 是纯数据，一个格子列表 + 各类物件的 spec 列表 + 分区列表。生成走 `LabLevel.Build`，编辑器预览和 Play 是同一路径。

---

## 验证体系

三层，从快到慢：

| 层 | 命令 | 覆盖 | 耗时 |
| --- | --- | --- | --- |
| **Self Check** | `GameJam → Run Level Baseline Self Check` | 纯数据规则：归一化、矩形合并、分区、相机 | 秒级 |
| **Play Check** | `GameJam → Run Level Play Check` | 真实物理：建临时关卡跑实际碰撞 | 十几秒 |
| **PlayMode 规格** | Unity Test Runner → PlayMode | 30 个测试文件，覆盖每个机制 | 约 80 秒 |

当前状态：**30 个测试文件**，覆盖全部机制与关键边界。

运行 PlayMode 全量的方式：

```bash
# Unity Test Runner 里手动跑，或用 CI
```

> **已知问题**：`LabHiddenBlockTests` 有部分用例会因测试间的静态状态泄漏而失败（单独跑该文件也会失败）。这是测试自身的问题，与业务代码无关。

---

## 构建与发布

### 自动（GitHub Actions）

打 tag 即触发：

```bash
git tag 1.0.0
git push origin 1.0.0
```

工作流 [`.github/workflows/release.yml`](.github/workflows/release.yml) 会构建 **macOS + Linux**（x64），打包成
`Hui-<版本>-<系统>-x64.zip`，自动创建 GitHub Release。

也可以在 Actions 页手动触发（`dry_run=true` 只构建不发版）。

**Windows 目前不在 CI 里** —— GitHub 托管 runner 上它的 Unity 容器会被 OOM killer 稳定杀掉（exit 137），见 [unity-builder#540](https://github.com/game-ci/unity-builder/issues/540)：限制容器内存只会让 OOM 更早发生。Windows 包请本地构建：

```
GameJam → Build Players → Windows
```

产物在 `Project/nameless/Builds/Windows/`。

### 本地

三个菜单项分别构建对应平台，产物在 `Project/nameless/Builds/`。

命令行也能构建：

```bash
Unity -batchmode -quit -projectPath Project/nameless \
      -executeMethod LabPlayerBuild.Run \
      -buildTarget linux -outPath /abs/out
```

发布包只含两个场景，顺序固定：

1. `Assets/Scenes/Home.unity`（主界面，**必须是第一个**）
2. `Assets/Scenes/Levels/REAL FINAL MAP.unity`

`TuningLab.unity` 是调试场景，不进发布包。构建脚本里有断言，主界面缺失会直接抛异常。

### 平台限制

| 平台 | 说明 |
| --- | --- |
| Windows x64 | 本地构建，CI 不可用 |
| macOS x64 | CI 可构建，**未签名未公证**，首次打开需右键 → 打开 |
| Linux x64 | CI 可构建，需要 glibc 2.31+ |

---

## 文档索引

### 先读这个

| 文档 | 内容 |
| --- | --- |
| [**开发准则**](docs/development-guidelines.md) | 硬约束、分支规范、代码要求、验证标准、PR 清单 |

### 开发基准（代码有疑问优先看这里）

| 文档 | 内容 |
| --- | --- |
| [当前项目基准](docs/current-baseline.md) | 架构边界、场景、菜单入口、验证层级 |
| [冲刺与对撞：现行规则](docs/冲刺与对撞-现行规则.md) | 按代码整理的全部玩法规则（最详细） |
| [关卡编辑器](docs/tuning-lab-level-editor.md) | 数据模型、代码地图、扩展方式 |
| [调试台手册](docs/tuning-lab.md) | TuningLab 操作 |
| [领域术语](GLOSSARY.md) | 隐藏方块、死亡反馈、残影等术语 |

### 工程流程

| 文档 | 内容 |
| --- | --- |
| [CI 与发布](docs/ci-release.md) | 工作流、产物命名、平台限制 |
| [CI 授权配置](docs/ci-secrets.md) | Unity 许可证 Secrets 怎么配 |
| [Issue 流程](docs/agents/issue-tracker.md) | 需求、验收、状态约定 |
| [策划 → 程序交接](docs/agents/策划-程序交接.md) | 设计变更如何落到代码 |
| [架构决策记录](docs/adr/) | ADR-0005 红热锻造 / 0006 单一基准 / 0007 灵体 |

### 设计文档（规划用，不覆盖代码基准）

[docs/策划案/](docs/策划案/) —— 概述、机制、关卡、剧情、美学

### 历史复盘

| 文档 | 内容 |
| --- | --- |
| [速度等级：基准与教训](docs/速度等级-基准与教训.md) | 数值设计的踩坑记录 |
| [最近更改](docs/recent-changes.md) | 近期合并记录 |

---

## 制作者

| | 分工 |
| --- | --- |
| [@sfher](https://github.com/sfher) | 主程序 |
| [@cyx-weimijiheheda](https://github.com/cyx-weimijiheheda) | 主程序 |
| [@declinegod](https://github.com/declinegod) | 策划 / 主程序 |
| [@strelizia-z](https://github.com/strelizia-z) | 美术 |

---

## 许可

**代码、关卡数据与自制美术** 采用 [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) 许可。
© 2026 Hui 制作组。全文见 [LICENSE](LICENSE)。

转载或改编时请注明原作者为「Hui 制作组」及上述协作者，保留版权声明，标明是否修改，并附协议链接。

### 第三方素材（各自遵循原授权，不在 CC BY 4.0 范围内）

| 素材 | 授权 |
| --- | --- |
| **Fusion Pixel Font**（像素字体） | SIL OFL 1.1，© 2022 TakWolf，全文见 `Assets/TuningLab/Resources/Fonts/LICENSE-OFL.txt` |
| **1-bit Pixel Icons**（设置图标） | 可用于个人与商业项目、无需署名；不得转售或再分发素材文件 |
| **Pixel Platformer Tileset**（游戏美术） | 同上 |

---

## 相关链接

- [Issues](https://github.com/sfher/hui/issues) · [Pull Requests](https://github.com/sfher/hui/pulls) · [Releases](https://github.com/sfher/hui/releases)
- 本仓库面向 **学院路 GameJam** 参赛，玩法与数值以代码和 [现行规则](docs/冲刺与对撞-现行规则.md) 为准，策划案仅作规划参考。
