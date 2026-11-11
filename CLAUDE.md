给 Claude Code 使用。仓库通用的开发流程、代码要求和 PR review 标准以 [AGENTS.md](AGENTS.md) 为准，这里只写额外的工程事实。

## 项目

双人 2D 横板闯关 Unity 原型。Unity **6000.6.4f1**，URP 2D，Input System 1.20。

- Unity 工程：`Project/nameless/`
- 唯一规则基准：`Project/nameless/Assets/TuningLab/`
- 关卡数据/生成：`Assets/TuningLab/Level/`（`LabLevelData` + `LabLevel`）
- 音效素材：`Assets/Sounds/Resources/`（`LabAudio` 运行时挂载）
- 基准场景：`Assets/Scenes/TuningLab.unity`
- 正式关卡：`Assets/Scenes/Levels/`
- 当前文档入口：[docs/current-baseline.md](docs/current-baseline.md)

旧 World、旧测试房、旧 `Assets/Scripts` 和旧 `Assets/Tests` 已删除，不要根据旧文档恢复它们。

## 署名规则

不要在提交信息、PR 描述、代码注释或文档里署名 Claude，不添加 Claude 水印或 Co-Authored-By。

## 开工前检查

```bash
git status --short
git diff --stat Project/nameless/ProjectSettings/
git diff Project/nameless/ProjectSettings/EditorBuildSettings.asset
```

只丢弃纯序列化噪音；真实的场景列表、图层、输入和渲染设置变化必须审查并按需提交。不要整体忽略 `ProjectSettings/`。

## 常用菜单

| 菜单项 | 用途 |
| --- | --- |
| GameJam → Build Tuning Lab | 生成/更新规则基准场景 |
| GameJam → New Level From Tuning Lab | 创建正式关卡 |
| GameJam → Convert Current Scene To Level | 转换 Tuning Lab 副本 |
| GameJam → Tuning Lab Level Editor | 编辑关卡数据 |
| GameJam → Build HiddenBlock Demo Level | 生成隐藏方块演示关 |
| GameJam → Run Level Baseline Self Check | 纯数据自检 |
| GameJam → Run Level Play Check | 真实物理自检 |

不要手改 `TuningLab.unity` 的生成物；关卡内容通过 `LabLevelData` 和 Level Editor 修改。分区 bounds 同时是相机范围，按 B 的位置选择当前分区；分区不能重叠，贴边相邻允许。

## 验证

当前验证主线是 Tuning Lab 的 self-check / PlayCheck 和 `Assets/TuningLab/Tests/` 的 PlayMode 规格，不要声称已运行不存在的旧测试套件。Unity CLI 示例见 [docs/tuning-lab-level-editor.md](docs/tuning-lab-level-editor.md)。跑不了就直说跑不了，不要编造结果。

## 代码约定

- 1 格 = 1 世界单位 = 16px。
- 规则、玩家和相机复用 `Assets/TuningLab/`；不要新建第二套速度或碰撞规则。
- 预览和 Play 共用 `LabLevel.Build`。
- 新增可摆放关卡组件时，同时考虑数据模型、生成、编辑器操作、撤销、分区移动、校验和文档。
- 关卡编辑器快捷键只允许 `1` 铺地形、`3` 擦地形、`4` 选区、`5` 选择；禁止新增或恢复其它快捷键。
- 每个资源文件提交对应 `.meta`；文本资源使用 LF。

## 变更流程

从最新 `main` 创建 `feat/`、`fix/`、`refactor/`、`docs/` 或 `ci/` 分支，不直接修改 `main`；保持变更最小并通过 PR 合入。

完整工程约定见 **[docs/development-guidelines.md](docs/development-guidelines.md)**（硬约束、分支规范、代码陷阱、验证要求、PR 清单）。