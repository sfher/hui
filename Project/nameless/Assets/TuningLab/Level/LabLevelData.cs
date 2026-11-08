using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一套关卡数据。坐标和 TuningLab 完全同一套：1 格 = 1 世界单位，
/// 格 (x, y) 占 [x, x+1) × [y, y+1)。不引入第二套网格或第二套尺度。
///
/// 数据只描述"关卡里有什么"，不负责搭场景；搭场景由 <see cref="LabLevel"/> 和
/// TuningLab 完成，两边读的是同一份数据。
/// </summary>
[System.Serializable]
public sealed class LabLevelData
{
    /// <summary>相机 clamp 范围。注意 TuningLab 基准房间的两面墙就在这个范围外 1 格，这是基准行为。</summary>
    public RectInt bounds = new RectInt(-13, -1, 26, 17);
    /// <summary>A 的落点（脚底），不是中心。</summary>
    public Vector2 spawnA = new Vector2(8f, 0f);
    /// <summary>B 的落点（脚底），不是中心。</summary>
    public Vector2 spawnB = new Vector2(10f, 0f);
    /// <summary>自由相机跟随的最小正交尺寸（格，半高）。</summary>
    public float cameraMinSize = TuningLab.DefaultCameraMinSize;
    /// <summary>自由相机跟随的最大正交尺寸（格，半高）。</summary>
    public float cameraMaxSize = TuningLab.DefaultCameraMaxSize;
    public List<Vector2Int> solids = new List<Vector2Int>();
    public List<LabRopeSpec> ropes = new List<LabRopeSpec>();
    /// <summary>钥匙（1×1，拾取后消失）。</summary>
    public List<LabKeySpec> keys = new List<LabKeySpec>();
    /// <summary>召回道具（1×1，拾取后全队永久解锁召回；地图重置不刷新）。</summary>
    public List<LabRecallSpec> recalls = new List<LabRecallSpec>();
    /// <summary>门（1×3，任意一把钥匙开任意一扇门）。</summary>
    public List<LabDoorSpec> doors = new List<LabDoorSpec>();
    /// <summary>地刺（16×6 像素，贴格边，一格一根，碰到即死）。</summary>
    public List<LabSpikeSpec> spikes = new List<LabSpikeSpec>();
    /// <summary>冰块（1×1，可编组；冰面滑行不碎，红热边接触融一块，非承重 v2 碰撞碎整组）。</summary>
    public List<LabIceSpec> ices = new List<LabIceSpec>();
    /// <summary>岩浆块（1×1，可编组；每个接触面首次接触强制红热，热二边再接触死亡，v2+ 撞击死亡，v3 碰撞碎整组）。</summary>
    public List<LabMagmaSpec> magmas = new List<LabMagmaSpec>();
    /// <summary>隐藏方块（1×1，可编组；无实体，外形与墙一致；任一玩家完全进入后整组永久揭示为半透明）。</summary>
    public List<LabHiddenBlockSpec> hiddenBlocks = new List<LabHiddenBlockSpec>();
    /// <summary>修复方块（1×1，与 A 同色；A 的红热边/灵体边接触其表面 → 那条边回普通，不限次数）。</summary>
    public List<LabRepairBlockSpec> repairBlocks = new List<LabRepairBlockSpec>();
    /// <summary>重生点（3×3 触发区，中心坐标）。</summary>
    public List<LabRespawnPointSpec> respawnPoints = new List<LabRespawnPointSpec>();
    /// <summary>传送点（3×3 触发区，中心坐标）。双人进入后运行时标记，已标记点可互为终点。</summary>
    public List<LabTeleportSpec> teleports = new List<LabTeleportSpec>();
    /// <summary>连续关卡分区。每个分区的 bounds 同时是它的相机范围；B 进入哪个分区就用哪个分区。</summary>
    public List<LabLevelPartition> partitions = new List<LabLevelPartition>();

    public bool HasCell(Vector2Int cell) => solids != null && solids.Contains(cell);

