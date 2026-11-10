# Tuning Lab 关卡编辑器：先做出地图，再看原理

本文按实际工作顺序写。第一次接触项目时，先看前面的“快速开始”；需要扩展编辑器或排查底层问题时，再看后面的“原理和代码地图”。

## 快速开始

### 1. 打开并编译工程

1. 用 **Unity Hub** 打开 `Project/nameless`。
2. 使用项目要求的 Unity 版本（当前验证版本：`6000.6.4f1`）。
3. 等 Unity 完成导入和脚本编译。项目里的 `.cs` 文件会由 Unity 自动编译，不需要单独运行 C# 编译器。
4. 如果找不到基准场景，先在 Unity 菜单执行：

   `GameJam → Build Tuning Lab`

   这会生成 `Assets/Scenes/TuningLab.unity`。

需要在命令行验证“能否编译并通过数据检查”时，见 §7.1 的 `LabLevelSelfCheck.RunBatch` 命令。

## 1. 边界

- **唯一规则基准**：`Assets/TuningLab/`。速度等级、跳/冲/撞、玩家尺寸、相机都以这里为准。
- **1 格 = 1 世界单位 = 16px**；原生分辨率 384×216；所有位置、尺寸、速度都用格描述。
- **关卡场景必须从 Tuning Lab 派生**：复制 `TuningLab.unity`，挂 `LabLevel`，用 `TuningLab.CaptureBaseline()` 写入基准房间。
- **一个 scene 放全部关卡**：`LabLevelData` 里一份地形/绳/出生点 + 多个 `LabLevelPartition`。
- **不再维护旧 World/Prototype/Materials/Forms 场景和脚本**；`Assets` 只保留 TuningLab、TuningLab 场景/Levels、`Sounds`、URP Settings。
- 旧文档（README/策划案等）仍保留，但描述的是清理前的旧游戏；以本文和 `Assets/TuningLab` 代码为准。

## 2. 代码地图

| 文件 | 职责 |
| --- | --- |
| `Assets/TuningLab/TuningLab.cs` | 参数、内置房间描述 `DescribeRoom`、`CaptureBaseline`、相机 `GetCameraFrame`、HUD/网格、音效/表现层挂载和死亡闪红 |
| `Assets/TuningLab/LabPlayer.cs` | 方块玩家：移动、跳、四向冲刺、撞击、驮带、形态、召回输入与幻影膛 |
| `Assets/TuningLab/LabVisual.cs` | 玩家分层视觉（外圈等级 / 边框 / 眼睛） |
| `Assets/TuningLab/LabSpeedFeedback.cs` | 速度反馈：V3 起、顶档 V6 的残影与拖尾，对象池回收 |
| `Assets/TuningLab/LabAudio.cs` | 运行时音效播放器：16 个 Cue，`Resources.LoadAll` 取素材，无场景引用 |
| `Assets/TuningLab/LabSettingsUI.cs` | 纯图标设置界面：Esc 暂停/恢复、音量、分辨率、全屏和预留位 |
| `Assets/TuningLab/LabSettingsStore.cs` | 设置 JSON：版本、临时文件、备份和坏档恢复 |
| `Assets/TuningLab/LabCamera.cs` | 跟随 A/B 中点；按 B 查当前分区的 bounds/min-max，并做混合 |
| `Assets/TuningLab/LabVerticalRope.cs` | 竖绳；零/一级挡住、二级沿接触法线穿过、三级以上沿法线反弹并提升一级；`ApplyShape` 同时服务预览和 Play |
| `Assets/TuningLab/LabHorizontalRope.cs` | 横绳平台；零/一级挡住、二级向下穿过并保留入射速度、三级以上反弹并提升一级 |
| `Assets/TuningLab/LabIce.cs` | 冰块：`groupId` 编组、组色边框、单块融化、整组破坏和地图重置恢复；冰面并入地形合并碰撞，滑行不碎不卡脚 |
| `Assets/TuningLab/LabMagma.cs` | 岩浆块：每个接触面首次接触强制红热、热二边再接触死亡、v2+ 撞击死亡、v3 整组破坏和地图重置恢复 |
| `Assets/TuningLab/LabHiddenBlock.cs` | 隐藏方块：无实体、外形与墙一致，按组盖住物件；玩家完全进入整组后永久半透明，地图重置不恢复 |
| `Assets/TuningLab/LabRepairBlock.cs` | 修复方块：1×1 实体，热二/灵体边接触其表面把那条边变回普通；不消耗、并入地形合并碰撞、不参与地图重置 |
| `Assets/TuningLab/LabRecallPickup.cs` | 召回道具：1×1 触发区，任一玩家拾取后全队永久解锁召回，地图重置不刷新 |
| `Assets/TuningLab/LabPhantom.cs` | 召回留下的幻影：复制被召回者形态、只对另一名玩家生效、最多 2 个 FIFO；A 的 u/L 幻影可让 B 从底面进入（炮口也可进）；电梯形幻影铺光桥（`LabLightBridgeBlock`） |
| `Assets/TuningLab/LabRespawnPoint.cs` | 重生点：3×3 触发区，任一玩家从区域外进入即激活并双人固定重生（活着进点的人保留当前形态）；两人都死亡时强制触发，一律以普通状态复活；`ApplyShape` 同时服务预览和 Play |
| `Assets/TuningLab/LabTeleportPoint.cs` | 传送点：3×3 触发区；两人进入后播放进出场动画并打开全互联终点选择 |
| `Assets/TuningLab/Level/LabLevelData.cs` | 关卡数据：实心格、绳、钥匙/门、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、像素画、重生点、传送点、出生点、相机尺寸、**关卡分区** |
| `Assets/TuningLab/Level/LabLevel.cs` | 数据 → 合并 `BoxCollider2D` / 绳 / 地刺 / 冰块 / 岩浆块 / 隐藏方块 / 修复方块 / 召回道具 / 像素画 / 重生点 / 传送点；编辑器预览和 Play 共用 |
| `Assets/TuningLab/Level/Editor/LabLevelScene.cs` | 从 `TuningLab.unity` 派生关卡场景；默认 `Level 1` 分区 |
| `Assets/TuningLab/Level/Editor/HiddenBlockDemoLevel.cs` | 生成隐藏方块演示关（`GameJam → Build HiddenBlock Demo Level`） |
| `Assets/TuningLab/Level/Editor/LabLevelEditorWindow.cs` | 关卡编辑器：地形/绳/出生点/分区/相机/机关/隐藏方块/修复方块/召回道具面板 |
| `Assets/TuningLab/Level/Editor/LabLevelMergeWindow.cs` | 合并两个场景：选 A/B、调一次 B 的偏移，复制 A 的场景并写入合并数据 |
| `Assets/TuningLab/Level/Editor/LabLevelSelfCheck.cs` | 纯数据自检（基准 73 格 ↔ 8 块、分区移动/删除、相机分区内外、隐藏方块、召回道具、修复方块的几何/去重/深拷贝/随分区移动与删除、合并偏移/组号/分区拒绝重叠） |
| `Assets/TuningLab/Level/Editor/LabLevelPlayCheck.cs` | 临时派关卡 → 进 Play → 真实物理检查 → 退出/删场景 |

