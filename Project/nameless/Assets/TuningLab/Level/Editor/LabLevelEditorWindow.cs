#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tuning Lab 关卡编辑器。照着 World 的 LevelEditorWindow 做，但编辑对象换成了
/// 从 Tuning Lab 派生的关卡场景（TuningLab + LabLevel），规则、相机、玩家全部跑调试台同一套脚本。
///
/// 地形直接写 <see cref="LabLevelData"/> 的格集合，渲染/碰撞由 LabLevel 按矩形合并生成。
/// 不碰 Tilemap，也不依赖 World.unity 那一套还没对齐的规范。
/// </summary>
public sealed partial class LabLevelEditorWindow : EditorWindow
{
    enum Tool
    {
        Paint = 0,
        Erase = 1,
        Rect = 2,
        Rope = 3,
        HorizontalRope = 4,
        SpawnA = 5,
        SpawnB = 6,
        Partition = 7,
        Key = 8,
        Door = 9,
        Spike = 10,
        Ice = 11,
        Magma = 12,
        RespawnPoint = 13,
        TeleportPoint = 14,
        Marquee = 15,
        Move = 16,
        HiddenBlock = 17,
        Recall = 18,
        RepairBlock = 19
    }

    /// <summary>下一次拉选区时怎么处理当前选区。</summary>
    /// <summary>矩形填充的目标：普通地形还是隐藏方块（Issue #109）。</summary>
    enum FillTarget
    {
        Solid = 0,
        HiddenBlock = 1,
    }

    enum MarqueeMode
    {
        Replace = 0,
        Add = 1,
        Subtract = 2
    }

    /// <summary>选择工具点中的东西。</summary>
    enum PickKind
    {
        None = 0,
        Solid = 1,
        Rope = 2,
        Key = 3,
        Door = 4,
        Spike = 5,
        Ice = 6,
        Magma = 7,
        RespawnPoint = 8,
        TeleportPoint = 9,
        HiddenBlock = 10,
        Recall = 11,
        RepairBlock = 12
    }

    struct Pick
    {
        public PickKind kind;
        public int index;
    }

    const int EditMargin = 8;                 // 相机范围外还能画多少格
    const int MaxRopeLength = 32;
    const float RopeStripWidth = 6f / 16f;    // 和 LabVerticalRope 一致

    static readonly string[] ToolLabels =
    {
        "铺地形", "擦地形", "矩形", "竖绳", "横绳", "出生点 A", "出生点 B", "关卡分区", "钥匙", "门", "地刺", "冰块", "岩浆块", "重生点", "传送点",
        "选区", "选择", "隐藏方块", "召回道具", "修复方块"
    };

    static readonly string[] MarqueeModeLabels = { "重新选择", "增加选区", "删除选区" };

    static readonly string[] SpikeDirLabels = { "朝上", "朝下", "朝左", "朝右" };
    static readonly LabSpikeDir[] SpikeDirs =
    {
        LabSpikeDir.Up, LabSpikeDir.Down, LabSpikeDir.Left, LabSpikeDir.Right
    };

    LabLevel level;
    TuningLab lab;
    Scene cachedScene;

    Tool tool = Tool.Paint;
    int ropeLength = 4;
    int ropeChainId;
    Color newDoorColor = LabDoor.DefaultClosedColor;
    LabSpikeDir newSpikeDir = LabSpikeDir.Up;
    int iceGroupId = 1;
    int magmaGroupId = 1;
    int hiddenGroupId = 1;
    bool showGrid = true;
    bool showSpawns = true;
    bool showCameraFrames = true;
    Vector2 windowScroll;
    Vector2 ropeScroll;
    Vector2 keyDoorScroll;
    Vector2 spikeScroll;
    Vector2 iceScroll;
    Vector2 magmaScroll;
    Vector2 hiddenScroll;
    Vector2 repairScroll;
    Vector2 respawnScroll;
    Vector2 teleportScroll;
    Vector2 recallScroll;

    bool gesture;
    Vector2Int rectStart;
    Vector2Int rectEnd;
    Vector2Int lastCell;

    // 连续摆放：拖动时每个经过的格子都按第一次的"放/删"语义执行。
    bool dragAdds;

    // 选区：同一时间只有一个，用互不重叠的矩形块拼出任意形状；增加选区走并集。
    MarqueeMode marqueeMode = MarqueeMode.Replace;
    readonly List<RectInt> selection = new List<RectInt>();
    bool marqueeDragging;
    Vector2Int marqueeStart;
    Vector2Int marqueeEnd;

    // 选择 / 拖动
    Pick picked;
    bool movingContent;
    /// <summary>矩形工具的填充目标。默认铺普通地形，保持原有手感。</summary>
    FillTarget fillTarget = FillTarget.Solid;
    /// <summary>填充隐藏方块时用的分组 id；0 也是普通组。</summary>
    int fillGroupId;
    bool movingSelection;
    LabLevelData.ContentSelection moveCapture;
    Vector2Int moveLastCell;

    int partitionIndex = -1;
    bool movePartitionContents = true;
    string partitionMessage;
    bool partitionCreating;
    int partitionDragIndex = -1;
    Vector2Int partitionDragLast;
    Vector2Int partitionCreateStart;
    Vector2Int partitionCreateEnd;

    bool validated;
    readonly List<string> issues = new List<string>();

    [MenuItem("GameJam/Tuning Lab Level Editor")]
    public static void Open()
    {
        var window = GetWindow<LabLevelEditorWindow>("Tuning Lab Level Editor");
        window.minSize = new Vector2(340f, 500f);
        window.Show();
        window.Refresh();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        Refresh();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnHierarchyChanged()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        RefreshIfNeeded();
    }

    void OnUndoRedo()
    {
        if (level != null && !Application.isPlaying)
        {
            level.RebuildPreview();
            validated = false;
        }
        Repaint();
    }

    // ------------------------------------------------------------------
    // 场景绑定
    // ------------------------------------------------------------------

    void Refresh()
    {
        LabLevel previous = level;
        cachedScene = SceneManager.GetActiveScene();
        level = FindInScene<LabLevel>(cachedScene);
        lab = FindInScene<TuningLab>(cachedScene);

        if (level == null && lab != null)
            level = lab.GetComponent<LabLevel>();

        if (level != null && lab == null)
            lab = level.GetComponent<TuningLab>();

        // 手工搭的关卡场景可能忘了把引用连上；这里补一次，Play 时 TuningLab 也会自己找。
        if (level != null && lab != null && lab.level == null)
        {
            lab.level = level;
            EditorUtility.SetDirty(lab);
        }

        // 打开/切到关卡场景时把预览补回来（场景创建后预览是故意清掉的，不写进文件）。
        if (level != null && level != previous && !Application.isPlaying)
            level.RebuildPreview();

        int count = level != null && level.Data.partitions != null ? level.Data.partitions.Count : 0;
        if (count == 0) partitionIndex = -1;
        else if (partitionIndex >= count) partitionIndex = count - 1;

        Repaint();
    }

    void RefreshIfNeeded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active != cachedScene || level == null) Refresh();
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
