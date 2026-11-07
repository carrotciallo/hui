using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 绳子规格（Issue #40）。
/// 两条症状：判定箱比绳本身左右各多出 7 格；三级以上撞绳弹回不保留入射速度。
/// 绳一律走关卡数据（LabLevelData → LabLevel.BuildRuntime），和关卡编辑器里摆出来的是同一条几何。
/// 竖绳另按“零/一级当墙、二级穿过、三级以上按横绳公式弹回”验收。
/// 绳不参与地形锻造：接触 / 下落 / 穿过都不改边态，只有三级以上的弹回把接触边点到热二；
/// 热二再被弹回不会塌框——弹回不是地形撞击，不能拿“热二遇热就死”那套状态机砸自己。
/// </summary>
public sealed class LabRopeTests
{
    [Test]
    public void RopeBouncePreservesIncomingSpeedAndAddsOneTier()
    {
        var go = new GameObject("RopeBounceLab");
        go.SetActive(false);
        var lab = go.AddComponent<TuningLab>();
        lab.autoBuild = false;
        go.SetActive(true);
        lab.gravity = 50f;
        lab.tierSpacingCells = 4f;

        const float incoming = 42f;
        float incomingCells = lab.CellsForSpeed(incoming);
        float bounceCells = lab.CellsForSpeed(lab.RopeBounceSpeed(incoming));

        Assert.That(bounceCells, Is.EqualTo(incomingCells + lab.tierSpacingCells).Within(0.001f));
        Assert.That(bounceCells, Is.GreaterThan(incomingCells));

        Object.DestroyImmediate(go);
    }