## 3. 数据与生成

### 3.1 `LabLevelData`

- `solids`：实心格；`ropes`：竖绳/横绳；`keys`、`recalls`、`doors`、`spikes`、`ices`、`magmas`、`hiddenBlocks`、`repairBlocks`、`pixelArts`、`respawnPoints`、`teleports`：机关与装饰；`spawnA/B`：脚底落点。
- `teleports` 只存位置：所有传送点互为终点，不存配对关系；进入任意一个后从其余点里选择出口。
- `cameraMinSize / cameraMaxSize`：**关卡级**相机跟随尺寸（正交 size，半高）。
- `background`：滚动拼接背景的资源名（`Resources/Backgrounds` 下，如 `level1`）；空串 = 不铺背景（见 §4.4）。
- `bounds`：编辑范围；没有分区时也作为单关相机 clamp 范围。
- `partitions`：关卡分区列表。
- `Normalize()`：排序/去重、旧字段回落、相机尺寸 clamp、分区正尺寸。
- `CopyFrom()`：深拷贝，撤销/重置/派生都靠它。
- `AppendFrom()`：合并场景用，按格偏移并入另一份数据（见 §4.5）。

### 3.2 `LabLevel.Build`

- 把 `solids` 合并成尽量少的矩形，每个矩形一个 `BoxCollider2D`；竖绳生成 `LabVerticalRope`，横绳生成 `LabHorizontalRope`。
- 所有实心矩形、冰块、岩浆块和修复方块并入同一个 `LabTerrainField` 的 `CompositeCollider2D`：冰—冰、冰—地形、岩浆—岩浆、岩浆—地形、修复方块—地形的接缝都没有可碰撞的内侧竖面，走上表面/冰面时不会被接缝卡住，也不会把上表面滑行误判成侧面接触。
- 钥匙、门、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、重生点、传送点都逐格/逐点生成；冰块和岩浆块按 `groupId` 共享整组破坏。冰块/岩浆块的视觉和破坏仍然逐块，破坏/重置后重建一次合并几何。修复方块不破坏、不消耗，所以没有运行时列表和重置钩子，只静态登记在 `LabRepairBlock.All` 里供合并地形按接触点回查身份。
- 隐藏方块逐格生成 `LabHiddenBlock`，只有 `SpriteRenderer`、没有碰撞体，`SortingOrder = 6`，盖住地形/门/冰/岩浆/刺/钥匙和绳，但低于玩家。同一格的多个组各自独立判定。
- 召回道具只生成一个触发区；被拾取后销毁，`MapReset` 故意不重建它（能力存在 `TuningLab.RecallUnlocked` 上，重置不清空）。
- 传送点生成后由 `TuningLab` 统一注册；选择终点时不依赖场景里的手工引用。
- 像素画按 `LabPixelArtSpec { sprite, origin, size, sortingOrder }` 生成 `SpriteRenderer`；Sprite 缩放进整数格占地，不生成碰撞体，也不参与地图重置。
- 绳子规格保存在 `LabRopeSpec.orientation`；旧数据缺省为竖绳。
- 地图重置时恢复被拾取的钥匙、关回门并恢复所有冰块和岩浆块；隐藏方块的“已揭示”状态和召回能力一样不被 `MapReset` 重置，只有场景重载才恢复。修复方块不会被破坏也不会被消耗，重置对它没有事可做。
- **预览和 Play 走同一条 `Build`**：编辑器里看到的几何就是 Play 撞到的几何。
- 预览物体带 `HideFlags.DontSaveInEditor`，不写进场景；`OnEnable` 重建，`ClearGenerated` 清理。
- 内置几何仍用 `TuningLab.WhiteSprite` / 运行时材质；像素画是例外，明确保存可摆放的 Sprite 引用并由 `LabLevel.Build` 生成显示对象。

## 4. 关卡分区

### 4.1 规则

- `LabLevelPartition { name, RectInt bounds }`；`bounds` **同时是相机范围**。
- 判定点固定是 **B 的中心位置**；B 进入哪个分区，相机 clamp 就用哪个分区。
- **分区不能重叠**：`AddPartition / MovePartition / ResizePartition` 在数据层直接拒绝；贴边相邻允许。
- 移动分区时可以带内容：地形按格中心、绳按中心、出生点按点；`TranslatePartition` 负责平移。
- 删除分区可以选择只删分区，或连里面的地形/绳/机关一起删；出生点保留。
- 新关卡默认生成一个覆盖 `bounds` 的 `Level 1` 分区。

### 4.4 传送点选择

- **所有已标记传送点互为终点**：数据只存位置，不存配对；双人同时进入某个点后，该点自动标记。
- 已标记传送点少于两个时，按冲刺不会播放动画或进入选择，也不会冻结玩家或移动镜头；至少有两个已标记点后，才可触发传送。
- 触发后先播放双人高亮、粒子和双色冲天动画，相机立即吸附来源点；两束光都离屏后才进入终点选择。
- 选择状态下玩家输入暂停，按 `A/D` 在其它已标记传送点之间循环切换，镜头约 250ms 平移到当前终点并持续停留，画面略暗。
- 按 `Enter` 确认后，相机先到达终点，再播放双人淡入、高亮和爆开动画；两名玩家落在终点两侧（A 左、B 右），动画结束后恢复进入前形态和各自进入时的完整二维速度。
- 按 `Escape` 取消后，两名玩家在来源点两侧播放相同时序的返场动画，最终速度为零；取消不重置地图、不吞钥匙，并保留已标记传送点。
- 确认传送会重置终点附近的地图并吞掉两人的钥匙；已激活的重生点不会被传送清掉。

### 4.2 编辑器操作

