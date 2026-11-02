using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Issue #14 红热锻造在实验台上的规格（ADR-0006）。
/// 边态阶梯：常态 → 热一 → 热二 → 灵体；热二再吃一记红热级撞击塌到本体。
/// 场景撞击和双人撞击共用同一套门槛：锻造门槛只砸不加热，红热门槛才加热。
///
/// 旧实现里断言「形态」和「死亡」的部分，这里降级成断言碎边组合和塌框事件：
/// 实验台还没有形态系统和生命系统，接上之后这两类断言原样补回来。
/// </summary>
public sealed class LabForgeTests
{
    const float FloorTop = 0f;

    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);

        // 实验台搭场地时统一用无摩擦材质；测试台不给的话，
        // 贴地滑动会被摩擦吃掉几个速度，红热门槛就说不过去了。
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        // TuningLab.Awake 会顺手把整个房间、两个玩家和相机搭出来，测试里不要它。
        // 先在禁用状态下 AddComponent，关掉 autoBuild，再激活。
        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("ForgeRoot");
        root.transform.SetParent(labGo.transform, false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    // 边态阶梯 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator OnlyRedHotStrikesHeatTheEdge()
    {
        var a = MakeA();

        a.StrikeEdge(LabPlayer.Edge.Bottom, false);
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Bottom), "一级撞击不该升温");

        a.StrikeEdge(LabPlayer.Edge.Bottom, true);
        Assert.AreEqual(LabPlayer.EdgeState.Hot1, a.GetEdge(LabPlayer.Edge.Bottom));

        a.StrikeEdge(LabPlayer.Edge.Bottom, true);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom));

        yield return null;
    }

    [UnityTest]
    public IEnumerator Hot2EdgeStruckAgainByRedHotCollapses()
    {
        var a = MakeA();
        int collapses = 0;
        LabPlayer.Edge collapsed = default;
        a.EdgeCollapsed += (p, e) => { collapses++; collapsed = e; };

        Heat(a, LabPlayer.Edge.Bottom);
        a.StrikeEdge(LabPlayer.Edge.Bottom, true);

        Assert.AreEqual(1, collapses, "热二再吃一记红热级撞击应塌到本体");
        Assert.AreEqual(LabPlayer.Edge.Bottom, collapsed);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom), "塌框由接入方决定后果，边态本身不自己变");

        yield return null;
    }

    [UnityTest]
    public IEnumerator Hot2EdgeShatteredByColdStrikeBecomesSpirit()
    {
        var a = MakeA();
        LabPlayer.EdgeState from = default, to = default;
        bool changed = false;
        a.EdgeStateChanged += (p, e, prev, next) =>
        {
            if (next != LabPlayer.EdgeState.Spirit) return;
            from = prev; to = next; changed = true;
        };

        Heat(a, LabPlayer.Edge.Bottom);
        a.StrikeEdge(LabPlayer.Edge.Bottom, false);

        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Bottom));
        Assert.IsTrue(changed, "碎边要外抛，形态层靠它决定切哪个形态");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, from);
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, to);
        Assert.IsTrue(a.GetComponent<Collider2D>().enabled, "碎掉的边框仍要有碰撞箱");

        yield return null;
    }

    // 碎边组合（形态的输入）--------------------------------------------

    [UnityTest]
    public IEnumerator BrokenEdgesAccumulateInStrikingOrder()
    {
        var a = MakeA();

        Shatter(a, LabPlayer.Edge.Bottom);
        Assert.AreEqual(1, CountSpirit(a), "碎 1 条");

        Shatter(a, LabPlayer.Edge.Left);
        Assert.AreEqual(2, CountSpirit(a), "碎相邻 2 条");

        Shatter(a, LabPlayer.Edge.Top);
        Assert.AreEqual(3, CountSpirit(a), "只剩一根");

        Shatter(a, LabPlayer.Edge.Right);
        Assert.AreEqual(4, CountSpirit(a), "全碎");

        yield return null;
    }

    [UnityTest]
    public IEnumerator OppositeAndAdjacentBrokenEdgesAreDistinguishable()
    {
        var opposite = MakeA("Opposite");
        Shatter(opposite, LabPlayer.Edge.Top);
        Shatter(opposite, LabPlayer.Edge.Bottom);
        Assert.IsTrue(opposite.IsSpirit(LabPlayer.Edge.Top) && opposite.IsSpirit(LabPlayer.Edge.Bottom),
                      "相对的 2 条碎 = 电梯的输入");
        Assert.IsFalse(opposite.IsSpirit(LabPlayer.Edge.Left), "没砸的边不受影响");

        var adjacent = MakeA("Adjacent");
        Shatter(adjacent, LabPlayer.Edge.Top);
        Shatter(adjacent, LabPlayer.Edge.Left);
        Assert.IsTrue(adjacent.IsSpirit(LabPlayer.Edge.Top) && adjacent.IsSpirit(LabPlayer.Edge.Left),
                      "相邻的 2 条碎 = L 转向器的输入");

        yield return null;
    }

    [UnityTest]
    public IEnumerator SpiritEdgeIgnoresLaterStrikes()
    {
        var a = MakeA();
        Shatter(a, LabPlayer.Edge.Bottom);

        a.StrikeEdge(LabPlayer.Edge.Bottom, true);
        a.StrikeEdge(LabPlayer.Edge.Bottom, true);

        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Bottom), "灵体不可复锻");

        yield return null;
    }

    [UnityTest]
    public IEnumerator ResetClearsForgedEdges()
    {
        var a = MakeA();
        Shatter(a, LabPlayer.Edge.Bottom);
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Bottom));

        a.Teleport(a.transform.position);      // 实验台的「复活」就是 Teleport 回出生点

        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Bottom), "重置应清掉灵体框");

        yield return null;
    }

    // 撞场景 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator FastFreeFallRedHotsBottomEdge()
    {
        MakeFloor();
        var a = MakeA();

        // 从 12 格高自由落体：落地沿轴速度 ≈ 34，过得了 28.28 的红热门槛
        a.Teleport(new Vector2(0f, 12f));

        yield return WaitUntil(() => a.Grounded, 3f);

        Assert.AreEqual(LabPlayer.EdgeState.Hot1, a.GetEdge(LabPlayer.Edge.Bottom),
                        "红热级的自由落体撞地面也应加热");
    }

    [UnityTest]
    public IEnumerator ForgeTierFreeFallShattersHot2Edge()
    {
        MakeFloor();
        var a = MakeA();

        // 先放好再加热：Teleport 会清框
        a.Teleport(new Vector2(0f, 6.5f));
        Heat(a, LabPlayer.Edge.Bottom);

        // 落地速度 ≈ 24.6：过得了 20 的锻造门槛，又够不到 28.28 的红热门槛
        yield return WaitUntil(() => a.Grounded, 3f);

        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Bottom), "一级落地应砸碎热二框");
    }

    [UnityTest]
    public IEnumerator Hot2EdgeStruckByFastFreeFallCollapses()
    {
        MakeFloor();
        var a = MakeA();
        int collapses = 0;
        a.EdgeCollapsed += (p, e) => collapses++;

        a.Teleport(new Vector2(0f, 12f));
        Heat(a, LabPlayer.Edge.Bottom);

        yield return WaitUntil(() => collapses > 0, 3f);

        Assert.AreEqual(1, collapses, "红热级落地砸的是下边，热二直接塌");
    }

    [UnityTest]
    public IEnumerator SlowFreeFallLeavesHot2EdgeUntouched()
    {
        MakeFloor();
        var a = MakeA();

        a.Teleport(new Vector2(0f, 2.5f));
        Heat(a, LabPlayer.Edge.Bottom);

        // 落地速度 ≈ 14.4：锻造门槛都不到
        yield return WaitUntil(() => a.Grounded, 3f);

        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom), "不到锻造门槛不碎框");
    }

    [UnityTest]
    public IEnumerator SlidingIntoWallHeatsTheContactedEdgeOnly()
    {
        MakeFloor();
        MakeWall(4f);
        var a = MakeA();

        // 这条测的是"撞哪条边就加热哪条边"，不是摩擦：把制动关掉，撞墙速度 = 起始速度
        lab.groundBrake = 0f;
        lab.horizontalAirDrag = false;
        a.SetVelocity(new Vector2(29f, 0f));

        yield return WaitUntil(() => a.HorizontalSpeed < 1f, 3f);     // 被墙挡住 = 撞上了

        Assert.AreEqual(LabPlayer.EdgeState.Hot1, a.GetEdge(LabPlayer.Edge.Right), "撞右边就加热右边");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Bottom), "另一条轴不参与");

        yield return null;
    }

    // 双人撞击 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator RedHotPlayerHitHeatsTheAFrameOnly()
    {
        MakeFloor();
        var a = MakeA("A_Box", true);
        var b = MakeA("B_Chunk", false);

        a.other = b;
        b.other = a;
        a.Teleport(new Vector2(0f, 0.5f));
        b.Teleport(new Vector2(2f, 0.4f));

        a.SetVelocity(new Vector2(28.3f, 0f));
        b.SetVelocity(new Vector2(-28.3f, 0f));

        yield return WaitUntil(() => a.GetEdge(LabPlayer.Edge.Right) != LabPlayer.EdgeState.Normal, 3f);

        Assert.AreEqual(LabPlayer.EdgeState.Hot1, a.GetEdge(LabPlayer.Edge.Right), "对撞 Vp 过门槛，A 的迎风边加热");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, b.GetEdge(LabPlayer.Edge.Left), "B 没有边，不参与锻造");
    }

    [UnityTest]
    public IEnumerator BHasNoForgableEdges()
    {
        var b = MakeA("B_Chunk", false);

        b.StrikeEdge(LabPlayer.Edge.Bottom, true);

        Assert.AreEqual(LabPlayer.EdgeState.Normal, b.GetEdge(LabPlayer.Edge.Bottom), "B 没有边框，不参与锻造");

        yield return null;
    }

    // 工具 -------------------------------------------------------------

    /// <summary>把某条边加热到热二（两次红热级撞击）。</summary>
    static void Heat(LabPlayer p, LabPlayer.Edge edge)
    {
        p.StrikeEdge(edge, true);
        p.StrikeEdge(edge, true);
    }

    /// <summary>加热后砸成灵体。</summary>
    static void Shatter(LabPlayer p, LabPlayer.Edge edge)
    {
        Heat(p, edge);
        p.StrikeEdge(edge, false);
    }

    static int CountSpirit(LabPlayer p)
    {
        int n = 0;
        for (int i = 0; i < 4; i++)
            if (p.IsSpirit((LabPlayer.Edge)i)) n++;
        return n;
    }

    LabPlayer MakeA(string name = "A_Box", bool canBeRedHot = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);

        int pixels = canBeRedHot ? TuningLab.A_Pixels : TuningLab.B_Pixels;
        float half = pixels / (2f * TuningLab.CellPixels);
        go.transform.position = new Vector2(0f, half + 0.02f);

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(half * 2f, half * 2f);
        bc.sharedMaterial = slick;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = lab.GravityScale;

        var p = go.AddComponent<LabPlayer>();
        p.lab = lab;
        p.pixels = pixels;
        p.canBeRedHot = canBeRedHot;
        p.scheme = canBeRedHot ? LabPlayer.Scheme.P1 : LabPlayer.Scheme.P2;
        return p;
    }

    void MakeFloor(float top = FloorTop)
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, top - 1f, 0f);
        go.transform.localScale = new Vector3(40f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    void MakeWall(float x, float top = 8f)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(x + 0.5f, top * 0.5f - 1f, 0f);
        go.transform.localScale = new Vector3(1f, top, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
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
