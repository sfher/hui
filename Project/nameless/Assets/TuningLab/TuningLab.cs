using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 手感调试台。一个 GameObject 建出整个世界：地面、楼梯、两队方块、相机、HUD。
/// 所有可调参数都在这里，运行时改（HUD 滑块）立刻生效。
/// 场景由菜单 GameJam → Build Tuning Lab 生成；也可以把这个组件拖进任意空场景直接 Play。
/// </summary>
[DisallowMultipleComponent]
public class TuningLab : MonoBehaviour
{
    [Header("时间")]
    [Tooltip("时间缩放，仅调试用。也可以在 Play 中直接改 Inspector，HUD 滑块与它是同一个值。")]
    [Range(0.05f, 2f)] public float timeScale = 1f;

    [Header("场地")]
    [Tooltip("地面和墙壁的长度倍数。1 = 原尺寸，4 = 放大四倍。台阶位置不变。")]
    public float roomScale = 1f;
    [Tooltip("打开则改用自由相机（跟随两人中点并按距离缩放）；像素完美相机会关掉。")]
    public bool freeCamera = true;

    [Header("重力（单位：格/秒²）")]
    [Tooltip("改它只改手感，不改任何等级门槛——门槛只认格。")]
    public float gravity = 50f;

    [Header("跳跃")]
    [Tooltip("跳高（格）。必须 >= 台阶高差，同时应当小于第一级门槛，否则站跳就充能。")]
    public float jumpCells = 2.2f;
    [Tooltip("开启后按住跳跃键会落地即跳（连跳）。关闭则必须每次都重新按。")]
    public bool holdToJump = false;
    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.08f;

    [Header("地面移动")]
    public float walkSpeed = 8f;
    public float walkAccel = 400f;
    [Tooltip("松手后的地面制动。调小 = 更滑，等级能保留更久。")]
    public float groundBrake = 30f;

    [Header("空中")]
    [Tooltip("0 = 空中完全保留动量，等级可以一直保持着飞。")]
    public float airBrake = 0f;
    [Tooltip("水平空气制动，大小 = 重力加速度 g（与竖直方向对称）。竖直没有额外制动，由重力负责。")]
    public bool horizontalAirDrag = true;

    [Header("冲刺")]
    [Tooltip("冲刺态的位移（格）。恒定值，与冲刺速度无关：时长 = 位移 / 速度。")]
    public float dashDistanceCells = 4f;
    [Tooltip("Vd1 = 比 v0 高出的落差（格）。\"反向进入\"和\"零级进入\"时用这个速度。")]
    public float dashOverV0Cells = 0.05f;
    [Tooltip("冲刺结束后锁输入的时间（秒）。")]
    public float dashLockTime = 0f;
    [Tooltip("充能从 0 充满所需时间。站在地面或碰到队友时才走，贴墙不算。")]
    public float dashCooldown = 0.8f;
    public bool groundOnlyDash = false;

    [Header("冲刺就绪反馈")]
    public bool chargeReadyFeedbackEnabled = true;
    [Tooltip("角色轮廓高亮一次的持续时间（秒）。")]
    [Min(0.04f)] public float chargeReadyFlashDuration = 0.16f;
    [Range(0.1f, 1f)] public float chargeReadyFlashAlpha = 0.85f;
    [Tooltip("充能完成时头顶喷出的粒子数量。")]
    [Range(1, 10)] public int chargeReadyParticleCount = 5;
    [Tooltip("头顶粒子的存活时间（秒）。")]
    [Min(0.1f)] public float chargeReadyParticleLife = 0.45f;

    [Header("撞击")]
    [Tooltip("红热判定的门槛取哪一级的边界（1 = v1/v2 边界）。Vp ≥ 该边界的 v² 就红热。")]
    [Range(0, 4)] public int redHotTierIndex = 1;
    [Tooltip("场景撞击的锻造门槛取哪一级（0 = v0/v1 边界）。到不了这个速度什么都不发生；到了就砸那一条边，够不到红热门槛只砸不加热。")]
    [Range(0, 4)] public int forgeTierIndex = 0;
    public bool verticalHitEnabled = true;

    [Header("冰块")]
    [Tooltip("冰块整组破坏的碰撞等级（2 = v2）。只算碰撞前沿接触法线撞进去的分量，站在冰面上横向滑行不算。")]
    [Range(0, 4)] public int iceBreakTierIndex = 2;

    [Header("传送门动画")]
    [Min(0f)] public float teleportHighlightSeconds = 0.35f;
    [Min(0.1f)] public float teleportBeamSpeed = 48f;
    [Min(0f)] public float teleportArrivalFadeSeconds = 0.15f;
    [Min(0f)] public float teleportArrivalHoldSeconds = 0.20f;
    [Min(0f)] public float teleportBurstSeconds = 0.15f;

    [Header("隐藏方块")]
    [Tooltip("隐藏方块被任一玩家完全进入后整组保持的不透明度（0.5 = 半透明）。只在揭示那一刻取值，之后改不影响已揭示的组。")]
    [Range(0f, 1f)] public float hiddenRevealedAlpha = 0.5f;

    [Header("灵体态")]
    [Tooltip("一次灵体状态存在的时间（秒）。从四条边全碎那一刻开始计，中途不暂停，耗尽即死并吐出一把临时钥匙。")]
    [Min(0.01f)] public float spiritDuration = 6f;

    /// <summary>关卡里没有任何重生点时的死亡后果。</summary>
    public enum DeathFallback
    {
        /// <summary>沿用实验台行为：把死亡的玩家直接销毁。</summary>
        DestroyPlayer = 0,
        /// <summary>重开本关：两人回出生点，门重新关上、被捡走的钥匙长回来。</summary>
        RespawnAtSpawn = 1
    }

    [Header("死亡")]
    [Tooltip("两人都死亡、且本关一个重生点都没有时才生效：销毁两人（原实验台行为）还是重开本关回出生点。\n" +
             "关卡里有重生点时一律强制触发重生点，两人以普通状态复活。\n" +
             "带地刺的正式关卡要用 RespawnAtSpawn，否则第一脚踩空就把人删了。")]
    public DeathFallback deathWhenNoCheckpoint = DeathFallback.DestroyPlayer;

    [Header("地图重置")]
    [Tooltip("范围重置半径（格）：复活/传送只恢复重生点周围这个半径内的碎冰、碎岩浆和门。\n" +
             "调大更接近整图重置；范围内没有任何破坏时几乎零开销。")]
    [Min(1f)] public float mapResetRadius = 40f;

    [Header("等级（等距）")]
    [Tooltip("第一道门槛的落差（格）= 零级的最大速度。必须大于跳高，否则站跳就越级。")]
    public float tierFirstCells = 4f;
    [Tooltip("每级之间的落差（格）。4 = 4/8/12/16 格。")]
    public float tierSpacingCells = 4f;
    [Tooltip("等级数（不含零级）。6 → 零级..六级。碰撞只读到顶档：更快的速度仍按顶档算。")]
    [Range(1, 6)] public int tierCount = 6;
    [Tooltip("玻璃大炮 / L 形发射的增益格数。v' = √(Vb² + 2·g·这个数)。2 个间距起步，越大射得越猛。")]
    public float cannonBoostCells = 8f;
    [Tooltip("幻影 L 形的进膛门槛（竖直来速）。低于这个速度撞上去不转入，避免走路蹭一下就触发。")]
    public float converterEntrySpeed = 12f;

    [Header("速度上限")]
    [Tooltip("顶档上限 = 顶档门槛 + 这个格数。下落和冰面加速共用同一个值；必须 > 0，否则速度永远差一点进不了顶档。默认 4 = 一个等级间距。")]
    [Min(0.01f)] public float capOverTopCells = 4f;

    // 1 格 = 16 px，原生 384×216 = 24 × 13.5 格。改这个等于重做所有关卡尺寸，别乱动。
    public const int CellPixels = 16;
    public const int RefWidth = 384;
    public const int RefHeight = 216;

    /// <summary>自由相机默认的最小正交尺寸（格，半高）：216 / (2×16) = 6.75，即 13.5 格高。</summary>
    public const float DefaultCameraMinSize = RefHeight / (2f * CellPixels);
    /// <summary>自由相机默认的最大正交尺寸（格，半高）：4 倍最小 = 27，即 54 格高。</summary>
    public const float DefaultCameraMaxSize = DefaultCameraMinSize * 4f;

