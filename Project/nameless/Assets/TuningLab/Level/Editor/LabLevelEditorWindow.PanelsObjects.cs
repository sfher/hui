#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：窗口面板（绳/钥匙门/刺/冰/岩浆/隐藏/修复/出生/重生/传送/相机/校验区）。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{
    void DrawRopes()
    {
        EditorGUILayout.LabelField("绳", EditorStyles.boldLabel);
        ropeLength = Mathf.Clamp(EditorGUILayout.IntField("新绳长度（格）", ropeLength), 1, MaxRopeLength);
        ropeChainId = EditorGUILayout.IntField("chainId", ropeChainId);

        var ropes = level.Data.ropes;
        if (ropes == null || ropes.Count == 0)
        {
            EditorGUILayout.LabelField("（没有绳）");
        }
        else
        {
            ropeScroll = EditorGUILayout.BeginScrollView(ropeScroll, GUILayout.MaxHeight(110f));
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("{0} ({1:0.##}, {2:0.##})  长 {3}  链 {4}",
                            rope.orientation == LabRopeOrientation.Horizontal ? "横" : "竖",
                            rope.center.x, rope.center.y, rope.cells, rope.chainId),
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删绳");
                        ropes.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        if (GUILayout.Button("清空绳"))
        {
            if (!EditorUtility.DisplayDialog("清空绳", "删掉所有绳？", "清空", "取消")) return;
            Record("清空绳");
            level.Data.ClearRopes();
            AfterStructuralEdit();
        }
    }

    void DrawKeyDoors()
    {
        EditorGUILayout.LabelField("钥匙 / 门", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUILayout.LabelField("钥匙", level.KeyCount + "（1×1，拾取后消失）");
        EditorGUILayout.LabelField("门", level.DoorCount + "（1×3，任意一把钥匙开门）");
        newDoorColor = EditorGUILayout.ColorField("新门颜色", newDoorColor);

        keyDoorScroll = EditorGUILayout.BeginScrollView(keyDoorScroll, GUILayout.MaxHeight(120f));
        if (data.keys != null)
        {
            for (int i = 0; i < data.keys.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("钥匙 ({0:0.##}, {1:0.##})",
                            data.keys[i].position.x, data.keys[i].position.y),
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删钥匙");
                        data.keys.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }

        if (data.doors != null)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var door = data.doors[i];
                    EditorGUILayout.LabelField(
                        string.Format("门 ({0:0.##}, {1:0.##})", door.position.x, door.position.y),
                        EditorStyles.miniLabel);
                    EditorGUI.BeginChangeCheck();
                    Color color = EditorGUILayout.ColorField(GUIContent.none, door.color, false, false, false,
                        GUILayout.Width(40f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改门颜色");
                        door.color = color;
                        data.doors[i] = door;
                        MarkDirty(true);
                    }
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删门");
                        data.doors.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(level.KeyCount == 0))
            {
                if (GUILayout.Button("清空钥匙"))
                {
                    if (!EditorUtility.DisplayDialog("清空钥匙", "删掉所有钥匙？", "清空", "取消")) return;
                    Record("清空钥匙");
                    level.Data.ClearKeys();
                    AfterStructuralEdit();
                }
            }
            using (new EditorGUI.DisabledScope(level.DoorCount == 0))
            {
                if (GUILayout.Button("清空门"))
                {
                    if (!EditorUtility.DisplayDialog("清空门", "删掉所有门？", "清空", "取消")) return;
                    Record("清空门");
                    level.Data.ClearDoors();
                    AfterStructuralEdit();
                }
            }
        }
    }

    void DrawSpikes()
    {
        EditorGUILayout.LabelField("地刺", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("刺", level.SpikeCount + "（16×6 像素，贴格边，碰到即死）");

        spikeScroll = EditorGUILayout.BeginScrollView(spikeScroll, GUILayout.MaxHeight(120f));
        if (data.spikes != null)
        {
            for (int i = 0; i < data.spikes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var spike = data.spikes[i];
                    bool floating = !data.HasCell(spike.BaseCell);
                    EditorGUILayout.LabelField(
                        string.Format("刺 ({0}, {1}){2}",
                            spike.cell.x, spike.cell.y, floating ? "  悬空" : ""),
                        EditorStyles.miniLabel, GUILayout.Width(120f));

                    int dirIndex = Mathf.Clamp(System.Array.IndexOf(SpikeDirs, spike.dir), 0, SpikeDirs.Length - 1);
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.Popup(dirIndex, SpikeDirLabels, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改地刺方向");
                        spike.dir = SpikeDirs[next];
                        data.spikes[i] = spike;
                        MarkDirty(true);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删地刺");
                        data.spikes.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.SpikeCount == 0))
        {
            if (GUILayout.Button("清空地刺"))
            {
                if (!EditorUtility.DisplayDialog("清空地刺", "删掉所有地刺？", "清空", "取消")) return;
                Record("清空地刺");
                level.Data.ClearSpikes();
                AfterStructuralEdit();
            }
        }
    }

    void DrawIces()
    {
        EditorGUILayout.LabelField("冰块", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("冰块", level.IceCount + "（1×1，同组共享 v2 整组破坏）");

        iceScroll = EditorGUILayout.BeginScrollView(iceScroll, GUILayout.MaxHeight(120f));
        if (data.ices != null)
        {
            for (int i = 0; i < data.ices.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var ice = data.ices[i];
                    EditorGUILayout.LabelField(
                        string.Format("冰 ({0}, {1})", ice.cell.x, ice.cell.y),
                        EditorStyles.miniLabel, GUILayout.Width(110f));

                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntField(ice.groupId, GUILayout.Width(48f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("改冰块组号");
                        ice.groupId = next;
                        data.ices[i] = ice;
                        MarkDirty(true);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删冰块");
                        data.ices.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.IceCount == 0))
        {
            if (GUILayout.Button("清空冰块"))
            {
                if (!EditorUtility.DisplayDialog("清空冰块", "删掉所有冰块？", "清空", "取消")) return;
                Record("清空冰块");
                level.Data.ClearIces();
                AfterStructuralEdit();
            }
        }
    }

    void DrawRecalls()
    {
        EditorGUILayout.LabelField("召回道具", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("道具", level.RecallCount + "（1×1，拾取后全队永久解锁召回）");

        recallScroll = EditorGUILayout.BeginScrollView(recallScroll, GUILayout.MaxHeight(100f));
        if (data.recalls != null)
        {
            for (int i = 0; i < data.recalls.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var recall = data.recalls[i];
                    EditorGUILayout.LabelField(
                        string.Format("召回 ({0}, {1})", recall.cell.x, recall.cell.y),
                        EditorStyles.miniLabel, GUILayout.Width(120f));

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("删召回道具");
                        data.recalls.RemoveAt(i);
                        AfterStructuralEdit();
                        break;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.RecallCount == 0))
        {
            if (GUILayout.Button("清空召回道具"))
            {
                if (!EditorUtility.DisplayDialog("清空召回道具", "删掉所有召回道具？", "清空", "取消")) return;
                Record("清空召回道具");
                level.Data.ClearRecalls();
                AfterStructuralEdit();
            }
        }
    }

    void DrawMagmas()
    {
        EditorGUILayout.LabelField("岩浆块", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.magmas == null) data.magmas = new List<LabMagmaSpec>();
        EditorGUILayout.LabelField("岩浆块", level.MagmaCount + "（1×1，首次接触强制红热 / 热二接触死亡 / v2+ 撞击死亡 / v3 整组破坏）");

        magmaScroll = EditorGUILayout.BeginScrollView(magmaScroll, GUILayout.MaxHeight(120f));
        for (int i = 0; i < data.magmas.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var magma = data.magmas[i];
                EditorGUILayout.LabelField(
                    string.Format("岩 ({0}, {1})", magma.cell.x, magma.cell.y),
                    EditorStyles.miniLabel, GUILayout.Width(110f));

                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(magma.groupId, GUILayout.Width(48f));
                if (EditorGUI.EndChangeCheck())
                {
                    Record("改岩浆块组号");
                    magma.groupId = next;
                    data.magmas[i] = magma;
                    MarkDirty(true);
                }

                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删岩浆块");
                    data.magmas.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.MagmaCount == 0))
        {
            if (GUILayout.Button("清空岩浆块"))
            {
                if (!EditorUtility.DisplayDialog("清空岩浆块", "删掉所有岩浆块？", "清空", "取消")) return;
                Record("清空岩浆块");
                level.Data.ClearMagmas();
                AfterStructuralEdit();
            }
        }
    }

    void DrawHiddenBlocks()
    {
        EditorGUILayout.LabelField("隐藏方块", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.hiddenBlocks == null) data.hiddenBlocks = new List<LabHiddenBlockSpec>();
        EditorGUILayout.LabelField("隐藏方块", level.HiddenBlockCount + "（无实体，外形与墙一致；玩家完全进入整组永久半透明）");

        hiddenScroll = EditorGUILayout.BeginScrollView(hiddenScroll, GUILayout.MaxHeight(120f));
        for (int i = 0; i < data.hiddenBlocks.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var block = data.hiddenBlocks[i];
                EditorGUILayout.LabelField(
                    string.Format("隐 ({0}, {1})", block.cell.x, block.cell.y),
                    EditorStyles.miniLabel, GUILayout.Width(110f));

                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(block.groupId, GUILayout.Width(48f));
                if (EditorGUI.EndChangeCheck())
                {
                    Record("改隐藏方块组号");
                    block.groupId = next;
                    data.hiddenBlocks[i] = block;
                    MarkDirty(true);
                }

                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删隐藏方块");
                    data.hiddenBlocks.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.HiddenBlockCount == 0))
        {
            if (GUILayout.Button("清空隐藏方块"))
            {
                if (!EditorUtility.DisplayDialog("清空隐藏方块", "删掉所有隐藏方块？", "清空", "取消")) return;
                Record("清空隐藏方块");
                level.Data.ClearHiddenBlocks();
                AfterStructuralEdit();
            }
        }
    }

    void DrawRepairBlocks()
    {
        EditorGUILayout.LabelField("修复方块", EditorStyles.boldLabel);
        var data = level.Data;
        if (data.repairBlocks == null) data.repairBlocks = new List<LabRepairBlockSpec>();
        EditorGUILayout.LabelField("修复方块", level.RepairBlockCount + "（1×1，红热边/灵体边接触 → 那条边回普通，不消耗）");

        repairScroll = EditorGUILayout.BeginScrollView(repairScroll, GUILayout.MaxHeight(120f));
        for (int i = 0; i < data.repairBlocks.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var block = data.repairBlocks[i];
                EditorGUILayout.LabelField(
                    string.Format("修 ({0}, {1})", block.cell.x, block.cell.y),
                    EditorStyles.miniLabel, GUILayout.Width(110f));

                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删修复方块");
                    data.repairBlocks.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.RepairBlockCount == 0))
        {
            if (GUILayout.Button("清空修复方块"))
            {
                if (!EditorUtility.DisplayDialog("清空修复方块", "删掉所有修复方块？", "清空", "取消")) return;
                Record("清空修复方块");
                level.Data.ClearRepairBlocks();
                AfterStructuralEdit();
            }
        }
    }

    void DrawSpawns()
    {
        EditorGUILayout.LabelField("出生点（脚底）", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUI.BeginChangeCheck();
        Vector2 a = EditorGUILayout.Vector2Field("A", data.spawnA);
        Vector2 b = EditorGUILayout.Vector2Field("B", data.spawnB);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改出生点");
            data.spawnA = a;
            data.spawnB = b;
            MarkDirty();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("A 回基准（8, 0）"))
            {
                Record("A 回基准出生点");
                data.spawnA = TuningLab.SpawnAFeet;
                MarkDirty();
            }
            if (GUILayout.Button("B 回基准（10, 0）"))
            {
                Record("B 回基准出生点");
                data.spawnB = TuningLab.SpawnBFeet;
                MarkDirty();
            }
        }
    }

    void DrawRespawnPoints()
    {
        EditorGUILayout.LabelField("重生点", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("重生点", level.RespawnPointCount + "（3×3，任一玩家接触触发）");
        if (data.respawnPoints == null) data.respawnPoints = new List<LabRespawnPointSpec>();

        respawnScroll = EditorGUILayout.BeginScrollView(respawnScroll, GUILayout.MaxHeight(100f));
        for (int i = 0; i < data.respawnPoints.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var point = data.respawnPoints[i];
                EditorGUILayout.LabelField(
                    string.Format("重生点 ({0:0.##}, {1:0.##})", point.position.x, point.position.y),
                    EditorStyles.miniLabel);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删重生点");
                    data.respawnPoints.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.RespawnPointCount == 0))
        {
            if (GUILayout.Button("清空重生点"))
            {
                if (!EditorUtility.DisplayDialog("清空重生点", "删掉所有重生点？", "清空", "取消")) return;
                Record("清空重生点");
                data.respawnPoints.Clear();
                AfterStructuralEdit();
            }
        }
    }

    void DrawTeleports()
    {
        EditorGUILayout.LabelField("传送点", EditorStyles.boldLabel);
        var data = level.Data;
        EditorGUILayout.LabelField("传送点", level.TeleportCount + "（3×3，运行时标记后互为终点）");
        if (data.teleports == null) data.teleports = new List<LabTeleportSpec>();

        if (data.teleports.Count == 1)
        {
            EditorGUILayout.HelpBox(
                "只有一个传送点：运行时最多只能标记一个，进入后没有可选终点。至少放置两个并让双人进入标记。",
                MessageType.Warning);
        }

        teleportScroll = EditorGUILayout.BeginScrollView(teleportScroll, GUILayout.MaxHeight(100f));
        for (int i = 0; i < data.teleports.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var point = data.teleports[i];
                EditorGUILayout.LabelField(
                    string.Format("传送点 ({0:0.##}, {1:0.##})", point.position.x, point.position.y),
                    EditorStyles.miniLabel);
                if (GUILayout.Button("×", GUILayout.Width(24f)))
                {
                    Record("删传送点");
                    data.teleports.RemoveAt(i);
                    AfterStructuralEdit();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(level.TeleportCount == 0))
        {
            if (GUILayout.Button("清空传送点"))
            {
                if (!EditorUtility.DisplayDialog("清空传送点", "删掉所有传送点？", "清空", "取消")) return;
                Record("清空传送点");
                data.teleports.Clear();
                AfterStructuralEdit();
            }
        }
    }

    void DrawBounds()
    {
        EditorGUILayout.LabelField("编辑范围 / 单关相机范围（clamp）", EditorStyles.boldLabel);
        var data = level.Data;

        EditorGUI.BeginChangeCheck();
        int x = EditorGUILayout.IntField("X", data.bounds.x);
        int y = EditorGUILayout.IntField("Y", data.bounds.y);
        int width = EditorGUILayout.IntField("宽", data.bounds.width);
        int height = EditorGUILayout.IntField("高", data.bounds.height);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改相机范围");
            data.bounds = new RectInt(x, y, Mathf.Max(1, width), Mathf.Max(1, height));
            MarkDirty();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("回 Tuning Lab 基准范围"))
            {
                Record("回基准相机范围");
                Rect defaults = TuningLab.BaselineBounds;
                if (lab != null)
                {
                    Rect custom = lab.defaultCameraBounds;
                    if (custom.width > 0f && custom.height > 0f) defaults = custom;
                }
                data.bounds = ToRectInt(defaults);
                MarkDirty();
            }
            if (GUILayout.Button("按地形计算"))
            {
                Record("按地形算相机范围");
                data.bounds = data.ContentBounds();
                MarkDirty();
            }
        }

        EditorGUILayout.HelpBox(
            "Tuning Lab 基准的两面墙在相机范围外 1 格，这是基准行为；范围管编辑/绘制，不限制画地形。\n" +
            "有关卡分区时，分区的 bounds 才是相机范围；这里的范围只作为编辑范围和没有分区时的单关相机范围。",
            MessageType.None);
    }

    void DrawCamera()
    {
        EditorGUILayout.LabelField("相机跟随尺寸（全关卡）", EditorStyles.boldLabel);
        var data = level.Data;

        int partitionCount = data.partitions != null ? data.partitions.Count : 0;
        if (partitionCount > 0)
        {
            EditorGUILayout.HelpBox(
                string.Format(
                    "已定义 {0} 个关卡分区：相机 clamp 用 B 所在分区的 bounds；下面的跟随尺寸对所有分区生效。\n" +
                    "B 不在任何分区时不夹相机，仍用关卡的相机跟随尺寸跟两人相对位置缩放。",
                    partitionCount),
                MessageType.Info);
            if (GUILayout.Button("选中 TuningLab 编辑相机默认"))
                Selection.activeGameObject = lab != null ? lab.gameObject : null;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "没有分区：相机用下面的单关范围/尺寸。\n" +
                "连续关卡请在「关卡分区」里新建分区。",
                MessageType.None);
        }

        float defaultMin = lab != null ? lab.defaultCameraMinSize : TuningLab.DefaultCameraMinSize;
        float defaultMax = lab != null ? lab.defaultCameraMaxSize : TuningLab.DefaultCameraMaxSize;
        if (defaultMin <= 0f) defaultMin = TuningLab.DefaultCameraMinSize;
        if (defaultMax < defaultMin) defaultMax = defaultMin;

        float min = data.cameraMinSize;
        float max = data.cameraMaxSize;

        EditorGUI.BeginChangeCheck();
        min = EditorGUILayout.FloatField("最小 size（格）", min);
        max = EditorGUILayout.FloatField("最大 size（格）", max);
        EditorGUILayout.MinMaxSlider("跟随范围", ref min, ref max, 0.5f, 128f);
        if (EditorGUI.EndChangeCheck())
        {
            Record("改相机尺寸");
            data.cameraMinSize = Mathf.Clamp(min, 0.5f, 128f);
            data.cameraMaxSize = Mathf.Clamp(max, data.cameraMinSize, 128f);
            MarkDirty();
        }

        const float aspect = 16f / 9f;
        EditorGUILayout.LabelField("最小视野",
            string.Format("{0:0.##} 格高 × {1:0.#} 格宽（16:9）",
                data.cameraMinSize * 2f, data.cameraMinSize * 2f * aspect));
        EditorGUILayout.LabelField("最大视野",
            string.Format("{0:0.##} 格高 × {1:0.#} 格宽（16:9）",
                data.cameraMaxSize * 2f, data.cameraMaxSize * 2f * aspect));
        EditorGUILayout.LabelField("1080p 像素缩放",
            string.Format("{0:0.##}× – {1:0.##}×",
                1080f / (data.cameraMinSize * 2f * TuningLab.CellPixels),
                1080f / (data.cameraMaxSize * 2f * TuningLab.CellPixels)));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tuning Lab 默认"))
            {
                Record("相机尺寸回基准");
                data.cameraMinSize = defaultMin;
                data.cameraMaxSize = defaultMax;
                MarkDirty();
            }
            if (GUILayout.Button("固定为最小"))
            {
                Record("相机固定为最小尺寸");
                data.cameraMaxSize = data.cameraMinSize;
                MarkDirty();
            }
        }

        showCameraFrames = EditorGUILayout.ToggleLeft("在 Scene 视图画出最小/最大视野（以出生点中心示意）", showCameraFrames);

        if (lab != null && !lab.freeCamera)
        {
            EditorGUILayout.HelpBox(
                "TuningLab.freeCamera = false 时用的是固定相机，这里的尺寸范围不生效。",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "size 是正交相机的半高（格），视野高度 = 2 × size；这是跟随范围，不是相机夹具。\n" +
            "只有整数像素缩放才逐像素对齐；非整数倍只适合概览或调试。",
            MessageType.None);
    }

    void DrawActions()
    {
        EditorGUILayout.LabelField("关卡", EditorStyles.boldLabel);

        if (GUILayout.Button("从 Tuning Lab 重置（地形 + 绳 + 出生点 + 相机 + 分区）", GUILayout.Height(28f)))
        {
            if (!EditorUtility.DisplayDialog(
                    "重置关卡",
                    "用 Tuning Lab 基准房间覆盖当前地形、绳、出生点、相机范围/尺寸和关卡分区？",
                    "重置", "取消")) return;

            Record("从 Tuning Lab 重置关卡");
            level.ApplyBaseline();
            if (lab != null) LabLevelScene.CopyCameraDefaults(lab, level.Data);
            LabLevelScene.EnsureDefaultPartition(level.Data);
            AfterStructuralEdit();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("校验摆放")) Validate();
            if (GUILayout.Button("保存场景")) SaveScene();
            if (GUILayout.Button("进入 Play")) EditorApplication.isPlaying = true;
        }

        DrawLevelAssetSection();
    }

    /// <summary>
    /// 关卡数据资源与合并（Issue #109）。
    /// 数据搬到独立 .asset 之后，各分区可以在不同场景里分开搭，最后合并成一份。
    /// </summary>
    void DrawLevelAssetSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("关卡数据资源", EditorStyles.boldLabel);

        var current = level.Asset;
        var picked = (LabLevelAsset)EditorGUILayout.ObjectField(
            "数据资源", current, typeof(LabLevelAsset), false);

        if (picked != current)
        {
            if (picked == null)
            {
                // 解绑前提醒：解绑后编辑器读的是内嵌那份旧数据。
                if (EditorUtility.DisplayDialog(
                        "解除绑定",
                        "解绑后编辑器会改回读场景内嵌的那份数据，资源里的内容不会自动搬回来。继续吗？",
                        "解绑", "取消"))
                    level.BindAsset(null);
            }
            else
            {
                level.BindAsset(picked);
            }
            Repaint();
        }

        if (level.Asset == null)
        {
            EditorGUILayout.HelpBox(
                "当前用场景内嵌数据。绑一个资源（或在 Project 里新建 GameJam/Lab Level Asset）之后，"
                + "这块区域就可以独立存盘、参与合并。",
                MessageType.Info);

            if (GUILayout.Button("新建资源并绑定"))
                CreateAndBindAsset();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("保存资源")) SaveAsset();
            if (GUILayout.Button("合并其它资源…")) MergeAssets();
        }
    }

    void CreateAndBindAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "新建关卡数据资源", level.gameObject.scene.name + "_Level", "asset", "选择保存位置");
        if (string.IsNullOrEmpty(path)) return;

        var asset = ScriptableObject.CreateInstance<LabLevelAsset>();
        // 把当前内嵌数据整体搬过去。用 JsonUtility 深拷贝：LabLevelData 不是 UnityEngine.Object，
        // 没有 CopySerialized，而直接赋值会让两边共用同一个对象。
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level.Data), asset.Data);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        level.BindAsset(asset);
        Repaint();
    }

    void SaveAsset()
    {
        if (level.Asset == null) return;
        EditorUtility.SetDirty(level.Asset);
        AssetDatabase.SaveAssets();
    }

    void MergeAssets()
    {
        string path = EditorUtility.OpenFilePanelWithFilters(
            "选择要合并进来的关卡资源", Application.dataPath, new[] { "关卡资源", "asset" });
        if (string.IsNullOrEmpty(path)) return;

        // OpenFilePanel 给的是绝对路径，转成工程内路径。
        if (!path.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
        {
            EditorUtility.DisplayDialog("无法合并", "只能合并工程 Assets 目录下的资源。", "好");
            return;
        }

        string assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
        var other = AssetDatabase.LoadAssetAtPath<LabLevelAsset>(assetPath);
        if (other == null)
        {
            EditorUtility.DisplayDialog("无法合并", "这个文件不是关卡数据资源。", "好");
            return;
        }

        if (other == level.Asset)
        {
            EditorUtility.DisplayDialog("无法合并", "不能和自己合并。", "好");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "合并关卡数据",
                "把「" + other.name + "」并进当前资源？\n\n"
                + "地形取并集，物件按格去重，相机范围取并集。\n"
                + "出生点和相机尺寸保持当前值不变。",
                "合并", "取消"))
            return;

        Record("合并关卡数据");
        int added = level.MergeFrom(other.Data);
        level.RebuildPreview();
        EditorUtility.SetDirty(level.Asset);
        AssetDatabase.SaveAssets();
        validated = false;

        EditorUtility.DisplayDialog("合并完成", "新增 " + added + " 个条目。", "好");
        Repaint();
    }

    void DrawValidation()
    {
        EditorGUILayout.Space();
        if (!validated)
        {
            EditorGUILayout.LabelField("校验：还没有跑。", EditorStyles.miniLabel);
            return;
        }

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
            return;
        }

        foreach (var issue in issues)
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
    }

}
#endif
