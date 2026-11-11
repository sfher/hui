# 开发准则

本仓库的工程约定。**做任何改动前先读一遍**，尤其注意标 ⛔ 的硬约束。

> 面向 AI 代理的版本在仓库根的 `AGENTS.md` / `CLAUDE.md`，内容与本文一致，只是措辞更针对代理。

---

## 目录

- [工程基准](#工程基准)
- [硬约束](#硬约束)
- [分支与提交](#分支与提交)
- [代码变更](#代码变更)
- [关卡内容](#关卡内容)
- [验证要求](#验证要求)
- [资源与版本控制](#资源与版本控制)
- [Pull Request](#pull-request)
- [沟通](#沟通)

---

## 工程基准

| 项 | 值 |
| --- | --- |
| Unity | **6000.6.4f1**（URP 2D，Input System） |
| 工程目录 | `Project/nameless/`（**不是仓库根**） |
| 规则基准 | `Assets/TuningLab/` |
| 关卡数据与生成 | `Assets/TuningLab/Level/` |
| 音效素材 | `Assets/Sounds/Resources/` |
| 基准场景 | `Assets/Scenes/TuningLab.unity` |
| 正式关卡 | `Assets/Scenes/Levels/` |
| 文档入口 | [current-baseline.md](current-baseline.md) |

> 工程目录仍叫 `nameless`。仓库名是 Hui，但改目录名会破坏所有 Unity 引用。

---

## 硬约束

⛔ **不直接在 `main` 上开发。** 所有改动走分支 + PR。

⛔ **规则只进 `Assets/TuningLab/`。** 不要恢复已删除的 `Assets/Scripts`、旧 `World` 架构或第二套玩家逻辑。

⛔ **关卡内容只进 `LabLevelData`。** 需要编辑器摆放、撤销、保存或随分区移动的东西，必须进数据模型。不要手改生成的碰撞体。

⛔ **预览和 Play 共用 `LabLevel.Build`。** 否则"看到的"和"撞到的"是两套几何。

⛔ **关卡编辑器快捷键只有 `1` `3` `4` `5`。** `1` 铺地形、`3` 擦地形、`4` 选区、`5` 选择。禁止新增或恢复其它快捷键，新工具从工具面板进。

⛔ **不伪造验证结果。** 没跑过就说没跑过，跑不了就说跑不了。

⛔ **不加 AI 署名。** 提交信息、PR 描述、代码注释里不要出现 AI 工具署名、水印或 `Co-Authored-By`。

---

## 分支与提交

### 分支命名

从最新 `main` 建分支：

| 前缀 | 用途 |
| --- | --- |
| `feat/` | 新功能 |
| `fix/` | 缺陷修复 |
| `refactor/` | 重构（不改行为） |
| `docs/` | 纯文档 |
| `ci/` | 构建流程 |

**分支名要能看出做什么**，例如 `fix/light-bridge-fill-cell` 而不是 `fix-2`。

### 提交信息

- 说明**实际变更**，不写"优化代码"这种空话
- 首行简要概括，正文说清为什么改、怎么改
- 涉及多件事时拆成多个提交
- 中文即可

格式参考：

```
fix: 打包版点开始游戏进不去，场景名对不上

打包后 Build Settings 存的是完整路径，而 HomeScreen 用的是短名，
编辑器能命中、打包后不能。

修法：ResolveSceneName 遍历 SceneGetScenePathByBuildIndex，
短名/短名+扩展名/完整路径三种都能解析。
```

---

## 代码变更

### 基本原则

- **改之前先读懂现有代码**，理解为什么要这么写（很多地方有踩坑注释）
- **保持变更范围最小**，修 A 就只修 A，顺手重构先问
- **关注边界条件**：空值、越界、并发、帧序
- **不引入第二套规则**：数值、碰撞、输入都必须复用现有实现

### 常见陷阱（都是踩过的）

| 陷阱 | 说明 |
| --- | --- |
| `Debug.isDebugBuild` 写在字段初始化器 | MonoBehaviour 构造阶段访问会抛 `UnityException`，要放 `Awake` |
| 一个色值同时当底色和前景色 | 改深了一个另一个就看不见了，拆成两组 |
| 在 `Update` 设标志、`FixedUpdate` 读 | 两者执行顺序不保证每帧一致，会丢输入 |
| 每帧 `RebuildPreview()` | 等于每帧拆了场景重搭，拖动会"破裂" |
| `CompositeCollider2D` 用 `Synchronous` | 关一个碰撞体就重建整张几何，碎冰会卡 |
| `Path.GetFullPath` 处理 CI 的 `BUILD_PATH` | 它相对仓库根，Unity 工作目录是 `Project/nameless` |

### 命名

- 规则类用 `Lab` 前缀（`LabPlayer`、`LabIce`）
- 测试在 `Assets/TuningLab/Tests/`，文件名 `Lab*Tests.cs`
- 编辑器工具在 `Editor/` 或 `Level/Editor/`

---

## 关卡内容

### 必须进数据模型的东西

地形、竖绳、横绳、钥匙、门、地刺、冰块、岩浆、隐藏方块、修复方块、召回道具、像素画、重生点、传送点、出生点、相机分区。

### 新增一种机关的步骤

1. 在 `Assets/TuningLab/` 写组件
2. `LabLevelData` 加 spec 列表和 `Place*` 方法
3. `LabLevel.BuildSteps` 加 `Add*` 步骤
4. `LabLevelEditorWindow` 加工具和绘制
5. 补 PlayMode 规格
6. 更新 [冲刺与对撞-现行规则](冲刺与对撞-现行规则.md) 和 [关卡编辑器文档](tuning-lab-level-editor.md)

### 分区

- 分区的 bounds 同时是相机范围
- 分区不能重叠，贴边相邻可以
- 按 B 的位置选择当前分区

---

## 验证要求

### 三层验证

| 层 | 命令 | 何时跑 |
| --- | --- | --- |
| Self Check | `GameJam → Run Level Baseline Self Check` | 改了数据/生成逻辑 |
| Play Check | `GameJam → Run Level Play Check` | 改了物理/碰撞/机关 |
| PlayMode 规格 | Unity Test Runner → PlayMode | **提交前必跑** |

### 补充规格

- 新增功能或修缺陷时，**适当补充规格**
- 修 bug 优先写能复现的回归测试——先让它红，再让它绿
- 规格要断言语义，不要断言实现细节

### 关于 CI

CI 只构建 macOS + Linux。**macOS 偶发 OOM，失败后重跑即可**，不是代码问题。详见 [ci-release.md](ci-release.md)。

Windows 需本地构建验证：`GameJam → Build Players → Windows`。

---

## 资源与版本控制

### 资源

- 每个资源文件提交对应 `.meta`
- 文本资源用 **LF** 换行
- 像素图导入：**Point 过滤、无压缩、关闭 Mipmap**

### ProjectSettings

Unity 打开工程会重写 `ProjectSettings/`。提交前必须检查：

```bash
git status --short
git diff --stat Project/nameless/ProjectSettings/
git diff Project/nameless/ProjectSettings/EditorBuildSettings.asset
```

**只有** `serializedVersion`、字段顺序、默认值变化属于序列化噪音，可以丢弃。

**场景列表、图层、物理、输入、渲染设置的真实变化必须审查后提交。** 不要整体忽略 `ProjectSettings/`。

### 不提交

```
Library/  Temp/  Logs/  UserSettings/
*.csproj  *.slnx
Builds/
.dsh-vision-toolkit/  .WorkBuddy-vision-toolkit/
```

### 提交前清噪

Unity 会改写 `.meta` 的尾随空格，场景文件会被重新序列化。提交前：

```bash
git checkout -- $(git diff --name-only -- '*.meta')
git checkout -- Project/nameless/Assets/Scenes/   # 如果只是 fileID 重排
```

---

## Pull Request

### 提交 PR 前

- [ ] 全量 PlayMode 规格跑过
- [ ] 改动范围最小，没有顺手改无关文件
- [ ] 提交信息说清了实际变更
- [ ] 没有 AI 署名

### Review 优先级

1. **正确性** —— 逻辑对不对，边界处理没有
2. **设计** —— 是否复用了现有实现，有没有引入第二套规则
3. **测试** —— 有没有覆盖，断言是否到位
4. **可维护性** —— 命名、注释、结构
5. **工程质量** —— 性能、资源、版本控制

发现问题指出**具体文件、位置、原因和建议**。没有实质问题且验证充分即可合并。

### PR 描述要说清

- 改了什么、为什么改
- 验证方式和结果（**没验证的明确写"未验证"**）
- 已知限制

---

## 沟通

- 简洁、明确、可执行
- 不确定的内容**明确标注**，不装作知道
- 不做无依据的假设，尤其是数值和平台行为
- Issue 流程见 [agents/issue-tracker.md](agents/issue-tracker.md)