    /// <summary>B 不在任何关卡分区时使用的“无夹具”范围；足够大，等价于不 clamp。</summary>
    public static readonly Rect NoCameraBounds = Rect.MinMaxRect(-100000f, -100000f, 100000f, 100000f);

    /// <summary>A（大框）：14×14，墙厚 2，内腔 10。留 1 像素余量才能进 1 格宽的通道。</summary>
    public const int A_Pixels = 14;
    /// <summary>B（小块）：10×10，正好填满 A 的内腔（14 - 2×2）。</summary>
    public const int B_Pixels = 10;

    [Header("像素")]
    [Tooltip("P 键切换：叠一层 1 像素的棋盘，用来看最终的像素密度。")]
    public bool showPixelGrid = false;

    [Tooltip("Tab 键切换：相机视野内的整数格网格线。")]
    public bool showGrid = true;

    [Header("死亡反馈")]
    [Tooltip("任一玩家死亡时，屏幕闪一次红色边缘渐晕。")]
    public bool deathFlashEnabled = true;
    [Tooltip("闪烁持续时间（秒）。")]
    public float deathFlashDuration = 0.55f;
    [Tooltip("闪烁期内亮度脉动的次数。")]
    [Range(1f, 8f)] public float deathFlashPulses = 3f;
    [Tooltip("最贴近屏幕边缘处的红色带深度 = 屏高 × 这个比例；往屏幕中心平滑衰减到无。")]
    [Range(0.05f, 0.5f)] public float deathFlashEdgeDepth = 0.18f;
    [Tooltip("满亮时的红色。")]
    public Color deathFlashColor = new Color(1f, 0.25f, 0.25f, 0.9f);

    [Header("速度反馈")]
    [Tooltip("残影和拖尾从第几档开始出现（合速度档，含竖直分量）。走路恒为 v0，默认 v3 起。")]
    [Range(1, 6)] public int speedFxStartTier = 3;
    [Tooltip("起始档同时存在的残影数。")]
    [Range(0, 12)] public int ghostCountTier3 = 3;
    [Tooltip("起始档残影初始不透明度。")]
    [Range(0f, 1f)] public float ghostAlphaTier3 = 0.5f;
    [Tooltip("起始档残影存活（秒）。")]
    public float ghostLifeTier3 = 0.6f;
    [Tooltip("顶档同时存在的残影数（tierCount=6 时即 V6）。")]
    [Range(0, 12)] public int ghostCountTopTier = 5;
    [Tooltip("顶档残影初始不透明度。")]
    [Range(0f, 1f)] public float ghostAlphaTopTier = 0.7f;
    [Tooltip("顶档残影存活（秒）。")]
    public float ghostLifeTopTier = 0.8f;
    [Tooltip("拖尾亮点同屏上限。")]
    [Range(0, 128)] public int trailCap = 64;
    [Tooltip("拖尾亮点存活（秒）：后段才淡出，像画笔留下的笔触。")]
    public float trailLife = 2f;
    [Tooltip("每隔几个物理步喷一个拖尾亮点（拉开成速度线，不糊在屁股上）。")]
    [Range(1, 6)] public int trailEverySteps = 2;
    [Tooltip("拖尾亮点 V3 初始不透明度。")]
    [Range(0f, 1f)] public float trailAlphaTier3 = 0.85f;
    [Tooltip("拖尾亮点顶档（V6）初始不透明度。")]
    [Range(0f, 1f)] public float trailAlphaTopTier = 1f;

    public LabPlayer PlayerA { get; private set; }
    public LabPlayer PlayerB { get; private set; }
    public Camera Cam { get; private set; }
    public LabHud Hud { get; private set; }
    public LabAudio Sound { get; private set; }
    public LabSettingsUI SettingsUI { get; private set; }

    /// <summary>
    /// 当前生效的按键绑定。默认是 Issue #94 定案的两套；设置界面改完之后
    /// 通过 <see cref="ApplyBindings"/> 整体换掉，玩家每帧从表里读，不需要额外通知。
    /// </summary>
    public LabBindings Bindings { get; private set; } = new LabBindings();

    /// <summary>换一套按键绑定。传 null 表示回到默认。</summary>
    public void ApplyBindings(LabBindings bindings)
    {
        Bindings = bindings != null ? bindings.Clone() : new LabBindings();
    }
    public LabRespawnPoint ActiveRespawnPoint { get; private set; }

    /// <summary>设置界面打开时所有玩法冻结；关闭后恢复暂停前的 timeScale。</summary>
    public bool Paused { get; private set; }
    float pausedTimeScale = 1f;
    public event System.Action MapReset;
    /// <summary>任一玩家死亡后广播（唯一必达点在 NotifyPlayerDied）；现阶段只有边框闪红订阅。</summary>
    public event System.Action<LabPlayer> PlayerDied;

    float deathFlashLeft;
    public bool TeleportBusy => teleportPhase != TeleportPhase.None;
    public bool TeleportSelecting => teleportPhase == TeleportPhase.Selecting;
    public bool TeleportCameraLocked => TeleportBusy;

    /// <summary>召回能力：任一玩家拾取道具后全队永久解锁，地图重置不清空。</summary>
    public bool RecallUnlocked { get; private set; }
    public void UnlockRecall() => RecallUnlocked = true;

