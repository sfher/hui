using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 <see cref="LabLevelData"/> 搭成真实场景物体：实心格按矩形合并后生成 BoxCollider2D，
/// 绳子生成 LabVerticalRope。
///
/// 编辑器里由关卡编辑器调用 <see cref="RebuildPreview"/>，物体只用于预览、不存盘；
/// Play 时由 TuningLab.BuildRoom 调用 <see cref="BuildRuntime"/>，走的是同一条生成路径。
/// 所以"编辑器里看到什么"和"Play 时撞到什么"是同一份几何。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LabLevel : MonoBehaviour
{
    public const string GeneratedRootName = "LabLevel_Generated";

    /// <summary>
    /// 批量读取场景数据时临时关掉预览生成：合并场景工具会连续打开多个关卡场景读数据，
    /// 每个都搭一遍预览纯属浪费。读完必须在 finally 里恢复。
    /// </summary>
#if UNITY_EDITOR
    public static bool SuppressPreviewBuild;
#endif

    [SerializeField, HideInInspector] LabLevelData data = new LabLevelData();

    /// <summary>
    /// 外部关卡数据资源（Issue #109）。有值时以资源为准，内嵌的 data 不再使用。
    /// 为空时行为和以前完全一样，老场景不用改。
    /// </summary>
    [SerializeField] LabLevelAsset asset;

    public LabLevelAsset Asset => asset;

    public LabLevelData Data
    {
        get
        {
            if (asset != null) return asset.Data;
            if (data == null) data = new LabLevelData();
            return data;
        }
    }

    /// <summary>换绑数据资源。传 null 表示退回内嵌数据。</summary>
    public void BindAsset(LabLevelAsset target)
    {
        asset = target;
        RebuildPreview();
    }

    /// <summary>
    /// 把另一份关卡数据合并进来（Issue #109）。
    ///
    /// 规则：
    ///   - 所有物件按格去重，同一格不会出现两份
    ///   - 地形格取并集
    ///   - bounds 取两者的并集（合并后相机范围要罩住全部内容）
    ///   - 出生点、相机尺寸这类"单值"字段**不动**，保留接收方自己的
    ///
    /// 返回实际新增的条目数。
    /// </summary>
    public int MergeFrom(LabLevelData other)
    {
        if (other == null) return 0;
        return Data.MergeFrom(other);
    }

    public Rect CameraBounds =>
        new Rect(Data.bounds.x, Data.bounds.y, Data.bounds.width, Data.bounds.height);

    public int SolidCellCount => Data.solids != null ? Data.solids.Count : 0;
    public int MergedRectCount => Data.MergedRects().Count;
    public int RopeCount => Data.ropes != null ? Data.ropes.Count : 0;
    public int KeyCount => Data.keys != null ? Data.keys.Count : 0;
    public int RecallCount => Data.recalls != null ? Data.recalls.Count : 0;
    public int DoorCount => Data.doors != null ? Data.doors.Count : 0;
    public int SpikeCount => Data.spikes != null ? Data.spikes.Count : 0;
    public int IceCount => Data.ices != null ? Data.ices.Count : 0;
    public int MagmaCount => Data.magmas != null ? Data.magmas.Count : 0;
    public int HiddenBlockCount => Data.hiddenBlocks != null ? Data.hiddenBlocks.Count : 0;
    public int RepairBlockCount => Data.repairBlocks != null ? Data.repairBlocks.Count : 0;
    public int PixelArtCount => Data.pixelArts != null ? Data.pixelArts.Count : 0;
    public int RespawnPointCount => Data.respawnPoints != null ? Data.respawnPoints.Count : 0;
    public int TeleportCount => Data.teleports != null ? Data.teleports.Count : 0;

    Transform generatedRoot;
    // 运行时生成的钥匙/门/冰/岩浆；地图重置时要关回去、把捡走的钥匙长回来、把碎掉的块恢复。
    readonly System.Collections.Generic.List<LabKey> runtimeKeys = new System.Collections.Generic.List<LabKey>();
    // 召回道具是一次性的：不登记到 MapReset 的恢复链里。
    readonly System.Collections.Generic.List<LabRecallPickup> runtimeRecalls = new System.Collections.Generic.List<LabRecallPickup>();
    readonly System.Collections.Generic.List<LabDoor> runtimeDoors = new System.Collections.Generic.List<LabDoor>();
    readonly System.Collections.Generic.List<LabIce> runtimeIces = new System.Collections.Generic.List<LabIce>();
    readonly System.Collections.Generic.List<LabMagma> runtimeMagmas = new System.Collections.Generic.List<LabMagma>();
    TuningLab resetLab;
    CompositeCollider2D runtimeComposite;
    bool terrainResetDirty;

    void OnEnable()
    {
        if (!Application.isPlaying) RebuildPreview();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    /// <summary>
    /// 用 TuningLab 的内置房间覆盖当前关卡。编辑器的"从 Tuning Lab 重置"走这里。
    /// 走 <see cref="Data"/> 而不是直接写内嵌字段：数据绑在独立资源上时，写内嵌字段等于没改。
    /// </summary>
    public void ApplyBaseline()
    {
        Data.CopyFrom(TuningLab.CaptureBaseline());
        if (!Application.isPlaying) RebuildPreview();
    }

    /// <summary>
    /// 用另一份数据覆盖当前关卡。<paramref name="rebuildPreview"/> = false 时不重建预览，
    /// 给合并工具用：它紧接着还要再走一次自己的保存/重建流程，没必要搭两遍。
    /// </summary>
    public void CopyFrom(LabLevelData source, bool rebuildPreview = true)
    {
        Data.CopyFrom(source);
        if (rebuildPreview && !Application.isPlaying) RebuildPreview();
    }

    /// <summary>编辑器预览。只在非 Play 下跑，生成的物体带 DontSaveInEditor，不会被存进场景。</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        // 合并工具批量读场景时会临时关掉预览：打开的瞬间大关卡不搭物体，读完立刻关。
        if (SuppressPreviewBuild) return;
#endif
        ClearGenerated();
        Build(transform, null, true);
    }

    /// <summary>Play 时由 TuningLab 调用。material 是 TuningLab 那块摩擦 0 的 LabSlick。</summary>
    public void BuildRuntime(Transform parent, PhysicsMaterial2D material)
    {
        ClearGenerated();
        Build(parent, material, false);
        SubscribeMapReset(parent);
    }

    /// <summary>登记地图重置链。同步搭建和流式搭建共用，保证复活/传送时的恢复行为一致。</summary>
    void SubscribeMapReset(Transform parent)
    {
        var lab = parent != null ? parent.GetComponentInParent<TuningLab>() : null;
        if (lab == null) return;
        resetLab = lab;
        lab.MapReset += ResetKeyDoors;
        lab.MapReset += ResetIces;
        lab.MapReset += ResetMagmas;
        lab.MapReset += RebuildTerrainAfterReset;
    }

    /// <summary>
    /// 地图重置（复活 / 传送）：范围内的门关回去。钥匙全局复生——钥匙不贵，
    /// 且范围重置只关近处的门，若远处的钥匙也被吞掉会出现"门关着、世上没钥匙"的死局。
    /// </summary>
    void ResetKeyDoors()
    {
        for (int i = 0; i < runtimeDoors.Count; i++)
        {
            var door = runtimeDoors[i];
            if (door != null && resetLab.InMapResetRange(door.transform.position))
                door.ResetDoor();
        }

        Transform root = generatedRoot;
        if (root == null) return;
        for (int i = 0; i < runtimeKeys.Count && i < Data.keys.Count; i++)
        {
            if (runtimeKeys[i] != null) continue;
            runtimeKeys[i] = CreateKey(root, Data.keys[i]);
        }
    }

    /// <summary>
    /// 地图重置：只恢复范围内被破坏的冰块，且按组完整恢复（组是破坏单位，不能只长半组）。
    /// 完好的块不动；几何重建推迟到整条重置链末尾只做一次。
    /// </summary>
    void ResetIces()
    {
        HashSet<int> groups = null;
        for (int i = 0; i < runtimeIces.Count; i++)
        {
            var ice = runtimeIces[i];
            if (ice == null || !ice.Broken) continue;
            if (!resetLab.InMapResetRange(ice.transform.position)) continue;
            if (groups == null) groups = new HashSet<int>();
            groups.Add(ice.groupId);
        }
        if (groups == null) return;

        for (int i = 0; i < runtimeIces.Count; i++)
        {
            var ice = runtimeIces[i];
            if (ice == null || !ice.Broken || !groups.Contains(ice.groupId)) continue;
            ice.ResetIce(false);
            terrainResetDirty = true;
        }
    }

    /// <summary>地图重置：岩浆与冰块同规则——范围内、已破坏、按组完整恢复。</summary>
    void ResetMagmas()
    {
        HashSet<int> groups = null;
        for (int i = 0; i < runtimeMagmas.Count; i++)
        {
            var magma = runtimeMagmas[i];
            if (magma == null || !magma.Broken) continue;
            if (!resetLab.InMapResetRange(magma.transform.position)) continue;
            if (groups == null) groups = new HashSet<int>();
            groups.Add(magma.groupId);
        }
        if (groups == null) return;

        for (int i = 0; i < runtimeMagmas.Count; i++)
        {
            var magma = runtimeMagmas[i];
            if (magma == null || !magma.Broken || !groups.Contains(magma.groupId)) continue;
            magma.ResetMagma(false);
            terrainResetDirty = true;
        }
    }

    /// <summary>重置链的最后一步：冰/岩浆真的恢复了才重建一次合并几何。</summary>
    void RebuildTerrainAfterReset()
    {
        if (!terrainResetDirty) return;
        terrainResetDirty = false;
        if (runtimeComposite != null) runtimeComposite.GenerateGeometry();
    }

    void Build(Transform parent, PhysicsMaterial2D material, bool preview)
    {
        foreach (var step in BuildSteps(parent, material, preview)) { }
    }

    /// <summary>预览、同步运行和分帧加载共用同一份数据与生成顺序。</summary>
    IEnumerable<object> BuildSteps(Transform parent, PhysicsMaterial2D material, bool preview)
    {
        var data = Data;
        Transform terrain = SetupGenerated(parent, material, preview, out var composite);
        Transform root = generatedRoot;
        var rects = data.MergedRects();
        for (int i = 0; i < rects.Count; i++) { AddSolid(terrain, rects[i], material); yield return null; }
        for (int i = 0; i < data.ropes.Count; i++) { AddRope(root, data.ropes[i], material); yield return null; }
        for (int i = 0; i < data.keys.Count; i++) { AddKey(root, data.keys[i], preview); yield return null; }
        for (int i = 0; i < data.recalls.Count; i++) { AddRecall(root, data.recalls[i], preview); yield return null; }
        for (int i = 0; i < data.doors.Count; i++) { AddDoor(root, data.doors[i], preview); yield return null; }
        for (int i = 0; i < data.spikes.Count; i++) { AddSpike(root, data.spikes[i]); yield return null; }
        for (int i = 0; i < data.ices.Count; i++) { AddIce(terrain, data.ices[i], material, preview); yield return null; }
        for (int i = 0; i < data.magmas.Count; i++) { AddMagma(terrain, data.magmas[i], material, preview); yield return null; }
        for (int i = 0; i < data.hiddenBlocks.Count; i++) { AddHiddenBlock(root, data.hiddenBlocks[i]); yield return null; }
        for (int i = 0; i < data.repairBlocks.Count; i++) { AddRepairBlock(terrain, data.repairBlocks[i], material); yield return null; }
        for (int i = 0; i < data.pixelArts.Count; i++) { AddPixelArt(root, data.pixelArts[i]); yield return null; }
        for (int i = 0; i < data.respawnPoints.Count; i++) { AddRespawnPoint(root, data.respawnPoints[i]); yield return null; }
        for (int i = 0; i < data.teleports.Count; i++) { AddTeleport(root, data.teleports[i]); yield return null; }

        // 一次性生成完整几何。
        //
        // 这里**保持 Manual**，不要切回 Synchronous：Synchronous 下每次
        // `body.enabled = false`（碎一块冰/一块岩浆）都会自动重建整张合并几何，
        // 关卡上万个格子 + 八百多块冰，撞碎一组就是几十到上百毫秒的停顿（#150）。
        // Manual 下由 LabIce/LabMagma 自己标脏、帧末统一重建一次。
        composite.generationType = CompositeCollider2D.GenerationType.Manual;
        composite.GenerateGeometry();
    }

    Transform SetupGenerated(Transform parent, PhysicsMaterial2D material, bool preview,
        out CompositeCollider2D composite)
    {
        var data = Data;

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(parent, false);
        // 预览物体不存场景；Play 时的运行时物体本来就不会被存。
        if (preview) root.hideFlags = HideFlags.DontSaveInEditor;
        generatedRoot = root.transform;

        data.Normalize();

        if (!preview && !string.IsNullOrEmpty(data.background))
        {
            var backgroundGo = new GameObject("Background");
            backgroundGo.transform.SetParent(root.transform, false);
            var background = backgroundGo.AddComponent<LabBackground>();
            background.resource = "Backgrounds/" + data.background;
            background.Load();
        }

        // 地形、冰和岩浆共用一个合并碰撞体：相邻 1×1 方块的内侧竖面会在接缝处卡住
        // 横走的玩家，也会把上表面滑行误判成侧面接触。
        var fieldGo = new GameObject("TerrainField");
        fieldGo.transform.SetParent(root.transform, false);
        var fieldBody = fieldGo.AddComponent<Rigidbody2D>();
        fieldBody.bodyType = RigidbodyType2D.Static;
        var fieldComposite = fieldGo.AddComponent<CompositeCollider2D>();
        fieldComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        fieldComposite.generationType = CompositeCollider2D.GenerationType.Manual;
        fieldComposite.offsetDistance = 0f;
        if (material != null) fieldComposite.sharedMaterial = material;
        fieldGo.AddComponent<LabTerrainField>();
        if (!preview) runtimeComposite = fieldComposite;

        composite = fieldComposite;
        return fieldGo.transform;
    }

    /// <summary>
    /// 按帧预算推进共享生成器；调用方在完成前冻结玩法，避免续玩到尚未生成的远端地形。
    /// 不创建逐物件 Action/闭包，也不在每个批次反复重建整张地图的合并碰撞体。
    /// </summary>
    public IEnumerator BuildRuntimeStreaming(Transform parent, PhysicsMaterial2D material, float budgetMs)
    {
        ClearGenerated();
        double budget = Mathf.Max(0.5f, budgetMs);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in BuildSteps(parent, material, false))
        {
            if (watch.Elapsed.TotalMilliseconds < budget) continue;
            yield return null;
            watch.Restart();
        }
        SubscribeMapReset(parent);
    }

    void AddSolid(Transform parent, RectInt rect, PhysicsMaterial2D material)
    {
        var go = new GameObject("Solid");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(
            rect.x + rect.width * 0.5f,
            rect.y + rect.height * 0.5f,
            0f);
        go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.color = TuningLab.SolidColor;
        sr.sortingOrder = 0;
            LabArt.Apply(sr, "terrain", true);

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        // 所有实心矩形并入同一个 LabTerrainField 的合并几何。
        bc.compositeOperation = Collider2D.CompositeOperation.Merge;
        if (material != null) bc.sharedMaterial = material;
    }

    void AddRope(Transform parent, LabRopeSpec spec, PhysicsMaterial2D material)
    {
        var go = new GameObject(spec.orientation == LabRopeOrientation.Horizontal
            ? "HorizontalRope" : "VerticalRope");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.center;

        var bc = go.AddComponent<BoxCollider2D>();
        if (material != null) bc.sharedMaterial = material;

        if (spec.orientation == LabRopeOrientation.Horizontal)
        {
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            bc.usedByEffector = true;
            var rope = go.AddComponent<LabHorizontalRope>();
            rope.cells = Mathf.Max(1, spec.cells);
            rope.ApplyShape();
        }
        else
        {
            var rope = go.AddComponent<LabVerticalRope>();
            rope.cells = Mathf.Max(1, spec.cells);
            rope.chainId = spec.chainId;
            rope.ApplyShape();
        }
    }

    /// <summary>把一格里的刺摆进场景：物件落在 1×0.375 刺带自己的中心，碰撞箱不留余量。</summary>
    void AddSpike(Transform parent, LabSpikeSpec spec)
    {
        var go = new GameObject("Spike");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.StripCenter;

        var spike = go.AddComponent<LabSpike>();
        spike.cell = spec.cell;
        spike.dir = spec.dir;
        spike.ApplyShape();
    }

    /// <summary>把一格冰块摆进场景：物件落在格中心，碰撞箱正好 1×1。</summary>
    void AddIce(Transform parent, LabIceSpec spec, PhysicsMaterial2D material, bool preview)
    {
        var go = new GameObject("Ice");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var ice = go.AddComponent<LabIce>();
        ice.cell = spec.cell;
        ice.groupId = spec.groupId;
        ice.useGroupColor = true;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null && material != null) body.sharedMaterial = material;

        ice.ApplyShape();
        if (!preview) runtimeIces.Add(ice);
    }

    /// <summary>把一格岩浆块摆进场景：物件落在格中心，碰撞箱正好 1×1。</summary>
    void AddMagma(Transform parent, LabMagmaSpec spec, PhysicsMaterial2D material, bool preview)
    {
        var go = new GameObject("Magma");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var magma = go.AddComponent<LabMagma>();
        magma.cell = spec.cell;
        magma.groupId = spec.groupId;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null && material != null) body.sharedMaterial = material;

        magma.ApplyShape();
        if (!preview) runtimeMagmas.Add(magma);
    }

    /// <summary>
    /// 把一格隐藏方块摆进场景：逐格 SpriteRenderer、无碰撞，颜色与实心墙一致。
    /// 揭示状态只存在运行时实例上，不订阅 MapReset——地图重置后保持半透明。
    /// </summary>
    void AddHiddenBlock(Transform parent, LabHiddenBlockSpec spec)
    {
        var go = new GameObject("HiddenBlock");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var block = go.AddComponent<LabHiddenBlock>();
        block.cell = spec.cell;
        block.groupId = spec.groupId;
        block.ApplyShape();
    }

    /// <summary>
    /// 把一格修复方块摆进场景：物件落在格中心，碰撞箱正好 1×1。
    /// 不会被破坏，所以不进 runtime 列表、也没有地图重置钩子。
    /// </summary>
    void AddRepairBlock(Transform parent, LabRepairBlockSpec spec, PhysicsMaterial2D material)
    {
        var go = new GameObject("RepairBlock");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var block = go.AddComponent<LabRepairBlock>();
        block.cell = spec.cell;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null && material != null) body.sharedMaterial = material;

        block.ApplyShape();
    }

    static void AddRespawnPoint(Transform parent, LabRespawnPointSpec spec)
    {
        var go = new GameObject("RespawnPoint");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var point = go.AddComponent<LabRespawnPoint>();
        point.ApplyShape();
    }

    /// <summary>像素画只负责显示：完整 Sprite 缩放进整数格占地，不创建碰撞体。</summary>
    static void AddPixelArt(Transform parent, LabPixelArtSpec spec)
    {
        if (spec.sprite == null) return;

        var go = new GameObject("PixelArt");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = spec.sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = spec.sortingOrder;

        Vector2 nativeSize = spec.sprite.bounds.size;
        if (nativeSize.x <= 0f || nativeSize.y <= 0f) return;
        go.transform.localScale = new Vector3(
            spec.size.x / nativeSize.x,
            spec.size.y / nativeSize.y,
            1f);
    }

    static void AddTeleport(Transform parent, LabTeleportSpec spec)
    {
        var go = new GameObject("TeleportPoint");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var point = go.AddComponent<LabTeleportPoint>();
        point.ApplyShape();
    }

    void AddKey(Transform parent, LabKeySpec spec, bool preview)
    {
        var key = CreateKey(parent, spec);
        if (!preview) runtimeKeys.Add(key);
    }

    /// <summary>把召回道具摆进场景。拾取后销毁；MapReset 故意不重建它。</summary>
    void AddRecall(Transform parent, LabRecallSpec spec, bool preview)
    {
        var go = new GameObject("RecallPickup");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;
        var pickup = go.AddComponent<LabRecallPickup>();
        pickup.ApplyShape();
        if (!preview) runtimeRecalls.Add(pickup);
    }

    static LabKey CreateKey(Transform parent, LabKeySpec spec)
    {
        var go = new GameObject("Key");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var key = go.AddComponent<LabKey>();
        key.ApplyShape();
        return key;
    }

    void AddDoor(Transform parent, LabDoorSpec spec, bool preview)
    {
        var go = new GameObject("Door");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var door = go.AddComponent<LabDoor>();
        door.SetClosedColor(spec.color);
        door.ApplyShape();
        if (!preview) runtimeDoors.Add(door);
    }

    public void ClearGenerated()
    {
        UnbindMapReset();
        runtimeKeys.Clear();
        runtimeRecalls.Clear();
        runtimeDoors.Clear();
        runtimeIces.Clear();
        runtimeMagmas.Clear();
        runtimeComposite = null;
        terrainResetDirty = false;

        Transform existing = generatedRoot;
        if (existing == null && transform != null) existing = transform.Find(GeneratedRootName);
        generatedRoot = null;
        if (existing == null) return;

        bool preview = (existing.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0;
        if (Application.isPlaying && !preview) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    void UnbindMapReset()
    {
        if (resetLab == null) return;
        resetLab.MapReset -= ResetKeyDoors;
        resetLab.MapReset -= ResetIces;
        resetLab.MapReset -= ResetMagmas;
        resetLab.MapReset -= RebuildTerrainAfterReset;
        resetLab = null;
    }

    [ContextMenu("重建关卡预览")]
    void RebuildPreviewMenu()
    {
        if (!Application.isPlaying) RebuildPreview();
    }
}
