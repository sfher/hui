#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>关卡编辑器拆分文件（partial）：Scene 视图手势与分区手势。只搬不改，自原 3895 行单文件按职责切出。</summary>
public sealed partial class LabLevelEditorWindow
{
    // ------------------------------------------------------------------
    // Scene 视图
    // ------------------------------------------------------------------

    void OnSceneGUI(SceneView view)
    {
        if (level == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Event.current == null) return;

        var data = level.Data;
        if (showGrid) DrawGrid(data);
        DrawBoundsHandle(data);
        DrawPartitionHandles(data);
        DrawRopeHandles(data);
        DrawKeyDoorHandles(data);
        DrawRecallHandles(data);
        DrawSpikeHandles(data);
        DrawIceHandles(data);
        DrawMagmaHandles(data);
        DrawHiddenBlockHandles(data);
        DrawRepairBlockHandles(data);
        DrawRespawnPointHandles(data);
        DrawTeleportHandles(data);
        DrawSelectionRegion();
        DrawPickedHighlight();
        if (showSpawns) DrawSpawnHandles(data);
        if (showCameraFrames) DrawCameraFrames(data);

        Event e = Event.current;
        Vector3 point = ScenePoint(e.mousePosition);
        Vector2Int cell = CellOf(point);
        bool inArea = InEditArea(cell, data);
        bool inBounds = data.ContainsCell(cell);

        if (e.type == EventType.KeyDown && !e.control && !e.alt)
        {
            if (HandleKey(e)) return;
        }

        DrawHover(cell, inArea, inBounds, data);

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0 && !e.alt:
                // 有选区时，移动工具在场景任意位置起手都拖整个选区；其它工具仍限制在编辑范围内。
                bool movesSelection = tool == Tool.Move && selection.Count > 0;
                if (tool != Tool.Partition && !inArea && !movesSelection) return;
                BeginGesture(cell, inBounds, view);
                e.Use();
                break;

            case EventType.MouseDrag when e.button == 0 && gesture:
                ContinueGesture(cell);
                e.Use();
                break;

            case EventType.MouseUp when e.button == 0 && gesture:
                EndGesture();
                e.Use();
                break;
        }
    }

    void BeginGesture(Vector2Int cell, bool inBounds, SceneView view)
    {
        if (IsPlaceTool()) dragAdds = ShouldAddAtStart(cell);

        switch (tool)
        {
            case Tool.Paint:
            case Tool.Erase:
                Record(tool == Tool.Paint ? "铺地形" : "擦地形");
                gesture = true;
                lastCell = cell;
                ApplyCell(cell, tool == Tool.Erase);
                MarkDirty(true);
                break;

            case Tool.Rect:
                Record("矩形地形");
                gesture = true;
                rectStart = cell;
                rectEnd = cell;
                Repaint();
                break;

            case Tool.Rope:
            case Tool.HorizontalRope:
                if (!inBounds)
                {
                    Notify(view, "绳必须放在相机范围里。");
                    return;
                }
                LabRopeOrientation orientation = tool == Tool.HorizontalRope
                    ? LabRopeOrientation.Horizontal
                    : LabRopeOrientation.Vertical;
                if (orientation == LabRopeOrientation.Vertical &&
                    cell.y + ropeLength > level.Data.bounds.yMax)
                {
                    Notify(view, "绳太长，超出相机范围上沿。");
                    return;
                }
                if (orientation == LabRopeOrientation.Horizontal &&
                    cell.x + ropeLength > level.Data.bounds.xMax)
                {
                    Notify(view, "绳太长，超出相机范围右沿。");
                    return;
                }
                if (orientation == LabRopeOrientation.Horizontal &&
                    cell.y + 1 > level.Data.bounds.yMax)
                {
                    Notify(view, "横绳超出相机范围上沿。");
                    return;
                }
                Record(orientation == LabRopeOrientation.Horizontal ? "放横绳" : "放竖绳");
                Vector2 center = orientation == LabRopeOrientation.Horizontal
                    ? new Vector2(cell.x + ropeLength * 0.5f, cell.y + 1.125f)
                    : new Vector2(cell.x + 0.5f, cell.y + ropeLength * 0.5f);
                level.Data.ropes.Add(new LabRopeSpec(
                    center, ropeLength, ropeChainId, orientation));
                AfterStructuralEdit();
                break;

            case Tool.SpawnA:
            case Tool.SpawnB:
                if (!inBounds)
                {
                    Notify(view, "出生点必须在相机范围里。");
                    return;
                }
                Record(tool == Tool.SpawnA ? "放出生点 A" : "放出生点 B");
                {
                    var feet = new Vector2(cell.x + 0.5f, cell.y);
                    if (tool == Tool.SpawnA) level.Data.spawnA = feet;
                    else level.Data.spawnB = feet;
                }
                MarkDirty();
                break;

            case Tool.Partition:
                BeginPartition(cell);
                break;

            case Tool.Key:
                if (!inBounds)
                {
                    Notify(view, "钥匙必须放在相机范围里。");
                    return;
                }
                Record("放钥匙");
                level.Data.keys.Add(new LabKeySpec(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
                AfterStructuralEdit();
                break;

            case Tool.Recall:
                if (!inBounds)
                {
                    Notify(view, "召回道具必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "召回道具不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasRecall(cell))
                {
                    Record("删召回道具");
                    level.Data.RemoveRecall(cell);
                }
                else
                {
                    Record("放召回道具");
                    level.Data.PlaceRecall(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.Door:
                if (!inBounds)
                {
                    Notify(view, "门必须放在相机范围里。");
                    return;
                }
                if (cell.y + LabDoorSpec.CellHeight > level.Data.bounds.yMax)
                {
                    Notify(view, "门有 3 格高，超出相机范围上沿。");
                    return;
                }
                Record("放门");
                level.Data.doors.Add(new LabDoorSpec(
                    new Vector2(cell.x + 0.5f, cell.y + LabDoorSpec.CellHeight * 0.5f),
                    newDoorColor));
                AfterStructuralEdit();
                break;

            case Tool.Spike:
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "刺不能放在实心格里：" + cell);
                    return;
                }

                var existing = level.Data.FindSpike(cell);
                if (existing.HasValue && existing.Value.dir == newSpikeDir)
                {
                    Record("删地刺");
                    level.Data.RemoveSpike(cell);
                }
                else
                {
                    Record(existing.HasValue ? "改地刺方向" : "放地刺");
                    level.Data.PlaceSpike(cell, newSpikeDir);
                }
                AfterStructuralEdit();
                break;

            case Tool.Ice:
                if (!inBounds)
                {
                    Notify(view, "冰块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "冰块不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasMagma(cell))
                {
                    Notify(view, "冰块不能和岩浆块放在同一格：" + cell);
                    return;
                }

                var existingIce = level.Data.FindIce(cell);
                if (existingIce.HasValue && existingIce.Value.groupId == iceGroupId)
                {
                    Record("删冰块");
                    level.Data.RemoveIce(cell);
                }
                else
                {
                    Record(existingIce.HasValue ? "改冰块组号" : "放冰块");
                    level.Data.PlaceIce(cell, iceGroupId);
                }
                AfterStructuralEdit();
                break;

            case Tool.Magma:
                if (!inBounds)
                {
                    Notify(view, "岩浆块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "岩浆块不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasIce(cell))
                {
                    Notify(view, "岩浆块不能和冰块放在同一格：" + cell);
                    return;
                }

                var existingMagma = level.Data.FindMagma(cell);
                if (existingMagma.HasValue && existingMagma.Value.groupId == magmaGroupId)
                {
                    Record("删岩浆块");
                    level.Data.RemoveMagma(cell);
                }
                else
                {
                    Record(existingMagma.HasValue ? "改岩浆块组号" : "放岩浆块");
                    level.Data.PlaceMagma(cell, magmaGroupId);
                }
                AfterStructuralEdit();
                break;

            case Tool.HiddenBlock:
                if (!inBounds)
                {
                    Notify(view, "隐藏方块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "隐藏方块不能放在实心格里：" + cell);
                    return;
                }

                if (level.Data.HasHiddenBlock(cell, hiddenGroupId))
                {
                    Record("删隐藏方块");
                    level.Data.RemoveHiddenBlock(cell, hiddenGroupId);
                }
                else
                {
                    Record(level.Data.HasHiddenBlock(cell) ? "隐藏方块加入本组" : "放隐藏方块");
                    level.Data.PlaceHiddenBlock(cell, hiddenGroupId);
                }
                AfterStructuralEdit();
                break;

            case Tool.RepairBlock:
                if (!inBounds)
                {
                    Notify(view, "修复方块必须放在相机范围里。");
                    return;
                }
                if (level.Data.HasCell(cell))
                {
                    Notify(view, "修复方块不能放在实心格里：" + cell);
                    return;
                }
                if (level.Data.HasIce(cell) || level.Data.HasMagma(cell))
                {
                    Notify(view, "修复方块不能和冰/岩浆放在同一格：" + cell);
                    return;
                }
                if (level.Data.HasRepairBlock(cell))
                {
                    Record("删修复方块");
                    level.Data.RemoveRepairBlock(cell);
                }
                else
                {
                    Record("放修复方块");
                    level.Data.PlaceRepairBlock(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.RespawnPoint:
                if (!inBounds)
                {
                    Notify(view, "重生点必须在相机范围里。");
                    return;
                }
                if (level.Data.HasRespawnPoint(cell))
                {
                    Record("删重生点");
                    level.Data.RemoveRespawnPoint(cell);
                }
                else
                {
                    Record("放重生点");
                    level.Data.PlaceRespawnPoint(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.TeleportPoint:
                if (!inBounds)
                {
                    Notify(view, "传送点必须在相机范围里。");
                    return;
                }
                if (level.Data.HasTeleport(cell))
                {
                    Record("删传送点");
                    level.Data.RemoveTeleport(cell);
                }
                else
                {
                    Record("放传送点");
                    level.Data.PlaceTeleport(cell);
                }
                AfterStructuralEdit();
                break;

            case Tool.Marquee:
                gesture = true;
                marqueeDragging = true;
                marqueeStart = cell;
                marqueeEnd = cell;
                Repaint();
                break;

            case Tool.Move:
                if (selection.Count > 0)
                {
                    // 锁住拖动开始时选区里的条目：框和内容一起走，沿途扫过的物件不会被带走。
                    Record("移动选区内容");
                    movingContent = true;
                    movingSelection = true;
                    moveCapture = level.Data.CaptureContents(selection);
                    moveLastCell = cell;
                    gesture = true;
                    Repaint();
                    break;
                }

                movingSelection = false;
                moveCapture = null;
                picked = PickAt(cell);
                if (picked.kind == PickKind.None)
                {
                    Repaint();
                    break;
                }

                Record("拖动物件");
                movingContent = true;
                moveLastCell = cell;
                gesture = true;
                Repaint();
                break;
        }

        // 摆放类工具：第一次已经落笔，后续拖动按同一个"放/删"语义连续执行。
        if (IsPlaceTool())
        {
            gesture = true;
            lastCell = cell;
        }
    }

    void ContinueGesture(Vector2Int cell)
    {
        if (tool == Tool.Partition)
        {
            ContinuePartition(cell);
            return;
        }

        if (tool == Tool.Marquee)
        {
            if (marqueeEnd == cell) return;
            marqueeEnd = cell;
            Repaint();
            return;
        }

        if (tool == Tool.Move)
        {
            if (!movingContent) return;
            var delta = cell - moveLastCell;
            if (delta == Vector2Int.zero) return;

            var areas = CurrentMoveAreas();
            if (areas == null) return;

            moveLastCell = cell;
            if (movingSelection)
            {
                level.Data.MoveCapturedContents(moveCapture, delta);
                TranslateRects(selection, delta);
            }
            else
            {
                level.Data.MoveContents(areas, delta);
            }

            // 拖动中不要重建预览：RebuildPreview 会先 ClearGenerated 再 Build，
            // 每帧跑一遍等于每帧把整个场景拆了重搭，大关卡上就是"碎裂"的观感。
            // 松手时 EndGesture 里重建一次即可。
            MarkDirty(false);
            return;
        }

        if (IsPlaceTool())
        {
            if (cell == lastCell) return;
            // 沿直线补齐，快速拖动也不会漏格；起点那格已经落过笔，跳过。
            foreach (var lineCell in LineCells(lastCell, cell))
            {
                if (lineCell == lastCell) continue;
                ApplyToolAtCell(lineCell, dragAdds);
            }
            lastCell = cell;
            MarkDirty(true);
            return;
        }

        switch (tool)
        {
            case Tool.Paint:
            case Tool.Erase:
                if (cell == lastCell) return;
                PaintLine(lastCell, cell, tool == Tool.Erase);
                lastCell = cell;
                MarkDirty(true);
                break;

            case Tool.Rect:
                rectEnd = cell;
                Repaint();
                break;
        }
    }

    void EndGesture()
    {
        if (tool == Tool.Partition)
        {
            EndPartition();
            return;
        }

        if (tool == Tool.Marquee)
        {
            CommitMarquee();
            marqueeDragging = false;
            gesture = false;
            Repaint();
            return;
        }

        if (tool == Tool.Move)
        {
            movingContent = false;
            movingSelection = false;
            moveCapture = null;
            gesture = false;
            level.Data.Normalize();
            level.RebuildPreview();
            EditorUtility.SetDirty(level);
            MarkSceneDirty();
            validated = false;
            Repaint();
            return;
        }

        if (tool == Tool.Rect)
        {
            var data = level.Data;
            int x0 = Mathf.Min(rectStart.x, rectEnd.x);
            int y0 = Mathf.Min(rectStart.y, rectEnd.y);
            int x1 = Mathf.Max(rectStart.x, rectEnd.x);
            int y1 = Mathf.Max(rectStart.y, rectEnd.y);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!InEditArea(cell, data)) continue;

                    if (fillTarget == FillTarget.HiddenBlock)
                        data.PlaceHiddenBlock(cell, fillGroupId);
                    else
                        data.AddCell(cell);
                }
        }

        gesture = false;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    void BeginPartition(Vector2Int cell)
    {
        var data = level.Data;
        int index = data.FindPartition(new Vector2(cell.x + 0.5f, cell.y + 0.5f));

        if (index >= 0)
        {
            partitionIndex = index;
            partitionCreating = false;
            partitionDragIndex = index;
            partitionDragLast = cell;
            Record("移动关卡分区");
        }
        else
        {
            partitionIndex = -1;
            partitionCreating = true;
            partitionDragIndex = -1;
            partitionCreateStart = cell;
            partitionCreateEnd = cell;
        }

        gesture = true;
        Repaint();
    }

    void ContinuePartition(Vector2Int cell)
    {
        if (partitionCreating)
        {
            if (partitionCreateEnd == cell) return;
            partitionCreateEnd = cell;
            Repaint();
            return;
        }

        if (partitionDragIndex < 0) return;

        var delta = cell - partitionDragLast;
        if (delta == Vector2Int.zero) return;

        if (!level.Data.MovePartition(partitionDragIndex, delta, movePartitionContents))
        {
            partitionMessage = "移动被拒绝：分区不能和其它分区重叠。";
            Repaint();
            return;
        }

        partitionMessage = null;
        partitionDragLast = cell;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

    RectInt PreviewPartitionBounds()
    {
        int x0 = Mathf.Min(partitionCreateStart.x, partitionCreateEnd.x);
        int y0 = Mathf.Min(partitionCreateStart.y, partitionCreateEnd.y);
        int width = Mathf.Abs(partitionCreateEnd.x - partitionCreateStart.x) + 1;
        int height = Mathf.Abs(partitionCreateEnd.y - partitionCreateStart.y) + 1;

        if (width <= 1 && height <= 1)
        {
            width = 16;
            height = 12;
        }

        return new RectInt(x0, y0, width, height);
    }

    void EndPartition()
    {
        if (partitionCreating)
        {
            var bounds = PreviewPartitionBounds();
            if (level.Data.OverlapsAny(bounds))
            {
                partitionMessage = "新分区会和已有分区重叠，未创建。";
            }
            else
            {
                Record("新建关卡分区");
                if (level.Data.AddPartition(bounds) != null)
                {
                    partitionIndex = level.Data.partitions.Count - 1;
                    partitionMessage = null;
                }
                else
                {
                    partitionMessage = "新建分区失败：和其它分区重叠。";
                }
            }

            partitionCreating = false;
        }

        partitionDragIndex = -1;
        gesture = false;
        level.Data.Normalize();
        level.RebuildPreview();
        EditorUtility.SetDirty(level);
        MarkSceneDirty();
        validated = false;
        Repaint();
    }

}
#endif
