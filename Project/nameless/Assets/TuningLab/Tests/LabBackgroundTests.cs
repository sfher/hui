using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabBackgroundTests
{
    GameObject go;
    LabBackground background;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("Background");
        background = go.AddComponent<LabBackground>();
        background.resource = "Backgrounds/level1";
        background.Load();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
    }

    [Test]
    public void TilesCoverCameraViewAtAnyZoom()
    {
        Assert.IsTrue(background.enabled, "level1 背景纹理应能加载");
        var center = new Vector2(100f, 50f);
        foreach (float halfHeight in new[] { 6.75f, 36.2f })
        {
            background.Refresh(center, halfHeight, 16f / 9f);
            float halfWidth = halfHeight * 16f / 9f;
            var corners = new[]
            {
                center + new Vector2(-halfWidth, -halfHeight),
                center + new Vector2(halfWidth, -halfHeight),
                center + new Vector2(-halfWidth, halfHeight),
                center + new Vector2(halfWidth, halfHeight),
            };
            foreach (var corner in corners)
            {
                bool covered = false;
                foreach (var tile in ActiveTiles())
                    covered |= tile.bounds.Contains(corner);
                Assert.IsTrue(covered, $"视野角 {corner} 没有被背景块盖住（halfHeight={halfHeight}）");
            }
        }
    }

    [Test]
    public void HorizontallyAdjacentTilesAreMirrored()
    {
        background.Refresh(Vector3.zero, 6.75f, 16f / 9f);
        var tiles = ActiveTiles();
        Assert.Greater(tiles.Length, 1);
        float tileWidth = tiles[0].sprite.bounds.size.x;
        foreach (var tile in tiles)
        {
            Vector2 rightCenter = (Vector2)tile.transform.position + Vector2.right * tileWidth;
            foreach (var other in tiles)
            {
                if (Vector2.Distance(other.transform.position, rightCenter) > 0.01f) continue;
                Assert.Less(tile.transform.localScale.x * other.transform.localScale.x, 0f,
                    "相邻块必须一正一反镜像翻转，接缝才是同一列像素");
            }
        }
    }

    [Test]
    public void BackgroundScrollsAtParallaxSpeed()
    {
        background.scrollFactor = 0.5f;
        background.Refresh(Vector3.zero, 6.75f, 16f / 9f);
        Vector3 before = go.transform.GetChild(0).position;

        // 位移小于一块背景，第 0 块还是同一个网格索引，世界位移应为 (1-factor)×相机位移。
        background.Refresh(new Vector3(10f, 6f, 0f), 6.75f, 16f / 9f);
        Vector3 delta = go.transform.GetChild(0).position - before;
        Assert.AreEqual(5f, delta.x, 0.001f);
        Assert.AreEqual(3f, delta.y, 0.001f);
    }

    [Test]
    public void TilePoolIsReusedInsteadOfGrowing()
    {
        background.Refresh(Vector3.zero, 6.75f, 16f / 9f);
        int count = go.transform.childCount;
        for (int i = 0; i < 5; i++)
        {
            background.Refresh(new Vector3(500f * i, -300f * i, 0f), 6.75f, 16f / 9f);
            background.Refresh(Vector3.zero, 6.75f, 16f / 9f);
        }
        Assert.AreEqual(count, go.transform.childCount, "滚动只回收重摆，不该越铺越多");

        // 瓷砖是 27 单位见方，视野得跨过瓷砖边界块数才会变。
        // 6.75 和 14 都还只覆盖 2×2（4 块），拿它们对比永远相等 —— 必须取
        // 跨过 27 的两组，才测得到"多余的块被停用"。
        background.Refresh(Vector3.zero, 14f, 16f / 9f);
        int smallActive = ActiveTiles().Length;
        background.Refresh(Vector3.zero, 30f, 16f / 9f);
        Assert.Greater(ActiveTiles().Length, smallActive,
            "视野变大后要铺更多块（小视野 " + smallActive + " 块）");
    }

    [Test]
    public void MissingResourceDisablesBackground()
    {
        LogAssert.Expect(LogType.Warning,
            "[LabBackground] 找不到背景纹理 Resources/Backgrounds/does-not-exist");
        background.resource = "Backgrounds/does-not-exist";
        background.Load();
        Assert.IsFalse(background.enabled);
    }

    [Test]
    public void LevelDataCopyCarriesBackgroundAndMergeKeepsReceiver()
    {
        var source = new LabLevelData { background = "level2" };
        var copy = new LabLevelData();
        copy.CopyFrom(source);
        Assert.AreEqual("level2", copy.background);

        var receiver = new LabLevelData { background = "level1" };
        receiver.MergeFrom(source);
        Assert.AreEqual("level1", receiver.background, "合并时背景这类单值字段保留接收方");

        copy.CopyFrom(null);
        Assert.AreEqual("", copy.background);
    }

    SpriteRenderer[] ActiveTiles()
    {
        var all = go.GetComponentsInChildren<SpriteRenderer>(true);
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var tile in all)
            if (tile.gameObject.activeSelf) list.Add(tile);
        return list.ToArray();
    }
}