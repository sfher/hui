using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格岩浆块：1×1 的橙红色实体方块，可以站立，走动规则和普通地面相同。
///
/// 和冰块的差别：
///   每条边第一次接触某个岩浆面时，把 A 对应的边强制点到热二；持续贴着不重复触发，
///   离开表面后再次接触才重新触发。强制红热走 StrikeEdge 的死亡逻辑，热二边再接触即死亡；
///   以 v2 及以上沿法线撞上来时，撞击红热 + 强制红热叠加，也会死亡；
///   红热边接触不会融掉岩浆块；只有沿接触法线的速度到三级（v3）才整组破坏。
/// 破坏时不销毁对象，只关碰撞和渲染，这样静态组表、地图重置和调试读数都还能找到它。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabMagma : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(1f, 0.34f, 0.12f);

    /// <summary>整组破坏要求的速度等级：固定三级。</summary>
    public const int BreakTier = 3;

    /// <summary>Play 时生成的所有岩浆块，按 groupId 编组查询。</summary>
    public static readonly List<LabMagma> All = new List<LabMagma>();

    public Vector2Int cell;
    public int groupId;
    public Color color = DefaultColor;

    public bool Broken { get; private set; }

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;
    CompositeCollider2D composite;

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// 把格子和组号应用到碰撞箱和视觉。编辑器预览也直接调它，不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        composite = GetComponentInParent<CompositeCollider2D>();
        if (body != null)
        {
            body.isTrigger = false;
            body.size = Vector2.one;
            body.offset = Vector2.zero;
            // 关卡生成时地形和岩浆共用一个 CompositeCollider2D，把相邻 1×1 方块之间的
            // 内侧竖面焊掉；测试或手摆的独立方块没有父级 composite，仍用自身碰撞箱。
            body.compositeOperation = composite != null
                ? Collider2D.CompositeOperation.Merge
                : Collider2D.CompositeOperation.None;
            body.enabled = !Broken;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = TuningLab.WhiteSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
            LabArt.Apply(spriteRenderer, "magma");
            spriteRenderer.enabled = !Broken;
        }
    }

    /// <summary>
    /// 同一 groupId 的所有岩浆块一起碎。0 也是普通组。
    /// 岩浆块没有"红热融一块"的路径，所以不提供单块破坏接口。
    /// </summary>
    public void BreakGroup()
    {
        var members = GroupOf(groupId);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].BreakSelf();

        // 整组关完碰撞后只重建一次合并几何，别让组里每块各算一遍。
        if (members.Count > 0 && members[0] != null) members[0].RebuildComposite();
    }

    void BreakSelf()
    {
        if (Broken) return;
        Broken = true;
        if (body != null) body.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    /// <summary>地图重置 / 复活时恢复：碰撞和渲染都回来。</summary>
    public void ResetMagma(bool rebuildCollision = true)
    {
        Broken = false;
        ApplyShape();
        if (rebuildCollision) RebuildComposite();
    }

    /// <summary>整组恢复后只重建一次合并几何。</summary>
    public void RebuildCollision() => RebuildComposite();

    /// <summary>
    /// 标脏，帧末统一重建（#150）。
    /// 和 LabIce 共用同一个重建器：一帧里碎冰又碎岩浆也只生成一次几何。
    /// </summary>
    void RebuildComposite()
    {
        if (composite == null) return;
        LabCompositeRebuild.Enqueue(composite);
    }

    /// <summary>这个点落在哪一块未破坏的岩浆上；合并地形需要靠接触点找回是哪一格。</summary>
    public static LabMagma AtCell(Vector2 point)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        for (int i = 0; i < All.Count; i++)
        {
            var magma = All[i];
            if (magma != null && !magma.Broken && magma.cell == cell) return magma;
        }
        return null;
    }

    /// <summary>当前 Play 里同一组的岩浆块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabMagma> GroupOf(int groupId)
    {
        var list = new List<LabMagma>();
        for (int i = 0; i < All.Count; i++)
        {
            var magma = All[i];
            if (magma != null && magma.groupId == groupId) list.Add(magma);
        }
        return list;
    }
}

/// <summary>
/// 一个关卡里所有静态地形和岩浆块共享的合并碰撞体。
/// 相邻的 1×1 方块（岩浆—岩浆、岩浆—地形）如果各有自己的 BoxCollider2D，
/// 接缝处的内侧竖面会卡住横走的玩家，也会把上表面行走误判成侧面接触。
/// 合并成一块几何后，接缝只存在于视觉和数据里。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CompositeCollider2D))]
public sealed class LabTerrainField : MonoBehaviour
{
}
