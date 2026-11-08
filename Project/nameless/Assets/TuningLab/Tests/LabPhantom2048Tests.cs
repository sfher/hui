using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 2048 幻影（Issue #118）：只剩一条边（矛形）的幻影会自己朝那条边指的方向一直滑，
/// 撞墙才停，过程中不受重力/阻力、速度固定为二级速度的最小值、不会与 B 相碰。
/// </summary>
public sealed class LabPhantom2048Tests
{
    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("Slick") { friction = 0f, bounciness = 0f };

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("Root");
        root.transform.SetParent(labGo.transform, false);

        LabPhantom.All.Clear();
        LabLightBridgeBlock.All.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var p in LabPhantom.All.ToArray())
            if (p != null) Object.Destroy(p.gameObject);
        LabPhantom.All.Clear();
        Object.Destroy(root);
        Object.Destroy(labGo);
        yield return null;
    }

    // mask 记录的是**碎掉**的边。矛形 = 碎 3 条，只剩 1 条。
    const int KeepTop = LabForms.BottomBit | LabForms.LeftBit | LabForms.RightBit;
    const int KeepRight = LabForms.TopBit | LabForms.BottomBit | LabForms.LeftBit;

    // ---- 触发 ---------------------------------------------------------

    [Test]
    public void SpearPhantomIs2048()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        // 剩一条边（碎 3 条）→ 矛。
        var phantom = SpawnPhantom(a, b, KeepTop);
        Assert.AreEqual(LabForm.Spear, phantom.Form);
        Assert.IsTrue(phantom.Is2048, "矛形幻影就是 2048");
    }

    [Test]
    public void NonSpearFormsAreNot2048()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        foreach (int mask in new[] { 0, LabForms.TopBit | LabForms.BottomBit, LabForms.TopBit | LabForms.RightBit, LabForms.TopBit })
        {
            var phantom = SpawnPhantom(a, b, mask);
            Assert.IsFalse(phantom.Is2048, "掩码 " + mask + " 的形状不是矛，不该是 2048");
        }
    }

    // ---- 移动 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator SlidesInRemainingEdgeDirection()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);
        // 剩的边在上 → 往上滑。
        var phantom = SpawnPhantom(a, b, KeepTop);
        yield return null;

        int startY = phantom.Cell.y;
        for (int i = 0; i < 240; i++) yield return new WaitForFixedUpdate();

        Assert.Greater(phantom.Cell.y, startY, "应该往上滑，实际 " + phantom.Cell);
        Assert.AreEqual(phantom.Cell.x, 0, "只能沿着那条边的方向，横向不动");
    }

    [UnityTest]
    public IEnumerator SlidesRightWhenRemainingEdgeIsRight()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);
        var phantom = SpawnPhantom(a, b, KeepRight);
        yield return null;

        for (int i = 0; i < 240; i++) yield return new WaitForFixedUpdate();
        Assert.Greater(phantom.Cell.x, 0, "剩的是右边 → 往右滑，实际 " + phantom.Cell);
    }

    [UnityTest]
    public IEnumerator StopsWhenHittingWall()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        // 在 (0,3) 放一堵墙，幻影从 (0,0) 往上滑，最多到 (0,2)。
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root.transform, false);
        wall.transform.position = new Vector3(0.5f, 3.5f, 0f);
        var bc = wall.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        bc.sharedMaterial = slick;
        yield return null;

        var phantom = SpawnPhantom(a, b, KeepTop);
        yield return null;

        for (int i = 0; i < 400; i++) yield return new WaitForFixedUpdate();

        Assert.AreEqual(2, phantom.Cell.y, "撞墙前一格停下，实际 " + phantom.Cell);
        Assert.IsFalse(phantom.Sliding, "停下后不再滑");
    }

    [UnityTest]
    public IEnumerator SpeedIsSecondTierMinimum()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);
        var phantom = SpawnPhantom(a, b, KeepTop);
        yield return null;

        // 从 y=0 滑到 y=4（4 格），按二级下界速度算，应该在 4/speed 秒左右。
        float expectedSpeed = lab.SpeedForCells(lab.Boundary(1));
        float startTime = Time.time;
        int guard = 0;
        while (phantom.Cell.y < 4 && guard++ < 2000)
            yield return new WaitForFixedUpdate();
        float elapsed = Time.time - startTime;

        float expectedTime = 4f / Mathf.Max(0.01f, expectedSpeed);
        Assert.AreEqual(expectedTime, elapsed, expectedTime * 0.6f,
            "滑 4 格大约要 4/速度 秒（速度 " + expectedSpeed.ToString("0.0") + "）");
    }

    // ---- 不与 B 相碰 ---------------------------------------------------

    [UnityTest]
    public IEnumerator DoesNotCollideWithTarget()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        // B 站在幻影滑动的路线上。
        b.Teleport(new Vector2(0.5f, 2.5f));
        yield return null;

        var phantom = SpawnPhantom(a, b, KeepTop);
        yield return null;

        // 等它滑过 B 所在的那一格。
        int guard = 0;
        while (phantom.Cell.y < 3 && guard++ < 600)
            yield return new WaitForFixedUpdate();

        Assert.GreaterOrEqual(phantom.Cell.y, 3, "要能穿过 B 继续滑，实际 " + phantom.Cell);
        Assert.IsFalse(b.Suspended, "B 不该被幻影吸进去（2048 不是大炮）");
        Assert.Greater(b.BodyBounds.min.y, -100f, "B 应该还在场上");
    }

    [UnityTest]
    public IEnumerator IgnoresSpiritTargetToo()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        // B 变成灵体（碰撞箱是 trigger），挡在路上。
        b.DebugEnterSpirit();
        b.Teleport(new Vector2(0.5f, 2.5f));
        yield return null;

        var phantom = SpawnPhantom(a, b, KeepTop);
        yield return null;

        int guard = 0;
        while (phantom.Cell.y < 3 && guard++ < 600)
            yield return new WaitForFixedUpdate();

        Assert.GreaterOrEqual(phantom.Cell.y, 3,
            "灵体态的 B 也不该挡住 2048，实际 " + phantom.Cell);
    }

    // ---- 不铺桥 -------------------------------------------------------

    [Test]
    public void SpearPhantomDoesNotBuildBridge()
    {
        var a = MakePlayer("A", TuningLab.A_Pixels, true);
        var b = MakePlayer("B", TuningLab.B_Pixels, false);
        Link(a, b);

        var phantom = SpawnPhantom(a, b, KeepTop);
        Assert.IsFalse(phantom.HasLightBridge, "只有电梯形铺桥，矛形不铺");
        Assert.AreEqual(0, phantom.Bridge.Count);
    }

    // ---- 工具 ---------------------------------------------------------

    LabPlayer MakePlayer(string name, int pixels, bool isFrame)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector2(-30f, 0f);

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(pixels / (float)TuningLab.CellPixels, pixels / (float)TuningLab.CellPixels);
        bc.sharedMaterial = slick;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = lab.GravityScale;

        var p = go.AddComponent<LabPlayer>();
        p.lab = lab;
        p.pixels = pixels;
        p.canBeRedHot = isFrame;
        p.scheme = isFrame ? LabPlayer.Scheme.P1 : LabPlayer.Scheme.P2;
        return p;
    }

    static void Link(LabPlayer a, LabPlayer b)
    {
        a.other = b;
        b.other = a;
    }

    LabPhantom SpawnPhantom(LabPlayer source, LabPlayer target, int spiritMask)
    {
        return LabPhantom.Spawn(lab, source, target, Vector2Int.zero,
            spiritMask, source.pixels, source.canBeRedHot);
    }
}