- 快捷键只有四个：`1` 铺地形、`3` 擦地形、`4` 选区、`5` 选择；其它工具一律从工具面板选择，**禁止新增或恢复快捷键**。
- 分区面板：新建、命名、X/Y/宽/高、删除、移动时带物件。
- 分区工具：空白处拖出新分区；分区内部拖动移动；重叠时预览变红、不能创建。
- 冰块工具：同一格再点同组 = 删除，换组号再点 = 改组；「新建组」取当前最大组号 + 1。
- 岩浆块工具：操作和冰块一致；同格再点同组 = 删除，换组号再点 = 改组。岩浆块和冰块不能放在同一格。
- 隐藏方块工具：按组判断；同一格可以同时属于多个组（嵌套组）。同格同组再点 = 摘掉这一组；换组号再点 = 加入新组。隐藏方块无实体，校验检查编辑范围、地形、负组号和出生点，不阻止与钥匙/门/绳同格。
- 召回道具工具：1×1，同一格再点删除；拾取后销毁，地图重置不刷新。
- 修复方块工具：1×1、没有组号，同一格再点 = 删除，按住拖动可以连续铺。不能放在实心格里，也不能和冰块、岩浆块同格；和地刺同格靠「校验」和 Scene 里的红框提醒，规则和岩浆块一致。
- 传送点工具：左键放 3×3 传送点，同一格再点删除；所有传送点自动全互联，Scene 视图会用连线标出。
- 像素画工具：素材放 `Assets/TuningLab/Art/Resources/PixelArt/`，面板可选择 Sprite、宽高（如 1×1、2×2）和排序层；左下角对齐鼠标格。像素画是纯视觉装饰，不生成碰撞体；同一起点再放同一张素材会删除。
- 选区工具：同一时间只有一个选区；重新选择会换掉旧选区，增加选区按并集把新矩形并进去，删除选区按矩形挖掉一块。
- 所有修改走 `Undo.RecordObject`，改完 `AfterStructuralEdit()` / `RebuildPreview()`。

### 4.3 相机

`TuningLab.GetCameraFrame(B.position, ...)` 的优先级：

| 情况 | bounds | min/max |
| --- | --- | --- |
| B 在某个分区里 | 该分区的 `bounds` | 关卡 `cameraMinSize/MaxSize` |
| B 不在任何分区（存在分区） | `NoCameraBounds`，等价于不夹 | 关卡 `cameraMinSize/MaxSize` |
| 没有分区、有 `LabLevel` | 关卡 `bounds` | 关卡尺寸 |
| 没有 `LabLevel`（基准场景） | `defaultCameraBounds` | TuningLab 默认 |

- 相机跟随 A/B 中点；只有"当前在哪个关卡"看 B。
- 进入 Play 时 `LabCamera` 先在 `Configure`（运行时构建）或 `Start`（烘焙场景）调 `SnapToPlayers`，把位置和正交尺寸一次对齐到 A/B 中点（独活时对准存活者），不从场景里的固定点插值过来。
- `LabCamera` 对 bounds/size 做 `cameraFrameBlend` 混合；出界时用超大矩形代替 `Rect.zero`，避免 rect 插值把镜头拽向原点。
- 没有分区时仍按单关相机设置；新关卡默认有分区，所以旧行为不变。

### 4.4 背景

- Issue #130 选定图：第一关 1A、第二关 2A、第三关 3A，存放在 `Assets/TuningLab/Art/Resources/Backgrounds/level1~3.png`。
- 关卡数据 `background` 填资源名（`level1` / `level2` / `level3`），相机面板有快捷按钮；留空不铺。
- Play 时 `LabLevel.Build` 生成 `LabBackground`：一张图复用成无限背景，相机视野周围铺贴图块、移动时回收重摆；相邻块镜像翻转，原图不必无缝。背景按 `scrollFactor`（默认 0.5）倍速跟随相机形成视差。
- 背景是关卡级单值字段：合并场景保留接收方（A）的；编辑器预览不铺背景，只有 Play 时生成。

### 4.5 合并两个场景

大关卡分开搭、最后拼成一个：

- 入口：`GameJam → Merge Level Scenes`，或关卡编辑器底部的 **合并两个场景…** 按钮。
- 窗口只有一页：选 **A（底子）** 和 **B（并进来）**，调一次 **B 的偏移（格）**，点 **合并成一个场景**。选好两个场景后会自动读一次两者的范围（只读数据，读完立刻关掉，不搭预览）；「放到 A 右边」按 8 格间距一键排开。
- **A 是底子**：新场景直接复制 A 的场景文件，TuningLab 参数、出生点 A/B、相机尺寸、渲染设置都保留 A 的。
- **参数听谁的**：合并后只有一份 TuningLab，听 A 的（出生点、相机尺寸同理）。想保留 B 的手感就把 B 放在 A 的位置。
- **合并规则**（`LabLevelData.AppendFrom`）：
  - B 的地形、绳、机关、召回道具、重生点、传送点、像素画和分区按偏移平移；
  - 冰块 / 岩浆块 / 隐藏方块的组号和绳的非 0 `chainId` 重新编号，两个场景不会撞号；
  - B 的分区和 A 重叠时直接拒绝合并，把偏移调开即可（分区不能重叠）；
  - B 没有分区时按它的 `bounds` 补一个；
  - 出生点和相机尺寸始终取 A 的；
  - A 绑了独立关卡资源时：输出改 A 就写回它自己的资源，输出新场景则给新场景另建一份 `_Data.asset`，不会让两个场景共用一份数据。
- **输出**：默认 `<A 所在目录>/<A 名>_merged.unity`；覆盖已有场景时 `.meta` / GUID 保持不变。输出填 A 的路径就直接改 A（点「用 A」），填别的场景必须是新场景或没开着的场景。合并前会先保存当前打开的场景，合并完成后打开结果场景。
- B 不会被修改；改完 B 再合一次即可。

## 5. 加一个新组件

先选类型，选错落点后面全是返工：

| 类型 | 例子 | 放哪 | 进 `LabLevelData`？ |
| --- | --- | --- | --- |
| 全局系统 | 相机、音效、表现层 | `Assets/TuningLab/` | 否 |
| 一次性场景物件 | 只在一个场景手摆的触发器 | `Assets/TuningLab/` | 否 |
| 关卡内容 | 弹簧、移动平台、新机关 | 运行时组件放 `Assets/TuningLab/`，数据和生成放 `Assets/TuningLab/Level/` | **是（推荐）** |

只要策划要在编辑器里摆、撤销、跟着分区移动、存盘后重新生成，就走第三条：

1. **数据**：在 `LabLevelData.cs` 加 `[Serializable]` spec、`List<>`、`Normalize`、`CopyFrom`、`Add/Move/Delete`；空间物件要接进 `MovePartition/TranslatePartition/DeletePartition`，还要接进 `MoveContents/DeleteContents/CountContents`，选区和选择工具才会带上它。
2. **生成**：在 `LabLevel.Build` 里生成真实物体，预览/Play 同路径；尺寸 = 碰撞 = 视觉；不要依赖已删的 `Assets/Art`。组件把初始化写进 `ApplyShape()`，`Build` 里显式调一次，编辑器预览才不会是一坨看不见的空物体。
3. **运行时**：自己的 `MonoBehaviour` 放 `Assets/TuningLab/`（和 `LabIce` / `LabHiddenBlock` 同级）；数据/spec 和生成仍放 `Assets/TuningLab/Level/`；和玩家交互用 `LabPlayer` 公开接口/碰撞回调；不要往 `LabPlayer` 塞表现逻辑，也不要另起一套速度规则。
4. **编辑器**：`LabLevelEditorWindow` 加工具、`ToolLabels`、面板、`Record`、`AfterStructuralEdit`、Handles 预览、校验；再把它接进 `IsPlaceTool / ShouldAddAtStart / ApplyToolAtCell`（拖动连续摆放）、`EraseObjectsAt`（擦地形带掉）、`PickAt / PickArea / ValidPick / PickLabel`（选择与拖动），这样"拖动摆放 + 擦除 + 选区 + 选择"四件事就自动有了。
5. **默认值**：规则/基准放 `TuningLab` Inspector；关卡内容放 `LabLevelData`。
6. **资源**：每个 `.cs/.prefab/.png` 和文件夹都要 `.meta`；`.cs` 用 LF。