    GameObject labGo;
    TuningLab lab;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);

        labGo = new GameObject("RopeLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        lab.gravity = 50f;
        lab.tierFirstCells = 4f;
        lab.tierSpacingCells = 4f;
        lab.tierCount = 6;
        labGo.SetActive(true);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    // 判定箱 = 绳本身，不多不少 ----------------------------------------

    [UnityTest]
    public IEnumerator VerticalRopeHitboxIsExactlyTheRope()
    {
        var level = BuildLevel(new LabRopeSpec(new Vector2(0f, 4f), 4));

        var rope = level.GetComponentInChildren<LabVerticalRope>();
        Assert.IsNotNull(rope, "关卡数据里的竖绳没生成");

        var body = rope.GetComponent<BoxCollider2D>();
        // 6px 宽 × 4 格高：左右各只该有 0.1875 格，多出来的都是幽灵判定
        Assert.AreEqual(6f / 16f, body.bounds.size.x, 0.001f,
            $"竖绳判定箱宽度不该超过素条：实际 {body.bounds.size.x:F3} 格");
        Assert.AreEqual(4f, body.bounds.size.y, 0.001f);
        Assert.AreEqual(0f, body.bounds.center.x, 0.001f);
        Assert.AreEqual(4f, body.bounds.center.y, 0.001f,
            $"判定箱中心不在绳的中心：{body.bounds.center}");

        // 判定箱必须和画出来的素条重合，而不是比它宽
        var strips = rope.GetComponentsInChildren<SpriteRenderer>();
        Assert.AreEqual(4, strips.Length, "4 格的绳就该有 4 段素条");
        float drawnWidth = strips[0].bounds.size.x;
        Assert.AreEqual(body.bounds.size.x, drawnWidth, 0.001f, "判定箱比绳宽，玩家会在没画出来的地方被挡住");

        yield return null;
    }

    [UnityTest]
    public IEnumerator HorizontalRopeHitboxIsExactlyTheRope()
    {
        var level = BuildLevel(new LabRopeSpec(new Vector2(0f, 3f), 6,
            0, LabRopeOrientation.Horizontal));

        yield return new WaitForFixedUpdate();

        var rope = level.GetComponentInChildren<LabHorizontalRope>();
        Assert.IsNotNull(rope, "关卡数据里的横绳没生成");

        var body = rope.GetComponent<BoxCollider2D>();
        Assert.AreEqual(6f, body.bounds.size.x, 0.001f);
        Assert.AreEqual(4f / 16f, body.bounds.size.y, 0.001f,
            $"横绳判定箱厚度不该超过 4px：实际 {body.bounds.size.y:F3} 格");

        var sr = rope.GetComponentInChildren<SpriteRenderer>();
        Assert.AreEqual(body.bounds.size.x, sr.bounds.size.x, 0.001f, "判定箱比画出来的绳长");
    }

    [UnityTest]
    public IEnumerator HorizontalRopeDoesNotJudgeSixCellsPastEitherEnd()
    {
        BuildLevel(new LabRopeSpec(new Vector2(0f, 3f), 4,
            0, LabRopeOrientation.Horizontal));

        yield return new WaitForFixedUpdate();

        Assert.IsNotNull(Physics2D.OverlapPoint(new Vector2(-1.5f, 3f)));
        Assert.IsNotNull(Physics2D.OverlapPoint(new Vector2(1.5f, 3f)));
        Assert.IsNull(Physics2D.OverlapPoint(new Vector2(-7.5f, 3f)),
            "4 格横绳左侧多判了 6 格");
        Assert.IsNull(Physics2D.OverlapPoint(new Vector2(7.5f, 3f)),
            "4 格横绳右侧多判了 6 格");
    }

    // 撞绳弹回：保留入射速度 + 一个等级间距 ----------------------------

    [UnityTest]
    public IEnumerator FallingOntoHorizontalRopeBouncesFasterThanItCameIn()
    {
        BuildLevel(new LabRopeSpec(new Vector2(0f, 3f), 6,
            0, LabRopeOrientation.Horizontal));

        // 从 22 格以上自由落体：入射已经在三级以上，旧规则会把人打回 40 的固定值（比进绳还慢）
        var player = MakePlayer(new Vector2(0f, 26f));

        float lastDown = 0f;
        int steps = 0;
        while (player.Velocity.y <= 0f && steps < 200)
        {
            lastDown = Mathf.Max(lastDown, -player.Velocity.y);
            yield return new UnityEngine.WaitForFixedUpdate();
            steps++;
        }

        Assert.Greater(steps, 5, "第一帧就反弹：碰撞根本没发生，用例没跑到东西");
        Assert.GreaterOrEqual(lab.TierOfSpeed(lastDown), 3,
            $"入射要够到三级以上才谈得上加档：实际折算 {lab.CellsForSpeed(lastDown):F1} 格");

        float bounce = player.Velocity.y;
        Assert.Greater(bounce, lastDown,
            $"弹回必须比入射快（入射 {lastDown:F2} → 弹回 {bounce:F2}）");
        Assert.AreEqual(lab.RopeBounceSpeed(lastDown), bounce, 2.5f,
            "弹回速度 = 入射速度折算格数 + 一个等级间距");
    }

    // 竖绳：零/一级当墙、二级穿过、三级以上按横绳公式弹回 -------------

    sealed class VerticalRopeRun
    {
        public LabPlayer player;
        public LabVerticalRope rope;
        public readonly List<LabVerticalRope.RopeContact> contacts = new List<LabVerticalRope.RopeContact>();
        public readonly List<Vector2> incoming = new List<Vector2>();
        public readonly List<Vector2> velocities = new List<Vector2>();
    }

    [UnityTest]
    public IEnumerator VerticalRopeBlocksPlayerBelowTierTwo()
    {
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(24f, 0f), 0.35f, run);

        Assert.AreEqual(1, run.contacts.Count, "一级撞绳应该只判一次");
        var hit = run.contacts[0];
        Assert.AreEqual(1, hit.tier, $"24 的入射应该是一级：实际 {hit.tier}");
        Assert.IsFalse(hit.passed, "一级不该穿过");
        Assert.IsFalse(hit.bounced, "一级不该弹回");
        Assert.Greater(Mathf.Abs(hit.normal.x), 0.5f,
            $"从绳下半段侧撞进来，法线应该是水平的；实际 {hit.normal}");
        Assert.Less(run.player.transform.position.x, -0.3f, "一级被绳挡住，不该跑到另一侧");
    }

    [UnityTest]
    public IEnumerator VerticalRopeLetsTierTwoPlayerPassWithoutSticking()
    {
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(31f, 0f), 0.45f, run);

        Assert.AreEqual(1, run.contacts.Count, "穿过只应该判一次，反复判会把玩家卡在绳里");
        var hit = run.contacts[0];
        Assert.AreEqual(2, hit.tier, $"31 的入射应该是二级：实际 {hit.tier}");
        Assert.IsTrue(hit.passed, "二级应该穿过");
        Assert.IsFalse(hit.bounced, "二级不该弹回");
        Assert.Greater(Mathf.Abs(hit.normal.x), 0.5f, $"侧撞法线应该水平；实际 {hit.normal}");
        Assert.Greater(run.velocities[0].x, 25f, "穿过时要保留入射速度，不能贴着绳面停住");
        Assert.Greater(run.player.transform.position.x, 0.5f, "二级应该穿到绳的另一侧");
    }

    [UnityTest]
    public IEnumerator VerticalRopeBouncesTierThreePlayerWithRopeSpeed()
    {
        const float speed = 37f;
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(speed, 0f), 0.35f, run);

        Assert.AreEqual(1, run.contacts.Count, "三级撞绳应该只判一次");
        var hit = run.contacts[0];
        Assert.AreEqual(3, hit.tier, $"37 的入射应该是三级：实际 {hit.tier}");
        Assert.IsFalse(hit.passed, "三级不该穿过");
        Assert.IsTrue(hit.bounced, "三级应该弹回");
        Assert.Less(hit.normal.x, -0.5f, $"被弹回时法线应该朝左；实际 {hit.normal}");
        Assert.Less(run.velocities[0].x, 0f, "三级应该被弹向反方向");
        Assert.AreEqual(lab.RopeBounceSpeed(Mathf.Abs(run.incoming[0].x)), -run.velocities[0].x, 0.5f,
            "弹回速度 = 入射速度 + 一个等级间距，和横绳同一条公式");
        Assert.Less(run.player.transform.position.x, -1f, "玩家应该被弹离绳");
    }

    [UnityTest]
    public IEnumerator VerticalRopeBouncesFallingTierThreePlayerUp()
    {
        const float speed = 37f;
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(0f, 4.5f), new Vector2(0f, -speed), 0.3f, run);

        Assert.AreEqual(1, run.contacts.Count, "从绳顶落下来应该只判一次");
        var hit = run.contacts[0];
        Assert.AreEqual(3, hit.tier, $"37 的下落应该是三级：实际 {hit.tier}");
        Assert.IsTrue(hit.bounced, "三级落在绳顶应该弹回");
        Assert.Greater(hit.normal.y, 0.5f, $"绳顶的法线应该朝上；实际 {hit.normal}");
        Assert.Greater(run.velocities[0].y, 0f, "应该被弹向上方");
        Assert.AreEqual(lab.RopeBounceSpeed(Mathf.Abs(run.incoming[0].y)), run.velocities[0].y, 0.5f,
            "弹回速度 = 入射速度 + 一个等级间距");
    }

    [UnityTest]
    public IEnumerator VerticalRopeKeepsIgnoringUntilTierTwoPlayerLeavesLongRope()
    {
        var run = new VerticalRopeRun();
        // 10 格长绳：按 31 下落，0.22s 的穿过窗口到期时人还在绳里，必须继续忽略到完全离开。
        yield return RunIntoVerticalRope(new Vector2(0f, 10.5f), new Vector2(0f, -31f), 0.7f, run, 10);

        Assert.AreEqual(1, run.contacts.Count, "穿过只应该判一次；窗口到期时人还在绳里也不能重新判");
        var hit = run.contacts[0];
        Assert.AreEqual(2, hit.tier, $"31 的下落应该是二级：实际 {hit.tier}");
        Assert.IsTrue(hit.passed, "二级应该从绳顶穿下去");
        Assert.Less(run.player.transform.position.y, -0.5f,
            "10 格长绳要一直忽略到人从下端出去，不能卡在绳中间");
    }

    IEnumerator RunIntoVerticalRope(Vector2 start, Vector2 velocity, float duration, VerticalRopeRun run,
        int ropeCells = 4, bool canBeRedHot = false, System.Action<LabPlayer> prepare = null)
    {
        // 关闭水平空气制动，让碰撞前的入射速度就是测试给的挡位。
        lab.horizontalAirDrag = false;

        // 绳底固定在 y = 0，长度改的是绳顶高度。
        var level = BuildLevel(new LabRopeSpec(new Vector2(0f, ropeCells * 0.5f), ropeCells));
        run.rope = level.GetComponentInChildren<LabVerticalRope>();
        Assert.IsNotNull(run.rope, "关卡数据里的竖绳没生成");

        run.player = MakePlayer(start, canBeRedHot);
        if (prepare != null) prepare(run.player);

        run.rope.Contacted += contact =>
        {
            run.contacts.Add(contact);
            run.incoming.Add(run.player.PreImpactVelocity);
            run.velocities.Add(run.player.Velocity);
        };

        yield return null;      // 等 LabVerticalRope.Start 抓到 lab

        run.player.SetVelocity(velocity);

        float deadline = Time.time + duration;
        while (Time.time < deadline) yield return new WaitForFixedUpdate();
    }

    // 绳不参与地形锻造：除反弹外不改边态 --------------------------------

    [UnityTest]
    public IEnumerator VerticalRopePassDoesNotHeatPlayer()
    {
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(31f, 0f), 0.45f, run,
            canBeRedHot: true);

        Assert.AreEqual(1, run.contacts.Count);
        Assert.AreEqual(2, run.contacts[0].tier);
        Assert.IsTrue(run.contacts[0].passed);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, run.player.GetEdge(LabPlayer.Edge.Right),
            "二级穿过竖绳不该把右边缘加热");
    }

    [UnityTest]
    public IEnumerator VerticalRopeBounceStillHeatsPlayer()
    {
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(37f, 0f), 0.35f, run,
            canBeRedHot: true);

        Assert.AreEqual(1, run.contacts.Count);
        Assert.AreEqual(3, run.contacts[0].tier);
        Assert.IsTrue(run.contacts[0].bounced);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, run.player.GetEdge(LabPlayer.Edge.Right),
            "三级反弹仍按红热撞击结算右边缘");
    }

    [UnityTest]
    public IEnumerator VerticalRopePassDoesNotCollapseHotEdge()
    {
        var run = new VerticalRopeRun();
        yield return RunIntoVerticalRope(new Vector2(-3f, 0.6f), new Vector2(31f, 0f), 0.45f, run,
            canBeRedHot: true,
            prepare: p => p.SetEdge(LabPlayer.Edge.Right, LabPlayer.EdgeState.Hot2));

        Assert.IsTrue(run.player != null, "热二边穿过竖绳不该塌框销毁玩家");
        Assert.IsTrue(run.player.IsAlive);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, run.player.GetEdge(LabPlayer.Edge.Right),
            "二级穿过竖绳不该让热二边碎成灵体或塌框");
    }

    sealed class HorizontalRopeRun
    {
        public LabPlayer player;
        public LabHorizontalRope rope;
        public readonly List<LabHorizontalRope.RopeContact> contacts = new List<LabHorizontalRope.RopeContact>();
        public readonly List<Vector2> incoming = new List<Vector2>();
        public readonly List<Vector2> velocities = new List<Vector2>();
    }

    [UnityTest]
    public IEnumerator HorizontalRopePassDoesNotHeatPlayer()
    {
        var run = new HorizontalRopeRun();
        yield return RunIntoHorizontalRope(new Vector2(0f, 4.5f), new Vector2(0f, -31f), 0.4f, run);

        Assert.AreEqual(1, run.contacts.Count);
        Assert.AreEqual(2, run.contacts[0].tier);
        Assert.IsTrue(run.contacts[0].passed);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, run.player.GetEdge(LabPlayer.Edge.Bottom),
            "二级下落穿过横绳不该把底边加热");
    }

    [UnityTest]
    public IEnumerator HorizontalRopeLetsTierTwoPlayerPassAtIncomingSpeed()
    {
        var run = new HorizontalRopeRun();
        yield return RunIntoHorizontalRope(new Vector2(0f, 4.5f), new Vector2(0f, -31f), 0.35f, run);

        Assert.AreEqual(1, run.contacts.Count, "穿过只应该判一次");
        Assert.AreEqual(2, run.contacts[0].tier, $"31 的下落应该是二级：实际 {run.contacts[0].tier}");
        Assert.IsTrue(run.contacts[0].passed, "二级应该穿过横绳");

        // 横绳只有 4px 厚，碰撞那一帧解算掉的竖直速度必须还回去，
        // 否则玩家会贴着绳面停住，再靠重力从 0 重新加速。
        Assert.AreEqual(run.incoming[0].y, run.velocities[0].y, 0.5f,
            $"穿过时要保留入射速度：入射 {run.incoming[0].y:F2} → 穿过 {run.velocities[0].y:F2}");
        Assert.Less(run.velocities[0].y, -25f, "穿过时不能被绳面刹停");
        Assert.Less(run.player.transform.position.y, 3f, "二级应该穿到横绳下方");
    }

    [UnityTest]
    public IEnumerator HorizontalRopeBounceStillHeatsPlayer()
    {
        var run = new HorizontalRopeRun();
        yield return RunIntoHorizontalRope(new Vector2(0f, 4.5f), new Vector2(0f, -37f), 0.3f, run);

        Assert.AreEqual(1, run.contacts.Count);
        Assert.AreEqual(3, run.contacts[0].tier);
        Assert.IsTrue(run.contacts[0].bounced);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, run.player.GetEdge(LabPlayer.Edge.Bottom),
            "三级落在横绳上反弹仍把底边点到热二");
    }

    [UnityTest]
    public IEnumerator HorizontalRopeBounceTwiceKeepsOneHotEdgeAndPlayerAlive()
    {
        var run = new HorizontalRopeRun();
        // 第一次弹回把底边点到热二；弹上去再落回同一根绳就是第二下。
        yield return RunIntoHorizontalRope(new Vector2(0f, 4.5f), new Vector2(0f, -37f), 2.4f, run);

        Assert.GreaterOrEqual(run.contacts.Count, 2, "弹回去应该再落回同一根绳，用例要覆盖第二下");
        Assert.IsTrue(run.contacts[1].bounced);
        Assert.IsTrue(run.player != null && run.player.IsAlive,
            "第二次落绳不能再把热二底边砸塌，绳反弹不是地形撞击");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, run.player.GetEdge(LabPlayer.Edge.Bottom),
            "反复弹回只保持同一条边热二");
    }

    IEnumerator RunIntoHorizontalRope(Vector2 start, Vector2 velocity, float duration, HorizontalRopeRun run)
    {
        var level = BuildLevel(new LabRopeSpec(new Vector2(0f, 3f), 6, 0, LabRopeOrientation.Horizontal));
        run.rope = level.GetComponentInChildren<LabHorizontalRope>();
        Assert.IsNotNull(run.rope, "关卡数据里的横绳没生成");

        run.player = MakePlayer(start, true);
        run.rope.Contacted += contact =>
        {
            run.contacts.Add(contact);
            run.incoming.Add(run.player.PreImpactVelocity);
            run.velocities.Add(run.player.Velocity);
        };

        yield return null;      // 等 LabHorizontalRope.Start 抓到 lab

        run.player.SetVelocity(velocity);

        float deadline = Time.time + duration;
        while (Time.time < deadline) yield return new WaitForFixedUpdate();
    }

    // 工具 ------------------------------------------------------------

    LabLevel BuildLevel(LabRopeSpec rope)
    {
        var go = new GameObject("Level");
        go.transform.SetParent(labGo.transform, false);
        var level = go.AddComponent<LabLevel>();

        var data = new LabLevelData();
        data.ropes.Add(rope);
        level.CopyFrom(data);
        level.BuildRuntime(go.transform, null);
        return level;
    }

    LabPlayer MakePlayer(Vector2 position, bool canBeRedHot = false)
    {
        var go = new GameObject("B_Chunk");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = position;

        go.AddComponent<BoxCollider2D>().size = new Vector2(10f / 16f, 10f / 16f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = lab.GravityScale;

        var p = go.AddComponent<LabPlayer>();
        p.lab = lab;
        p.pixels = 10;
        p.scheme = LabPlayer.Scheme.P2;
        p.canBeRedHot = canBeRedHot;
        return p;
    }
}

