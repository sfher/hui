#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：窗口面板（地形/分区）。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{

    // ------------------------------------------------------------------
    // 窗口 UI
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Play 模式下不能编辑关卡。退出 Play 再改。", MessageType.Info);
            return;
        }

        RefreshIfNeeded();

        if (level == null)
        {
            DrawOnboarding();
            return;
        }

        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        DrawHeader();
        EditorGUILayout.Space();
        DrawTools();
        EditorGUILayout.Space();
        DrawSelection();
        EditorGUILayout.Space();
        DrawTerrain();
        EditorGUILayout.Space();
        DrawPartitions();
        EditorGUILayout.Space();
        DrawRopes();
        EditorGUILayout.Space();
        DrawKeyDoors();
        EditorGUILayout.Space();
        DrawRecalls();
        EditorGUILayout.Space();
        DrawSpikes();
        EditorGUILayout.Space();
        DrawIces();
        EditorGUILayout.Space();
        DrawMagmas();
        EditorGUILayout.Space();
        DrawHiddenBlocks();
        EditorGUILayout.Space();
        DrawRepairBlocks();
        EditorGUILayout.Space();
        DrawRespawnPoints();
        EditorGUILayout.Space();
        DrawTeleports();
        EditorGUILayout.Space();
        DrawSpawns();
        EditorGUILayout.Space();
        DrawBounds();
        EditorGUILayout.Space();
        DrawCamera();
        EditorGUILayout.Space();
        DrawActions();
        DrawValidation();

        EditorGUILayout.EndScrollView();
    }

    void DrawOnboarding()
    {
        EditorGUILayout.LabelField("Tuning Lab 关卡编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "当前场景没有 LabLevel 数据。\n" +
            "关卡场景必须从 Tuning Lab 派生，不能直接改 TuningLab.unity（那是基准场景）。",
            MessageType.Warning);

        if (GUILayout.Button("从 Tuning Lab 新建关卡场景", GUILayout.Height(32f)))
        {
            LabLevelScene.CreateInteractive();
            Refresh();
        }

        if (GUILayout.Button("把当前场景转成关卡（仅限 Tuning Lab 副本）"))
        {
            LabLevelScene.ConvertCurrentInteractive();
            Refresh();
        }

        if (GUILayout.Button("打开 Tuning Lab 基准场景"))
            LabLevelScene.OpenLab();

        EditorGUILayout.Space();
        DrawExistingLevels();
    }

    void DrawExistingLevels()
    {
        if (!AssetDatabase.IsValidFolder(LabLevelScene.LevelFolder)) return;

        var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { LabLevelScene.LevelFolder });
        if (guids.Length == 0) return;

        var paths = new List<string>();
        foreach (var guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal);

        EditorGUILayout.LabelField("已有派生关卡", EditorStyles.boldLabel);
        foreach (var path in paths)
        {
            if (GUILayout.Button(Path.GetFileNameWithoutExtension(path)))
                LabLevelScene.OpenScene(path);
        }
    }

    void DrawHeader()
    {
        EditorGUILayout.LabelField(SceneManager.GetActiveScene().name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("关卡数据来自 Tuning Lab 基准（CaptureBaseline）", EditorStyles.miniLabel);

        if (lab == null)
        {
            EditorGUILayout.HelpBox("场景里没有 TuningLab，Play 时不会生成场地。", MessageType.Error);
            return;
        }

        if (lab.level != level)
        {
            EditorGUILayout.HelpBox("TuningLab 没有指向这个 LabLevel。", MessageType.Warning);
            if (GUILayout.Button("绑定到当前 LabLevel"))
            {
                Undo.RecordObject(lab, "绑定关卡数据");
                lab.level = level;
                EditorUtility.SetDirty(lab);
                MarkSceneDirty();
            }
        }

        if (!lab.autoBuild)
        {
            EditorGUILayout.HelpBox("TuningLab.autoBuild 关着，Play 不会生成场地（这是烘焙场景的遗留状态）。",
                                    MessageType.Warning);
            if (GUILayout.Button("打开 autoBuild"))
            {
                Undo.RecordObject(lab, "打开 autoBuild");
                lab.autoBuild = true;
                EditorUtility.SetDirty(lab);
                MarkSceneDirty();
            }
        }

        if (!Mathf.Approximately(lab.roomScale, 1f))
        {
            EditorGUILayout.HelpBox("TuningLab.roomScale ≠ 1。有 LabLevel 时场地完全按关卡数据生成，roomScale 不生效。",
                                    MessageType.Info);
        }
    }

    void DrawTools()
    {
        EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
        tool = (Tool)GUILayout.SelectionGrid((int)tool, ToolLabels, 3);

        switch (tool)
        {
            case Tool.Paint:
                EditorGUILayout.HelpBox("左键拖动铺 1 格实心地形。", MessageType.None);
                break;
            case Tool.Erase:
                EditorGUILayout.HelpBox("左键拖动擦掉实心格。", MessageType.None);
                break;
            case Tool.Rect:
                EditorGUILayout.HelpBox(
                    fillTarget == FillTarget.HiddenBlock
                        ? "左键拖出一个矩形，松手后填充隐藏方块。"
                        : "左键拖出一个矩形，松手后填充实心地形。",
                    MessageType.None);

                EditorGUILayout.LabelField("填充内容", EditorStyles.boldLabel);
                fillTarget = (FillTarget)GUILayout.Toolbar(
                    (int)fillTarget, new[] { "实心地形", "隐藏方块" });

                if (fillTarget == FillTarget.HiddenBlock)
                    fillGroupId = EditorGUILayout.IntField("分组 id", fillGroupId);
                break;
            case Tool.Rope:
                EditorGUILayout.HelpBox("左键在格子底部放一根竖绳，向上长。", MessageType.None);
                break;
            case Tool.HorizontalRope:
                EditorGUILayout.HelpBox("左键沿格子顶部放一根横绳，向右长。", MessageType.None);
                break;
            case Tool.SpawnA:
                EditorGUILayout.HelpBox("左键设置 A 的落点（脚底）。必须在相机范围内。", MessageType.None);
                break;
            case Tool.SpawnB:
                EditorGUILayout.HelpBox("左键设置 B 的落点（脚底）。必须在相机范围内。", MessageType.None);
                break;
            case Tool.Partition:
                EditorGUILayout.HelpBox(
                    "左键空白处拖出新分区；在分区内部拖动可移动分区（默认带上里面的地形/绳/出生点）。\n" +
                    "改名 / 调大小 / 删除在下面的「关卡分区」面板里。",
                    MessageType.None);
                break;
            case Tool.Key:
                EditorGUILayout.HelpBox("左键在一个格子上放一把钥匙（1×1，拾取后消失）。", MessageType.None);
                break;
            case Tool.Recall:
                EditorGUILayout.HelpBox(
                    "左键放一个 1×1 召回道具；同一格再点删除。任一玩家拾取后全队永久解锁召回，\n" +
                    "地图重置不会刷新道具。A 按 Q、B 按右 Shift，把另一名玩家召回到头顶并留下幻影。",
                    MessageType.None);
                break;
            case Tool.Door:
                EditorGUILayout.HelpBox(
                    "左键点门最下面那一格，门向上占 3 格（1×3）。任意一把钥匙能开任意一扇门，\n" +
                    "颜色只用于区分，在下面的「钥匙 / 门」面板里调。",
                    MessageType.None);
                break;
            case Tool.Spike:
                EditorGUILayout.HelpBox(
                    "左键点刺所在的那一格（不能是实心格）；换个方向再点 = 改向，同方向再点 = 删掉这根刺。" +
                    "「擦地形」把该格的地形和刺一起擦掉。\n" +
                    "朝上 = 立在地板顶上的地刺；朝下 = 吊在天花板下的倒刺；朝左 / 朝右 = 贴墙的侧刺。" +
                    "刺的底座必须有实心格，悬空的会在「校验」里列出来。",
                    MessageType.None);
                int spikeDirIndex = System.Array.IndexOf(SpikeDirs, newSpikeDir);
                spikeDirIndex = GUILayout.SelectionGrid(spikeDirIndex, SpikeDirLabels, 4);
                newSpikeDir = SpikeDirs[Mathf.Clamp(spikeDirIndex, 0, SpikeDirs.Length - 1)];
                break;
            case Tool.Ice:
                EditorGUILayout.HelpBox(
                    "左键放一块 1×1 冰块，按住拖可以连续铺。同组冰块共享 v2 整组破坏；\n" +
                    "冰面滑行（含 v2+）不算撞击，不会碎冰；冰面已并入地形合并碰撞，滑行不卡脚。\n" +
                    "红热边接触只融一块。同格再点 = 删除，换组号再点 = 改组。\n" +
                    "0 也是普通组；「新建组」会给一个当前最大组号 + 1 的新组。",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    iceGroupId = EditorGUILayout.IntField("组号", iceGroupId);
                    if (GUILayout.Button("新建组", GUILayout.Width(64f)))
                        iceGroupId = level.Data.NextIceGroupId();
                }
                break;
            case Tool.Magma:
                EditorGUILayout.HelpBox(
                    "左键放一块 1×1 岩浆块，按住拖可以连续铺。每个接触面只在第一次接触时强制 A 的对应边热二；\n" +
                    "已经热二的边再接触直接死亡。v2 及以上撞上来时，撞击热 + 强制热也会死亡；v3 撞击整组破坏。\n" +
                    "在上面走动和普通地面相同。同格再点 = 删除，换组号再点 = 改组。\n" +
                    "0 也是普通组；「新建组」会给一个当前最大组号 + 1 的新组。",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    magmaGroupId = EditorGUILayout.IntField("组号", magmaGroupId);
                    if (GUILayout.Button("新建组", GUILayout.Width(64f)))
                        magmaGroupId = level.Data.NextMagmaGroupId();
                }
                break;
            case Tool.HiddenBlock:
                EditorGUILayout.HelpBox(
                    "左键放一块隐藏方块，按住拖可以连续铺。无实体、外形和墙一致，用来盖住后面的物件。\n" +
                    "任一玩家完全进入一组后，该组永久变为半透明（地图重置也不会恢复）。\n" +
                    "不能压实心格；允许压钥匙、门、绳、冰、岩浆等。同格再点 = 从本组删除；同一格可以属于多个组。\n" +
                    "0 也是普通组；「新建组」会给一个当前最大组号 + 1 的新组。",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    hiddenGroupId = EditorGUILayout.IntField("组号", hiddenGroupId);
                    if (GUILayout.Button("新建组", GUILayout.Width(64f)))
                        hiddenGroupId = level.Data.NextHiddenGroupId();
                }
                break;
            case Tool.RepairBlock:
                EditorGUILayout.HelpBox(
                    "左键放一块 1×1 修复方块，按住拖可以连续铺。A 的红热边（热二）或灵体边碰到它的表面 → " +
                    "那条边回到普通态；不限次数、不会被消耗，方块本身也能站、能挡路。\n" +
                    "装填态（膛里有人）和幽魂态（四条边全碎）的 A 不响应。\n" +
                    "碰到修复面不触发锻造：不会点热、不会砸碎、也不会因为热二再撞而死亡。\n" +
                    "同格再点 = 删除。",
                    MessageType.None);
                break;
            case Tool.RespawnPoint:
                EditorGUILayout.HelpBox(
                    "左键放置 3×3 重生点；同一格再次点击删除。重生点由 LabLevel 数据生成，" +
                    "不直接编辑场景生成物。",
                    MessageType.None);
                break;
            case Tool.TeleportPoint:
                EditorGUILayout.HelpBox(
                    "左键放置 3×3 传送点；同一格再次点击删除。运行时双人进入后标记，已标记点互为终点：\n" +
                    "Play 中两人同时进入后按冲刺键，A/D 选终点、Enter 确认。",
                    MessageType.None);
                break;

            case Tool.Marquee:
                EditorGUILayout.HelpBox(
                    "左键拖出一个矩形选区，起点和对角就是它的两个顶点。同一时间只有一个选区。\n" +
                    "下一次拉选区怎么处理它：重新选择（默认，换掉旧的）/ 增加选区（并进去，可以拼出不规则形状）/ " +
                    "删除选区（从选区里挖掉这块）。",
                    MessageType.None);
                marqueeMode = (MarqueeMode)GUILayout.SelectionGrid((int)marqueeMode, MarqueeModeLabels, 3);
                break;

            case Tool.Move:
                EditorGUILayout.HelpBox(
                    "左键点中物件后拖动 = 移动那个物件。只要场景里有选区，不管鼠标从哪里开始拖，" +
                    "都改成拖动选区框和里面的全部内容；框扫过、但原本不在选区里的物件不会被带走。\n" +
                    "删除用下面「选区 / 选择」面板里的按钮。移动和删除都能撤销。",
                    MessageType.None);
                break;

        }

        if (GUILayout.Button("跳到关卡")) FrameLevel();
        showGrid = EditorGUILayout.ToggleLeft("显示网格", showGrid);
        EditorGUILayout.LabelField("快捷键：1 铺地形 / 3 擦地形 / 4 选区 / 5 选择（禁止新增）",
            EditorStyles.miniLabel);
    }

    void DrawSelection()
    {
        EditorGUILayout.LabelField("选区 / 选择", EditorStyles.boldLabel);

        marqueeMode = (MarqueeMode)GUILayout.SelectionGrid((int)marqueeMode, MarqueeModeLabels, 3);

        if (selection.Count == 0)
        {
            EditorGUILayout.LabelField("（没有选区）");
        }
        else
        {
            RectInt bounds = SelectionBounds(selection);
            EditorGUILayout.LabelField(
                string.Format("当前选区：{0} 格，包围盒 {1}×{2} @ ({3}, {4})",
                    SelectionArea(selection), bounds.width, bounds.height, bounds.x, bounds.y));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                if (GUILayout.Button("清空选区")) selection.Clear();
                if (GUILayout.Button("删除选区内容")) DeleteSelection();
            }
        }

        EditorGUILayout.LabelField("选中", picked.kind == PickKind.None ? "（无）" : PickLabel(picked));

        EditorGUILayout.HelpBox(
            "同一时间只有一个选区，它只是编辑器的状态，不存进关卡数据。\n" +
            "拖动时选区框和里面的物件一起移动；删除作用在选区覆盖的物件上。\n" +
            "增加选区会把新拖出的矩形并进当前选区，所以选区可以是不规则形状。\n" +
            "地形格、绳、钥匙、门、地刺、冰块、岩浆块、重生点、传送点都算物件；出生点和关卡分区不算。",
            MessageType.None);
    }

    void DrawTerrain()
    {
        EditorGUILayout.LabelField("地形", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("实心格", level.SolidCellCount.ToString());
        EditorGUILayout.LabelField("合并后碰撞矩形", level.MergedRectCount.ToString());
        EditorGUILayout.LabelField("可选范围", "相机范围外扩 " + EditMargin + " 格");

        if (GUILayout.Button("清空地形"))
        {
            if (!EditorUtility.DisplayDialog("清空地形", "删掉所有实心格？绳和出生点保留。", "清空", "取消")) return;
            Record("清空地形");
            level.Data.ClearCells();
            AfterStructuralEdit();
        }
    }

    void DrawPartitions()
    {
        EditorGUILayout.LabelField("关卡分区", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.partitions == null) data.partitions = new List<LabLevelPartition>();

        EditorGUILayout.HelpBox(
            "分区的 bounds 就是相机范围：B 进入哪个分区，相机就用哪个分区。\n" +
            "移动分区时可选择把里面的地形/绳/出生点一起带走。",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("新建分区")) CreatePartitionAtView();
            using (new EditorGUI.DisabledScope(partitionIndex < 0 || partitionIndex >= data.partitions.Count))
            {
                if (GUILayout.Button("删除选中")) DeleteSelectedPartition();
            }
        }

        for (int i = 0; i < data.partitions.Count; i++)
        {
            var partition = data.partitions[i];
            if (partition == null) continue;
            string label = (i == partitionIndex ? "▶ " : "") + partition.name + "   " + partition.bounds;
            if (GUILayout.Button(label)) partitionIndex = i;
        }

        if (data.partitions.Count == 0)
            EditorGUILayout.LabelField("（没有分区：相机用单关范围/尺寸）");

        if (!string.IsNullOrEmpty(partitionMessage))
            EditorGUILayout.HelpBox(partitionMessage, MessageType.Warning);

        if (partitionIndex >= 0 && partitionIndex < data.partitions.Count)
        {
            var partition = data.partitions[partitionIndex];
            if (partition != null)
            {
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("名称", partition.name);
                int x = EditorGUILayout.IntField("X", partition.bounds.x);
                int y = EditorGUILayout.IntField("Y", partition.bounds.y);
                int width = EditorGUILayout.IntField("宽", partition.bounds.width);
                int height = EditorGUILayout.IntField("高", partition.bounds.height);
                movePartitionContents = EditorGUILayout.ToggleLeft("移动时带上里面的物件", movePartitionContents);

                if (EditorGUI.EndChangeCheck())
                {
                    var oldBounds = partition.bounds;
                    var next = new RectInt(x, y, Mathf.Max(1, width), Mathf.Max(1, height));
                    var delta = new Vector2Int(next.x - oldBounds.x, next.y - oldBounds.y);
                    bool boundsChanged = next.x != oldBounds.x || next.y != oldBounds.y ||
                                         next.width != oldBounds.width || next.height != oldBounds.height;

                    if (boundsChanged && data.OverlapsAny(next, partitionIndex))
                    {
                        partitionMessage = "新位置/大小和其它关卡分区重叠，未应用。";
                        Repaint();
                        return;
                    }

                    Record("改关卡分区");
                    if (delta != Vector2Int.zero && movePartitionContents)
                        data.TranslatePartition(partitionIndex, delta, true);

                    partition.name = name;
                    partition.bounds = next;
                    partitionMessage = null;
                    data.Normalize();
                    AfterStructuralEdit();
                }
            }
        }

        EditorGUILayout.HelpBox(
            "分区之间不能重叠。B 不在任何分区里时，相机不加范围，只用关卡的相机跟随尺寸跟两人相对位置缩放。",
            MessageType.None);
    }

    void CreatePartitionAtView()
    {
        var view = SceneView.lastActiveSceneView;
        Vector2 center = view != null
            ? new Vector2(view.pivot.x, view.pivot.y)
            : new Vector2(
                level.Data.bounds.x + level.Data.bounds.width * 0.5f,
                level.Data.bounds.y + level.Data.bounds.height * 0.5f);

        var bounds = new RectInt(
            Mathf.FloorToInt(center.x) - 8,
            Mathf.FloorToInt(center.y) - 6,
            16, 12);

        if (level.Data.OverlapsAny(bounds))
        {
            partitionMessage = "视图中心的新分区会和现有分区重叠，先移动/缩小已有分区，或用分区工具在空白处拖。";
            Repaint();
            return;
        }

        Record("新建关卡分区");
        if (level.Data.AddPartition(bounds) == null)
        {
            partitionMessage = "新建分区失败：和其它分区重叠。";
            Repaint();
            return;
        }

        partitionIndex = level.Data.partitions.Count - 1;
        partitionMessage = null;
        AfterStructuralEdit();
    }

    void DeleteSelectedPartition()
    {
        var data = level.Data;
        if (partitionIndex < 0 || partitionIndex >= data.partitions.Count) return;

        int choice = EditorUtility.DisplayDialogComplex(
            "删除关卡分区",
            "同时删除分区里的地形和绳？",
            "删除分区和内容",
            "只删分区",
            "取消");
        if (choice == 2) return;

        Record("删除关卡分区");
        data.DeletePartition(partitionIndex, choice == 0);
        partitionIndex = Mathf.Min(partitionIndex, data.partitions.Count - 1);
        partitionMessage = null;
        AfterStructuralEdit();
    }

}
#endif