## 6. 调试

- **先 Play**：TuningLab HUD 直接看速度、折算格数、等级、充能、撞击读数；`R` 重置，`P` 像素网格，`Tab` 格线；timeScale/重力/跳高/冲刺都能拖。
- **Scene 视图**：预览就是 Play 的几何；分区、相机框、编辑范围都有 Handles/Gizmos。新组件自己加 `OnDrawGizmosSelected`。
- **分而治之**：`New Level From Tuning Lab` 建小场景，只放这个组件；用分区把 B 限制在一小段单独看相机/碰撞。
- **日志**：`Debug.Log(..., this)`；看 `Logs/Editor.log`。
- **常见症状**：
  - 编辑器看得到、Play 没有：`autoBuild`、`TuningLab.level`、`ClearGenerated`。
  - Play 有、编辑器没有：`RebuildPreview`、`OnEnable`、预览 `HideFlags`。
  - 分区移动组件不跟着走：`MovePartition/TranslatePartition` 没接。
  - B 出界相机被夹：`GetCameraFrame` 没走 `NoCameraBounds` 分支。

## 7. 验证

工程里没有旧的 `Assets/Tests`；验证靠 SelfCheck、PlayCheck、`Assets/TuningLab/Tests/` 的 PlayMode 规格和编辑器手测。

### 7.1 纯数据/数学自检

在 `Assets/TuningLab/Level/Editor/` 加 `[MenuItem]` 或扩 `LabLevelSelfCheck`，提供 `RunBatch()`，
失败时 `EditorApplication.Exit(1)`：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelSelfCheck.RunBatch ^
  -logFile Logs\selfcheck.log
```

退出码为 `0` 表示通过；失败时先看 `Logs/selfcheck.log`。Unity 的 `pipe not found` 偶发是批处理环境问题，重试一次即可。

### 2. 先调试基准规则

打开：

`GameJam → Open Tuning Lab Scene`

然后点击 **Play**。这里是调试台，不是正式关卡。HUD 可以直接看到速度、格数、等级、充能和撞击读数：

- `R`：重置
- `P`：像素网格
- `Tab`：格线
- `timeScale`、重力、跳高、冲刺等参数可在 Inspector 中调

先在这里确认玩家手感、相机和碰撞符合预期。所有正式关卡都会从这个场景派生，不要在正式关卡里另做一套玩家或相机规则。

### 3. 新建自己的地图

1. 执行 `GameJam → New Level From Tuning Lab`。
2. 在 `Assets/Scenes/Levels` 下输入关卡文件名，例如 `Level_02`。
3. 打开 `GameJam → Tuning Lab Level Editor`。
4. 在 Scene 视图中编辑地图。编辑器会自动显示预览，修改后点击面板里的 **保存场景**。
5. 点击 **Play**，直接测试这张地图。

新地图已经包含 Tuning Lab 的玩家、相机和规则。不要复制旧 World 场景，也不要直接修改 `Assets/Scenes/TuningLab.unity`。

### 4. 在编辑器里摆东西

编辑器工具快捷键：

| 按键 | 工具 |
| --- | --- |
| `1` | 铺地形 |
| `3` | 擦地形 |
| `4` | 选区 |
| `5` | 选择 |

**只允许这四个快捷键，禁止新增其它快捷键。** 其它工具（矩形、绳、出生点、分区、钥匙、门、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、重生点、传送点）从工具面板选择；网格显示和“跳到关卡”也在面板里操作。

常用操作：

- 在格子上点击或拖动即可编辑。
- 门从最下面那一格开始，向上占 `3` 格。
- 地刺面板选择朝上、朝下、朝左或朝右；同一格再次放置会改方向，不会生成第二根。
- 冰块按 `groupId` 编组；同一格再次放置同组会删除，换组号再点会改组。`0` 也是普通组，「新建组」会取当前最大组号 + 1。编辑器手柄和运行时冰块共用 `LabIce.GroupColor`：填充始终是默认冰蓝，0 号组用深蓝色边框，其余组在冰块外圈显示不同的 1 像素边框色（不透明度 0.9，明度上限 0.88 以免刺眼）；前 1024 个组号不重复。
- 岩浆块同样按 `groupId` 编组，操作和冰块一致；岩浆块和冰块不能放在同一格。校验会提示「和地形重叠」「和地刺同格」「和冰块同格」「压在出生点上」。
- 隐藏方块按 `groupId` 编组；同一格可以属于多个组（嵌套组），各组独立判定是否揭示。它不是实体，玩家可以穿过；只有整个身体完全落在该组格子并集内时才会把整组变半透明。
- 修复方块没有组号、不会被消耗：同一格再点一次删除，面板可以逐条删除或清空。校验会提示「不在编辑范围内」「和地形重叠」「和地刺/冰块/岩浆块同格」「压在出生点上」「和钥匙同格」「在门里」。
- 召回道具是 1×1 触发区：同一格再点删除；拾取后全队永久解锁召回，地图重置不会刷回来。
- 重生点是 3×3 触发区：同一格再点一次删除。面板可以逐个删除或清空；校验会提示「在地形里」和「两个重生点重叠」。
- 传送点也是 3×3 触发区：同一格再点一次删除；所有已标记传送点互为终点，Scene 视图用连线表示全互联。至少让两个传送点被双人进入并标记后，冲刺才会进入选择。校验会提示「在地形里」「重合」「触发区重叠」和「只有一个传送点」。
- 分区工具在空白处拖出分区，在分区内部拖动可移动分区；重叠分区会标红并且不能创建。
- 面板中的 **钥匙 / 门**、**地刺**、**冰块**、**岩浆块**、**隐藏方块**、**修复方块**、**召回道具**、**重生点**、**传送点**、**分区**可以逐条删除或清空。
- 所有机关摆放工具都能**按住拖动连续摆放**：钥匙、门、竖绳、横绳、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、重生点、传送点，拖着走一路铺过去。像素画按单次点击放置，避免拖动时刷出大量重叠素材。开关型工具（地刺 / 冰块 / 岩浆块 / 隐藏方块 / 修复方块 / 召回道具 / 重生点 / 传送点）按第一格的状态决定这一笔是"放"还是"删"，整笔保持一致。
- **擦地形**不只擦实心格：这一格上的钥匙、门、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、重生点、传送点、绳也一起擦掉，拖动可以连续擦。
- **选区工具**（`4`）：左键拖出一个矩形，起点和对角是它的两个顶点。**同一时间只有一个选区**；下一次拉选区怎么处理它由面板的三种模式决定——**重新选择**（默认，换掉旧的）、**增加选区**（按并集把新矩形并进当前选区，可以拼出不规则形状）、**删除选区**（从当前选区里按矩形挖掉一块）。选区只存在于编辑器，不写进关卡数据。
- **选择工具**（`5`）：点中物件后拖动移动它；**只要场景里有选区，不管鼠标从哪里开始拖，都拖动选区框和里面的全部内容**（地形格、绳、钥匙、门、地刺、冰块、岩浆块、隐藏方块、修复方块、召回道具、像素画、重生点、传送点都算，出生点和关卡分区不算）；框扫过、但原本不在选区里的物件不会被带走。删除用「选区 / 选择」面板里的 **删除选区内容** 按钮；移动和删除都可撤销。
- 所有编辑操作都支持撤销/重做。

地图数据保存在 `LabLevelData` 中，不要直接修改预览生成出来的碰撞体。预览和 Play 使用同一条生成路径，所以编辑器里看到的几何就是运行时实际碰撞的几何。

### 5. 调试自己的地图

推荐按下面的顺序排查：

1. **只放一个新东西**：先建一张小地图，只测试这个机关或组件。
2. **看 Scene 预览**：确认位置、尺寸、朝向和相机分区正确。
3. **进 Play 测碰撞**：不要只看画面，实际走过去、跳、撞、拿钥匙、开门或触发死亡。
4. **检查分区**：B 的中心位置决定当前分区和相机范围；B 不在任何分区时，相机不应被错误夹到原点。
5. **需要日志时**：使用 `Debug.Log(..., this)`；编辑器日志在 `Logs/Editor.log`。

常见问题：

| 现象 | 先检查 |
| --- | --- |
| Scene 里有，Play 里没有 | `autoBuild`、`TuningLab.level`、`LabLevel.BuildRuntime` |
| Play 里有，Scene 里没有 | `RebuildPreview`、`OnEnable`、预览物体是否使用了 `HideFlags.DontSaveInEditor` |
| 移动分区后物件没动 | 是否接入 `MovePartition/TranslatePartition` |
| B 出界时相机被拉向原点 | `GetCameraFrame` 是否走了 `NoCameraBounds` 分支 |
| 踩刺后两人都被销毁 | 正式关卡的 `deathWhenNoCheckpoint` 是否设为 `RespawnAtSpawn` |

### 6. 提交前验证

先跑纯数据检查：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelSelfCheck.RunBatch ^
  -logFile Logs\selfcheck.log
```

