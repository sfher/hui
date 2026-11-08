using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 光桥方块：电梯形（‖ / =）幻影沿平行线方向延伸出来的临时格子（Issue #76）。
///
/// 形状：填满整格（1×1），一格就是一条光柱，不再是贴边薄片。半透明白色，比幻影本体
/// （0.35）实一点，看得出是同一条光桥。
///
/// 通行规则按方向分两种：
/// - 横向（=）：当单向平台用。从下往上穿得过去、站得住，从上落下和横向走都不挡。
///   直接复用 <see cref="PlatformEffector2D"/>，和横绳同一套参数。
/// - 纵向（‖）：竖绳的二级穿过逻辑。0/1 级挡住，2 级沿法线穿过，3 级及以上弹回并算一次红热撞击。
///
/// A（幻影来源）永远能穿过；B 默认按上面的规则走，另外"出生时就压在 B 身上"的那些格子对 B 也让路，
/// 避免凭空把人夹进方块里。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabLightBridgeBlock : MonoBehaviour
{
    /// <summary>半透明白片，比幻影本体更实一点。</summary>
    public static readonly Color DefaultColor = new Color(0.86f, 0.95f, 1f, 0.55f);

    /// <summary>Play 时的全部光桥方块。</summary>
    public static readonly List<LabLightBridgeBlock> All = new List<LabLightBridgeBlock>();

    public Vector2Int Cell { get; private set; }
    public LabPhantom Owner { get; private set; }
    /// <summary>竖直光桥：走竖绳的二级穿过逻辑。</summary>
    public bool Vertical { get; private set; }
    /// <summary>这一格生成时就压在 B 身上，直接对 B 让路。</summary>
    public bool PassesForbiddenPlayer { get; private set; }
    /// <summary>二级穿过后的忽略时长，和竖绳保持一致。</summary>
    public float passWindow = 0.22f;

    /// <summary>碰撞箱范围，供攀附判定用。</summary>
    public Bounds Bounds => body != null ? body.bounds : new Bounds(transform.position, Vector3.one);

    /// <summary>一次光桥接触。字段与 <see cref="LabVerticalRope.RopeContact"/> 对齐。</summary>
    public struct BridgeContact
    {
        public LabLightBridgeBlock block;
        public LabPlayer player;
        public int tier;
        public Vector2 normal;     // 光桥 → 玩家
        public bool passed;
        public bool bounced;
    }

    public event System.Action<BridgeContact> Contacted;

    BoxCollider2D body;
    TuningLab lab;
    readonly List<Collider2D> passThrough = new List<Collider2D>();
    readonly List<float> passUntil = new List<float>();
    readonly HashSet<LabPlayer> upwardPassNotified = new HashSet<LabPlayer>();

    void Awake()
    {
        body = GetComponent<BoxCollider2D>();
        lab = FindAnyObjectByType<TuningLab>();
    }

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        ReleasePassThrough();
    }

    void Update()
    {
        // 穿过窗口到点、并且玩家已经离开方块体，才恢复碰撞。
        // 人还在方块里就别恢复，否则会被推出去或者卡在半截。
        for (int i = passThrough.Count - 1; i >= 0; i--)
        {
            Collider2D target = passThrough[i];
            if (target == null)
            {
                passThrough.RemoveAt(i);
                passUntil.RemoveAt(i);
                continue;
            }
            if (Time.time < passUntil[i]) continue;
            if (body.bounds.Intersects(target.bounds)) continue;

            Physics2D.IgnoreCollision(target, body, false);
            passThrough.RemoveAt(i);
            passUntil.RemoveAt(i);
        }
    }

    void FixedUpdate()
    {
        TrackHorizontalUpwardPass();
    }

    /// <summary>生成一个光桥方块。填满整格，不再有薄片偏移。</summary>
    public static LabLightBridgeBlock Spawn(
        LabPhantom owner, Vector2Int cell, bool vertical, bool passForbidden,
        Transform parent, PhysicsMaterial2D material)
    {
        var go = new GameObject("LightBridgeBlock");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;          // 填满整格
        bc.offset = Vector2.zero;
        bc.isTrigger = false;
        if (material != null) bc.sharedMaterial = material;

        if (!vertical)
        {
            // 横向光桥 = 单向平台：从下往上穿过并站住，其它方向不挡。
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            bc.usedByEffector = true;
        }

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(go.transform, false);
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.color = DefaultColor;
        sr.sortingOrder = 1;

        var block = go.AddComponent<LabLightBridgeBlock>()
            .Configure(owner, cell, vertical, passForbidden, spriteGo.transform);
        return block;
    }

    LabLightBridgeBlock Configure(
        LabPhantom owner, Vector2Int cell, bool vertical, bool passForbidden,
        Transform spriteTransform)
    {
        Owner = owner;
        Cell = cell;
        Vertical = vertical;
        PassesForbiddenPlayer = passForbidden;

        var sr = spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : null;
        if (sr != null && sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x > 0.0001f && spriteSize.y > 0.0001f)
                sr.transform.localScale = new Vector3(1f / spriteSize.x, 1f / spriteSize.y, 1f);
        }

        return this;
    }

    /// <summary>让指定玩家穿过这个方块。</summary>
    public void IgnorePlayer(LabPlayer player)
    {
        if (player == null || body == null) return;
        var other = player.GetComponent<Collider2D>();
        if (other == null) return;
        Physics2D.IgnoreCollision(body, other, true);
    }

    /// <summary>
    /// 竖直光桥的接触结算，和 <see cref="LabVerticalRope.Resolve"/> 同构：
    /// 0/1 级交给物理解算挡住，2 级沿法线穿过去，3 级及以上弹回并按红热撞击结算。
    /// 横向光桥走 PlatformEffector2D，不进来。
    /// </summary>
    void OnCollisionEnter2D(Collision2D c)
    {
        if (body == null || !c.collider.TryGetComponent(out LabPlayer player)) return;
        if (Owner != null && player != Owner.Target) return;

        if (!Vertical)
        {
            Vector2 platformNormal = ContactNormal(player, c);
            if (platformNormal.y > 0.5f)
                Raise(player, 0, platformNormal, false, false);
            return;
        }

        Vector2 normal = ContactNormal(player, c);
        float impact = Mathf.Abs(Vector2.Dot(player.PreImpactVelocity, normal));
        int tier = lab != null ? lab.TierOfSpeed(impact) : player.SpeedTier;

        // 零级 / 一级：和普通墙一样交给物理解算挡住。
        if (tier < 2)
        {
            Raise(player, tier, normal, false, false);
            return;
        }

        // 二级：沿法线穿过。忽略到玩家离开方块体，避免反复触发卡住。
        if (tier == 2)
        {
            var playerCollider = c.collider;
            Physics2D.IgnoreCollision(playerCollider, body, true);
            if (!passThrough.Contains(playerCollider))
            {
                passThrough.Add(playerCollider);
                passUntil.Add(Time.time + passWindow);
            }
            // 进入回调时碰撞已经把法向速度解算掉了；把入射分量还回去才真的穿过去。
            player.ApplyAxisVelocity(normal, Vector2.Dot(player.PreImpactVelocity, normal));
            Raise(player, tier, normal, true, false);
            return;
        }

        // 三级及以上：弹回，并且按一次红热撞击结算（和竖绳同一套）。
        float speed = BounceSpeed(player, normal);
        player.ApplyBounce(normal, speed);
        player.ApplyTerrainImpact(normal, impact);
        Raise(player, tier, normal, false, true);
    }

    /// <summary>
    /// 横向光桥从下往上顶穿时不会产生 Collision2D，只能在玩家穿过体块期间补判。
    /// 同一名玩家在同一条光桥里只报一次，离开体块后重新上弦。
    /// </summary>
    void TrackHorizontalUpwardPass()
    {
        if (Vertical || body == null || Owner == null || Owner.Target == null) return;

        LabPlayer player = Owner.Target;
        bool overlaps = body.bounds.Intersects(player.BodyBounds);
        if (!overlaps)
        {
            upwardPassNotified.Remove(player);
            return;
        }

        Vector2 upwardVelocity = player.PreImpactVelocity;
        bool fromBelow = upwardVelocity.y > 0.01f &&
                         player.BodyBounds.center.y < body.bounds.center.y;
        if (!fromBelow || !upwardPassNotified.Add(player)) return;

        int tier = lab != null ? lab.TierOfSpeed(upwardVelocity.y) : player.VerticalTier;
        Raise(player, tier, Vector2.up, true, false);
    }

    /// <summary>接触法线，方向统一为“方块 → 玩家”。只取轴向，再用玩家中心方位定朝外。</summary>
    Vector2 ContactNormal(LabPlayer player, Collision2D c)
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < c.contactCount; i++) sum += c.GetContact(i).normal;
        if (sum.sqrMagnitude < 0.0001f) sum = (Vector2)player.transform.position - (Vector2)transform.position;

        Vector2 axis = Mathf.Abs(sum.x) >= Mathf.Abs(sum.y)
            ? new Vector2(Mathf.Sign(sum.x), 0f)
            : new Vector2(0f, Mathf.Sign(sum.y));

        Vector2 away = (Vector2)player.transform.position - (Vector2)transform.position;
        if (Vector2.Dot(away, axis) < 0f) axis = -axis;
        return axis;
    }

    /// <summary>弹回速度：保留碰撞前速度，并增加一个等级间距（与竖绳同一套）。</summary>
    float BounceSpeed(LabPlayer player, Vector2 normal)
    {
        if (lab == null) return 30f;
        return lab.RopeBounceSpeed(Vector2.Dot(player.PreImpactVelocity, normal));
    }

    void Raise(LabPlayer player, int tier, Vector2 normal, bool passed, bool bounced)
    {
        Contacted?.Invoke(new BridgeContact
        {
            block = this,
            player = player,
            tier = tier,
            normal = normal,
            passed = passed,
            bounced = bounced
        });
    }

    void ReleasePassThrough()
    {
        for (int i = 0; i < passThrough.Count; i++)
            if (passThrough[i] != null) Physics2D.IgnoreCollision(passThrough[i], body, false);
        passThrough.Clear();
        passUntil.Clear();
    }

    /// <summary>这一格还空着吗？两条光桥不能叠在同一格。</summary>
    public static bool IsFree(Vector2Int cell)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var block = All[i];
            if (block != null && block.Cell == cell) return false;
        }
        return true;
    }

    void OnDestroy() => All.Remove(this);
}
