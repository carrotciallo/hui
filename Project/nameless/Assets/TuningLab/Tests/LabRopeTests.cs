using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 绳子规格（Issue #40）。
/// 两条症状：判定箱比绳本身左右各多出 7 格；三级以上撞绳弹回不保留入射速度。
/// 绳一律走关卡数据（LabLevelData → LabLevel.BuildRuntime），和关卡编辑器里摆出来的是同一条几何。
/// </summary>
public sealed class LabRopeTests
{
    [Test]
    public void RopeBouncePreservesMaxFallSpeedAndAddsOneTier()
    {
        var go = new GameObject("RopeBounceLab");
        go.SetActive(false);
        var lab = go.AddComponent<TuningLab>();
        lab.autoBuild = false;
        go.SetActive(true);
        lab.gravity = 50f;
        lab.tierSpacingCells = 4f;
        lab.maxFallSpeed = 42f;

        float incomingCells = lab.CellsForSpeed(lab.maxFallSpeed);
        float bounceCells = lab.CellsForSpeed(lab.RopeBounceSpeed(lab.maxFallSpeed));

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
        lab.tierCount = 4;
        lab.maxFallSpeed = 80f;
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

        // 从 22 格以上自由落体：入射已经是四级速度，旧规则会把人打回 40 的固定值（比进绳还慢）
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

    LabPlayer MakePlayer(Vector2 position)
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
        return p;
    }
}