再跑真实物理 Play 检查：

```bat
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" ^
  -batchmode -nographics -projectPath Project\nameless ^
  -executeMethod LabLevelPlayCheck.RunFromCommandLine ^
  -logFile Logs\playcheck.log
```

最后手工回归：

- 编辑器预览和 Play 一致；
- 摆放、撤销、重做、保存、重新打开都正常；
- 分区新建、移动、改大小、删除正常，移动时内容跟随；
- 钥匙、门、地刺、竖绳、横绳的尺寸、方向、碰撞和重置正常；
- 冰块的 1×1 碰撞、组号、冰面滑行（含 v2+）不碎不卡脚、红热融一块、侧面 v2 碎整组和地图重置恢复正常；
- 岩浆块的 1×1 碰撞、组号、首次接触强制红热、持续接触不重复触发、热二边再接触死亡、v2+ 撞击死亡、v3 碎整组和地图重置恢复正常；
- 隐藏方块的 1×1 视觉、组号、嵌套组、玩家完全进入后整组永久半透明、地图重置不恢复、场景重载恢复；
- 修复方块的 1×1 碰撞、热二边贴面回普通、灵体边贴面恢复形状、贴面不锻造不消耗、装填态/幽魂态不响应；
- 召回道具的 1×1 触发区、拾取后全队解锁、地图重置不刷新；幻影的格心吸附、形态复制、仅对方可碰撞、FIFO 2 个和幻影炮/L 幻影；
- V3 起、顶档 V6 的残影与拖尾、死亡边框闪红、16 个音效 Cue 正常；
- 传送点的 3×3 触发区、双人进入标记、两个标记点门槛、A/D 切换终点、Enter 确认、Escape 取消、地图重置和吞钥匙正常；
- B 在分区内和分区外时，相机范围正确；
- 合并场景：两个小关卡合成一个，检查偏移、组号、分区、出生点、TuningLab 参数和输出场景的 `.meta`；
- 没有新字段或分区的旧关卡仍能打开。

## 关卡规则速查

