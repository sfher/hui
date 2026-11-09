using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabRespawnPointTests
{
    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        labGo = new GameObject("RespawnPointLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        labGo.SetActive(true);
        Physics2D.gravity = Vector2.zero;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        Physics2D.gravity = previousGravity;
    }

    [UnityTest]
    public IEnumerator OnePlayerEnteringMovesBothToFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);

        Assert.IsTrue(checkpoint.IsTouching(lab.PlayerA), "重生点没有记录进入的 A");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f,
            "A 应固定在重生点左侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f,
            "B 应固定在重生点右侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator PlayerTeleportedIntoCheckpointDoesNotTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(0, resetCount, "被传送/复活直接放进区域不算“进入”");
        Assert.IsNull(lab.ActiveRespawnPoint);

        // 先离开，再走进来，才算一次完整进入。
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "离开后重新进入应触发");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator PlayersAlreadyInsideWhenPointStartsDoNotTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(checkpointPosition);
        lab.PlayerB.Teleport(checkpointPosition + Vector2.right);
        yield return new WaitForFixedUpdate();

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(0, resetCount, "启动时就在区域里不算进入");
        Assert.IsNull(lab.ActiveRespawnPoint);

        yield return MoveBothOutside(checkpointPosition);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "全员离开后再进入应重新触发");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointResetsMapButKeepsForms()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA));
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        lab.PlayerB.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetVelocity(Vector2.right * 4f);
        lab.PlayerB.SetVelocity(Vector2.left * 3f);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        MoveBody(lab.PlayerA, checkpointPosition);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "每次进入只应重置地图一次");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
        Assert.AreEqual(0, lab.PlayerB.KeyCount);
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity);
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left),
            "活着进点应保留 A 的形态");
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerB.GetEdge(LabPlayer.Edge.Top),
            "活着进点应保留 B 的边框状态");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerA.GetEdge(LabPlayer.Edge.Top));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Left));
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator BothPlayersEnteringTogetherTreatItAsOneTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(Outside(checkpointPosition) + Vector2.left);
        lab.PlayerB.Teleport(Outside(checkpointPosition) + Vector2.right);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        MoveBody(lab.PlayerA, checkpointPosition + Vector2.left);
        MoveBody(lab.PlayerB, checkpointPosition + Vector2.right);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "两人同时进入仍只视为一次触发");
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
    }

    [UnityTest]
    public IEnumerator CheckpointTriggersAgainOnlyAfterEveryoneLeaves()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        yield return Activate(checkpointPosition);
        Assert.AreEqual(1, resetCount);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, resetCount, "两人仍在触发器内时不得重复重置");

        yield return MoveBothOutside(checkpointPosition);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(2, resetCount, "全部离开后重新进入应再次触发");
    }

    [UnityTest]
    public IEnumerator SingleDeathWaitsForTeammate()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(lab.PlayerA.IsAlive, "单人死亡应保持死亡等待");
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(0, resetCount, "队友还活着时不重置地图");
        Assert.IsFalse(lab.PlayerA.GetComponent<Collider2D>().enabled, "死亡等待时碰撞箱关闭");
        Assert.AreEqual(RigidbodyType2D.Kinematic,
            lab.PlayerA.GetComponent<Rigidbody2D>().bodyType, "死亡等待时冻结刚体");
        Assert.IsFalse(lab.PlayerA.GetComponentInChildren<SpriteRenderer>().enabled,
            "死亡等待时隐藏");
    }

    [UnityTest]
    public IEnumerator BothDeathsRespawnTogetherAtFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 12f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        // 复活位置要在**复活那一刻**读：#98 —— 这个用例的场地没有地板，玩家
        // 复活后（WakeFromDeath 把 Kinematic 改回 Dynamic）会立刻自由落体，
        // 重力换算下来约 50 格/秒²，等两个物理步就已经掉了 0.3 格。
        // 之前的断言在几帧之后才读位置，量到的其实是"掉到哪了"，不是"落在哪"。
        // Kill() 到复活是同步走完的（RespawnRecordedPlayers 当场调用 ReviveAt），
        // 所以 Kill() 一返回就读，拿到的就是落点。
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, resetCount, "只有一个死亡时不触发重生");
        Assert.IsFalse(lab.PlayerA.IsAlive);

        lab.PlayerB.Kill();

        // 双人死亡结算被推迟到下一次 Update（死亡来自物理回调，不能在同一个物理步里
        // 先生成再销毁）。所以 Kill() 一返回时位置还是死亡现场，必须先让 Update 跑一次。
        yield return null;

        Vector2 slotA = lab.PlayerA.transform.position;
        Vector2 slotB = lab.PlayerB.transform.position;
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(1, resetCount, "两人都死亡后只重置一次地图");
        Assert.AreEqual(checkpointPosition.x - 1f, slotA.x, 0.001f, "A 落在激活点左侧一格");
        Assert.AreEqual(checkpointPosition.x + 1f, slotB.x, 0.001f, "B 落在激活点右侧一格");
        Assert.AreEqual(checkpointPosition.y, slotA.y, 0.001f, "A 的落点高度就是激活点高度");
        Assert.AreEqual(checkpointPosition.y, slotB.y, 0.001f, "B 的落点高度就是激活点高度");
    }

    [UnityTest]
    public IEnumerator BothDeathsAlwaysRespawnWithNormalForms()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);

        // 激活时两人都带着碎边/红热边。
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        lab.PlayerB.Teleport(Outside(checkpointPosition) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        lab.PlayerA.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        lab.PlayerB.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Hot2);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        yield return MoveBothOutside(checkpointPosition);
        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        AssertNormal(lab.PlayerA, "两人都死亡时应以普通状态复活 A，不重放碎边/红热边");
        AssertNormal(lab.PlayerB, "两人都死亡时应以普通状态复活 B，不重放碎边/红热边");
    }

    [UnityTest]
    public IEnumerator OneDeadActivationRevivesDeadPlayerNormalAndKeepsLivingForm()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(lab.PlayerB.IsAlive);

        lab.PlayerA.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerB.IsAlive, "存活者进点应复活队友");
        AssertNormal(lab.PlayerB, "死的人应以普通状态复活");
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerA.GetEdge(LabPlayer.Edge.Top),
            "活着进点应保留当前形态");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left));
    }

    [UnityTest]
    public IEnumerator SurvivingPlayerEnteringCheckpointRevivesDeadTeammate()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(lab.PlayerB.IsAlive);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerB.IsAlive, "存活者进入重生点应复活队友");
        Assert.AreEqual(1, resetCount);
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
    }

    /// <summary>
    /// 两人同时压着相邻的两根刺：一次物理步里会产生四条接触回调。重生如果发生在回调中间，
    /// 同一物理步剩下的回调会把刚复活的人再杀一次，出现“两人都死、只活一个”。
    /// 重生必须推迟到回调全部结束之后。
    /// </summary>
    [UnityTest]
    public IEnumerator BothDeathsFromOverlappingSpikesInOneStepReviveTogether()
    {
        var checkpointPosition = new Vector2(-8f, 1f);
        MakeCheckpoint(checkpointPosition);
        MakeSpike(0);
        MakeSpike(1);
        MakeSpike(2);
        yield return null;

        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        // 每名玩家的碰撞箱都横跨两根刺，制造同一物理步里的多条致死回调。
        for (int trial = 0; trial < 4; trial++)
        {
            float ax = 0.70f + 0.02f * trial;
            float bx = 1.70f + 0.02f * trial;
            lab.PlayerA.Teleport(new Vector2(ax, 0.5f));
            lab.PlayerB.Teleport(new Vector2(bx, 0.5f));
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(lab.PlayerA.IsAlive, $"第 {trial} 次：A 应复活");
            Assert.IsTrue(lab.PlayerB.IsAlive, $"第 {trial} 次：B 应复活");
            Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
            Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
            Assert.AreEqual(trial + 1, resetCount, "每次两人都死亡只重置一次地图");

            // 回安全位置，让重生点判定“全员离开”，再开始下一轮。
            lab.PlayerA.Teleport(new Vector2(-8f, 5f));
            lab.PlayerB.Teleport(new Vector2(-6f, 5f));
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator BothDeathsWithoutCheckpointRespawnAtSpawn()
    {
        lab.deathWhenNoCheckpoint = TuningLab.DeathFallback.RespawnAtSpawn;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        var spawnA = (Vector2)lab.PlayerA.transform.position;
        var spawnB = (Vector2)lab.PlayerB.transform.position;

        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, resetCount, "单人死亡不重开本关");
        Assert.IsFalse(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);

        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "两人都死亡后只重开一次");
        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(spawnA.x, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(spawnA.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(spawnB.x, lab.PlayerB.transform.position.x, 0.001f);
        Assert.AreEqual(spawnB.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator BothDeathsWithoutCheckpointDestroyBothPlayers()
    {
        lab.deathWhenNoCheckpoint = TuningLab.DeathFallback.DestroyPlayer;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(lab.PlayerB.IsAlive, "单人死亡只隐藏等待");

        lab.PlayerB.Kill();
        yield return null;
        yield return null;

        Assert.IsTrue(lab.PlayerA == null, "两人都死亡后按 DestroyPlayer 销毁");
        Assert.IsTrue(lab.PlayerB == null);
    }

    [UnityTest]
    public IEnumerator BothDeathsWithNoActivePointForcesNearestCheckpoint()
    {
        var near = MakeCheckpoint(new Vector2(0f, 5f));
        var far = MakeCheckpoint(new Vector2(20f, 5f));
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);

        // 两个点都没被“走进”过：A 在近点附近死，B 在远点附近死，最后一次死亡决定落点。
        MoveBody(lab.PlayerA, new Vector2(2f, 3f));
        MoveBody(lab.PlayerB, new Vector2(31f, 3f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsNull(lab.ActiveRespawnPoint, "两人都没有走进过任何重生点");

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreNotSame(near, lab.ActiveRespawnPoint);
        Assert.AreSame(far, lab.ActiveRespawnPoint, "没激活过时强制触发离最后一次死亡最近的点");
        Assert.IsTrue(lab.PlayerA.IsAlive, "两人都死亡应强制触发重生点");
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(1, resetCount, "强制触发只重置一次地图");
        Assert.AreEqual(19f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(21f, lab.PlayerB.transform.position.x, 0.001f);
        AssertNormal(lab.PlayerA, "强制触发应以普通状态复活");
        AssertNormal(lab.PlayerB, "强制触发应以普通状态复活");
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointRestoresPickedLevelKey()
    {
        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(labGo.transform, false);
        var level = levelGo.AddComponent<LabLevel>();
        var data = new LabLevelData();
        data.keys.Add(new LabKeySpec(new Vector2(3f, 3f)));
        level.CopyFrom(data);
        level.BuildRuntime(levelGo.transform, null);

        var key = level.GetComponentInChildren<LabKey>();
        Assert.IsNotNull(key);
        Assert.IsTrue(key.TryCollect(lab.PlayerA));
        yield return null;
        Assert.IsNull(level.GetComponentInChildren<LabKey>(), "拾取后世界里的钥匙应消失");

        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);

        Assert.IsNotNull(level.GetComponentInChildren<LabKey>(), "触发重生点后钥匙应刷新");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
    }

    [UnityTest]
    public IEnumerator ResetPlayersReturnsTeamToActiveCheckpoint()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);

        // 走远之后按 R：应回激活过的重生点，而不是出生点。
        MoveBody(lab.PlayerA, new Vector2(200f, 5f));
        MoveBody(lab.PlayerB, new Vector2(202f, 5f));
        lab.ResetPlayers();

        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f,
            "R 重开应把 A 放回重生点左侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f,
            "R 重开应把 B 放回重生点右侧 1 格");
        AssertNormal(lab.PlayerA, "R 重开应以普通状态复活");
        AssertNormal(lab.PlayerB, "R 重开应以普通状态复活");
    }

    [UnityTest]
    public IEnumerator CheckpointResetRestoresOnlyNearbyBrokenIce()
    {
        lab.mapResetRadius = 10f;
        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(labGo.transform, false);
        var level = levelGo.AddComponent<LabLevel>();
        var data = new LabLevelData();
        data.ices.Add(new LabIceSpec(new Vector2Int(3, 5), 1));
        data.ices.Add(new LabIceSpec(new Vector2Int(60, 5), 2));
        level.CopyFrom(data);
        level.BuildRuntime(levelGo.transform, null);

        LabIce near = null, far = null;
        for (int i = 0; i < LabIce.All.Count; i++)
        {
            if (LabIce.All[i] != null && LabIce.All[i].cell.x == 3) near = LabIce.All[i];
            if (LabIce.All[i] != null && LabIce.All[i].cell.x == 60) far = LabIce.All[i];
        }
        Assert.IsNotNull(near, "近处冰块应生成");
        Assert.IsNotNull(far, "远处冰块应生成");
        near.BreakOne();
        far.BreakOne();

        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);

        Assert.IsFalse(near.Broken, "范围内的碎冰应恢复");
        Assert.IsTrue(far.Broken, "范围外的碎冰应保持破坏（范围重置）");
    }

    /// <summary>把 A 放在区域外，等重生点采样一次，再从外面走进中心触发。</summary>
    IEnumerator Activate(Vector2 center)
    {
        lab.PlayerA.Teleport(Outside(center));
        lab.PlayerB.Teleport(Outside(center) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        MoveBody(lab.PlayerA, center);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    IEnumerator MoveBothOutside(Vector2 center)
    {
        MoveBody(lab.PlayerA, Outside(center));
        MoveBody(lab.PlayerB, Outside(center) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    static void MakeKinematic(LabPlayer a, LabPlayer b)
    {
        a.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        b.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>四条边和形态都必须是普通状态。</summary>
    static void AssertNormal(LabPlayer player, string message)
    {
        Assert.AreEqual(LabPlayer.EdgeState.Normal, player.GetEdge(LabPlayer.Edge.Top), message);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, player.GetEdge(LabPlayer.Edge.Bottom), message);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, player.GetEdge(LabPlayer.Edge.Left), message);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, player.GetEdge(LabPlayer.Edge.Right), message);
        Assert.AreEqual(LabForm.Normal, player.Form, message);
    }

    /// <summary>直接挪刚体，不走 Teleport —— 这样才算一次真正的“走进去”。</summary>
    static void MoveBody(LabPlayer player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>重生点外、离 3×3 边缘还有半格的位置。</summary>
    static Vector2 Outside(Vector2 center) => center + Vector2.down * 2.2f;

    LabRespawnPoint MakeCheckpoint(Vector2 position)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = position;
        var point = go.AddComponent<LabRespawnPoint>();
        point.ApplyShape();
        return point;
    }

    void MakeSpike(int cellX)
    {
        var cell = new Vector2Int(cellX, 0);
        var spec = new LabSpikeSpec(cell, LabSpikeDir.Up);
        var go = new GameObject("Spike");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = spec.StripCenter;
        var spike = go.AddComponent<LabSpike>();
        spike.cell = cell;
        spike.dir = LabSpikeDir.Up;
        spike.ApplyShape();
    }

    LabKey MakeKey()
    {
        var go = new GameObject("Key");
        go.transform.SetParent(labGo.transform, false);
        return go.AddComponent<LabKey>();
    }
}
