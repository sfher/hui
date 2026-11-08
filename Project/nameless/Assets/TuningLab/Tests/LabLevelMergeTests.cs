using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 关卡数据合并与资源绑定（Issue #109）。
///
/// 合并的约定：地形取并集、物件按格去重、相机范围取并集，
/// 出生点和相机尺寸这类单值字段保持接收方自己的。
/// </summary>
public sealed class LabLevelMergeTests
{
    static LabLevelData MakeRect(int x0, int y0, int w, int h)
    {
        var data = new LabLevelData();
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                data.AddCell(new Vector2Int(x, y));
        return data;
    }

    // ---- 地形 ---------------------------------------------------------

    [Test]
    public void MergedSolidsAreUnion()
    {
        var a = MakeRect(0, 0, 2, 2);      // (0,0)(1,0)(0,1)(1,1)
        var b = MakeRect(1, 1, 2, 2);      // (1,1)(2,1)(1,2)(2,2)

        int added = a.MergeFrom(b);

        Assert.AreEqual(3, added, "重叠的 (1,1) 不该重复计入");
        Assert.AreEqual(7, a.solids.Count, "并集是 7 格");
        Assert.IsTrue(a.solids.Contains(new Vector2Int(2, 2)), "b 独有的格子要进来");
    }

    [Test]
    public void MergingSameDataTwiceAddsNothing()
    {
        var a = MakeRect(0, 0, 3, 3);
        var b = MakeRect(0, 0, 3, 3);

        Assert.AreEqual(0, a.MergeFrom(b), "完全重合不该新增");
        Assert.AreEqual(9, a.solids.Count);
    }

    [Test]
    public void MergeIntoEmptyTakesEverything()
    {
        var a = new LabLevelData();
        var b = MakeRect(5, 5, 4, 4);

        int added = a.MergeFrom(b);
        Assert.AreEqual(16, added);
        Assert.AreEqual(16, a.solids.Count);
    }

    [Test]
    public void SelfMergeIsNoOp()
    {
        var a = MakeRect(0, 0, 2, 2);
        Assert.AreEqual(0, a.MergeFrom(a), "自己和自己合并什么都不该发生");
        Assert.AreEqual(4, a.solids.Count);
    }

    [Test]
    public void NullMergeIsSafe()
    {
        var a = MakeRect(0, 0, 2, 2);
        Assert.AreEqual(0, a.MergeFrom(null));
        Assert.AreEqual(4, a.solids.Count);
    }

    // ---- 物件 ---------------------------------------------------------

    [Test]
    public void ObjectsAreDedupedByCell()
    {
        var a = new LabLevelData();
        var b = new LabLevelData();

        a.PlaceIce(new Vector2Int(0, 0), 1);
        b.PlaceIce(new Vector2Int(0, 0), 1);     // 同格同组 → 重复
        b.PlaceIce(new Vector2Int(1, 0), 1);     // 不同格 → 新增

        int added = a.MergeFrom(b);

        Assert.AreEqual(1, added, "只该新增不同格的那个");
        Assert.AreEqual(2, a.ices.Count);
    }

    [Test]
    public void SameCellDifferentGroupBothSurvive()
    {
        var a = new LabLevelData();
        var b = new LabLevelData();

        a.PlaceHiddenBlock(new Vector2Int(0, 0), 1);
        b.PlaceHiddenBlock(new Vector2Int(0, 0), 2);   // 同格不同组，算两个

        a.MergeFrom(b);
        Assert.AreEqual(2, a.hiddenBlocks.Count, "同一格可以属于多个组");
    }

    [Test]
    public void RopesDedupedByGeometry()
    {
        var a = new LabLevelData();
        var b = new LabLevelData();

        var rope = new LabRopeSpec(new Vector2(0.5f, 1f), 3, 0);
        a.ropes.Add(rope);
        b.ropes.Add(rope);                              // 一模一样
        b.ropes.Add(new LabRopeSpec(new Vector2(2.5f, 1f), 3, 0));   // 不同位置

        a.MergeFrom(b);
        Assert.AreEqual(2, a.ropes.Count, "完全相同的那根不该重复");
    }

    // ---- 单值字段 -----------------------------------------------------

    [Test]
    public void SingleValuedFieldsKeepReceiverValues()
    {
        var a = new LabLevelData { spawnA = new Vector2(1f, 2f), spawnB = new Vector2(3f, 4f) };
        var b = new LabLevelData { spawnA = new Vector2(99f, 99f), spawnB = new Vector2(88f, 88f) };

        a.MergeFrom(b);

        Assert.AreEqual(new Vector2(1f, 2f), a.spawnA, "出生点保留接收方的");
        Assert.AreEqual(new Vector2(3f, 4f), a.spawnB);
    }

    [Test]
    public void BoundsTakeUnion()
    {
        var a = new LabLevelData { bounds = new RectInt(0, 0, 10, 10) };
        var b = new LabLevelData { bounds = new RectInt(-5, -5, 10, 10) };

        a.MergeFrom(b);

        Assert.AreEqual(-5, a.bounds.xMin, "并集要往左扩");
        Assert.AreEqual(10, a.bounds.xMax);
        Assert.AreEqual(-5, a.bounds.yMin);
        Assert.AreEqual(10, a.bounds.yMax);
    }

    // ---- LabLevel 绑定 -------------------------------------------------

    [Test]
    public void LabLevelReadsFromAssetWhenBound()
    {
        var go = new GameObject("Level");
        try
        {
            var level = go.AddComponent<LabLevel>();

            // 先把内嵌数据放点东西。
            level.Data.AddCell(Vector2Int.zero);
            Assert.AreEqual(1, level.SolidCellCount);

            var asset = ScriptableObject.CreateInstance<LabLevelAsset>();
            asset.Data.AddCell(new Vector2Int(5, 5));
            asset.Data.AddCell(new Vector2Int(6, 5));

            level.BindAsset(asset);

            Assert.AreEqual(2, level.SolidCellCount, "绑定之后读的是资源里的数据");
            Assert.IsTrue(level.Data.solids.Contains(new Vector2Int(5, 5)));

            Object.DestroyImmediate(asset);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UnbindingFallsBackToEmbeddedData()
    {
        var go = new GameObject("Level");
        try
        {
            var level = go.AddComponent<LabLevel>();
            level.Data.AddCell(new Vector2Int(1, 1));

            var asset = ScriptableObject.CreateInstance<LabLevelAsset>();
            asset.Data.AddCell(new Vector2Int(9, 9));
            level.BindAsset(asset);
            Assert.AreEqual(1, level.SolidCellCount);

            level.BindAsset(null);
            Assert.AreEqual(1, level.SolidCellCount, "解绑后回到内嵌数据");
            Assert.IsTrue(level.Data.solids.Contains(new Vector2Int(1, 1)));

            Object.DestroyImmediate(asset);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