- **唯一规则基准**：`Assets/TuningLab/`。
- **1 格 = 1 世界单位 = 16 px**；原生分辨率 `384×216`。
- 正式关卡必须从 `Assets/Scenes/TuningLab.unity` 派生。
- 多个连续关卡放在同一个 scene 中，用 `LabLevelPartition` 分区，不要为每一关复制一套规则。
- 相机跟随 A/B 中点，但只用 **B 的中心**判断当前分区。
- 分区不能重叠，贴边相邻允许。
- 钥匙和门是共享的：任意钥匙可以打开任意门，门的颜色只用于区分。**持钥上限 1 把**：身上已有一把时再捡无效，钥匙留在原处，花掉后脚下那把可以立刻补捡。灵体时间耗尽时若持有一把，会在方块中心所在格（没被场景实体占住）掉出一把**临时钥匙**，除"地图重置会清掉"外与普通钥匙完全相同。
- 绳按 `{ center, cells, chainId, orientation }` 保存；`orientation = Vertical/Horizontal`，旧数据缺省为竖绳。横绳是平台：零/一级挡住、二级向下穿过并保留入射速度、三级以上反弹并提升一级。竖绳的挡位按碰撞前沿接触法线撞进去的分量算：零/一级和普通墙一样挡死，二级沿法线穿过（只判一次，保留入射速度，忽略碰撞直到离开绳体），三级以上沿法线弹回，弹回速度 = 入射速度 + 一个等级间距（和横绳同一条公式）。绳不参与地形锻造：接触 / 下落 / 穿过都不改边态，只有三级以上的弹回会把接触边点到热二；热二的边再被弹回不会塌框或碎边。
- 地刺按 `{ cell, dir }` 保存；每格最多一根，碰到即死。
- 冰块按 `{ cell, groupId }` 保存；1×1 实体，可站立/滑行。运行时保持默认冰蓝填充，外圈显示 1 像素的组色边框，颜色由 `LabIce.GroupColor` 统一生成（编辑器和 Play 同色）。冰和地形共用合并碰撞，冰面滑行只产生顶面接触，不会卡脚；在冰面上加速到 v2/v3 不算撞击，不会碎冰。只有侧面/非承重接触才按 v2 整组碎；红热边只在非承重接触时融一块并把边回普通，冰格落在玩家脚下 footprint 内（含不同分组交界处的侧向接触）不会融掉脚下。冰面按住方向按 `a = g` 加速，4 格升一级，封顶 v4；松手保留滑行。
- 岩浆块按 `{ cell, groupId }` 保存；1×1 实体，可站立，走动按普通地面（不是冰面）。每条边只在第一次接触某个岩浆面时强制热二，持续贴着不重复触发；离开表面再接触才重新按第一次接触算。强制红热走 `StrikeEdge` 的死亡状态机：热二边接触岩浆直接死亡；灵体边不可复锻，不会被岩浆重新加热。以 v2 及以上沿法线撞上岩浆时，撞击红热和强制红热叠加，同样死亡；v3 撞击仍整组破坏。地图重置时恢复。
- 隐藏方块按 `{ cell, groupId }` 保存；无实体、外形与墙一致。同一格允许多个组；任一玩家的整个身体完全落入某一组的格子并集时，整组永久变半透明。`MapReset` 不恢复已揭示的组，只有场景重载才恢复。
- 修复方块按 `{ cell }` 保存；1×1 实体，可站立、挡路，外形是和 A 同色的蓝方块。A 的热二边或灵体边贴上它的表面 → 那条边回普通，形态按剩下的碎边重算；普通边接触什么都不发生。不限次数、不会被消耗，所以没有组号、没有破坏、也没有地图重置钩子。接触结算顺序是岩浆 → 修复 → 冰 → 普通地形：修复面只把边修回普通，不参与锻造，站在修复方块上不会把边加热。装填态和幽魂态的 A 不响应（门禁只写在 `LabPlayer.TryRepairEdge` 一处）。
- 召回道具按 `{ cell }` 保存；1×1 触发区。任一玩家拾取后全队永久解锁召回，默认键位 A 按 `LeftCtrl`、B 按 `Enter`（可在设置-操控页改）：施法者头顶有 1 格空位时，把另一名玩家传送到头顶，并在其旧位置留下一个吸附格心的幻影。幻影复制被召回者当时的形态，最多同时 2 个（FIFO）。A 的 u 炮 / L 转向器幻影允许 B 从底面进入；电梯形铺光桥；矛形（“2048”）会自己朝剩余边方向滑到撞墙且不与 B 相碰；普通 / 灵体形态是仅 B 可碰撞的普通方块。地图重置清空幻影，但保留能力、不刷新已拾取的道具。
- 传送点按 `{ position }` 保存；3×3 触发区，双人进入后运行时标记；至少两个已标记点才可进入选择，选择时 `A/D` 选终点、`Enter` 确认、`Escape` 取消；传送会重置地图并吞掉钥匙（临时钥匙同样被清掉）。处于灵体态的玩家不算“人在里面”，按一人死亡处理，凑不齐两人就不标记、不开选择。
- 表现层不是关卡数据：V3 起、顶档 V6 的残影和拖尾、死亡边框闪红、16 个运行时音效 Cue 都由 `LabSpeedFeedback` / `LabVisual` / `TuningLab` / `LabAudio` 负责，不需要在关卡里摆放，也不写进 `LabLevelData`。
- 预览生成物不保存到场景；运行时重新从数据生成。

### 场景用途

| 场景 | 用途 |
| --- | --- |
| `Assets/Scenes/TuningLab.unity` | 规则基准和调试台，禁止手改 |
| `Assets/Scenes/Levels/` | 正式关卡和单机制验证关（`KeyDoor_Test`、`传送测试`、`RespawnTeleport_Test`、`Final Map` 等） |
| `Assets/Scenes/Levels/ddebug room.unity` | 临时调试房，可随意验证组件 |

先在 `ddebug room` 或新建的小关卡里验证组件，再放进正式地图。

## 实装一个新组件

如果策划需要在编辑器里摆放、撤销、保存、随分区移动，并在 Play 时重新生成，这个组件就应该成为 `LabLevelData` 的关卡数据：

1. **定义数据**：在 `LabLevelData.cs` 添加 `[Serializable]` spec、`List<>`、`Normalize()`、`CopyFrom()` 和增删改方法。
2. **接入分区操作**：在 `MovePartition`、`TranslatePartition`、`DeletePartition` 中处理这个组件。
3. **实现运行时组件**：放在 `Assets/TuningLab/`，只使用现有 `LabPlayer` 接口和碰撞回调，不另起一套速度规则。
4. **接入生成**：在 `LabLevel.Build` 中生成真实物体；预览和 Play 必须走同一条路径。
5. **保持三者一致**：尺寸、碰撞和视觉都由同一份数据决定。
6. **接入编辑器**：在 `LabLevelEditorWindow` 增加工具、面板、Undo、Scene Handles 和校验。
7. **放对默认值**：规则参数放 `TuningLab` Inspector；地图内容放 `LabLevelData`。
8. **补齐资源文件**：每个 `.cs`、`.prefab`、`.png` 和目录都要有对应 `.meta`；C# 文件使用 LF 换行。

类型选择：

| 类型 | 示例 | 位置 | 是否进 `LabLevelData` |
| --- | --- | --- | --- |
| 全局系统 | 相机、音效、表现层 | `Assets/TuningLab/` | 否 |
| 单场景物件 | 只手摆一次的触发器 | `Assets/TuningLab/` | 否 |
| 关卡内容 | 弹簧、移动平台、新机关 | 运行时组件 `Assets/TuningLab/`；数据和生成 `Assets/TuningLab/Level/` | 是，推荐 |

## 原理和代码地图

### 数据如何变成地图

`LabLevelData` 保存：

- `solids`：实心格；
- `ropes`：竖绳/横绳；
- `keys`、`recalls`、`doors`、`spikes`、`ices`、`magmas`、`hiddenBlocks`：机关；
- `pixelArts`：纯视觉像素画，使用 Sprite 引用和整数格占地；
- `respawnPoints`、`teleports`：3×3 触发点；
- `spawnA/B`：出生点脚底位置；
- `cameraMinSize/cameraMaxSize`：关卡级相机尺寸；
- `bounds`：编辑范围和无分区时的相机范围；
- `partitions`：关卡分区。

`Normalize()` 负责排序、去重、旧字段回落、相机尺寸限制和分区尺寸修正；`CopyFrom()` 做深拷贝，撤销、重置和派生都依赖它。

`LabLevel.Build` 会把实心格合并成尽量少的矩形 `BoxCollider2D`，再生成绳、钥匙、门、地刺、冰块、岩浆块、修复方块和像素画。钥匙、门、地刺、冰块、岩浆块、修复方块的 `ApplyShape()` 同时用于预览和 Play；像素画只生成 `SpriteRenderer`，按 `size` 缩放且无碰撞体。

