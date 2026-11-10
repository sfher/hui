using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格冰块：1×1 的实体方块，可以站立和滑行。
/// 视觉是默认冰蓝填充 + 按 groupId 推导的 2 像素组色边框，编辑器手柄和运行时共用同一套颜色。
///
/// 破坏有两条路，都由玩家侧结算：
///   组内任意一块沿接触法线被 v2 撞击 → 整组一起碎（优先于红热）；
///   红热边（Hot2）的非承重接触 → 只融这一块，边回普通；
///   冰格落在玩家脚下 footprint 内的接触不融，避免融掉脚下承重块（含不同分组交界）。
///   在冰面上加速到 v2/v3 属于滑行，不算撞击，不会碎冰。
///
/// 关卡生成时冰和地形共用一个 CompositeCollider2D，相邻冰块、冰块和地形之间没有
/// 可碰撞的内侧竖面，所以冰面滑行不会卡脚。
/// 破坏时不销毁对象，只关碰撞和渲染，这样静态组表、地图重置和调试读数都还能找到它。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabIce : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(0.42f, 0.75f, 1f);
    /// <summary>0 号组的边框色：比填充更深，避免和默认冰蓝混在一起。</summary>
    public static readonly Color DefaultBorderColor = new Color(0.12f, 0.42f, 0.85f);

    /// <summary>
    /// 组色边框的线宽（像素）。一格是 16 像素，取 1 像素即整格的 1/16：
    /// 缩放显示后仍能分辨组色，又不像 2 像素那样把冰格压得发闷。
    /// </summary>
    public const int BorderPixels = 1;

    /// <summary>边框不透明度。留一点底色透出来，比纯不透明的色带柔和。</summary>
    public const float BorderAlpha = 0.9f;

    /// <summary>组色序列的长度：前 1024 个 groupId 的颜色互不重复（测试覆盖）。</summary>
    public const int GroupColorCount = 1024;

    /// <summary>Play 时生成的所有冰块，按 groupId 编组查询。</summary>
    public static readonly List<LabIce> All = new List<LabIce>();

    public Vector2Int cell;
    public int groupId;
    [Tooltip("勾选后按 groupId 自动取边框组色；取消后边框使用下面的 color，填充始终是默认冰蓝。")]
    public bool useGroupColor = true;
    public Color color = DefaultColor;

    static Sprite cachedBorderSprite;

    public bool Broken { get; private set; }

    /// <summary>组色边框的渲染器；填充仍然是原来那块默认冰蓝。</summary>
    public SpriteRenderer GroupBorder => borderRenderer;

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;
    SpriteRenderer borderRenderer;
    CompositeCollider2D composite;
    Collider2D[] solidHits = new Collider2D[8];

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// 由一个 groupId 推导稳定的边框色。0 号组用深蓝；其余组用黄金角散列，
    /// 再叠加饱和度和明度的低频变化，前 <see cref="GroupColorCount"/> 组互不重复。
    /// 编辑器手柄和运行时边框都调用这一个函数，保证两边颜色一致。
    /// 明度压在 0.72–0.88：上限不到 1，避免在亮冰蓝填充上过于刺眼。
    /// </summary>
    public static Color GroupColor(int groupId)
    {
        if (groupId == 0) return DefaultBorderColor;

        float hue = Mathf.Repeat(0.58f + groupId * 0.61803398875f, 1f);
        float saturation = 0.50f + 0.25f * Mathf.Repeat(groupId * 0.41421356237f, 1f);
        float value = 0.72f + 0.16f * Mathf.Repeat(groupId * 0.73205080757f, 1f);
        return Color.HSVToRGB(hue, saturation, value);
    }

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
            // 关卡生成时冰和地形共用一个 CompositeCollider2D，把相邻 1×1 方块之间的
            // 内侧竖面焊掉；测试或手摆的独立方块没有父级 composite，仍用自身碰撞箱。
            body.compositeOperation = composite != null
                ? Collider2D.CompositeOperation.Merge
                : Collider2D.CompositeOperation.None;
            body.enabled = !Broken;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 填充保持原版冰块的蓝色；组色只加在外圈边框上。
            spriteRenderer.sprite = TuningLab.WhiteSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
            LabArt.Apply(spriteRenderer, "ice");
            spriteRenderer.enabled = !Broken;
        }

        EnsureBorder();
        if (borderRenderer != null)
        {
            Color borderColor = useGroupColor ? GroupColor(groupId) : color;
            borderColor.a *= BorderAlpha;
            borderRenderer.color = borderColor;
            borderRenderer.enabled = !Broken;
        }
    }

    /// <summary>边框是冰块的子物体，只有 1 像素宽的边，中间透明，露出蓝色填充。</summary>
    void EnsureBorder()
    {
        if (borderRenderer != null) return;

        var go = new GameObject("GroupBorder");
        go.transform.SetParent(transform, false);
        borderRenderer = go.AddComponent<SpriteRenderer>();
        borderRenderer.sprite = BorderSprite();
        borderRenderer.sortingOrder = 2;
    }

    /// <summary>只碎这一块；所在组的其它冰块不动。</summary>
    public void BreakOne()
    {
        BreakSelf();
        RebuildComposite();
    }

    /// <summary>同一 groupId 的所有冰块一起碎。0 也是普通组。</summary>
    public void BreakGroup()
    {
        var members = GroupOf(groupId);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].BreakSelf();

        // 整组关完碰撞后只重建一次合并几何。
        if (members.Count > 0 && members[0] != null) members[0].RebuildComposite();
        LabAudio.Play(LabAudio.Cue.IceBreak);
    }

    void BreakSelf()
    {
        if (Broken) return;
        Broken = true;
        if (body != null) body.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (borderRenderer != null) borderRenderer.enabled = false;
    }

    /// <summary>地图重置 / 复活时恢复：碰撞和渲染都回来。</summary>
    public void ResetIce(bool rebuildCollision = true)
    {
        Broken = false;
        ApplyShape();
        if (rebuildCollision) RebuildComposite();
    }

    /// <summary>整组恢复后只重建一次合并几何。</summary>
    public void RebuildCollision() => RebuildComposite();

    /// <summary>
    /// 把自己所在的合并几何标脏，帧末统一重建一次（#150）。
    ///
    /// composite 是 Manual 的，碎冰只关碰撞不重建；真正的 GenerateGeometry
    /// 由 <see cref="LabCompositeRebuild"/> 在这一帧末尾合并执行，
    /// 同一帧碎多少组冰都只算一次。
    /// </summary>
    void RebuildComposite() => LabCompositeRebuild.Enqueue(composite);

    /// <summary>
    /// 组色边框的贴图：<see cref="BorderPixels"/> 像素宽的边，中间完全透明。颜色由
    /// SpriteRenderer.color 染色，所以同一张贴图能显示任意组色，不需要每组生成一张贴图。
    /// 边框只取 1 像素（整格的 1/16），够看清是哪一组，又不会喧宾夺主。
    /// </summary>
    static Sprite BorderSprite()
    {
        if (cachedBorderSprite != null) return cachedBorderSprite;

        const int size = TuningLab.CellPixels;
        const int border = BorderPixels;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || y < border || x >= size - border || y >= size - border;
                pixels[y * size + x] = edge
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        cachedBorderSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        return cachedBorderSprite;
    }

    /// <summary>这个点落在哪一块未破坏的冰上；合并地形需要靠接触点找回是哪一格。</summary>
    public static LabIce AtCell(Vector2 point)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        for (int i = 0; i < All.Count; i++)
        {
            var ice = All[i];
            if (ice != null && !ice.Broken && ice.cell == cell) return ice;
        }
        return null;
    }

    /// <summary>
    /// 这一格的冰块现在挡不挡人。别读 <see cref="BoxCollider2D.enabled"/>：并进地形的
    /// <see cref="Collider2D.CompositeOperation.Merge"/> 之后，子碰撞体的开关不代表合并几何里
    /// 还有没有这块——`enabled = true` 也可能正处于"由合并体接管"的状态。这里区分两种来源：
    /// 没并进合并体时看自己的碰撞箱；并进之后看合并几何在该格中心到底有没有实体。
    /// </summary>
    public bool IsSolidAtCell()
    {
        if (Broken) return false;

        if (composite == null)
            return body != null && body.enabled && body.gameObject.activeInHierarchy;

        var center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        int count = Physics2D.OverlapPoint(center, ContactFilter2D.noFilter, solidHits);
        for (int i = 0; i < count; i++)
        {
            var hit = solidHits[i];
            if (hit != null && hit != body && !hit.isTrigger) return true;
        }
        return false;
    }

    /// <summary>这个点落在哪一块未破坏的冰上；合并地形需要靠接触点找回是哪一格。</summary>

    /// <summary>当前 Play 里同一组的冰块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabIce> GroupOf(int groupId)
    {
        var list = new List<LabIce>();
        for (int i = 0; i < All.Count; i++)
        {
            var ice = All[i];
            if (ice != null && ice.groupId == groupId) list.Add(ice);
        }
        return list;
    }
}
