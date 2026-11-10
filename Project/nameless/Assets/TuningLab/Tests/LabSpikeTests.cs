using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 地刺（16×6 像素贴格边，碰到即死）的规格。
/// 测三件事：数据 → 物件这条生成管子、接触致死、以及满速冲过去会不会穿过去。
/// </summary>
public sealed class LabSpikeTests
{
    /// <summary>
    /// 比游戏里能跑出的最快速度还高一档：满级 4 挡的顶棚是 √(16格×100) = 40，
    /// 炮出口再加 2g×4格 = √2000 ≈ 44.7。70 → 每个物理步走 1.4 格，
    /// 而"玩家 1 格 + 刺带 0.375 格"的重叠窗口只有 1.375 格 —— 这是最坏相位。
    /// </summary>
    const float WorstCaseSpeed = 70f;

    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;
    string saveDir;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };
        // 死亡强制触发重生点会自动存档：测试期间改写到临时目录。
        saveDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "hui-spike-" + System.Guid.NewGuid().ToString("N"));
        LabCheckpointStore.DirectoryOverride = saveDir;
        LabCheckpointStore.PendingContinue = null;

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("SpikeRoot");
        root.transform.SetParent(labGo.transform, false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        LabCheckpointStore.DirectoryOverride = null;
        LabCheckpointStore.PendingContinue = null;
        if (System.IO.Directory.Exists(saveDir)) System.IO.Directory.Delete(saveDir, true);
    }

    // 几何 ---------------------------------------------------------------

    [Test]
    public void TheFourVariantsHugTheirCellEdge()
    {
        var up = new LabSpikeSpec(new Vector2Int(4, 2), LabSpikeDir.Up);
        Assert.AreEqual(new Vector2(1f, 0.375f), up.StripSize, "横刺 = 16×6 像素");
        Assert.AreEqual(new Vector2(4.5f, 2.1875f), up.StripCenter, "朝上：贴本格底边");
        Assert.AreEqual(new Vector2Int(4, 1), up.BaseCell, "朝上：底座踩下一格");

        var down = new LabSpikeSpec(new Vector2Int(4, 2), LabSpikeDir.Down);
        Assert.AreEqual(new Vector2(4.5f, 2.8125f), down.StripCenter, "朝下：贴本格顶边");
        Assert.AreEqual(new Vector2Int(4, 3), down.BaseCell);

        var left = new LabSpikeSpec(new Vector2Int(4, 2), LabSpikeDir.Left);
        Assert.AreEqual(new Vector2(0.375f, 1f), left.StripSize, "竖刺 = 6×16 像素");
        Assert.AreEqual(new Vector2(4.8125f, 2.5f), left.StripCenter, "朝左：贴本格右边");
        Assert.AreEqual(new Vector2Int(5, 2), left.BaseCell);

        var right = new LabSpikeSpec(new Vector2Int(4, 2), LabSpikeDir.Right);
        Assert.AreEqual(new Vector2(4.1875f, 2.5f), right.StripCenter, "朝右：贴本格左边");
        Assert.AreEqual(new Vector2Int(3, 2), right.BaseCell);
    }

    // 生成管子：关卡数据 → 场景物件 --------------------------------------

    [UnityTest]
    public IEnumerator LevelDataBuildsOneSpikePerCell()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceSpike(new Vector2Int(-6, 0), LabSpikeDir.Up);
        level.Data.PlaceSpike(new Vector2Int(3, 4), LabSpikeDir.Left);
        level.Data.Normalize();

        level.BuildRuntime(root.transform, null);

        var spikes = root.GetComponentsInChildren<LabSpike>();
        Assert.AreEqual(2, spikes.Length, "一格一根，两根都长出来");

        var first = new LabSpikeSpec(new Vector2Int(-6, 0), LabSpikeDir.Up);
        var second = new LabSpikeSpec(new Vector2Int(3, 4), LabSpikeDir.Left);
        Assert.AreEqual((Vector3)first.StripCenter, spikes[0].transform.position, "按格排序后落在刺带中心");
        Assert.AreEqual((Vector3)second.StripCenter, spikes[1].transform.position);
        Assert.AreEqual(LabSpikeDir.Left, spikes[1].dir);

        for (int i = 0; i < spikes.Length; i++)
        {
            var spec = new LabSpikeSpec(spikes[i].cell, spikes[i].dir);
            var body = spikes[i].GetComponent<BoxCollider2D>();
            Assert.IsTrue(body.isTrigger, "刺是触发器：不挡路，只杀人");
            Assert.AreEqual(spec.StripSize, body.size, "碰撞 = 16×6 那一条，一点不让");
            Assert.IsNotNull(spikes[i].GetComponent<SpriteRenderer>().sprite, "刺有看得见的贴图");
        }

        Assert.IsTrue(spikes[1].GetComponent<SpriteRenderer>().flipX, "朝左 = 水平翻");
        Assert.IsFalse(spikes[0].GetComponent<SpriteRenderer>().flipX, "朝上 = 不翻");
        Assert.IsFalse(spikes[0].GetComponent<SpriteRenderer>().flipY);

        yield return null;
    }

    // 接触致死 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator SteppingOnASpikeKills()
    {
        MakeFloor();
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up);   // 立在地板顶上那一格

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 0.55f));              // 脚底踩进刺带

        yield return WaitUntil(() => a == null, 2f);
    }

    [UnityTest]
    public IEnumerator SteppingOnASpikeKillsBothPlayers()
    {
        MakeFloor();
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up);

        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        b.Teleport(new Vector2(0.5f, 0.5f));

        yield return WaitUntil(() => b == null, 2f);
    }

    /// <summary>
    /// 满速横穿：证明连续碰撞检测真的把 0.375 格厚的刺带扫到了，没有穿过去。
    /// 这条是测量，不是保底 —— 哪天速度上限抬高导致穿模，它就该红。
    /// </summary>
    [UnityTest]
    public IEnumerator MaxSpeedSweepAcrossASpikeIsCaught()
    {
        MakeFloor();
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up);

        lab.groundBrake = 0f;          // 别把速度吃掉，要测的就是最坏速度
        lab.horizontalAirDrag = false;

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(-1.5f, 0.5f));
        a.SetVelocity(new Vector2(WorstCaseSpeed, 0f));

        yield return WaitUntil(() => a == null, 1f);
    }

    [UnityTest]
    public IEnumerator TouchingSpikeFromTheSideIsFatalForASidewaysVariant()
    {
        MakeFloor();
        // 竖着的刺：0.375 格厚、横着挡在玩家冲过去的路上，最容易穿模的一种摆法
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Right);

        lab.groundBrake = 0f;
        lab.horizontalAirDrag = false;

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(-1.5f, 0.5f));
        a.SetVelocity(new Vector2(WorstCaseSpeed, 0f));

        yield return WaitUntil(() => a == null, 1f);
    }

    [UnityTest]
    public IEnumerator ZeroOverlapAtHorizontalSpikeEdgeDoesNotKill()
    {
        MakeFloor();
        var spec = new LabSpikeSpec(new Vector2Int(0, 0), LabSpikeDir.Up);
        MakeSpike(spec.cell, spec.dir);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        float playerHalfWidth = TuningLab.A_Pixels / (2f * TuningLab.CellPixels);
        float spikeLeft = spec.StripCenter.x - spec.StripSize.x * 0.5f;
        a.Teleport(new Vector2(spikeLeft - playerHalfWidth, 0.5f));

        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsTrue(a != null, "玩家与横向刺只有零面积接触时不应触发死亡");
    }

    // 死亡的去向 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator SpikeDeathReleasesThePlayerInTheChamber()
    {
        MakeFloor();
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        a.other = b;
        b.other = a;

        Shatter(a, LabPlayer.Edge.Top);          // 口朝上
        yield return LoadInto(a, b, Vector2.down, 12f);
        Assert.IsTrue(b.Suspended, "先进膛");

        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up)
            .Touch(a.GetComponent<Collider2D>());

        Assert.IsFalse(b.Suspended, "膛里的人不能跟着宿主一起没");
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled, "B 的碰撞箱还回来");

        yield return null;
        yield return null;
        Assert.IsTrue(a == null, "踩刺的 A 被销毁");
        Assert.IsTrue(b != null);
    }

    [UnityTest]
    public IEnumerator SpikeDeathWithACheckpointRespawnsInsteadOfDestroying()
    {
        MakeFloor();
        MakeCheckpoint();
        bool mapReset = false;
        lab.MapReset += () => mapReset = true;

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up);
        a.Teleport(new Vector2(0.5f, 0.55f));

        yield return WaitUntil(() => mapReset, 2f);
        Assert.IsFalse(a == null, "有检查点 → 回检查点，不是把人删掉");
    }

    [UnityTest]
    public IEnumerator SpikeDeathWithNoCheckpointFollowsTheFallbackSetting()
    {
        MakeFloor();
        bool mapReset = false;
        lab.MapReset += () => mapReset = true;

        lab.deathWhenNoCheckpoint = TuningLab.DeathFallback.RespawnAtSpawn;
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        MakeSpike(new Vector2Int(0, 0), LabSpikeDir.Up);
        a.Teleport(new Vector2(0.5f, 0.55f));

        yield return WaitUntil(() => mapReset, 2f);
        Assert.IsFalse(a == null, "重开本关的挡位下不销毁 GameObject");
    }

    // 工具 ---------------------------------------------------------------

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    LabSpike MakeSpike(Vector2Int cell, LabSpikeDir dir)
    {
        var spec = new LabSpikeSpec(cell, dir);
        var go = new GameObject("Spike");
        go.transform.SetParent(root.transform, false);
        go.transform.position = spec.StripCenter;

        var spike = go.AddComponent<LabSpike>();
        spike.cell = cell;
        spike.dir = dir;
        spike.ApplyShape();
        return spike;
    }

    LabRespawnPoint MakeCheckpoint()
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(-8f, 1f, 0f);
        var point = go.AddComponent<LabRespawnPoint>();
        lab.RegisterRespawnPoint(point);
        return point;
    }

    LabPlayer MakePlayer(string name, int pixels, bool isFrame)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector2(0f, pixels / (2f * TuningLab.CellPixels));

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

    static void Heat(LabPlayer p, LabPlayer.Edge edge)
    {
        p.StrikeEdge(edge, true);
    }

    static void Shatter(LabPlayer p, LabPlayer.Edge edge)
    {
        Heat(p, edge);
        p.StrikeEdge(edge, false);
    }

    /// <summary>把 B 以冲刺态送进膛里（和 LabCannonTests 同一套路）。</summary>
    IEnumerator LoadInto(LabPlayer a, LabPlayer b, Vector2 dir, float speed, float gap = 1.5f)
    {
        Vector2 start = (Vector2)a.transform.position - dir * (a.HalfHeight + b.HalfHeight + gap);
        b.Teleport(start);
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = dir,
            speed = speed,
            exitSpeed = speed,
            timer = (gap + 0.5f) / Mathf.Max(0.01f, speed)
        });

        yield return WaitUntil(() => a.Loaded || !b.InDashState, 3f);
        Assert.IsTrue(a.Loaded, "冲刺态撞进 u 形口应装填");
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(condition(), $"等待条件超时（{timeoutSeconds}s）");
    }
}