地图重置时，`LabLevel` 会重新生成被拾取的钥匙、关回门并恢复所有冰块和岩浆块，同时清空幻影和灵体掉出的临时钥匙（`LabKey.ClearTemporaries()`）；召回能力和隐藏方块的已揭示状态不会被 `MapReset` 重置，已拾取的召回道具也不会重新生成。修复方块不会被破坏也不会被消耗，重置对它没有事可做。运行时订阅 `TuningLab.MapReset`，清理生成物时退订。

### 相机如何选择范围

`TuningLab.GetCameraFrame(B.position, ...)` 的优先级：

| 情况 | bounds | min/max |
| --- | --- | --- |
| B 在分区内 | 当前分区 bounds | 关卡尺寸 |
| 有分区但 B 不在任何分区 | `NoCameraBounds`，不夹 | 关卡尺寸 |
| 没有分区但有 `LabLevel` | 关卡 bounds | 关卡尺寸 |
| 基准场景 | `defaultCameraBounds` | TuningLab 默认值 |

`LabCamera` 跟随 A/B 中点，并对范围和尺寸做 `cameraFrameBlend` 混合。无边界时使用超大 `NoCameraBounds`，不能拿 `Rect.zero` 直接插值，否则镜头会被拉向原点。

### 玩家死亡和重生

地刺和塌框最终都调用 `LabPlayer.Kill()`。死亡先隐藏冻结，只有两名玩家都死亡才结算：

| 条件 | 行为 |
| --- | --- |
| 只有一人死亡 | 该玩家隐藏冻结（无碰撞、不可操作、镜头不再跟随），另一人继续 |
| 两人都死亡，有激活重生点 | 强制触发该点：两人以普通状态回 A 左 / B 右固定位置，清钥匙并重置地图 |
| 两人都死亡，有重生点但一条都没激活过 | 强制触发离最后一次死亡位置最近的点，两人同样以普通状态复活 |
| 两人都死亡，没有重生点，`RespawnAtSpawn` | 两人回出生点并重置地图 |
| 两人都死亡，没有重生点，`DestroyPlayer` | 销毁死亡玩家 |

重生点由任一玩家从区域外进入 3×3 触发区时激活，不再要求钥匙；多个重生点按实际接触切换且同时只有一个激活。触发时地图只重置一次，两人清空钥匙和运动状态，并分别回到以重生点为中心、左右各 1 格的固定位置。已经在区域里、贴着边缘停留不会重复触发；全员离开后再次进入才再次触发。被传送、复活或出生直接放进区域不算“进入”。

触发分两种：活着走进触发区时，活着的人保留当前形态、已经死亡的队友以普通状态复活；两人都死亡时由 `TuningLab` 强制触发，两人一律清回普通状态（四条边全普通），不重放死亡前的碎边 / 红热边。

按 `R`（或 HUD 的「重置玩家」）：有激活过的重生点就强制触发该点，两人以普通状态复活并重置地图；一个点都没激活过才回出生点。

地图重置是**范围重置**：复活 / 进重生点 / 传送只恢复中心周围 `TuningLab.mapResetRadius` 格（默认 40，Inspector 可调）内的碎冰、碎岩浆和门；冰 / 岩浆按组完整恢复（组内任一块在范围内就整组恢复），完好的块不动。钥匙全局复生，避免"近处的门关了、远处的钥匙却没长回来"的死锁；幻影和临时钥匙一律全清。整条重置链只在确有块恢复时重建一次合并几何；范围内没有破坏时几乎零开销。无参 `ResetMap()` 仍是全图重置，供编辑器自检和测试使用。

**自动存档**：重生点每次换点激活（踩点或双人死亡强制触发）就把「场景名 + 点位置」写进 `persistentDataPath/checkpoint.json`（`LabCheckpointStore`，临时文件替换 + 备份，与设置存档同一路数）；反复死在同一个点不重复写盘。主页第二个按钮「读取存档」进关后第一帧消费 `PendingContinue`，走 `ForceRespawn` 把整队以普通状态复活到存档点（清钥匙、范围重置）；没有存档或存档场景不在构建列表时按钮禁用。第一个按钮「开始游戏」在有存档时先弹覆盖确认（Esc / 取消可退），确认后删档再进关。主页第四个按钮和 ESC 设置页左侧栏底部的「门 + 箭头」图标都是退出游戏（编辑器里停止 Play）；退出图标是代码绘制的，不在 SettingsIcons 素材包里。

两人都死亡后的结算统一推迟到本帧 `Update`，也就是所有 `FixedUpdate` 和物理接触回调都结束之后。死亡发生在碰撞 / 触发回调里，如果在回调中间立刻复活，同一物理步剩下的接触回调会再次打到刚复活的人（表现为“两人都死，只有一个人回到重生点，另一个死在重生点位置”）。不要为了省一帧把 `NotifyPlayerDied` 改回同步 `ForceRespawn`；重生规格 `LabRespawnPointTests.BothDeathsFromOverlappingSpikesInOneStepReviveTogether` 覆盖这条。

关卡编辑器的“重生点”工具会把点写入 `LabLevelData`，预览和 Play 都由 `LabLevel.Build` 生成 3×3 触发器。点击同一格可删除，编辑器面板也支持逐个删除和清空；分区移动、删除和撤销会同步处理这些点。

“传送点”工具同样把点写进 `LabLevelData`，`LabLevel.Build` 生成 3×3 触发器，`TuningLab` 在 Play 时注册所有传送点。数据不保存配对：进入任意一个传送点后，其余传送点都是可选终点，`A/D` 切换、`Enter` 确认，确认后两人落在终点两侧并重置终点附近的地图、清掉钥匙。分区移动、删除和撤销会同步处理这些点。

正式关卡如果使用地刺又没有放重生点，应把 `deathWhenNoCheckpoint` 改为 `RespawnAtSpawn`，否则第一脚踩空两人就被销毁。

### 主要代码文件