    /// <summary>摩擦 0 的共用材质；幻影等运行时实体也用它。</summary>
    internal PhysicsMaterial2D SlickMaterial => slick;
    /// <summary>选择界面里当前选中的终点；没有在选终点时为 null。</summary>
    public LabTeleportPoint TeleportTarget =>
        teleportSelectionIndex >= 0 && teleportSelectionIndex < teleportTargets.Count
            ? teleportTargets[teleportSelectionIndex]
            : null;
    /// <summary>当前可选的终点数量（不含进入的那个传送点）。</summary>
    public int TeleportTargetCount => teleportTargets.Count;
    public int MarkedTeleportPointCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < teleports.Count; i++)
                if (teleports[i] != null && teleports[i].IsMarked) count++;
            return count;
        }
    }
    /// <summary>已注册的传送点数量；用来区分"没生成"和"生成了但没注册"。</summary>
    public int TeleportPointCount => teleports.Count;

    enum TeleportPhase
    {
        None,
        Departing,
        Selecting,
        Arriving,
        Cancelling
    }

    readonly List<LabTeleportPoint> teleports = new List<LabTeleportPoint>();
    readonly List<LabTeleportPoint> teleportTargets = new List<LabTeleportPoint>();
    LabTeleportPoint teleportSource;
    LabTeleportAnimation teleportAnimation;
    Coroutine teleportRoutine;
    TeleportPhase teleportPhase;
    Vector2 teleportSourcePosition;
    Vector2 teleportVelocityA;
    Vector2 teleportVelocityB;
    LabPlayer.FormState teleportFormA;
    LabPlayer.FormState teleportFormB;
    int teleportSelectionIndex;
    const float TeleportCameraMoveSeconds = 0.25f;
    readonly List<LabRespawnPoint> respawnPoints = new List<LabRespawnPoint>();
    bool teamDeathPending;
    LabPlayer teamDeathPlayer;
    readonly Collider2D[] recallHits = new Collider2D[16];   // TryRecall 头顶探测的 NonAlloc 缓冲

    // ---- 派生量：所有"速度"都由格算出来，改重力自动跟着走 ----
    public float FixedDt => Time.fixedDeltaTime;
    public float JumpVelocity => TierMath.JumpVelocity(jumpCells, gravity, Time.fixedDeltaTime);
    /// <summary>零级的速度上限，也就是 v0。</summary>
    public float V0Speed => TierMath.SpeedForCells(Boundary(0), gravity);
    /// <summary>Vd1：略大于 v0 的冲刺速度（反向/零级进入时用）。</summary>
    public float Vd1 => TierMath.SpeedForCells(Boundary(0) + Mathf.Max(0.01f, dashOverV0Cells), gravity);
    /// <summary>冲刺态的位移（格），恒定。</summary>
    public float DashDistance => Mathf.Max(0f, dashDistanceCells);

    /// <summary>红热门槛（速度）。双人撞击比的是 Vp = 速度平方，用时再平方。</summary>
    public float RedHotSpeed => SpeedForCells(Boundary(redHotTierIndex));
    /// <summary>锻造门槛（速度）：场景撞击的下限，永不高于红热门槛。</summary>
    public float ForgeSpeed => SpeedForCells(Boundary(Mathf.Min(forgeTierIndex, redHotTierIndex)));
    /// <summary>冰块整组破坏的目标等级（0..MaxTier）。</summary>
    public int IceBreakTier => Mathf.Clamp(iceBreakTierIndex, 0, MaxTier);
    /// <summary>冰块整组破坏的碰撞门槛（该等级的下边界速度）。</summary>
    public float IceBreakSpeed => SpeedForCells(Boundary(Mathf.Max(0, IceBreakTier - 1)));
    /// <summary>岩浆块整组破坏的碰撞门槛：固定三级（该等级的下边界速度）。</summary>
    public float MagmaBreakSpeed => SpeedForCells(Boundary(Mathf.Max(0, LabMagma.BreakTier - 1)));
    /// <summary>
    /// 顶档上限：下落速度和冰面加速都封在这里（默认 6 级 → 24 + 4 = 28 格 ≈ 52.92）。
    /// 大炮、绳子反弹、双人互换是冲量，可以超过它；超过后等级读数仍是顶档。
    /// </summary>
    public float TopTierSpeed => SpeedForCells(Boundary(MaxTier - 1) + Mathf.Max(0.01f, capOverTopCells));
    /// <summary>
    /// 大炮发射的增量 = cannonBoostCells 格的 v²（= 2·g·格数）。v' = √(Vb² + 这个数)。
    /// 公式保持"平方和再开方"不变，只把增益格数做成可调参数。
    /// </summary>
    public float CannonBoostV2 => 2f * gravity * Mathf.Max(0.01f, cannonBoostCells);

    // ---- 等级门槛：等距生成，不再手填数组 ----
    float[] cachedBounds;

    /// <summary>第 i 项 = 第 i 级与第 i+1 级之间的落差门槛（格）。</summary>
    public float[] Bounds
    {
        get
        {
            float spacing = Mathf.Max(0.01f, tierSpacingCells);
            int n = Mathf.Clamp(tierCount, 1, 8);

            bool stale = cachedBounds == null || cachedBounds.Length != n;
            if (!stale)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!Mathf.Approximately(cachedBounds[i], tierFirstCells + i * spacing))
                    {
                        stale = true;
                        break;
                    }
                }
            }

            if (stale)
            {
                cachedBounds = new float[n];
                for (int i = 0; i < n; i++)
                    cachedBounds[i] = tierFirstCells + i * spacing;
            }
            return cachedBounds;
        }
    }

    /// <summary>等级读数上限：碰撞只认到这一级，更快的速度不会解锁更强的效果。</summary>
    public int MaxTier => Bounds.Length;
    public float Boundary(int index) => index >= 0 && index < Bounds.Length ? Bounds[index] : 0f;

    /// <summary>Rigidbody2D.gravityScale 的换算（Physics2D.gravity 固定 -9.81）。</summary>
    public float GravityScale => gravity / 9.81f;

    public float SpeedForCells(float cells) => TierMath.SpeedForCells(cells, gravity);
    public float CellsForSpeed(float speed) => TierMath.CellsForSpeed(speed, gravity);
    public int TierOfSpeed(float speed) => TierMath.TierForCells(CellsForSpeed(speed), Bounds);

    /// <summary>绳子三级以上反弹：保留入射速度，并增加一个等级间距。</summary>
    public float RopeBounceSpeed(float incomingSpeed)
    {
        float incomingCells = CellsForSpeed(Mathf.Abs(incomingSpeed));
        return SpeedForCells(incomingCells + tierSpacingCells);
    }
    public int TierOfCells(float cells) => TierMath.TierForCells(cells, Bounds);

    // ---- 双人碰撞事件：其它机制挂这里 ----
    public struct HitInfo
    {
        public LabPlayer hitter;
        public LabPlayer victim;
        public Vector2 normal;      // 撞击者 → 被撞者
        public bool horizontal;
        public int tierBefore;
        public int tierAfter;
        public float speed;         // 加速逻辑之后，快的那一方的新速度
        public float vp;            // 红热判定用的 Vp（速度平方的和/差）
        public bool redHot;
    }

    public event System.Action<HitInfo> PlayerHit;
    public void RaisePlayerHit(HitInfo info) { if (PlayerHit != null) PlayerHit(info); }

    // ---- 像素读数 ----
    /// <summary>当前屏幕上竖向可见多少格。</summary>
    public float VisibleCellsY => Cam != null ? Cam.orthographicSize * 2f : 0f;
    public float VisibleCellsX => Cam != null ? VisibleCellsY * Cam.aspect : 0f;
    /// <summary>一个美术像素在屏幕上占几个屏幕像素。是整数才对得齐。</summary>
    public float TexelScale
    {
        get
        {
            float h = VisibleCellsY * CellPixels;
            return h > 0.01f ? Screen.height / h : 0f;
        }
    }

    static Sprite whiteSprite;
    PhysicsMaterial2D slick;
    GameObject pixelGridGo;

    public const float BaselineWallHalfWidth = 13f;
    public const float BaselineFloorY = -1f;
    public const float BaselineCeilingY = 16f;
    public const float RopeCenterX = 9f;
    public const float RopeCenterY = 2f;
    public const int RopeCells = 4;

    public static readonly Vector2 SpawnAFeet = new Vector2(8f, 0f);
    public static readonly Vector2 SpawnBFeet = new Vector2(10f, 0f);

    public static readonly Color SolidColor = new Color(0.24f, 0.26f, 0.32f);
    public static readonly Color ColorA = new Color(0.36f, 0.62f, 1f);
    public static readonly Color ColorB = new Color(1f, 0.45f, 0.4f);
    static readonly Color GridColor = new Color(1f, 1f, 1f, 0.06f);

    [Tooltip("勾上 = 运行时自动搭整个场地；烘焙成场景后应当关掉，否则会重复生成。")]
    public bool autoBuild = true;

    [Header("关卡")]
    [Tooltip("有 LabLevel 就按关卡数据搭场地；没有就用调试台内置房间。")]
    public LabLevel level;

    [Header("相机默认")]
    [Tooltip("默认相机最小正交尺寸（格，半高）。没有 LabLevel 时使用；有 LabLevel 时用关卡编辑器的相机跟随尺寸。")]
    public float defaultCameraMinSize = DefaultCameraMinSize;
    [Tooltip("默认相机最大正交尺寸（格，半高）。没有 LabLevel 时使用；有 LabLevel 时用关卡编辑器的相机跟随尺寸。")]
    public float defaultCameraMaxSize = DefaultCameraMaxSize;
    [Tooltip("默认相机 clamp 范围（世界坐标，1 格 = 1 单位）。没有 LabLevel 时使用；有关卡分区但 B 出界时不夹相机。")]
    public Rect defaultCameraBounds = Rect.MinMaxRect(-13f, -1f, 13f, 16f);
    [Tooltip("分区切换时 bounds / size 的混合速度；0 = 立即切换。")]
    public float cameraFrameBlend = 8f;

    void Awake()
    {
        if (level == null) level = GetComponent<LabLevel>();
        if (autoBuild) BuildAll();
        // 已经烘焙好的旧关卡不会再跑 BuildAll；有 HUD 就说明是完整调试台/关卡，补挂设置界面。
        else if (GetComponent<LabHud>() != null) EnsureSettingsUI();
    }

    void OnValidate()
    {
        NormalizeSizes(ref defaultCameraMinSize, ref defaultCameraMaxSize);

        defaultCameraBounds = NormalizeRect(defaultCameraBounds);
        if (defaultCameraBounds.width <= 0f || defaultCameraBounds.height <= 0f)
            defaultCameraBounds = BaselineBounds;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        DrawGizmoRect(defaultCameraBounds);

        if (level == null) return;
        var partitions = level.Data.partitions;
        if (partitions == null) return;

        for (int i = 0; i < partitions.Count; i++)
        {
            var partition = partitions[i];
            if (partition == null) continue;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            DrawGizmoRect(LabLevelData.ToRect(partition.bounds));
        }
    }

    static void DrawGizmoRect(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Gizmos.DrawWireCube(
            new Vector3(rect.center.x, rect.center.y, 0f),
            new Vector3(rect.width, rect.height, 0.01f));
    }

    /// <summary>
    /// 一次性搭出整个场地。Awake 里调用，也可以被编辑器烘焙流程直接调用，
    /// 生成真实的 GameObject 之后存成场景，就能在编辑器里直接改。
    /// </summary>
    public void BuildAll()
    {
        if (level == null) level = GetComponent<LabLevel>();
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Time.timeScale = timeScale;

        BuildRoom();
        BuildPlayers();
        BuildCamera();
        BuildPixelGrid();

        if (Hud == null) Hud = gameObject.GetComponent<LabHud>();
        if (Hud == null) Hud = gameObject.AddComponent<LabHud>();
        Hud.Bind(this);

        // 音效和 HUD 同一个路子：运行时挂上来，不进场景、不进 prefab。
        if (Sound == null) Sound = gameObject.GetComponent<LabAudio>();
        if (Sound == null) Sound = gameObject.AddComponent<LabAudio>();
        Sound.Bind(this);

        // 设置界面同样运行时挂载；编辑器烘焙场景时只把组件留在场景里，不在这里应用窗口设置。
        EnsureSettingsUI();
    }

    void EnsureSettingsUI()
    {
        if (SettingsUI == null) SettingsUI = gameObject.GetComponent<LabSettingsUI>();
        if (SettingsUI == null) SettingsUI = gameObject.AddComponent<LabSettingsUI>();
        if (Application.isPlaying) SettingsUI.Bind(this);
    }

    /// <summary>暂停/恢复由设置界面调用；只改 timescale，不销毁或禁用任何玩法对象。</summary>
    public void SetPaused(bool paused)
    {
        if (Paused == paused) return;
        Paused = paused;

        if (paused)
        {
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = pausedTimeScale > 0f ? pausedTimeScale : timeScale;
        }
    }

    void OnDestroy()
    {
        Paused = false;
        AbortTeleport();
        Time.timeScale = 1f;
        LabPhantom.ClearAll();
    }

    void Update()
    {
        ConsumePendingContinue();
        if (Paused) return;
        ResolvePendingTeamDeath();
        if (deathFlashLeft > 0f) deathFlashLeft = Mathf.Max(0f, deathFlashLeft - Time.deltaTime);

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        UpdateTeleportSelection(kb);
        if (TeleportBusy) return;
        if (kb.rKey.wasPressedThisFrame) ResetPlayers();
        if (kb.pKey.wasPressedThisFrame) showPixelGrid = !showPixelGrid;
        if (kb.tabKey.wasPressedThisFrame) showGrid = !showGrid;
        if (kb.f1Key.wasPressedThisFrame && PlayerA != null) PlayerA.DebugShatterNextEdge();
        if (kb.f2Key.wasPressedThisFrame && PlayerA != null) PlayerA.ResetEdges();
    }

    void LateUpdate()
    {
        UpdateGrid();

        if (pixelGridGo == null || Cam == null) return;
        pixelGridGo.SetActive(showPixelGrid);
        float h = Cam.orthographicSize * 2f;
        float w = h * Cam.aspect;
        pixelGridGo.transform.position = Cam.transform.position;
        pixelGridGo.transform.localScale = new Vector3(w / RefWidth, h / RefHeight, 1f);
    }

    public void ResetPlayers()
    {
        // 激活过重生点就整队回那个点（普通状态 + 重置地图），而不是回出生点。
        if (ActiveRespawnPoint != null)
        {
            ActiveRespawnPoint.ForceRespawn(this);
            return;
        }
        if (PlayerA != null)
        {
            Vector2 feet = level != null ? level.Data.spawnA : SpawnAFeet;
            PlayerA.ReviveAt(new Vector2(feet.x, feet.y + PlayerA.HalfHeight));
        }
        if (PlayerB != null)
        {
            Vector2 feet = level != null ? level.Data.spawnB : SpawnBFeet;
            PlayerB.ReviveAt(new Vector2(feet.x, feet.y + PlayerB.HalfHeight));
        }
    }

    public void RegisterRespawnPoint(LabRespawnPoint point)
    {
        if (point != null && !respawnPoints.Contains(point))
            respawnPoints.Add(point);
    }

    /// <summary>
    /// 主页“读取存档”进关后的第一帧：把队伍强制复活到存档重生点（普通状态、清钥匙、范围重置）。
    /// 此时所有重生点的 Start 注册已完成；点找不到（关卡改过）就留在出生点并告警。
    /// </summary>
    void ConsumePendingContinue()
    {
        Vector2? pending = LabCheckpointStore.PendingContinue;
        if (pending == null) return;
        LabCheckpointStore.PendingContinue = null;

        for (int i = respawnPoints.Count - 1; i >= 0; i--)
            if (respawnPoints[i] == null) respawnPoints.RemoveAt(i);

        for (int i = 0; i < respawnPoints.Count; i++)
        {
            LabRespawnPoint point = respawnPoints[i];
            if (((Vector2)point.transform.position - pending.Value).sqrMagnitude > 1e-4f) continue;
            ActivateRespawnPoint(point);
            point.ForceRespawn(this);
            return;
        }

        Debug.LogWarning($"TuningLab: 存档里的重生点 {pending.Value} 在当前场景不存在，队伍留在出生点。");
    }

    public void ActivateRespawnPoint(LabRespawnPoint point)
    {
        if (point == null) return;
        for (int i = respawnPoints.Count - 1; i >= 0; i--)
        {
            if (respawnPoints[i] == null)
            {
                respawnPoints.RemoveAt(i);
                continue;
            }

            respawnPoints[i].SetActive(respawnPoints[i] == point);
        }

        if (!respawnPoints.Contains(point))
            respawnPoints.Add(point);
        bool newlyActivated = ActiveRespawnPoint != point;
        ActiveRespawnPoint = point;
        // 激活即自动存档（踩点或双人死亡强制触发）；反复死在同一个点不重复写盘。
        if (newlyActivated)
            LabCheckpointStore.Save(SceneManager.GetActiveScene().name, point.transform.position);
    }

    /// <summary>
    /// 任意一名玩家死亡后由 <see cref="LabPlayer.Kill"/> 调用。
    /// 单人只是隐藏等待；两人都死亡才结算：强制触发一个重生点（优先最后激活的点，
    /// 没激活过就选离最后一次死亡位置最近的点），两人以普通状态双人重生；
    /// 关卡里没有重生点时才按 <see cref="deathWhenNoCheckpoint"/> 处理。
    /// 结算本身推迟到下一次 <see cref="Update"/>：死亡来自物理接触回调，在同一物理步里
    /// 立刻复活会被这个步剩下的接触回调再打一次，出现两人都死却只有一个活下来。
    /// </summary>
    public void NotifyPlayerDied(LabPlayer deadPlayer)
    {
        TriggerDeathFlash();
        PlayerDied?.Invoke(deadPlayer);
        if (TeleportBusy) AbortTeleport();

        // 只创建了一个玩家（测试/调试）时没有“队友”，直接按单人结算。
        if (PlayerA != null || PlayerB != null)
        {
            if (PlayerA != null && PlayerA.IsAlive) return;
            if (PlayerB != null && PlayerB.IsAlive) return;
        }

        teamDeathPending = true;
        teamDeathPlayer = deadPlayer;
    }

    /// <summary>物理回调和 FixedUpdate 都跑完之后再结算双人重生，避免在回调里复活。</summary>
    void ResolvePendingTeamDeath()
    {
        if (!teamDeathPending) return;
        teamDeathPending = false;
        LabPlayer deadPlayer = teamDeathPlayer;
        teamDeathPlayer = null;

        // 等待期间有人被别的路径救活（例如队友踩进重生点）就不再强制重生。
        if (PlayerA != null && PlayerA.IsAlive) return;
        if (PlayerB != null && PlayerB.IsAlive) return;

        ResolveTeamDeath(deadPlayer);
    }

    void ResolveTeamDeath(LabPlayer deadPlayer)
    {
        // 两人都死亡一律强制触发：有激活的点回激活点，没有就回离最后一次死亡最近的点。
        LabRespawnPoint point = ActiveRespawnPoint != null
            ? ActiveRespawnPoint
            : NearestRespawnPoint(TeamDeathPosition(deadPlayer));

        if (point != null)
        {
            ActivateRespawnPoint(point);
            point.ForceRespawn(this);
            return;
        }

        if (deathWhenNoCheckpoint == DeathFallback.RespawnAtSpawn)
        {
            ResetMap(level != null ? level.Data.spawnA : SpawnAFeet);
            ResetPlayers();
            return;
        }

        DestroyDeadPlayers(deadPlayer);
    }

    /// <summary>强制触发的参考位置：最后一次死亡的位置；没有死亡位置时回落到存活玩家。</summary>
    Vector2 TeamDeathPosition(LabPlayer deadPlayer)
    {
        if (deadPlayer != null) return deadPlayer.transform.position;
        if (PlayerA != null) return PlayerA.transform.position;
        if (PlayerB != null) return PlayerB.transform.position;
        return Vector2.zero;
    }

    /// <summary>离参考位置最近的重生点；同距离取注册顺序靠前的那个，保证结果确定。</summary>
    LabRespawnPoint NearestRespawnPoint(Vector2 position)
    {
        for (int i = respawnPoints.Count - 1; i >= 0; i--)
            if (respawnPoints[i] == null) respawnPoints.RemoveAt(i);

        LabRespawnPoint nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < respawnPoints.Count; i++)
        {
            var point = respawnPoints[i];
            float distance = ((Vector2)point.transform.position - position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            nearest = point;
        }
        return nearest;
    }

    void DestroyDeadPlayers(LabPlayer deadPlayer)
    {
        if (PlayerA != null && PlayerA.IsDead) Destroy(PlayerA.gameObject);
        if (PlayerB != null && PlayerB.IsDead) Destroy(PlayerB.gameObject);

        // 场景里手工摆的玩家不在 PlayerA/B 上，也要按死亡处理，不能漏掉。
        if (deadPlayer != null && deadPlayer != PlayerA && deadPlayer != PlayerB)
            Destroy(deadPlayer.gameObject);
    }

    /// <summary>
    /// 全图重置：编辑器自检和测试用。玩法里的复活/传送走 <see cref="ResetMap(Vector2?)"/>
    /// 的范围重置，避免大地图每次重生都恢复全部冰块岩浆。
    /// </summary>
    public void ResetMap() => ResetMap(null);

    /// <summary>
    /// 地图重置。传 center 时只恢复中心附近 <see cref="mapResetRadius"/> 格内的物件
    /// （冰/岩浆按组完整恢复）；传 null 恢复全图。幻影和临时钥匙一律全清。
    /// </summary>
    public void ResetMap(Vector2? center)
    {
        mapResetCenter = center;
        LabPhantom.ClearAll();
        LabKey.ClearTemporaries();
        try
        {
            MapReset?.Invoke();
        }
        finally
        {
            mapResetCenter = null;
        }
    }

    /// <summary>本次重置是否覆盖这个位置；全图重置（无中心）恒为 true。</summary>
    public bool InMapResetRange(Vector2 position)
        => mapResetCenter == null
           || (position - mapResetCenter.Value).sqrMagnitude <= mapResetRadius * mapResetRadius;

    Vector2? mapResetCenter;

    /// <summary>
    /// 召回：施法者头顶有 1 格空位时，把另一名玩家传送到头顶，并在其旧位置留下幻影。
    /// 能力是全队、永久的；判定集中在这里，输入层只负责读键。
    ///
    /// 顺序按 Issue #76 定案：先判断有没有幻影已经被用过（用过就整次召回无效），
    /// 再生成新幻影，最后才销毁旧幻影 —— 旧幻影的光桥要参与新幻影的生成判定，
    /// 所以不能先拆桥再铺桥。
    /// </summary>
    public bool TryRecall(LabPlayer caster)
    {
        if (!RecallUnlocked || caster == null) return false;

        LabPlayer recalled = caster.other;
        if (recalled == null || !caster.IsAlive || !recalled.IsAlive) return false;
        if (caster.Suspended || recalled.Suspended) return false;

        // 1. 已经有幻影被消耗/使用过（大炮发射或 L 转向）：本次召回无效。
        if (LabPhantom.AnyUsed) return false;

        Bounds body = caster.BodyBounds;
        float headTop = body.max.y;
        Vector2 probeCenter = new Vector2(caster.transform.position.x, headTop + 0.5f);
        int hitCount = Physics2D.OverlapBox(probeCenter, Vector2.one, 0f, ContactFilter2D.noFilter, recallHits);
        var casterCollider = caster.GetComponent<Collider2D>();
        for (int i = 0; i < hitCount; i++)
        {
            var hit = recallHits[i];
            if (hit == null || hit == casterCollider) continue;
            if (hit.isTrigger) continue;
            return false;      // 头顶被实体占住：拒绝，不生成幻影
        }

        Vector2 before = recalled.transform.position;
        int spiritMask = recalled.SpiritMask;
        int pixels = recalled.pixels;
        bool framed = recalled.canBeRedHot;

        Vector2 target = new Vector2(
            caster.transform.position.x,
            headTop + recalled.HalfHeight + 0.01f);
        // 保留被召回者的碎边/形态：普通 Teleport 会 ResetEdges。
        recalled.TeleportPreservingState(target, Vector2.zero);

        var cell = new Vector2Int(
            Mathf.FloorToInt(before.x),
            Mathf.FloorToInt(before.y));

        // 2. 先生成新幻影——淘汰旧幻影放在它自己的生成逻辑里之后，见 Spawn。
        LabPhantom.Spawn(this, recalled, caster, cell, spiritMask, pixels, framed, deferEviction: true);

        // 3. 新幻影铺完桥之后再销毁旧的：旧幻影的光桥此时仍参与过生成判定。
        LabPhantom.EvictOverflow();
        return true;
    }

    public void RegisterTeleport(LabTeleportPoint point)
    {
        if (point != null && !teleports.Contains(point)) teleports.Add(point);
    }

    public void UnregisterTeleport(LabTeleportPoint point)
    {
        if (point == null) return;
        teleports.Remove(point);
        teleportTargets.Remove(point);
        if (TeleportSelecting && (teleportSource == point || !IsTeleportTargetValid()))
            CancelTeleport();
    }

    public void MarkTeleportPoint(LabTeleportPoint point)
    {
        if (point == null) return;
        RegisterTeleport(point);
        point.SetMarked(true);
    }

    bool IsTeleportTargetValid()
    {
        return TeleportTarget != null && TeleportTarget.IsMarked;
    }

    /// <summary>
    /// 已标记传送点达到两个后先播放离场动画，再进入终点选择。
    /// 候选不足或双人不可用时什么都不做，避免冻结玩家或把自己传送到自己。
    /// </summary>
    public void EnterTeleportSelection(LabTeleportPoint point)
    {
        if (point == null || TeleportBusy || !point.IsMarked || !CanAnimateTeleportPlayers()) return;

        teleports.RemoveAll(candidate => candidate == null);
        teleportTargets.Clear();
        for (int i = 0; i < teleports.Count; i++)
        {
            var candidate = teleports[i];
            if (candidate == point || !candidate.IsMarked) continue;
            teleportTargets.Add(candidate);
        }
        teleportTargets.Sort((left, right) =>
        {
            int x = left.transform.position.x.CompareTo(right.transform.position.x);
            return x != 0 ? x : left.transform.position.y.CompareTo(right.transform.position.y);
        });
        if (teleportTargets.Count == 0) return;

        teleportSource = point;
        teleportSourcePosition = point.transform.position;
        teleportSelectionIndex = 0;
        teleportVelocityA = PlayerA.Velocity;
        teleportVelocityB = PlayerB.Velocity;
        teleportFormA = PlayerA.CaptureForm();
        teleportFormB = PlayerB.CaptureForm();
        teleportPhase = TeleportPhase.Departing;
        LabAudio.Play(LabAudio.Cue.PortalEnter);

        PlayerA.FreezeForTeleportSelection();
        PlayerB.FreezeForTeleportSelection();
        if (teleportAnimation == null) teleportAnimation = new LabTeleportAnimation(this);
        Cam?.GetComponent<LabCamera>()?.SnapFocus(teleportSourcePosition);
        teleportRoutine = StartCoroutine(RunTeleportDeparture());
    }

    bool CanAnimateTeleportPlayers()
    {
        return PlayerA != null && PlayerB != null
               && PlayerA.IsAlive && PlayerB.IsAlive
               && !PlayerA.Suspended && !PlayerB.Suspended;
    }

    IEnumerator RunTeleportDeparture()
    {
        yield return teleportAnimation.PlayDeparture(PlayerA, PlayerB, Cam);
        teleportRoutine = null;
        if (teleportPhase != TeleportPhase.Departing) yield break;

        if (!IsTeleportTargetValid())
        {
            BeginTeleportCancellation();
            yield break;
        }

        teleportPhase = TeleportPhase.Selecting;
        teleportSource?.SetHighlighted(true);
        TeleportTarget?.SetHighlighted(true);
        if (Cam != null && TeleportTarget != null)
            Cam.GetComponent<LabCamera>()?.FocusOn(TeleportTarget.transform.position, TeleportCameraMoveSeconds);
    }

    IEnumerator RunTeleportArrival(Vector2 position, bool restoreVelocity)
    {
        LabAudio.Play(LabAudio.Cue.PortalEnter);
        LabCamera labCamera = Cam != null ? Cam.GetComponent<LabCamera>() : null;
        if (labCamera != null && !labCamera.IsFocusing
            && Vector2.Distance(Cam.transform.position, position) > 0.001f)
            labCamera.FocusOn(position, TeleportCameraMoveSeconds);
        while (labCamera != null && labCamera.IsFocusing) yield return null;

        yield return teleportAnimation.PlayArrival(PlayerA, PlayerB);
        FinishTeleportAnimation(restoreVelocity);
    }

    void FocusTeleportCamera(Vector2 position)
    {
        Cam?.GetComponent<LabCamera>()?.FocusOn(position, TeleportCameraMoveSeconds);
    }

    void UpdateTeleportSelection(UnityEngine.InputSystem.Keyboard kb)
    {
        if (!TeleportSelecting) return;

        if (!IsTeleportTargetValid())
            CancelTeleport();
        else if (kb.escapeKey.wasPressedThisFrame)
            CancelTeleport();
        else if (kb.aKey.wasPressedThisFrame)
            SelectTeleport(-1);
        else if (kb.dKey.wasPressedThisFrame)
            SelectTeleport(1);
        else if (kb.enterKey.wasPressedThisFrame)
            ConfirmTeleport();
    }

    public void SelectTeleport(int delta)
    {
        if (!TeleportSelecting || teleportTargets.Count == 0) return;
        if (!IsTeleportTargetValid())
        {
            CancelTeleport();
            return;
        }
        TeleportTarget?.SetHighlighted(false);
        teleportSelectionIndex = (teleportSelectionIndex + delta + teleportTargets.Count) % teleportTargets.Count;
        TeleportTarget?.SetHighlighted(true);
        LabAudio.Play(LabAudio.Cue.PortalUi);
        if (Cam != null && TeleportTarget != null)
            Cam.GetComponent<LabCamera>()?.FocusOn(
                TeleportTarget.transform.position, TeleportCameraMoveSeconds);
    }

    public void ConfirmTeleport()
    {
        if (!TeleportSelecting || !IsTeleportTargetValid())
        {
            if (TeleportSelecting) CancelTeleport();
            return;
        }

        LabTeleportPoint target = TeleportTarget;
        Vector2 targetPosition = target.transform.position;
        teleportPhase = TeleportPhase.Arriving;
        LabAudio.Play(LabAudio.Cue.PortalUi);
        ClearTeleportSelectionHighlights();
        target.TeleportPlayersHere(this);
        PlayerA.ApplyForm(teleportFormA);
        PlayerB.ApplyForm(teleportFormB);
        teleportRoutine = StartCoroutine(RunTeleportArrival(targetPosition, true));
    }

    public void CancelTeleport()
    {
        if (!TeleportSelecting) return;
        BeginTeleportCancellation();
    }

    void BeginTeleportCancellation()
    {
        teleportPhase = TeleportPhase.Cancelling;
        LabAudio.Play(LabAudio.Cue.PortalUi);
        ClearTeleportSelectionHighlights();
        PlayerA.TeleportPreservingState(teleportSourcePosition + Vector2.left * 0.65f, Vector2.zero);
        PlayerB.TeleportPreservingState(teleportSourcePosition + Vector2.right * 0.65f, Vector2.zero);
        PlayerA.ApplyForm(teleportFormA);
        PlayerB.ApplyForm(teleportFormB);
        FocusTeleportCamera(teleportSourcePosition);
        teleportRoutine = StartCoroutine(RunTeleportArrival(teleportSourcePosition, false));
    }

    void FinishTeleportAnimation(bool restoreVelocity)
    {
        ClearTeleportSelection();
        teleportPhase = TeleportPhase.None;
        PlayerA.ResumeFromTeleportSelection();
        PlayerB.ResumeFromTeleportSelection();
        PlayerA.ApplyForm(teleportFormA);
        PlayerB.ApplyForm(teleportFormB);
        PlayerA.SetVelocity(restoreVelocity ? teleportVelocityA : Vector2.zero);
        PlayerB.SetVelocity(restoreVelocity ? teleportVelocityB : Vector2.zero);
        Cam?.GetComponent<LabCamera>()?.ResumeFollowing();
        teleportRoutine = null;
    }

    void AbortTeleport()
    {
        if (!TeleportBusy) return;
        if (teleportRoutine != null) StopCoroutine(teleportRoutine);
        teleportRoutine = null;
        teleportAnimation?.Abort(PlayerA, PlayerB);
        ClearTeleportSelection();
        teleportPhase = TeleportPhase.None;
        if (PlayerA != null && PlayerA.IsAlive)
        {
            PlayerA.ResumeFromTeleportSelection();
            PlayerA.ApplyForm(teleportFormA);
        }
        if (PlayerB != null && PlayerB.IsAlive)
        {
            PlayerB.ResumeFromTeleportSelection();
            PlayerB.ApplyForm(teleportFormB);
        }
        Cam?.GetComponent<LabCamera>()?.ResumeFollowing();
    }

    void ClearTeleportSelectionHighlights()
    {
        teleportSource?.SetHighlighted(false);
        for (int i = 0; i < teleportTargets.Count; i++)
            teleportTargets[i]?.SetHighlighted(false);
    }

    void ClearTeleportSelection()
    {
        ClearTeleportSelectionHighlights();
        teleportSource = null;
        teleportTargets.Clear();
        teleportSelectionIndex = 0;
    }

    void OnGUI()
    {
        if (Paused) return;
        if (TeleportSelecting) DrawTeleportSelectionGui();
        DrawDeathFlashGui();
    }

    /// <summary>死亡边框闪红：one-shot 重放，两人同死也只显示一次闪光。</summary>
    void TriggerDeathFlash()
    {
        if (!deathFlashEnabled) return;
        deathFlashLeft = Mathf.Max(0.01f, deathFlashDuration);
    }

    void DrawDeathFlashGui()
    {
        if (deathFlashLeft <= 0f) return;

        float duration = Mathf.Max(0.05f, deathFlashDuration);
        float progress = 1f - Mathf.Clamp01(deathFlashLeft / duration);

        // 亮度闪烁：正弦脉动 × 整体衰减，中心频率由 deathFlashPulses 控制。
        float pulses = Mathf.Max(0.5f, deathFlashPulses);
        float envelope = 1f - progress;
        float multiplier = envelope * Mathf.Max(0f, Mathf.Sin(progress * pulses * Mathf.PI));

        Color c = deathFlashColor;
        c.a *= multiplier;
        if (c.a <= 0.004f) return;

        Color previous = GUI.color;
        GUI.color = c;
        float depthH = Screen.height * Mathf.Clamp(deathFlashEdgeDepth, 0.05f, 0.5f);
        float depthW = Screen.width * Mathf.Clamp(deathFlashEdgeDepth, 0.05f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, depthH), VignetteBar((int)depthH));
        GUI.DrawTexture(new Rect(0f, Screen.height - depthH, Screen.width, depthH), VignetteBar((int)depthH));
        GUI.DrawTexture(new Rect(0f, 0f, depthW, Screen.height), VignetteBar((int)depthW));
        GUI.DrawTexture(new Rect(Screen.width - depthW, 0f, depthW, Screen.height), VignetteBar((int)depthW));
        GUI.color = previous;
    }

    static readonly System.Collections.Generic.Dictionary<int, Texture2D> vignetteBars =
        new System.Collections.Generic.Dictionary<int, Texture2D>();

    /// <summary>
    /// 边缘渐晕条：1×H 渐变纹理，两端不透明、中心透明（smoothstep），
    /// 双线性拉伸后整条带宽内没有硬边界；按高度缓存，运行时生成、不落资产。
    /// </summary>
    static Texture2D VignetteBar(int height)
    {
        int h = Mathf.Clamp(height, 16, 1024);
        if (vignetteBars.TryGetValue(h, out var cached) && cached != null) return cached;

        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[h];
        var white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            float d = Mathf.Min(t, 1f - t) * 2f;             // 边缘 0 → 中心 1
            px[y] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt((1f - d * d * (3f - 2f * d)) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        vignetteBars[h] = tex;
        return tex;
    }

    void DrawTeleportSelectionGui()
    {
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.22f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string label = string.Format("传送门 {0}/{1}   A/D 选择   Enter 确认",
            teleportSelectionIndex + 1, teleportTargets.Count);
        GUI.Label(new Rect(24f, 24f, 500f, 32f), label);
        GUI.color = previous;
    }

    // ------------------------------------------------------------------
    // 世界搭建
    // ------------------------------------------------------------------

    void BuildRoom()
    {
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        if (level != null)
        {
            // 关卡场景：几何完全来自 LabLevel，和编辑器预览走同一条生成路径。
            level.BuildRuntime(transform, slick);
            return;
        }

        // 调试台基准场景：内置房间。CaptureBaseline 也读同一段描述，保证两边不会漂。
        DescribeRoom(roomScale, new ObjectRoomSink(this));
    }

    /// <summary>
    /// 调试台内置房间的唯一描述。BuildRoom 用它建 GameObject，CaptureBaseline 用它抓关卡数据。
    /// 4 倍场地走同一个描述，所以缩放行为不变。
    /// </summary>
    internal static void DescribeRoom(float roomScale, IRoomSink sink)
    {
        float scale = Mathf.Max(0.01f, roomScale);

        // 地板：上表面 y = 0
        sink.Solid("Floor", new Vector2(0f, -0.5f), new Vector2(26f * scale, 1f));

        // 楼梯：每级高 2 格、宽 3 格，从 y=2 爬到 y=10。往右走每掉一级就是 2 格落差。
        sink.Solid("Step_y02", new Vector2(4.5f, 1.5f), new Vector2(3f, 1f));
        sink.Solid("Step_y04", new Vector2(1.5f, 3.5f), new Vector2(3f, 1f));
        sink.Solid("Step_y06", new Vector2(-1.5f, 5.5f), new Vector2(3f, 1f));
        sink.Solid("Step_y08", new Vector2(-4.5f, 7.5f), new Vector2(3f, 1f));
        sink.Solid("Step_y10", new Vector2(-7.5f, 9.5f), new Vector2(3f, 1f));

        // 侧墙
        sink.Solid("WallL", new Vector2(-13.5f * scale, 8f * scale), new Vector2(1f, 16f * scale));
        sink.Solid("WallR", new Vector2(13.5f * scale, 8f * scale), new Vector2(1f, 16f * scale));

        // 竖绳测试：4 格长，底端贴地
        sink.Rope(new Vector2(RopeCenterX, RopeCenterY), RopeCells);
    }

    internal interface IRoomSink
    {
        void Solid(string name, Vector2 center, Vector2 size);
        void Rope(Vector2 center, int cells);
    }

    sealed class ObjectRoomSink : IRoomSink
    {
        readonly TuningLab lab;

        public ObjectRoomSink(TuningLab lab) { this.lab = lab; }

        public void Solid(string name, Vector2 center, Vector2 size) => lab.AddSolid(name, center, size);
        public void Rope(Vector2 center, int cells) => lab.AddRope(center, cells);
    }

    sealed class DataRoomSink : IRoomSink
    {
        readonly LabLevelData data;
        public int Failures { get; private set; }

        public DataRoomSink(LabLevelData data) { this.data = data; }

        public void Solid(string name, Vector2 center, Vector2 size)
        {
            if (data.AddAlignedRect(center, size)) return;
            Failures++;
            Debug.LogWarning($"[TuningLab] 基准房间的 {name} 没对齐 1 格，无法写进关卡数据。");
        }

        public void Rope(Vector2 center, int cells)
            => data.ropes.Add(new LabRopeSpec(center, cells));
    }

    /// <summary>
    /// 把调试台脚本生成的房间抓成关卡数据。这是"从 Tuning Lab 派生关卡"的唯一来源：
    /// 不复制场景里的物体，而是读同一段 DescribeRoom，所以格子、尺寸、绳位置和基准场景完全一致。
    /// </summary>
    public static LabLevelData CaptureBaseline()
    {
        var data = new LabLevelData();
        var sink = new DataRoomSink(data);
        DescribeRoom(1f, sink);

        data.bounds = ToRectInt(BaselineBounds);
        data.spawnA = SpawnAFeet;
        data.spawnB = SpawnBFeet;
        data.cameraMinSize = DefaultCameraMinSize;
        data.cameraMaxSize = DefaultCameraMaxSize;
        data.Normalize();

        if (sink.Failures > 0)
            Debug.LogWarning($"[TuningLab] 基准房间有 {sink.Failures} 块没抓进关卡数据。");

        return data;
    }

    /// <summary>调试台基准房间的相机 clamp 范围（1 倍场地）。</summary>
    public static Rect BaselineBounds => StaticRoomBounds(1f);

    /// <summary>按场地倍数取默认相机 clamp 范围；4 倍调试台用。</summary>
    public static Rect RoomBoundsForScale(float roomScale) => StaticRoomBounds(roomScale);

    static Rect StaticRoomBounds(float roomScale)
    {
        float scale = Mathf.Max(0.01f, roomScale);
        return Rect.MinMaxRect(-BaselineWallHalfWidth * scale, BaselineFloorY,
                               BaselineWallHalfWidth * scale, BaselineCeilingY * scale);
    }

    static RectInt ToRectInt(Rect rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.xMin),
            Mathf.RoundToInt(rect.yMin),
            Mathf.RoundToInt(rect.width),
            Mathf.RoundToInt(rect.height));
    }

    /// <summary>
    /// 当前相机应该使用的 bounds / size。B 的 position 是关卡判定点：
    ///   有 LabLevel + 关卡分区时：B 在哪个分区的 bounds 里，就用哪个分区的大小当相机范围；
    ///   B 不在任何分区里就不夹相机（NoCameraBounds），只用默认 min/max 跟两人相对位置缩放。
    ///   没有分区时：有 LabLevel 就用单关数据，否则用默认值。
    /// </summary>
    public void GetCameraFrame(Vector2 probePosition, out Rect bounds, out float minSize, out float maxSize)
    {
        if (level != null)
        {
            var data = level.Data;
            if (data.partitions != null && data.partitions.Count > 0)
            {
                for (int i = 0; i < data.partitions.Count; i++)
                {
                    var partition = data.partitions[i];
                    if (partition == null) continue;

                    Rect partitionBounds = LabLevelData.ToRect(partition.bounds);
                    if (!partitionBounds.Contains(probePosition)) continue;

                    bounds = partitionBounds;
                    minSize = data.cameraMinSize;
                    maxSize = data.cameraMaxSize;
                    NormalizeFrame(ref bounds, ref minSize, ref maxSize);
                    return;
                }

                bounds = NoCameraBounds;
                minSize = data.cameraMinSize;
                maxSize = data.cameraMaxSize;
                NormalizeFrame(ref bounds, ref minSize, ref maxSize);
                return;
            }

            bounds = LabLevelData.ToRect(data.bounds);
            minSize = data.cameraMinSize;
            maxSize = data.cameraMaxSize;
            NormalizeFrame(ref bounds, ref minSize, ref maxSize);
            return;
        }

        bounds = defaultCameraBounds;
        minSize = defaultCameraMinSize;
        maxSize = defaultCameraMaxSize;
        NormalizeFrame(ref bounds, ref minSize, ref maxSize);
    }

    static void NormalizeFrame(ref Rect bounds, ref float minSize, ref float maxSize)
    {
        bounds = NormalizeRect(bounds);
        if (bounds.width <= 0f || bounds.height <= 0f) bounds = BaselineBounds;
        NormalizeSizes(ref minSize, ref maxSize);
    }

    static void NormalizeSizes(ref float minSize, ref float maxSize)
    {
        if (minSize <= 0f) minSize = DefaultCameraMinSize;
        if (maxSize <= 0f) maxSize = DefaultCameraMaxSize;
        minSize = Mathf.Clamp(minSize, 0.1f, 128f);
        maxSize = Mathf.Clamp(maxSize, minSize, 128f);
    }

    static Rect NormalizeRect(Rect rect)
    {
        if (rect.width < 0f)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }
        if (rect.height < 0f)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }
        return rect;
    }

    readonly System.Collections.Generic.List<SpriteRenderer> gridV = new System.Collections.Generic.List<SpriteRenderer>();
    readonly System.Collections.Generic.List<SpriteRenderer> gridH = new System.Collections.Generic.List<SpriteRenderer>();

    /// <summary>
    /// 网格只在相机视野内显示：每帧按当前可见矩形算出整数格范围，
    /// 复用一组线对象摆过去。自由相机缩放时网格会跟着变密。
    /// </summary>
    void UpdateGrid()
    {
        Camera cam = Cam != null ? Cam : Camera.main;
        if (cam == null) return;

        if (!showGrid)
        {
            HideAll(gridV);
            HideAll(gridH);
            return;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 center = cam.transform.position;
        float thickness = 1f / CellPixels;

        int x0 = Mathf.FloorToInt(center.x - halfW);
        int x1 = Mathf.CeilToInt(center.x + halfW);
        int y0 = Mathf.FloorToInt(center.y - halfH);
        int y1 = Mathf.CeilToInt(center.y + halfH);

        int used = 0;
        for (int x = x0; x <= x1; x++, used++)
        {
            var sr = Line(gridV, used, "GridV");
            sr.transform.position = new Vector3(x, center.y, 0f);
            sr.transform.localScale = new Vector3(thickness, halfH * 2f, 1f);
        }
        for (int i = used; i < gridV.Count; i++) gridV[i].enabled = false;

        used = 0;
        for (int y = y0; y <= y1; y++, used++)
        {
            var sr = Line(gridH, used, "GridH");
            sr.transform.position = new Vector3(center.x, y, 0f);
            sr.transform.localScale = new Vector3(halfW * 2f, thickness, 1f);
        }
        for (int i = used; i < gridH.Count; i++) gridH[i].enabled = false;
    }

    SpriteRenderer Line(System.Collections.Generic.List<SpriteRenderer> pool, int index, string name)
    {
        while (pool.Count <= index)
        {
            var go = new GameObject(name + "_" + pool.Count);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = GridColor;
            sr.sortingOrder = -10;
            pool.Add(sr);
        }

        var line = pool[index];
        line.enabled = true;
        return line;
    }

    static void HideAll(System.Collections.Generic.List<SpriteRenderer> pool)
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) pool[i].enabled = false;
    }

    /// <summary>
    /// 一层 1 像素的棋盘，按原生分辨率生成，贴满视口。
    /// P 键开关：用来确认"一格到底是多大一块像素"。
    /// </summary>
    void BuildPixelGrid()
    {
        var tex = new Texture2D(RefWidth, RefHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[RefWidth * RefHeight];
        for (int y = 0; y < RefHeight; y++)
            for (int x = 0; x < RefWidth; x++)
                px[y * RefWidth + x] = ((x + y) & 1) == 0
                    ? new Color32(255, 255, 255, 26)
                    : new Color32(255, 255, 255, 8);

        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, RefWidth, RefHeight), new Vector2(0.5f, 0.5f), 1f);

        pixelGridGo = new GameObject("PixelGrid");
        pixelGridGo.transform.SetParent(transform, false);
        var sr = pixelGridGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5000;
        pixelGridGo.SetActive(showPixelGrid);
    }

    void AddLine(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite;
        sr.color = GridColor;
        sr.sortingOrder = -10;
    }

    /// <summary>竖绳：实心碰撞，按玩家挡位决定挡住 / 放行 / 弹回。</summary>
    void AddRope(Vector2 center, int cells, int chainId = 0)
    {
        var go = new GameObject("VerticalRope");
        go.transform.SetParent(transform, false);
        go.transform.position = center;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.sharedMaterial = slick;

        var rope = go.AddComponent<LabVerticalRope>();
        rope.cells = cells;
        rope.chainId = chainId;
    }

    GameObject AddSolid(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite;
        sr.color = SolidColor;
        sr.sortingOrder = 0;
        LabArt.Apply(sr, "terrain", true);

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        bc.sharedMaterial = slick;
        return go;
    }

    void BuildPlayers()
    {
        Vector2 feetA = level != null ? level.Data.spawnA : SpawnAFeet;
        Vector2 feetB = level != null ? level.Data.spawnB : SpawnBFeet;

        PlayerA = MakePlayer("A_Box", new Vector2(feetA.x, feetA.y + A_Pixels / (2f * CellPixels)),
                             ColorA, LabPlayer.Scheme.P1, A_Pixels, 2);
        PlayerB = MakePlayer("B_Chunk", new Vector2(feetB.x, feetB.y + B_Pixels / (2f * CellPixels)),
                             ColorB, LabPlayer.Scheme.P2, B_Pixels, 0);
        PlayerA.canBeRedHot = true;   // 红热只在 A 判定
        PlayerA.other = PlayerB;
        PlayerB.other = PlayerA;
    }

    LabPlayer MakePlayer(string name, Vector2 position, Color color, LabPlayer.Scheme scheme,
                         int pixels, int wall)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        var bc = go.AddComponent<BoxCollider2D>();
        float units = pixels / (float)CellPixels;
        bc.size = new Vector2(units, units);       // 碰撞箱 = 精灵尺寸，一点不让
        bc.sharedMaterial = slick;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = GravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var p = go.AddComponent<LabPlayer>();
        p.lab = this;
        p.scheme = scheme;
        p.pixels = pixels;
        p.visual = LabVisual.Build(go.transform, pixels, wall > 0, color);

        var fxGo = new GameObject("SpeedFx");
        fxGo.transform.SetParent(go.transform, false);
        fxGo.AddComponent<LabSpeedFeedback>().Bind(this, p);

        var chargeFxGo = new GameObject("ChargeFx");
        chargeFxGo.transform.SetParent(go.transform, false);
        chargeFxGo.AddComponent<LabChargeFeedback>().Bind(this, p);
        return p;
    }

    static readonly System.Collections.Generic.Dictionary<long, Sprite> squareCache =
        new System.Collections.Generic.Dictionary<long, Sprite>();

    /// <summary>生成 N×N 的方块精灵；wall &gt; 0 时是空心的框（A），wall = 0 时是实心块（B）。</summary>
    static Sprite SquareSprite(int pixels, int wall)
    {
        long key = ((long)pixels << 8) | (uint)wall;
        if (squareCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[pixels * pixels];
        for (int y = 0; y < pixels; y++)
            for (int x = 0; x < pixels; x++)
            {
                bool border = wall > 0 && (x < wall || y < wall || x >= pixels - wall || y >= pixels - wall);
                px[y * pixels + x] = (wall == 0 || border)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

        tex.SetPixels32(px);
        tex.Apply();

        // PPU = 每格像素数 → 精灵的世界尺寸自动就是 pixels/16 格
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), CellPixels);
        squareCache[key] = sprite;
        return sprite;
    }

    void BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = RefHeight / (2f * CellPixels);   // 216 / 32 = 6.75 → 可见 13.5 格
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        // z 固定在 -10（精灵都在 z=0）；自由相机稍后会在 Configure 里立即对齐玩家，不用从这里的固定点飞过去。
        cam.transform.position = new Vector3(0f, 5f, -10f);
        go.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData(); // URP 项目里必须有，否则相机不渲染

        if (freeCamera)
        {
            // 自由相机：跟随两人中点；B 的位置决定用哪个关卡分区的 bounds / size。
            var follow = go.AddComponent<LabCamera>();
            GetCameraFrame(PlayerB.transform.position, out Rect initialBounds, out float initialMin, out float initialMax);
            follow.Configure(this, PlayerA.transform, PlayerB.transform,
                             initialBounds, initialMin, initialMax, cameraFrameBlend);
        }
        // 固定视角：正交尺寸已在上方设为 13.5 格，不用再加组件

        Cam = cam;
    }

    /// <summary>TuningLab 生成整个场地用的白块精灵；LabLevel 的预览也用它，保证两边同形。</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return whiteSprite;
        }
    }
}
