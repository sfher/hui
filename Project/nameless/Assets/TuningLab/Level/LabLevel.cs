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

    /// <summary>用 TuningLab 的内置房间覆盖当前关卡。编辑器的"从 Tuning Lab 重置"走这里。</summary>
    public void ApplyBaseline()
    {
        data = TuningLab.CaptureBaseline();
        if (!Application.isPlaying) RebuildPreview();
    }

    public void CopyFrom(LabLevelData source)
    {
        Data.CopyFrom(source);
        if (!Application.isPlaying) RebuildPreview();
    }

    /// <summary>编辑器预览。只在非 Play 下跑，生成的物体带 DontSaveInEditor，不会被存进场景。</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        ClearGenerated();
        Build(transform, null, true);
    }

    /// <summary>Play 时由 TuningLab 调用。material 是 TuningLab 那块摩擦 0 的 LabSlick。</summary>
    public void BuildRuntime(Transform parent, PhysicsMaterial2D material)
    {
        ClearGenerated();
        Build(parent, material, false);

        var lab = parent != null ? parent.GetComponentInParent<TuningLab>() : null;
        if (lab == null) return;
        resetLab = lab;
        lab.MapReset += ResetKeyDoors;
        lab.MapReset += ResetIces;
        lab.MapReset += ResetMagmas;
    }

    /// <summary>地图重置（复活 / 传送）时门关回去，被捡走的钥匙重新长出来。</summary>
    void ResetKeyDoors()
    {
        for (int i = 0; i < runtimeDoors.Count; i++)
            if (runtimeDoors[i] != null) runtimeDoors[i].ResetDoor();

        Transform root = generatedRoot;
        if (root == null) return;
        for (int i = 0; i < runtimeKeys.Count && i < Data.keys.Count; i++)
        {
            if (runtimeKeys[i] != null) continue;
            runtimeKeys[i] = CreateKey(root, Data.keys[i]);
        }
    }

    /// <summary>地图重置（复活 / 传送）时把所有被破坏的冰块恢复。</summary>
    void ResetIces()
    {
        LabIce any = null;
        for (int i = 0; i < runtimeIces.Count; i++)
        {
            var ice = runtimeIces[i];
            if (ice == null) continue;
            ice.ResetIce(false);
            if (any == null) any = ice;
        }

        // 恢复整组后只重建一次地形合并几何。
        if (any != null) any.RebuildCollision();
    }

    /// <summary>地图重置（复活 / 传送）时把所有被破坏的岩浆块恢复。</summary>
    void ResetMagmas()
    {
        LabMagma any = null;
        for (int i = 0; i < runtimeMagmas.Count; i++)
        {
            var magma = runtimeMagmas[i];
            if (magma == null) continue;
            magma.ResetMagma(false);
            if (any == null) any = magma;
        }

        // 恢复整组后只重建一次地形合并几何。
        if (any != null) any.RebuildCollision();
    }

    void Build(Transform parent, PhysicsMaterial2D material, bool preview)
    {
        if (data == null) data = new LabLevelData();

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(parent, false);
        // 预览物体不存场景；Play 时的运行时物体本来就不会被存。
        if (preview) root.hideFlags = HideFlags.DontSaveInEditor;
        generatedRoot = root.transform;

        data.Normalize();

        // 地形、冰和岩浆共用一个合并碰撞体：相邻 1×1 方块（冰—冰、冰—地形、
        // 岩浆—岩浆、岩浆—地形）的内侧竖面会在接缝处卡住横走的玩家，也会把
        // 上表面滑行误判成侧面接触。
        var fieldGo = new GameObject("TerrainField");
        fieldGo.transform.SetParent(root.transform, false);
        var fieldBody = fieldGo.AddComponent<Rigidbody2D>();
        fieldBody.bodyType = RigidbodyType2D.Static;
        var fieldComposite = fieldGo.AddComponent<CompositeCollider2D>();
        fieldComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        fieldComposite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        fieldComposite.offsetDistance = 0f;
        if (material != null) fieldComposite.sharedMaterial = material;
        fieldGo.AddComponent<LabTerrainField>();

        Transform terrain = fieldGo.transform;
        var rects = data.MergedRects();
        for (int i = 0; i < rects.Count; i++) AddSolid(terrain, rects[i], material);

        for (int i = 0; i < data.ropes.Count; i++) AddRope(root.transform, data.ropes[i], material);
        for (int i = 0; i < data.keys.Count; i++) AddKey(root.transform, data.keys[i], preview);
        for (int i = 0; i < data.recalls.Count; i++) AddRecall(root.transform, data.recalls[i], preview);
        for (int i = 0; i < data.doors.Count; i++) AddDoor(root.transform, data.doors[i], preview);
        for (int i = 0; i < data.spikes.Count; i++) AddSpike(root.transform, data.spikes[i]);
        for (int i = 0; i < data.ices.Count; i++) AddIce(terrain, data.ices[i], material, preview);
        for (int i = 0; i < data.magmas.Count; i++) AddMagma(terrain, data.magmas[i], material, preview);
        for (int i = 0; i < data.hiddenBlocks.Count; i++) AddHiddenBlock(root.transform, data.hiddenBlocks[i]);
        for (int i = 0; i < data.repairBlocks.Count; i++)
            AddRepairBlock(terrain, data.repairBlocks[i], material);
        for (int i = 0; i < data.respawnPoints.Count; i++)
            AddRespawnPoint(root.transform, data.respawnPoints[i]);
        for (int i = 0; i < data.teleports.Count; i++)
            AddTeleport(root.transform, data.teleports[i]);
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
        resetLab = null;
    }

    [ContextMenu("重建关卡预览")]
    void RebuildPreviewMenu()
    {
        if (!Application.isPlaying) RebuildPreview();
    }
}