| 文件 | 职责 |
| --- | --- |
| `Assets/TuningLab/TuningLab.cs` | 规则参数、基准房间、HUD、网格、相机范围、音效/表现层挂载和死亡闪红 |
| `Assets/TuningLab/LabPlayer.cs` | 玩家移动、跳、冲刺、撞击、形态、死亡、召回输入和幻影膛 |
| `Assets/TuningLab/LabVisual.cs` | 玩家分层视觉和边框状态 |
| `Assets/TuningLab/LabSpeedFeedback.cs` | V3 起、顶档 V6 的残影和拖尾 |
| `Assets/TuningLab/LabAudio.cs` | 16 个 Cue 的运行时音效播放器 |
| `Assets/TuningLab/LabSettingsUI.cs` | 纯图标设置界面、Esc 暂停/恢复 |
| `Assets/TuningLab/LabSettingsStore.cs` | 设置存档、备份与坏档恢复 |
| `Assets/TuningLab/LabCheckpointStore.cs` | 重生点自动存档读写（checkpoint.json + 备份）与续玩点交接 |
| `Assets/TuningLab/LabCamera.cs` | A/B 中点跟随和分区相机范围 |
| `Assets/TuningLab/LabVerticalRope.cs` | 竖绳及其形状应用 |
| `Assets/TuningLab/LabHorizontalRope.cs` | 横绳平台及其形状应用 |
| `Assets/TuningLab/LabKey.cs` | 钥匙 |
| `Assets/TuningLab/LabRecallPickup.cs` | 召回道具：拾取解锁 |
| `Assets/TuningLab/LabPhantom.cs` | 召回幻影：形态复制、选择性碰撞、FIFO、幻影炮/L 幻影 |
| `Assets/TuningLab/LabDoor.cs` | 门 |
| `Assets/TuningLab/LabSpike.cs` | 地刺 |
| `Assets/TuningLab/LabIce.cs` | 冰块：编组、组色边框、单块融化、整组破坏和恢复 |
| `Assets/TuningLab/LabMagma.cs` | 岩浆块：编组、每个接触面首次接触强制红热、热二再接触死亡、v2+ 撞击死亡、v3 整组破坏和恢复 |
| `Assets/TuningLab/LabHiddenBlock.cs` | 隐藏方块：无实体、按组揭示、永久半透明 |
| `Assets/TuningLab/LabRepairBlock.cs` | 修复方块：1×1 实体、热二/灵体边贴面回普通、不消耗；`AtCell` 供合并地形按接触点回查身份 |
| `Assets/TuningLab/LabRespawnPoint.cs` | 重生点：3×3 触发、双人固定重生和地图刷新；活着进点保留形态，两人都死亡后强制触发以普通状态复活 |
| `Assets/TuningLab/LabTeleportPoint.cs` | 传送点：3×3 触发、进出场动画、全互联终点选择和传送 |
| `Assets/TuningLab/Level/LabLevelData.cs` | 关卡数据和分区 |
| `Assets/TuningLab/Level/LabLevel.cs` | 数据到碰撞体和运行时物体的生成 |
| `Assets/TuningLab/Level/Editor/LabLevelScene.cs` | 从 Tuning Lab 派生关卡 scene |
| `Assets/TuningLab/Level/Editor/HiddenBlockDemoLevel.cs` | 隐藏方块演示关生成 |
| `Assets/TuningLab/Level/Editor/LabLevelEditorWindow.cs` | 编辑器工具、面板和 Scene 预览 |
| `Assets/TuningLab/Level/Editor/LabLevelMergeWindow.cs` | 合并两个场景：选 A/B、调一次 B 的偏移、复制 A 场景并写入合并数据 |
| `Assets/TuningLab/Level/Editor/LabLevelSelfCheck.cs` | 纯数据自检 |
| `Assets/TuningLab/Level/Editor/LabLevelPlayCheck.cs` | 临时关卡的真实物理检查 |

## 设计决策和踩坑

1. **不要复用旧 World 场景。** Tilemap/脚本和 Tuning Lab 的 1 格实体不是同一套规范。
2. **不要用一个全局相机矩形管理连续关卡。** 用分区，并由 B 判定当前分区。
3. **预览物体不能存盘。** 使用 `HideFlags.DontSaveInEditor`，并在清理时移除生成物。
4. **运行时 Sprite 和 PhysicsMaterial 不是资产。** 场景烘焙可能丢引用，数据驱动重建更可靠。
5. **分区移动必须带上内容。** 只移动 bounds 会让地形、机关和出生点留在原地。
6. **重叠检查必须在数据层拒绝。** 不能只在编辑器里显示警告。
7. **不要把 ProjectSettings、用户生成的关卡和 packages-lock 的无关改动带进提交。**
8. **冰块/岩浆块的组号 0 是普通组。** 编辑器默认从 1 开始分配新组；手动把多块冰或岩浆设成 0 会让它们共享整组破坏。它们都不并入 `MergedRects`，必须逐格生成才能单块结算。岩浆块和冰块不允许占同一格。
9. **隐藏方块按组揭示，不是按格。** 同一格可以属于多个组（嵌套组）；判定是“某一组的格子并集是否完整包住玩家”，不是单块是否被踩到。揭示不可逆且不被地图重置恢复。
10. **修复方块也必须逐格生成。** 它和冰、岩浆一样不进 `MergedRects`：合并矩形只保留几何，会丢掉"这一格是修复方块"的身份，玩家侧的 `LabRepairBlock.AtCell` 就查不到东西，接触会退回普通地形锻造。

## 当前限制

- 分区尺寸目前通过面板数值修改，Scene 视图只支持拖动，不支持边角拖拽。
- 相机 min/max 是关卡级，不能为每个分区单独设置。
- 鼠标交互不能在无头批处理模式模拟，仍需要在 Unity 编辑器内手测。
- 红热边融冰在碰撞进入时结算；合并地形里新贴上的冰面接触会在地面 Stay 中补扫，但玩家脚下 footprint 内的承重块始终不融。
- 岩浆块的强制红热和撞击结算按“这条边第一次接触某个岩浆面”触发；持续贴着同一面不重复触发，离开表面后再次接触才按新接触结算。
- 地形/冰/岩浆/修复方块共用合并碰撞体，接触点正好落在不同物块边界时按回退 0.02 格后的格子归属，边界上的单点接触可能只算其中一侧。
- 修复方块和岩浆块一样按"边贴到面"结算，`OnCollisionEnter2D` 和 `OnCollisionStay2D` 都会扫：贴在修复面上时，同一边不会重复触发（回普通之后已经是普通边，普通边是空操作）。修复面优先于锻造：同一步里既算修复又算撞地的情况按修复处理，这条边不会被加热。
- 冰面加速按 `a = g` 持续加速，封顶 v4；已超速的玩家在冰面上不会被强制减速。
- 传送点按双人进入运行时标记；只有已标记点可互为终点，未进入的点不会出现在候选列表。
- 隐藏方块的揭示是单局内的不可逆过程；`MapReset` 不会恢复，只有场景重载才回到不透明。
- 召回幻影最多 2 个（FIFO）；炮/L 幻影的出口按被召回者原形态决定，不按进入方向反向；玻璃大炮进膛即射出并自毁，L 幻影进膛当步转向。
- 音效是运行时 `Resources.LoadAll` 挂载，没有场景引用；音量在 `LabSettingsUI` 里调，静音入口仍是静态 `LabAudio.Muted`。音乐音量当前只存值，还没有音乐轨。
- 残影/拖尾和死亡闪红的参数在 `TuningLab` Inspector 上，不写进关卡数据。
- 合并结果不可撤销：输出是新场景文件（覆盖时保留 `.meta`），A、B 不会被改；想重来就再合一次。
- 合并按 `bounds` 算摆放：内容跑到 `bounds` 外的场景仍会并入，但默认偏移和重叠检查按 `bounds` 算。
- 选 B 的参数时如果 B 没打开，会临时打开一次读完就关；大场景会停一下。
- `docs/策划案` 仍可能包含清理前的旧游戏描述；关卡编辑以本文和 `Assets/TuningLab` 代码为准。