    public bool AddCell(Vector2Int cell)
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (solids.Contains(cell)) return false;
        solids.Add(cell);
        return true;
    }

    public bool RemoveCell(Vector2Int cell) => solids != null && solids.Remove(cell);

    public void ClearCells()
    {
        if (solids == null) solids = new List<Vector2Int>();
        else solids.Clear();
    }

    public void ClearRopes()
    {
        if (ropes == null) ropes = new List<LabRopeSpec>();
        else ropes.Clear();
    }

    public void ClearKeys()
    {
        if (keys == null) keys = new List<LabKeySpec>();
        else keys.Clear();
    }

    public void ClearRecalls()
    {
        if (recalls == null) recalls = new List<LabRecallSpec>();
        else recalls.Clear();
    }

    public void ClearDoors()
    {
        if (doors == null) doors = new List<LabDoorSpec>();
        else doors.Clear();
    }

    public void ClearSpikes()
    {
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        else spikes.Clear();
    }

    public void ClearIces()
    {
        if (ices == null) ices = new List<LabIceSpec>();
        else ices.Clear();
    }

    public void ClearMagmas()
    {
        if (magmas == null) magmas = new List<LabMagmaSpec>();
        else magmas.Clear();
    }

    public void ClearHiddenBlocks()
    {
        if (hiddenBlocks == null) hiddenBlocks = new List<LabHiddenBlockSpec>();
        else hiddenBlocks.Clear();
    }

    public void ClearRepairBlocks()
    {
        if (repairBlocks == null) repairBlocks = new List<LabRepairBlockSpec>();
        else repairBlocks.Clear();
    }

    public void ClearTeleports()
    {
        if (teleports == null) teleports = new List<LabTeleportSpec>();
        else teleports.Clear();
    }

    /// <summary>这一格的刺；没有返回 null。</summary>
    public LabSpikeSpec? FindSpike(Vector2Int cell)
    {
        if (spikes == null) return null;
        for (int i = 0; i < spikes.Count; i++)
            if (spikes[i].cell == cell) return spikes[i];
        return null;
    }

    public bool HasSpike(Vector2Int cell) => FindSpike(cell).HasValue;

    /// <summary>放一根刺，或把这一格已有刺改成新方向。方向也相同则什么都不做。</summary>
    public bool PlaceSpike(Vector2Int cell, LabSpikeDir dir)
    {
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        for (int i = 0; i < spikes.Count; i++)
        {
            var spike = spikes[i];
            if (spike.cell != cell) continue;
            if (spike.dir == dir) return false;
            spike.dir = dir;
            spikes[i] = spike;
            return true;
        }

        spikes.Add(new LabSpikeSpec(cell, dir));
        return true;
    }

    public bool RemoveSpike(Vector2Int cell)
    {
        if (spikes == null) return false;
        return spikes.RemoveAll(spike => spike.cell == cell) > 0;
    }

    /// <summary>这一格的召回道具；没有返回 null。</summary>
    public LabRecallSpec? FindRecall(Vector2Int cell)
    {
        if (recalls == null) return null;
        for (int i = 0; i < recalls.Count; i++)
            if (recalls[i].cell == cell) return recalls[i];
        return null;
    }

    public bool HasRecall(Vector2Int cell) => FindRecall(cell).HasValue;

    /// <summary>放一个召回道具；这一格已有则什么都不做。</summary>
    public bool PlaceRecall(Vector2Int cell)
    {
        if (recalls == null) recalls = new List<LabRecallSpec>();
        if (HasRecall(cell)) return false;
        recalls.Add(new LabRecallSpec(cell));
        return true;
    }

    public bool RemoveRecall(Vector2Int cell)
    {
        if (recalls == null) return false;
        return recalls.RemoveAll(recall => recall.cell == cell) > 0;
    }

    /// <summary>这一格的冰块；没有返回 null。</summary>
    public LabIceSpec? FindIce(Vector2Int cell)
    {
        if (ices == null) return null;
        for (int i = 0; i < ices.Count; i++)
            if (ices[i].cell == cell) return ices[i];
        return null;
    }

    public bool HasIce(Vector2Int cell) => FindIce(cell).HasValue;

    /// <summary>放一块冰，或把这一格已有冰块改到新组。组号也相同则什么都不做。</summary>
    public bool PlaceIce(Vector2Int cell, int groupId)
    {
        if (ices == null) ices = new List<LabIceSpec>();
        for (int i = 0; i < ices.Count; i++)
        {
            var ice = ices[i];
            if (ice.cell != cell) continue;
            if (ice.groupId == groupId) return false;
            ice.groupId = groupId;
            ices[i] = ice;
            return true;
        }

        ices.Add(new LabIceSpec(cell, groupId));
        return true;
    }

    public bool RemoveIce(Vector2Int cell)
    {
        if (ices == null) return false;
        return ices.RemoveAll(ice => ice.cell == cell) > 0;
    }

    /// <summary>这一格的岩浆块；没有返回 null。</summary>
    public LabMagmaSpec? FindMagma(Vector2Int cell)
    {
        if (magmas == null) return null;
        for (int i = 0; i < magmas.Count; i++)
            if (magmas[i].cell == cell) return magmas[i];
        return null;
    }

    public bool HasMagma(Vector2Int cell) => FindMagma(cell).HasValue;

    /// <summary>放一块岩浆，或把这一格已有岩浆块改到新组。组号也相同则什么都不做。</summary>
    public bool PlaceMagma(Vector2Int cell, int groupId)
    {
        if (magmas == null) magmas = new List<LabMagmaSpec>();
        for (int i = 0; i < magmas.Count; i++)
        {
            var magma = magmas[i];
            if (magma.cell != cell) continue;
            if (magma.groupId == groupId) return false;
            magma.groupId = groupId;
            magmas[i] = magma;
            return true;
        }

        magmas.Add(new LabMagmaSpec(cell, groupId));
        return true;
    }

    public bool RemoveMagma(Vector2Int cell)
    {
        if (magmas == null) return false;
        return magmas.RemoveAll(magma => magma.cell == cell) > 0;
    }

    /// <summary>这一格是否有任意组的隐藏方块。</summary>
    public bool HasHiddenBlock(Vector2Int cell)
    {
        if (hiddenBlocks == null) return false;
        for (int i = 0; i < hiddenBlocks.Count; i++)
            if (hiddenBlocks[i].cell == cell) return true;
        return false;
    }

    /// <summary>这一格是否属于指定组的隐藏方块。</summary>
    public bool HasHiddenBlock(Vector2Int cell, int groupId)
    {
        if (hiddenBlocks == null) return false;
        for (int i = 0; i < hiddenBlocks.Count; i++)
            if (hiddenBlocks[i].cell == cell && hiddenBlocks[i].groupId == groupId) return true;
        return false;
    }

    /// <summary>把这一格加进指定组的隐藏方块。已在该组则什么都不做。同一格可以同时属于多个组。</summary>
    public bool PlaceHiddenBlock(Vector2Int cell, int groupId)
    {
        if (hiddenBlocks == null) hiddenBlocks = new List<LabHiddenBlockSpec>();
        for (int i = 0; i < hiddenBlocks.Count; i++)
            if (hiddenBlocks[i].cell == cell && hiddenBlocks[i].groupId == groupId) return false;
        hiddenBlocks.Add(new LabHiddenBlockSpec(cell, groupId));
        return true;
    }

    /// <summary>把这一格从指定组摘掉；摘掉后该格不再属于任何组才返回 true。</summary>
    public bool RemoveHiddenBlock(Vector2Int cell, int groupId)
    {
        if (hiddenBlocks == null) return false;
        int before = hiddenBlocks.Count;
        hiddenBlocks.RemoveAll(block => block.cell == cell && block.groupId == groupId);
        return hiddenBlocks.Count < before;
    }

    /// <summary>擦掉这一格上所有组里的隐藏方块。</summary>
    public bool RemoveHiddenBlockAll(Vector2Int cell)
    {
        if (hiddenBlocks == null) return false;
        return hiddenBlocks.RemoveAll(block => block.cell == cell) > 0;
    }

    /// <summary>这一格的修复方块；没有返回 null。</summary>
    public LabRepairBlockSpec? FindRepairBlock(Vector2Int cell)
    {
        if (repairBlocks == null) return null;
        for (int i = 0; i < repairBlocks.Count; i++)
            if (repairBlocks[i].cell == cell) return repairBlocks[i];
        return null;
    }

    public bool HasRepairBlock(Vector2Int cell) => FindRepairBlock(cell).HasValue;

    /// <summary>放一块修复方块；这一格已经有了则什么都不做（没有组号可改）。</summary>
    public bool PlaceRepairBlock(Vector2Int cell)
    {
        if (repairBlocks == null) repairBlocks = new List<LabRepairBlockSpec>();
        if (HasRepairBlock(cell)) return false;
        repairBlocks.Add(new LabRepairBlockSpec(cell));
        return true;
    }

    public bool RemoveRepairBlock(Vector2Int cell)
    {
        if (repairBlocks == null) return false;
        return repairBlocks.RemoveAll(block => block.cell == cell) > 0;
    }

    public bool PlaceRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) respawnPoints = new List<LabRespawnPointSpec>();
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < respawnPoints.Count; i++)
            if (respawnPoints[i].position == position) return false;
        respawnPoints.Add(new LabRespawnPointSpec(position));
        return true;
    }

    public bool RemoveRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        return respawnPoints.RemoveAll(point => point.position == position) > 0;
    }

    public bool HasRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < respawnPoints.Count; i++)
            if (respawnPoints[i].position == position) return true;
        return false;
    }

    /// <summary>放一个传送点。运行时双人进入后标记，已标记点互为终点，数据里不保存配对关系。</summary>
    public bool PlaceTeleport(Vector2Int cell)
    {
        if (teleports == null) teleports = new List<LabTeleportSpec>();
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < teleports.Count; i++)
            if (teleports[i].position == position) return false;
        teleports.Add(new LabTeleportSpec(position));
        return true;
    }

    public bool RemoveTeleport(Vector2Int cell)
    {
        if (teleports == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        return teleports.RemoveAll(point => point.position == position) > 0;
    }

    public bool HasTeleport(Vector2Int cell)
    {
        if (teleports == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < teleports.Count; i++)
            if (teleports[i].position == position) return true;
        return false;
    }

    /// <summary>编辑器新建组用：当前最大组号 + 1；没有冰块时从 1 开始。0 是合法组，不作为默认。</summary>
    public int NextIceGroupId()
    {
        int max = 0;
        if (ices != null)
            for (int i = 0; i < ices.Count; i++)
                if (ices[i].groupId > max) max = ices[i].groupId;
        return max + 1;
    }

    /// <summary>同一组的所有冰块（深拷贝，按格排序），给校验和测试用。</summary>
    public List<LabIceSpec> GroupIces(int groupId)
    {
        var list = new List<LabIceSpec>();
        if (ices == null) return list;
        for (int i = 0; i < ices.Count; i++)
            if (ices[i].groupId == groupId) list.Add(ices[i]);
        list.Sort((a, b) => CompareCells(a.cell, b.cell));
        return list;
    }

    /// <summary>编辑器新建组用：当前最大组号 + 1；没有岩浆块时从 1 开始。0 是合法组，不作为默认。</summary>
    public int NextMagmaGroupId()
    {
        int max = 0;
        if (magmas != null)
            for (int i = 0; i < magmas.Count; i++)
                if (magmas[i].groupId > max) max = magmas[i].groupId;
        return max + 1;
    }

    /// <summary>同一组的所有岩浆块（深拷贝，按格排序），给校验和测试用。</summary>
    public List<LabMagmaSpec> GroupMagmas(int groupId)
    {
        var list = new List<LabMagmaSpec>();
        if (magmas == null) return list;
        for (int i = 0; i < magmas.Count; i++)
            if (magmas[i].groupId == groupId) list.Add(magmas[i]);
        list.Sort((a, b) => CompareCells(a.cell, b.cell));
        return list;
    }

    /// <summary>编辑器新建组用：当前最大组号 + 1；没有隐藏方块时从 1 开始。0 是合法组，不作为默认。</summary>
    public int NextHiddenGroupId()
    {
        int max = 0;
        if (hiddenBlocks != null)
            for (int i = 0; i < hiddenBlocks.Count; i++)
                if (hiddenBlocks[i].groupId > max) max = hiddenBlocks[i].groupId;
        return max + 1;
    }

    /// <summary>同一组的所有隐藏方块（深拷贝，按格排序），给校验和运行时判定用。</summary>
    public List<LabHiddenBlockSpec> GroupHiddenBlocks(int groupId)
    {
        var list = new List<LabHiddenBlockSpec>();
        if (hiddenBlocks == null) return list;
        for (int i = 0; i < hiddenBlocks.Count; i++)
            if (hiddenBlocks[i].groupId == groupId) list.Add(hiddenBlocks[i]);
        list.Sort((a, b) => CompareCells(a.cell, b.cell));
        return list;
    }

    public bool ContainsCell(Vector2Int cell)
    {
        return cell.x >= bounds.x && cell.x < bounds.x + bounds.width
            && cell.y >= bounds.y && cell.y < bounds.y + bounds.height;
    }

    public bool ContainsPoint(Vector2 point)
    {
        return point.x >= bounds.x && point.x < bounds.x + bounds.width
            && point.y >= bounds.y && point.y < bounds.y + bounds.height;
    }

    /// <summary>排序 + 去重。编辑器每次改完都调一次，保证存盘顺序稳定、序列化 diff 干净。</summary>
    public void Normalize()
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (ropes == null) ropes = new List<LabRopeSpec>();
        if (keys == null) keys = new List<LabKeySpec>();
        if (recalls == null) recalls = new List<LabRecallSpec>();
        if (doors == null) doors = new List<LabDoorSpec>();
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        if (ices == null) ices = new List<LabIceSpec>();
        if (magmas == null) magmas = new List<LabMagmaSpec>();
        if (hiddenBlocks == null) hiddenBlocks = new List<LabHiddenBlockSpec>();
        if (repairBlocks == null) repairBlocks = new List<LabRepairBlockSpec>();
        if (respawnPoints == null) respawnPoints = new List<LabRespawnPointSpec>();
        if (teleports == null) teleports = new List<LabTeleportSpec>();
        if (partitions == null) partitions = new List<LabLevelPartition>();

        // 门色漏写/手填成透明时回落到默认色，否则门在场景里完全看不见。
        for (int i = 0; i < doors.Count; i++)
        {
            var door = doors[i];
            if (door.color.a <= 0.01f)
            {
                door.color = LabDoor.DefaultClosedColor;
                doors[i] = door;
            }
        }

        // 旧场景里没有这两个字段时读出来是 0：回落到 Tuning Lab 默认，避免相机被锁死在 0.1。
        if (cameraMinSize <= 0f) cameraMinSize = TuningLab.DefaultCameraMinSize;
        if (cameraMaxSize <= 0f) cameraMaxSize = TuningLab.DefaultCameraMaxSize;
        cameraMinSize = Mathf.Clamp(cameraMinSize, 0.1f, 128f);
        cameraMaxSize = Mathf.Clamp(cameraMaxSize, cameraMinSize, 128f);

        for (int i = partitions.Count - 1; i >= 0; i--)
        {
            var partition = partitions[i];
            if (partition == null)
            {
                partitions.RemoveAt(i);
                continue;
            }

            partition.bounds = NormalizeRect(partition.bounds);
            if (string.IsNullOrEmpty(partition.name)) partition.name = "Level " + (i + 1);
        }

        var uniqueRespawnPoints = new HashSet<Vector2>();
        for (int i = respawnPoints.Count - 1; i >= 0; i--)
        {
            if (!uniqueRespawnPoints.Add(respawnPoints[i].position))
                respawnPoints.RemoveAt(i);
        }
        respawnPoints.Sort((a, b) =>
        {
            int y = a.position.y.CompareTo(b.position.y);
            return y != 0 ? y : a.position.x.CompareTo(b.position.x);
        });

        var uniqueTeleports = new HashSet<Vector2>();
        for (int i = teleports.Count - 1; i >= 0; i--)
        {
            if (!uniqueTeleports.Add(teleports[i].position))
                teleports.RemoveAt(i);
        }
        teleports.Sort((a, b) =>
        {
            int y = a.position.y.CompareTo(b.position.y);
            return y != 0 ? y : a.position.x.CompareTo(b.position.x);
        });

        solids.Sort(CompareCells);

        int write = 0;
        for (int read = 0; read < solids.Count; read++)
        {
            if (write > 0 && solids[read] == solids[write - 1]) continue;
            solids[write++] = solids[read];
        }
        if (write < solids.Count) solids.RemoveRange(write, solids.Count - write);

        for (int i = ropes.Count - 1; i >= 0; i--)
        {
            var rope = ropes[i];
            if (rope.cells <= 0)
            {
                ropes.RemoveAt(i);
                continue;
            }
            rope.cells = Mathf.Min(rope.cells, 256);
            ropes[i] = rope;
        }

        // 一格只留一根刺：按格排序后丢掉同格重复项。
        spikes.Sort((a, b) => CompareCells(a.cell, b.cell));
        int spikeWrite = 0;
        for (int read = 0; read < spikes.Count; read++)
        {
            if (spikeWrite > 0 && spikes[read].cell == spikes[spikeWrite - 1].cell) continue;
            spikes[spikeWrite++] = spikes[read];
        }
        if (spikeWrite < spikes.Count) spikes.RemoveRange(spikeWrite, spikes.Count - spikeWrite);

        // 一格只留一个召回道具：按格排序后丢掉同格重复项。
        recalls.Sort((a, b) => CompareCells(a.cell, b.cell));
        int recallWrite = 0;
        for (int read = 0; read < recalls.Count; read++)
        {
            if (recallWrite > 0 && recalls[read].cell == recalls[recallWrite - 1].cell) continue;
            recalls[recallWrite++] = recalls[read];
        }
        if (recallWrite < recalls.Count) recalls.RemoveRange(recallWrite, recalls.Count - recallWrite);

        // 一格只留一块冰：按格排序后丢掉同格重复项，组号随保留项。
        ices.Sort((a, b) => CompareCells(a.cell, b.cell));
        int iceWrite = 0;
        for (int read = 0; read < ices.Count; read++)
        {
            if (iceWrite > 0 && ices[read].cell == ices[iceWrite - 1].cell) continue;
            ices[iceWrite++] = ices[read];
        }
        if (iceWrite < ices.Count) ices.RemoveRange(iceWrite, ices.Count - iceWrite);

        // 一格只留一块岩浆：规则同冰块，组号随保留项。
        magmas.Sort((a, b) => CompareCells(a.cell, b.cell));
        int magmaWrite = 0;
        for (int read = 0; read < magmas.Count; read++)
        {
            if (magmaWrite > 0 && magmas[read].cell == magmas[magmaWrite - 1].cell) continue;
            magmas[magmaWrite++] = magmas[read];
        }
        if (magmaWrite < magmas.Count) magmas.RemoveRange(magmaWrite, magmas.Count - magmaWrite);

        // 隐藏方块按 (格, 组) 去重：同一格允许属于多个组（嵌套组独立判定），
        // 同格同组只留一条。
        hiddenBlocks.Sort((a, b) =>
        {
            int byCell = CompareCells(a.cell, b.cell);
            return byCell != 0 ? byCell : a.groupId.CompareTo(b.groupId);
        });
        int hiddenWrite = 0;
        for (int read = 0; read < hiddenBlocks.Count; read++)
        {
            if (hiddenWrite > 0 && hiddenBlocks[read].cell == hiddenBlocks[hiddenWrite - 1].cell
                && hiddenBlocks[read].groupId == hiddenBlocks[hiddenWrite - 1].groupId) continue;
            hiddenBlocks[hiddenWrite++] = hiddenBlocks[read];
        }
        if (hiddenWrite < hiddenBlocks.Count) hiddenBlocks.RemoveRange(hiddenWrite, hiddenBlocks.Count - hiddenWrite);

        // 一格只留一块修复方块：规则同冰和岩浆，它没有组号。
        repairBlocks.Sort((a, b) => CompareCells(a.cell, b.cell));
        int repairWrite = 0;
        for (int read = 0; read < repairBlocks.Count; read++)
        {
            if (repairWrite > 0 && repairBlocks[read].cell == repairBlocks[repairWrite - 1].cell) continue;
            repairBlocks[repairWrite++] = repairBlocks[read];
        }
        if (repairWrite < repairBlocks.Count) repairBlocks.RemoveRange(repairWrite, repairBlocks.Count - repairWrite);
    }

    public void CopyFrom(LabLevelData other)
    {
        if (other == null)
        {
            bounds = new RectInt(-13, -1, 26, 17);
            spawnA = new Vector2(8f, 0f);
            spawnB = new Vector2(10f, 0f);
            cameraMinSize = TuningLab.DefaultCameraMinSize;
            cameraMaxSize = TuningLab.DefaultCameraMaxSize;
            solids = new List<Vector2Int>();
            ropes = new List<LabRopeSpec>();
            keys = new List<LabKeySpec>();
            recalls = new List<LabRecallSpec>();
            doors = new List<LabDoorSpec>();
            spikes = new List<LabSpikeSpec>();
            ices = new List<LabIceSpec>();
            magmas = new List<LabMagmaSpec>();
            hiddenBlocks = new List<LabHiddenBlockSpec>();
            repairBlocks = new List<LabRepairBlockSpec>();
            respawnPoints = new List<LabRespawnPointSpec>();
            teleports = new List<LabTeleportSpec>();
            partitions = new List<LabLevelPartition>();
            return;
        }

        bounds = other.bounds;
        spawnA = other.spawnA;
        spawnB = other.spawnB;
        cameraMinSize = other.cameraMinSize;
        cameraMaxSize = other.cameraMaxSize;
        solids = new List<Vector2Int>(other.solids ?? new List<Vector2Int>());
        ropes = new List<LabRopeSpec>(other.ropes ?? new List<LabRopeSpec>());
        keys = new List<LabKeySpec>(other.keys ?? new List<LabKeySpec>());
        recalls = new List<LabRecallSpec>(other.recalls ?? new List<LabRecallSpec>());
        doors = new List<LabDoorSpec>(other.doors ?? new List<LabDoorSpec>());
        spikes = new List<LabSpikeSpec>(other.spikes ?? new List<LabSpikeSpec>());
        ices = new List<LabIceSpec>(other.ices ?? new List<LabIceSpec>());
        magmas = new List<LabMagmaSpec>(other.magmas ?? new List<LabMagmaSpec>());
        hiddenBlocks = new List<LabHiddenBlockSpec>(
            other.hiddenBlocks ?? new List<LabHiddenBlockSpec>());
        repairBlocks = new List<LabRepairBlockSpec>(
            other.repairBlocks ?? new List<LabRepairBlockSpec>());
        respawnPoints = new List<LabRespawnPointSpec>(
            other.respawnPoints ?? new List<LabRespawnPointSpec>());
        teleports = new List<LabTeleportSpec>(
            other.teleports ?? new List<LabTeleportSpec>());
        partitions = new List<LabLevelPartition>();
        if (other.partitions != null)
        {
            foreach (var partition in other.partitions)
            {
                if (partition == null) continue;
                partitions.Add(new LabLevelPartition
                {
                    name = partition.name,
                    bounds = partition.bounds
                });
            }
        }
        Normalize();
    }

    public LabLevelData Clone()
    {
        var copy = new LabLevelData();
        copy.CopyFrom(this);
        return copy;
    }

    public static Rect ToRect(RectInt rect)
        => new Rect(rect.x, rect.y, rect.width, rect.height);

    public static RectInt NormalizeRect(RectInt rect)
    {
        if (rect.width < 0)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }
        if (rect.height < 0)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }
        if (rect.width <= 0) rect.width = 1;
        if (rect.height <= 0) rect.height = 1;
        return rect;
    }

    public static bool Overlaps(RectInt a, RectInt b)
    {
        return a.x < b.x + b.width && a.x + a.width > b.x
            && a.y < b.y + b.height && a.y + a.height > b.y;
    }

    /// <summary>这个 bounds 是否和任何已有分区重叠；ignoreIndex 用于移动/改大小自己。</summary>
    public bool OverlapsAny(RectInt bounds, int ignoreIndex = -1)
    {
        if (partitions == null) return false;
        bounds = NormalizeRect(bounds);

        for (int i = 0; i < partitions.Count; i++)
        {
            if (i == ignoreIndex) continue;
            var partition = partitions[i];
            if (partition != null && Overlaps(bounds, partition.bounds)) return true;
        }
        return false;
    }

    /// <summary>B 的判定：点在哪个分区的 bounds 里；没有返回 -1。</summary>
    public int FindPartition(Vector2 point)
    {
        if (partitions == null) return -1;
        for (int i = 0; i < partitions.Count; i++)
        {
            var partition = partitions[i];
            if (partition != null && ToRect(partition.bounds).Contains(point)) return i;
        }
        return -1;
    }

    public LabLevelPartition AddPartition(RectInt bounds, string name = null)
    {
        if (partitions == null) partitions = new List<LabLevelPartition>();
        bounds = NormalizeRect(bounds);
        if (OverlapsAny(bounds)) return null;

        var partition = new LabLevelPartition
        {
            name = string.IsNullOrEmpty(name) ? "Level " + (partitions.Count + 1) : name,
            bounds = bounds
        };
        partitions.Add(partition);
        return partition;
    }

    /// <summary>只平移 bounds 和里面的物件，不做重叠检查。调用方必须先验证目标位置。</summary>
    public void TranslatePartition(int index, Vector2Int delta, bool moveContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return;
        var partition = partitions[index];
        if (partition == null || delta == Vector2Int.zero) return;

        Rect oldRect = ToRect(partition.bounds);
        partition.bounds = new RectInt(
            partition.bounds.x + delta.x,
            partition.bounds.y + delta.y,
            partition.bounds.width,
            partition.bounds.height);

        if (!moveContents) return;

        var shift = new Vector2(delta.x, delta.y);

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                if (oldRect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)))
                    solids[i] = cell + delta;
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                if (!oldRect.Contains(rope.center)) continue;
                rope.center += shift;
                ropes[i] = rope;
            }
        }

        if (keys != null)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (!oldRect.Contains(key.position)) continue;
                key.position += shift;
                keys[i] = key;
            }
        }

        if (recalls != null)
        {
            for (int i = 0; i < recalls.Count; i++)
            {
                var recall = recalls[i];
                if (!oldRect.Contains(new Vector2(recall.cell.x + 0.5f, recall.cell.y + 0.5f))) continue;
                recall.cell += delta;
                recalls[i] = recall;
            }
        }

        if (doors != null)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (!oldRect.Contains(door.position)) continue;
                door.position += shift;
                doors[i] = door;
            }
        }

        if (spikes != null)
        {
            for (int i = 0; i < spikes.Count; i++)
            {
                var spike = spikes[i];
                if (!oldRect.Contains(new Vector2(spike.cell.x + 0.5f, spike.cell.y + 0.5f))) continue;
                spike.cell += delta;
                spikes[i] = spike;
            }
        }

        if (ices != null)
        {
            for (int i = 0; i < ices.Count; i++)
            {
                var ice = ices[i];
                if (!oldRect.Contains(new Vector2(ice.cell.x + 0.5f, ice.cell.y + 0.5f))) continue;
                ice.cell += delta;
                ices[i] = ice;
            }
        }

        if (magmas != null)
        {
            for (int i = 0; i < magmas.Count; i++)
            {
                var magma = magmas[i];
                if (!oldRect.Contains(new Vector2(magma.cell.x + 0.5f, magma.cell.y + 0.5f))) continue;
                magma.cell += delta;
                magmas[i] = magma;
            }
        }

        if (hiddenBlocks != null)
        {
            for (int i = 0; i < hiddenBlocks.Count; i++)
            {
                var block = hiddenBlocks[i];
                if (!oldRect.Contains(new Vector2(block.cell.x + 0.5f, block.cell.y + 0.5f))) continue;
                block.cell += delta;
                hiddenBlocks[i] = block;
            }
        }

        if (repairBlocks != null)
        {
            for (int i = 0; i < repairBlocks.Count; i++)
            {
                var block = repairBlocks[i];
                if (!oldRect.Contains(block.Center)) continue;
                block.cell += delta;
                repairBlocks[i] = block;
            }
        }

        if (respawnPoints != null)
        {
            for (int i = 0; i < respawnPoints.Count; i++)
            {
                var point = respawnPoints[i];
                if (!oldRect.Contains(point.position)) continue;
                point.position += shift;
                respawnPoints[i] = point;
            }
        }

        if (teleports != null)
        {
            for (int i = 0; i < teleports.Count; i++)
            {
                var point = teleports[i];
                if (!oldRect.Contains(point.position)) continue;
                point.position += shift;
                teleports[i] = point;
            }
        }

        if (oldRect.Contains(spawnA)) spawnA += shift;
        if (oldRect.Contains(spawnB)) spawnB += shift;
    }

    /// <summary>移动分区。目标位置和其它分区重叠时直接拒绝，不会移动里面的物件。</summary>
    public bool MovePartition(int index, Vector2Int delta, bool moveContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;
        var partition = partitions[index];
        if (partition == null || delta == Vector2Int.zero) return false;

        var next = new RectInt(
            partition.bounds.x + delta.x,
            partition.bounds.y + delta.y,
            partition.bounds.width,
            partition.bounds.height);
        if (OverlapsAny(next, index)) return false;

        TranslatePartition(index, delta, moveContents);
        return true;
    }

    /// <summary>改分区大小。和其它分区重叠时直接拒绝。</summary>
    public bool ResizePartition(int index, RectInt bounds)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;
        var partition = partitions[index];
        if (partition == null) return false;

        bounds = NormalizeRect(bounds);
        if (OverlapsAny(bounds, index)) return false;

        partition.bounds = bounds;
        return true;
    }

    /// <summary>删除分区。deleteContents = true 时清掉分区里的地形和绳；出生点保留。</summary>
    public bool DeletePartition(int index, bool deleteContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;

        var partition = partitions[index];
        if (partition != null && deleteContents)
        {
            Rect rect = ToRect(partition.bounds);
            if (solids != null)
                solids.RemoveAll(cell => rect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
            if (ropes != null)
                ropes.RemoveAll(rope => rect.Contains(rope.center));
            if (keys != null)
                keys.RemoveAll(key => rect.Contains(key.position));
            if (recalls != null)
                recalls.RemoveAll(recall => rect.Contains(new Vector2(recall.cell.x + 0.5f, recall.cell.y + 0.5f)));
            if (doors != null)
                doors.RemoveAll(door => rect.Contains(door.position));
            if (spikes != null)
                spikes.RemoveAll(spike => rect.Contains(new Vector2(spike.cell.x + 0.5f, spike.cell.y + 0.5f)));
            if (ices != null)
                ices.RemoveAll(ice => rect.Contains(new Vector2(ice.cell.x + 0.5f, ice.cell.y + 0.5f)));
            if (magmas != null)
                magmas.RemoveAll(magma => rect.Contains(new Vector2(magma.cell.x + 0.5f, magma.cell.y + 0.5f)));
            if (hiddenBlocks != null)
                hiddenBlocks.RemoveAll(block => rect.Contains(new Vector2(block.cell.x + 0.5f, block.cell.y + 0.5f)));
            if (repairBlocks != null)
                repairBlocks.RemoveAll(block => rect.Contains(block.Center));
            if (respawnPoints != null)
                respawnPoints.RemoveAll(point => rect.Contains(point.position));
            if (teleports != null)
                teleports.RemoveAll(point => rect.Contains(point.position));
        }

        partitions.RemoveAt(index);
        return true;
    }

    // ------------------------------------------------------------------
    // 选区操作：编辑器里的矩形选区直接改这份数据
    // ------------------------------------------------------------------

    /// <summary>一个点是否落在任意选区里。</summary>
    public static bool InAnyArea(IList<RectInt> areas, Vector2 point)
    {
        if (areas == null) return false;
        for (int i = 0; i < areas.Count; i++)
            if (ToRect(areas[i]).Contains(point)) return true;
        return false;
    }

    static Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>
    /// 拖动开始时锁定的选区内容：记下每种条目的索引，之后只平移这些条目。
    /// 这样选区框移动时不会把沿途扫过、但原本不在选区里的物件带进来。
    /// </summary>
    public sealed class ContentSelection
    {
        internal readonly List<int> solids = new List<int>();
        internal readonly List<int> ropes = new List<int>();
        internal readonly List<int> keys = new List<int>();
        internal readonly List<int> recalls = new List<int>();
        internal readonly List<int> doors = new List<int>();
        internal readonly List<int> spikes = new List<int>();
        internal readonly List<int> ices = new List<int>();
        internal readonly List<int> magmas = new List<int>();
        internal readonly List<int> hiddenBlocks = new List<int>();
        internal readonly List<int> repairBlocks = new List<int>();
        internal readonly List<int> respawnPoints = new List<int>();
        internal readonly List<int> teleports = new List<int>();

        /// <summary>锁定的条目数（地形格也算一条）。</summary>
        public int Count => solids.Count + ropes.Count + keys.Count + recalls.Count + doors.Count
            + spikes.Count + ices.Count + magmas.Count + hiddenBlocks.Count + repairBlocks.Count
            + respawnPoints.Count + teleports.Count;
    }

    /// <summary>
    /// 按当前选区锁定要移动的条目。拖动期间不要增删条目，否则锁定的索引会失效。
    /// </summary>
    public ContentSelection CaptureContents(IList<RectInt> areas)
    {
        var capture = new ContentSelection();
        if (areas == null || areas.Count == 0) return capture;

        if (solids != null)
            for (int i = 0; i < solids.Count; i++)
                if (InAnyArea(areas, CellCenter(solids[i]))) capture.solids.Add(i);

        if (ropes != null)
            for (int i = 0; i < ropes.Count; i++)
                if (InAnyArea(areas, ropes[i].center)) capture.ropes.Add(i);

        if (keys != null)
            for (int i = 0; i < keys.Count; i++)
                if (InAnyArea(areas, keys[i].position)) capture.keys.Add(i);

        if (recalls != null)
            for (int i = 0; i < recalls.Count; i++)
                if (InAnyArea(areas, CellCenter(recalls[i].cell))) capture.recalls.Add(i);

        if (doors != null)
            for (int i = 0; i < doors.Count; i++)
                if (InAnyArea(areas, doors[i].position)) capture.doors.Add(i);

        if (spikes != null)
            for (int i = 0; i < spikes.Count; i++)
                if (InAnyArea(areas, CellCenter(spikes[i].cell))) capture.spikes.Add(i);

        if (ices != null)
            for (int i = 0; i < ices.Count; i++)
                if (InAnyArea(areas, CellCenter(ices[i].cell))) capture.ices.Add(i);

        if (magmas != null)
            for (int i = 0; i < magmas.Count; i++)
                if (InAnyArea(areas, CellCenter(magmas[i].cell))) capture.magmas.Add(i);

        if (hiddenBlocks != null)
            for (int i = 0; i < hiddenBlocks.Count; i++)
                if (InAnyArea(areas, CellCenter(hiddenBlocks[i].cell))) capture.hiddenBlocks.Add(i);

        if (repairBlocks != null)
            for (int i = 0; i < repairBlocks.Count; i++)
                if (InAnyArea(areas, CellCenter(repairBlocks[i].cell))) capture.repairBlocks.Add(i);

        if (respawnPoints != null)
            for (int i = 0; i < respawnPoints.Count; i++)
                if (InAnyArea(areas, respawnPoints[i].position)) capture.respawnPoints.Add(i);

        if (teleports != null)
            for (int i = 0; i < teleports.Count; i++)
                if (InAnyArea(areas, teleports[i].position)) capture.teleports.Add(i);

        return capture;
    }

    /// <summary>
    /// 平移一次之前锁定的内容。地形、地刺、冰块按格心；绳/钥匙/门/重生点/传送点按中心。
    /// 返回移动的条目数。
    /// </summary>
    public int MoveCapturedContents(ContentSelection capture, Vector2Int delta)
    {
        if (capture == null || delta == Vector2Int.zero) return 0;
        int moved = 0;
        var shift = new Vector2(delta.x, delta.y);

        if (solids != null)
            foreach (int i in capture.solids)
            {
                solids[i] += delta;
                moved++;
            }

        if (ropes != null)
            foreach (int i in capture.ropes)
            {
                var rope = ropes[i];
                rope.center += shift;
                ropes[i] = rope;
                moved++;
            }

        if (keys != null)
            foreach (int i in capture.keys)
            {
                var key = keys[i];
                key.position += shift;
                keys[i] = key;
                moved++;
            }

        if (recalls != null)
            foreach (int i in capture.recalls)
            {
                var recall = recalls[i];
                recall.cell += delta;
                recalls[i] = recall;
                moved++;
            }

        if (doors != null)
            foreach (int i in capture.doors)
            {
                var door = doors[i];
                door.position += shift;
                doors[i] = door;
                moved++;
            }

        if (spikes != null)
            foreach (int i in capture.spikes)
            {
                var spike = spikes[i];
                spike.cell += delta;
                spikes[i] = spike;
                moved++;
            }

        if (ices != null)
            foreach (int i in capture.ices)
            {
                var ice = ices[i];
                ice.cell += delta;
                ices[i] = ice;
                moved++;
            }

        if (magmas != null)
            foreach (int i in capture.magmas)
            {
                var magma = magmas[i];
                magma.cell += delta;
                magmas[i] = magma;
                moved++;
            }

        if (hiddenBlocks != null)
            foreach (int i in capture.hiddenBlocks)
            {
                var block = hiddenBlocks[i];
                block.cell += delta;
                hiddenBlocks[i] = block;
                moved++;
            }

        if (repairBlocks != null)
            foreach (int i in capture.repairBlocks)
            {
                var block = repairBlocks[i];
                block.cell += delta;
                repairBlocks[i] = block;
                moved++;
            }

        if (respawnPoints != null)
            foreach (int i in capture.respawnPoints)
            {
                var point = respawnPoints[i];
                point.position += shift;
                respawnPoints[i] = point;
                moved++;
            }

        if (teleports != null)
            foreach (int i in capture.teleports)
            {
                var point = teleports[i];
                point.position += shift;
                teleports[i] = point;
                moved++;
            }

        return moved;
    }

    /// <summary>
    /// 把选区里的内容整体平移（按当前选区判定一次）。编辑器拖动请用
    /// <see cref="CaptureContents"/> + <see cref="MoveCapturedContents"/>，保证拖动过程中
    /// 选区框扫过的新物件不会被带走。出生点和关卡分区不算"物件"，不参与。
    /// </summary>
    public int MoveContents(IList<RectInt> areas, Vector2Int delta)
    {
        if (areas == null || areas.Count == 0 || delta == Vector2Int.zero) return 0;
        return MoveCapturedContents(CaptureContents(areas), delta);
    }

    /// <summary>删掉选区里的内容，返回删掉的条目数。includeSolids = false 时只删摆放物件。</summary>
    public int DeleteContents(IList<RectInt> areas, bool includeSolids)
    {
        if (areas == null || areas.Count == 0) return 0;
        int removed = 0;

        if (includeSolids && solids != null)
            removed += solids.RemoveAll(cell => InAnyArea(areas, CellCenter(cell)));

        if (ropes != null) removed += ropes.RemoveAll(rope => InAnyArea(areas, rope.center));
        if (keys != null) removed += keys.RemoveAll(key => InAnyArea(areas, key.position));
        if (recalls != null) removed += recalls.RemoveAll(recall => InAnyArea(areas, CellCenter(recall.cell)));
        if (doors != null) removed += doors.RemoveAll(door => InAnyArea(areas, door.position));
        if (spikes != null) removed += spikes.RemoveAll(spike => InAnyArea(areas, CellCenter(spike.cell)));
        if (ices != null) removed += ices.RemoveAll(ice => InAnyArea(areas, CellCenter(ice.cell)));
        if (magmas != null) removed += magmas.RemoveAll(magma => InAnyArea(areas, CellCenter(magma.cell)));
        if (hiddenBlocks != null)
            removed += hiddenBlocks.RemoveAll(block => InAnyArea(areas, CellCenter(block.cell)));
        if (repairBlocks != null)
            removed += repairBlocks.RemoveAll(block => InAnyArea(areas, CellCenter(block.cell)));
        if (respawnPoints != null)
            removed += respawnPoints.RemoveAll(point => InAnyArea(areas, point.position));
        if (teleports != null)
            removed += teleports.RemoveAll(point => InAnyArea(areas, point.position));

        return removed;
    }

    /// <summary>
    /// 把另一份关卡数据并进来（Issue #109）。
    ///
    /// 所有条目按"身份"去重：地形按格、物件按格+分组，同一个东西不会出现两份。
    /// bounds 取并集，因为合并后相机得罩住全部内容。
    /// 出生点和相机尺寸这类单值字段保持接收方自己的，不被覆盖。
    ///
    /// 返回新增的条目总数。
    /// </summary>
    public int MergeFrom(LabLevelData other)
    {
        if (other == null || ReferenceEquals(other, this)) return 0;
        Normalize();
        other.Normalize();

        int added = 0;

        // ---- 地形：取并集 ----
        if (other.solids != null)
            for (int i = 0; i < other.solids.Count; i++)
                if (AddCell(other.solids[i])) added++;

        // ---- 绳：同一中心 + 同一朝向 + 同一长度算同一根 ----
        if (other.ropes != null)
            for (int i = 0; i < other.ropes.Count; i++)
            {
                var rope = other.ropes[i];
                if (HasRope(rope)) continue;
                ropes.Add(rope);
                added++;
            }

        // ---- 钥匙 / 召回：按位置 ----
        if (other.keys != null)
            for (int i = 0; i < other.keys.Count; i++)
            {
                var key = other.keys[i];
                if (HasAt(keys, p => p.position, key.position)) continue;
                keys.Add(key);
                added++;
            }

        if (other.recalls != null)
            for (int i = 0; i < other.recalls.Count; i++)
                if (PlaceRecall(other.recalls[i].cell)) added++;

        // ---- 门：按位置 ----
        if (other.doors != null)
            for (int i = 0; i < other.doors.Count; i++)
            {
                var door = other.doors[i];
                if (HasAt(doors, d => d.position, door.position)) continue;
                doors.Add(door);
                added++;
            }

        // ---- 地刺：按格，朝向取先来的 ----
        if (other.spikes != null)
            for (int i = 0; i < other.spikes.Count; i++)
                if (PlaceSpike(other.spikes[i].cell, other.spikes[i].dir)) added++;

        // ---- 冰 / 岩浆 / 隐藏方块：按格 + 分组 ----
        if (other.ices != null)
            for (int i = 0; i < other.ices.Count; i++)
                if (PlaceIce(other.ices[i].cell, other.ices[i].groupId)) added++;

        if (other.magmas != null)
            for (int i = 0; i < other.magmas.Count; i++)
                if (PlaceMagma(other.magmas[i].cell, other.magmas[i].groupId)) added++;

        if (other.hiddenBlocks != null)
            for (int i = 0; i < other.hiddenBlocks.Count; i++)
                if (PlaceHiddenBlock(other.hiddenBlocks[i].cell, other.hiddenBlocks[i].groupId)) added++;

        if (other.repairBlocks != null)
            for (int i = 0; i < other.repairBlocks.Count; i++)
                if (PlaceRepairBlock(other.repairBlocks[i].cell)) added++;

        // ---- 重生点 / 传送点：按中心 ----
        if (other.respawnPoints != null)
            for (int i = 0; i < other.respawnPoints.Count; i++)
            {
                var point = other.respawnPoints[i];
                if (HasAt(respawnPoints, p => p.position, point.position)) continue;
                respawnPoints.Add(point);
                added++;
            }

        if (other.teleports != null)
            for (int i = 0; i < other.teleports.Count; i++)
            {
                var point = other.teleports[i];
                if (HasAt(teleports, p => p.position, point.position)) continue;
                teleports.Add(point);
                added++;
            }

        // ---- 相机范围取并集 ----
        bounds = Union(bounds, other.bounds);

        return added;
    }

    static RectInt Union(RectInt a, RectInt b)
    {
        int xMin = Mathf.Min(a.xMin, b.xMin);
        int yMin = Mathf.Min(a.yMin, b.yMin);
        int xMax = Mathf.Max(a.xMax, b.xMax);
        int yMax = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    bool HasRope(LabRopeSpec rope)
    {
        if (ropes == null) return false;
        for (int i = 0; i < ropes.Count; i++)
        {
            var existing = ropes[i];
            if (existing.center != rope.center) continue;
            if (existing.orientation != rope.orientation) continue;
            if (existing.cells != rope.cells) continue;
            return true;
        }
        return false;
    }

    static bool HasAt<T>(List<T> list, System.Func<T, Vector2> position, Vector2 target)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
            if ((position(list[i]) - target).sqrMagnitude < 0.0001f) return true;
        return false;
    }

    /// <summary>选区里有几个条目（地形格也算一个），给面板显示用。</summary>
    public int CountContents(IList<RectInt> areas, bool includeSolids)
    {
        if (areas == null || areas.Count == 0) return 0;
        int count = 0;

        if (includeSolids && solids != null)
            for (int i = 0; i < solids.Count; i++)
                if (InAnyArea(areas, CellCenter(solids[i]))) count++;

        if (ropes != null)
            for (int i = 0; i < ropes.Count; i++)
                if (InAnyArea(areas, ropes[i].center)) count++;
        if (keys != null)
            for (int i = 0; i < keys.Count; i++)
                if (InAnyArea(areas, keys[i].position)) count++;
        if (recalls != null)
            for (int i = 0; i < recalls.Count; i++)
                if (InAnyArea(areas, CellCenter(recalls[i].cell))) count++;
        if (doors != null)
            for (int i = 0; i < doors.Count; i++)
                if (InAnyArea(areas, doors[i].position)) count++;
        if (spikes != null)
            for (int i = 0; i < spikes.Count; i++)
                if (InAnyArea(areas, CellCenter(spikes[i].cell))) count++;
        if (ices != null)
            for (int i = 0; i < ices.Count; i++)
                if (InAnyArea(areas, CellCenter(ices[i].cell))) count++;
        if (magmas != null)
            for (int i = 0; i < magmas.Count; i++)
                if (InAnyArea(areas, CellCenter(magmas[i].cell))) count++;
        if (hiddenBlocks != null)
            for (int i = 0; i < hiddenBlocks.Count; i++)
                if (InAnyArea(areas, CellCenter(hiddenBlocks[i].cell))) count++;
        if (repairBlocks != null)
            for (int i = 0; i < repairBlocks.Count; i++)
                if (InAnyArea(areas, CellCenter(repairBlocks[i].cell))) count++;
        if (respawnPoints != null)
            for (int i = 0; i < respawnPoints.Count; i++)
                if (InAnyArea(areas, respawnPoints[i].position)) count++;
        if (teleports != null)
            for (int i = 0; i < teleports.Count; i++)
                if (InAnyArea(areas, teleports[i].position)) count++;

        return count;
    }

    /// <summary>
    /// 把实心格合并成尽量少的矩形，降低 Collider 数量，也保证运行时生成出来的
    /// 就是 TuningLab 那种"一整块平台一个 BoxCollider2D"。
    /// 贪心：从最下、最左的格开始，先向右扩，再整行向上扩。
    /// </summary>
    public List<RectInt> MergedRects()
    {
        var rects = new List<RectInt>();
        var set = new HashSet<Vector2Int>();
        if (solids != null)
            for (int i = 0; i < solids.Count; i++) set.Add(solids[i]);

        while (set.Count > 0)
        {
            Vector2Int start = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (var cell in set)
            {
                if (cell.y < start.y || (cell.y == start.y && cell.x < start.x))
                    start = cell;
            }

            int width = 1;
            while (set.Contains(new Vector2Int(start.x + width, start.y))) width++;

            int height = 1;
            while (true)
            {
                bool fullRow = true;
                int row = start.y + height;
                for (int x = start.x; x < start.x + width; x++)
                {
                    if (set.Contains(new Vector2Int(x, row))) continue;
                    fullRow = false;
                    break;
                }
                if (!fullRow) break;
                height++;
            }

            rects.Add(new RectInt(start.x, start.y, width, height));
            for (int y = start.y; y < start.y + height; y++)
                for (int x = start.x; x < start.x + width; x++)
                    set.Remove(new Vector2Int(x, y));
        }

        return rects;
    }

    /// <summary>
    /// 把 TuningLab.BuildRoom 里的"中心 + 尺寸"矩形写成格。
    /// 必须完整对齐 1 格，否则返回 false（4 倍场地就是故意不对齐的，不能拿来当关卡基准）。
    /// </summary>
    public bool AddAlignedRect(Vector2 center, Vector2 size, float tolerance = 0.001f)
    {
        float xMin = center.x - size.x * 0.5f;
        float xMax = center.x + size.x * 0.5f;
        float yMin = center.y - size.y * 0.5f;
        float yMax = center.y + size.y * 0.5f;

        int x0 = Mathf.RoundToInt(xMin);
        int x1 = Mathf.RoundToInt(xMax);
        int y0 = Mathf.RoundToInt(yMin);
        int y1 = Mathf.RoundToInt(yMax);

        if (x1 <= x0 || y1 <= y0) return false;
        if (Mathf.Abs(xMin - x0) > tolerance || Mathf.Abs(xMax - x1) > tolerance) return false;
        if (Mathf.Abs(yMin - y0) > tolerance || Mathf.Abs(yMax - y1) > tolerance) return false;

        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                AddCell(new Vector2Int(x, y));
        return true;
    }

    /// <summary>地形和绳子的格子范围；没有内容时退回相机范围。给"跳到关卡"用。</summary>
    public RectInt ContentBounds()
    {
        bool any = false;
        int x0 = 0, y0 = 0, x1 = 0, y1 = 0;

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                int cx0 = cell.x, cx1 = cell.x + 1;
                int cy0 = cell.y, cy1 = cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                float halfWidth = rope.orientation == LabRopeOrientation.Horizontal
                    ? rope.cells * 0.5f : 0.1875f;
                float halfHeight = rope.orientation == LabRopeOrientation.Horizontal
                    ? 0.125f : rope.cells * 0.5f;
                int cx0 = Mathf.FloorToInt(rope.center.x - halfWidth);
                int cx1 = Mathf.CeilToInt(rope.center.x + halfWidth);
                int cy0 = Mathf.FloorToInt(rope.center.y - halfHeight);
                int cy1 = Mathf.CeilToInt(rope.center.y + halfHeight);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (ices != null)
        {
            for (int i = 0; i < ices.Count; i++)
            {
                var ice = ices[i];
                int cx0 = ice.cell.x, cx1 = ice.cell.x + 1;
                int cy0 = ice.cell.y, cy1 = ice.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }

            }
        }

        if (magmas != null)
        {
            for (int i = 0; i < magmas.Count; i++)
            {
                var magma = magmas[i];
                int cx0 = magma.cell.x, cx1 = magma.cell.x + 1;
                int cy0 = magma.cell.y, cy1 = magma.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (hiddenBlocks != null)
        {
            for (int i = 0; i < hiddenBlocks.Count; i++)
            {
                var block = hiddenBlocks[i];
                int cx0 = block.cell.x, cx1 = block.cell.x + 1;
                int cy0 = block.cell.y, cy1 = block.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (repairBlocks != null)
        {
            for (int i = 0; i < repairBlocks.Count; i++)
            {
                var block = repairBlocks[i];
                int cx0 = block.cell.x, cx1 = block.cell.x + 1;
                int cy0 = block.cell.y, cy1 = block.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (recalls != null)
        {
            for (int i = 0; i < recalls.Count; i++)
            {
                var recall = recalls[i];
                int cx0 = recall.cell.x, cx1 = recall.cell.x + 1;
                int cy0 = recall.cell.y, cy1 = recall.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (respawnPoints != null)
        {
            for (int i = 0; i < respawnPoints.Count; i++)
            {
                var point = respawnPoints[i];
                int cx0 = Mathf.FloorToInt(point.position.x - 1.5f);
                int cx1 = Mathf.CeilToInt(point.position.x + 1.5f);
                int cy0 = Mathf.FloorToInt(point.position.y - 1.5f);
                int cy1 = Mathf.CeilToInt(point.position.y + 1.5f);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (teleports != null)
        {
            for (int i = 0; i < teleports.Count; i++)
            {
                var point = teleports[i];
                int cx0 = Mathf.FloorToInt(point.position.x - 1.5f);
                int cx1 = Mathf.CeilToInt(point.position.x + 1.5f);
                int cy0 = Mathf.FloorToInt(point.position.y - 1.5f);
                int cy1 = Mathf.CeilToInt(point.position.y + 1.5f);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        return any ? new RectInt(x0, y0, x1 - x0, y1 - y0) : bounds;
    }

    static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int y = a.y.CompareTo(b.y);
        return y != 0 ? y : a.x.CompareTo(b.x);
    }
}

[System.Serializable]
public enum LabRopeOrientation
{
    Vertical = 0,
    Horizontal = 1
}

[System.Serializable]
public struct LabRopeSpec
{
    /// <summary>绳中心（世界坐标）。宽 6 像素、每格 16 像素，所以 x 是连续值。</summary>
    public Vector2 center;
    /// <summary>绳长（格）。</summary>
    public int cells;
    public int chainId;
    public LabRopeOrientation orientation;

    public float Bottom => center.y - (orientation == LabRopeOrientation.Horizontal ? 0.125f : cells * 0.5f);
    public float Top => center.y + (orientation == LabRopeOrientation.Horizontal ? 0.125f : cells * 0.5f);

    public LabRopeSpec(Vector2 center, int cells, int chainId = 0,
        LabRopeOrientation orientation = LabRopeOrientation.Vertical)
    {
        this.center = center;
        this.cells = cells;
        this.chainId = chainId;
        this.orientation = orientation;
    }
}

/// <summary>
/// 一把钥匙。1×1，位置是中心；编辑器按格中心摆放，所以 x/y 都是格 + 0.5。
/// </summary>
[System.Serializable]
public struct LabKeySpec
{
    public Vector2 position;

    public LabKeySpec(Vector2 position)
    {
        this.position = position;
    }

    /// <summary>钥匙盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x),
        Mathf.FloorToInt(position.y),
        1, 1);
}

/// <summary>
/// 一个召回道具。1×1，位置是格心；拾取后全队永久解锁召回，地图重置不会刷新。
/// </summary>
[System.Serializable]
public struct LabRecallSpec
{
    public Vector2Int cell;

    public LabRecallSpec(Vector2Int cell)
    {
        this.cell = cell;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>道具盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

/// <summary>
/// 一扇门。1×3，位置是中心；任意一把钥匙都能开任意一扇门，颜色只用于区分/辨认。
/// </summary>
[System.Serializable]
public struct LabDoorSpec
{
    public const int CellHeight = 3;

    public Vector2 position;
    public Color color;

    public LabDoorSpec(Vector2 position, Color color)
    {
        this.position = position;
        this.color = color;
    }

    public static LabDoorSpec At(Vector2 position) => new LabDoorSpec(position, LabDoor.DefaultClosedColor);

    /// <summary>门盖住的格子：宽 1、高 <see cref="CellHeight"/>，按中心算。</summary>
    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x),
        Mathf.FloorToInt(position.y - CellHeight * 0.5f),
        1, CellHeight);
}

/// <summary>地刺的朝向：尖刺指向这个方向，底座贴在方向反面的那条格边上。
/// 地刺（朝上、立在地板顶上那格）= <see cref="Up"/>。</summary>
public enum LabSpikeDir
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// 一格一根地刺。外形 16×6 像素 = 1 × 0.375 世界单位，长边紧贴格边。
/// 碰撞箱 = 视觉外接矩形，一点不让：齿之间的凹陷也算碰到。
/// </summary>
[System.Serializable]
public struct LabSpikeSpec
{
    /// <summary>刺的厚度（世界单位）= 6 美术像素 / 16。</summary>
    public const float Thickness = 6f / TuningLab.CellPixels;

    public Vector2Int cell;
    public LabSpikeDir dir;

    public LabSpikeSpec(Vector2Int cell, LabSpikeDir dir)
    {
        this.cell = cell;
        this.dir = dir;
    }

    /// <summary>尖刺指向的单位轴。</summary>
    public Vector2 Facing
    {
        get
        {
            if (dir == LabSpikeDir.Up) return Vector2.up;
            if (dir == LabSpikeDir.Down) return Vector2.down;
            if (dir == LabSpikeDir.Left) return Vector2.left;
            return Vector2.right;
        }
    }

    /// <summary>刺自己那条 1×0.375 带的中心，也是生成物件的落点。</summary>
    public Vector2 StripCenter
        => new Vector2(cell.x + 0.5f, cell.y + 0.5f) - Facing * (0.5f - Thickness * 0.5f);

    /// <summary>碰撞尺寸：横着的刺宽 1 高 0.375，竖着的反之。</summary>
    public Vector2 StripSize
        => dir == LabSpikeDir.Up || dir == LabSpikeDir.Down
            ? new Vector2(1f, Thickness)
            : new Vector2(Thickness, 1f);

    /// <summary>底座踩在哪一格——那一格必须是实心，否则刺悬空。</summary>
    public Vector2Int BaseCell
        => cell - new Vector2Int(Mathf.RoundToInt(Facing.x), Mathf.RoundToInt(Facing.y));
}

/// <summary>
/// 一块冰：1×1 格，实体碰撞，可以站立/滑行。
/// groupId 沿用绳子的整数编组模式；0 也是普通组，不是“未编组”。
/// </summary>
[System.Serializable]
public struct LabIceSpec
{
    public Vector2Int cell;
    public int groupId;

    public LabIceSpec(Vector2Int cell, int groupId)
    {
        this.cell = cell;
        this.groupId = groupId;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>冰块盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

/// <summary>
/// 一块岩浆：1×1 格，实体碰撞，可以站立，走动规则和普通地面相同。
/// groupId 和冰块共用同一套整数编组语义；0 也是普通组。
/// 任意方向接触强制 A 的对应边红热；只有 v3 撞击才整组破坏。
/// </summary>
[System.Serializable]
public struct LabMagmaSpec
{
    public Vector2Int cell;
    public int groupId;

    public LabMagmaSpec(Vector2Int cell, int groupId)
    {
        this.cell = cell;
        this.groupId = groupId;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>岩浆块盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

/// <summary>
/// 一块隐藏方块：1×1 格，无实体，外形与墙一致。
/// groupId 与冰/岩浆共用同一套整数编组语义；0 也是普通组。
/// 任一玩家 BodyBounds 完全落入某组的格子并集后，该组整组揭示为半透明，
/// 揭示对地图重置免疫；同一格可以同时属于多个组（嵌套组独立判定）。
/// </summary>
[System.Serializable]
public struct LabHiddenBlockSpec
{
    public Vector2Int cell;
    public int groupId;

    public LabHiddenBlockSpec(Vector2Int cell, int groupId)
    {
        this.cell = cell;
        this.groupId = groupId;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>隐藏方块盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

/// <summary>
/// 一块修复方块：1×1 格，实体碰撞，可以站立，外形和 A 一样蓝。
/// A 的红热边（热二）/ 灵体边接触其表面 → 那条边回普通；不限使用次数、不会被消耗。
/// 没有 groupId：不参与分组破坏，也不参与合并几何分组。
/// </summary>
[System.Serializable]
public struct LabRepairBlockSpec
{
    public Vector2Int cell;

    public LabRepairBlockSpec(Vector2Int cell)
    {
        this.cell = cell;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>修复方块盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

[System.Serializable]
public struct LabRespawnPointSpec
{
    public Vector2 position;

    public LabRespawnPointSpec(Vector2 position)
    {
        this.position = position;
    }

    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x - 1.5f),
        Mathf.FloorToInt(position.y - 1.5f),
        3, 3);
}

/// <summary>
/// 一个传送点：3×3 触发区，位置是中心。数据不记录配对——已标记传送点互为终点，
/// 运行时进入其中一个后从其余传送点里选择出口。
/// </summary>
[System.Serializable]
public struct LabTeleportSpec
{
    public Vector2 position;

    public LabTeleportSpec(Vector2 position)
    {
        this.position = position;
    }

    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x - 1.5f),
        Mathf.FloorToInt(position.y - 1.5f),
        3, 3);
}

/// <summary>
/// 连续关卡分区。bounds 就是这一关的相机 clamp 范围：
/// B 进入哪个分区的 bounds，相机就用哪个分区的大小。移动分区时可以一起移动里面的物件。
/// </summary>
[System.Serializable]
public sealed class LabLevelPartition
{
    public string name = "Level";
    public RectInt bounds = new RectInt(0, 0, 16, 12);
}
