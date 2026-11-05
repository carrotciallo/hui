using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 岩浆块的规格：数据 → 物件、整组破坏、地图重置恢复、任意面强制红热、
/// v3 才破坏、红热接触不融、走动按普通地面。
/// 强制红热依赖真实碰撞回调，放在 Tuning Lab 里也用手工验收。
/// </summary>
public sealed class LabMagmaTests
{
    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("MagmaRoot");
        root.transform.SetParent(labGo.transform, false);

        LabMagma.All.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    // 生成管子：关卡数据 → 场景物件 --------------------------------------

    [UnityTest]
    public IEnumerator LevelDataBuildsOneMagmaPerCell()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceMagma(new Vector2Int(-6, 0), 5);
        level.Data.PlaceMagma(new Vector2Int(3, 4), 5);
        level.Data.PlaceMagma(new Vector2Int(4, 4), 7);
        level.Data.Normalize();

        level.BuildRuntime(root.transform, slick);

        var magmas = root.GetComponentsInChildren<LabMagma>();
        Assert.AreEqual(3, magmas.Length, "一格一块岩浆");

        Assert.AreEqual(new Vector3(-5.5f, 0.5f, 0f), magmas[0].transform.position, "按格排序后落在格中心");
        Assert.AreEqual(new Vector3(3.5f, 4.5f, 0f), magmas[1].transform.position);
        Assert.AreEqual(new Vector3(4.5f, 4.5f, 0f), magmas[2].transform.position);
        Assert.AreEqual(5, magmas[0].groupId);
        Assert.AreEqual(7, magmas[2].groupId);

        for (int i = 0; i < magmas.Length; i++)
        {
            var body = magmas[i].GetComponent<BoxCollider2D>();
            Assert.IsFalse(body.isTrigger, "岩浆块是实体碰撞，能站");
            Assert.AreEqual(Vector2.one, body.size, "碰撞 = 1×1");
            Assert.IsNotNull(magmas[i].GetComponent<SpriteRenderer>().sprite, "岩浆块有看得见的贴图");
            Assert.AreEqual(LabMagma.DefaultColor, magmas[i].GetComponent<SpriteRenderer>().color);
        }

        yield return null;
    }

    // 破坏 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator BreakGroupBreaksOnlyThatGroup()
    {
        var a = MakeMagma(new Vector2Int(0, 0), 3);
        var b = MakeMagma(new Vector2Int(1, 0), 3);
        var c = MakeMagma(new Vector2Int(2, 0), 4);

        a.BreakGroup();

        Assert.IsTrue(a.Broken && b.Broken, "同组全部碎");
        Assert.IsFalse(c.Broken, "别组保留");
        Assert.IsFalse(a.GetComponent<BoxCollider2D>().enabled, "碎掉的岩浆块碰撞关闭");
        Assert.IsFalse(a.GetComponent<SpriteRenderer>().enabled, "碎掉的岩浆块不再渲染");
        Assert.AreEqual(2, LabMagma.GroupOf(3).Count, "破坏后对象仍留在组表里，等待重置");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResetMagmaRestoresColliderAndRenderer()
    {
        var magma = MakeMagma(new Vector2Int(0, 0), 1);
        magma.BreakGroup();
        Assert.IsTrue(magma.Broken);

        magma.ResetMagma();

        Assert.IsFalse(magma.Broken, "重置后不再是破坏态");
        Assert.IsTrue(magma.GetComponent<BoxCollider2D>().enabled, "碰撞恢复");
        Assert.IsTrue(magma.GetComponent<SpriteRenderer>().enabled, "渲染恢复");
        yield return null;
    }

    // 强制红热 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator StandingOnMagmaIsNotIceAndForcesBottomHot()
    {
        MakeFloor();
        var magma = MakeMagma(new Vector2Int(0, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));

        yield return WaitUntil(
            () => a.Grounded && a.GetEdge(LabPlayer.Edge.Bottom) == LabPlayer.EdgeState.Hot2, 2f);

        Assert.IsFalse(a.OnIce, "岩浆不是冰面，移动按普通地面");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom),
            "踩在岩浆上强制底边热二");
        Assert.IsFalse(magma.Broken, "承重接触不破坏岩浆块");
        yield return null;
    }

    [UnityTest]
    public IEnumerator StandingOnMagmaNeverCollapsesOrKillsA()
    {
        MakeFloor();
        MakeMagma(new Vector2Int(0, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(
            () => a.Grounded && a.GetEdge(LabPlayer.Edge.Bottom) == LabPlayer.EdgeState.Hot2, 2f);

        for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

        Assert.IsTrue(a != null && a.IsAlive, "持续接触只保证热二，不应触发热二遇热的塌框");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom),
            "三十帧后底边仍是热二");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SideContactForcesThatSideHot()
    {
        MakeFloor();
        var magma = MakeMagma(new Vector2Int(1, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.2f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(12f, 0f));
        yield return WaitUntil(() => a.GetEdge(LabPlayer.Edge.Right) == LabPlayer.EdgeState.Hot2, 2f);

        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Right),
            "撞上岩浆块的侧面就强制右边热二");
        Assert.IsFalse(magma.Broken, "低速接触不破坏岩浆块");
    }

    // v3 破坏 / 红热不融 -------------------------------------------------

    [UnityTest]
    public IEnumerator SideImpactAtV3BreaksWholeGroup()
    {
        MakeFloor();
        var first = MakeMagma(new Vector2Int(1, 0), 1);
        var second = MakeMagma(new Vector2Int(2, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(lab.MagmaBreakSpeed + 4f, 0f));
        yield return WaitUntil(() => first.Broken, 2f);

        Assert.IsTrue(first.Broken, "v3 侧撞应破坏被撞块");
        Assert.IsTrue(second.Broken, "同组岩浆块一起碎");
    }

    [UnityTest]
    public IEnumerator SideImpactBelowV3ForcesHotButDoesNotBreak()
    {
        MakeFloor();
        var first = MakeMagma(new Vector2Int(1, 0), 1);
        var second = MakeMagma(new Vector2Int(2, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(lab.IceBreakSpeed + 4f, 0f));   // v2：冰会碎，岩浆不会
        yield return WaitUntil(() => a.GetEdge(LabPlayer.Edge.Right) == LabPlayer.EdgeState.Hot2, 2f);
        for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

        Assert.IsFalse(first.Broken, "v2 撞击不破坏岩浆块");
        Assert.IsFalse(second.Broken, "整组都没碎");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Right),
            "接触后右边被强制热二");
    }

    [UnityTest]
    public IEnumerator HotEdgeContactDoesNotMeltMagma()
    {
        MakeFloor();
        var first = MakeMagma(new Vector2Int(1, 0), 1);
        var second = MakeMagma(new Vector2Int(2, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.StrikeEdge(LabPlayer.Edge.Right, true);                 // 红热边侧撞
        a.SetVelocity(new Vector2(15f, 0f));
        for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

        Assert.IsFalse(first.Broken, "红热边接触不会融掉岩浆块");
        Assert.IsFalse(second.Broken, "同组也不受影响");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Right),
            "强制热二后边不会被打回普通");
    }

    [UnityTest]
    public IEnumerator MergedTerrainForcesMagmaSideHot()
    {
        MakeFloor();
        MakeMagmaLevel(2, 1, 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.2f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(12f, 0f));
        yield return WaitUntil(() => a.GetEdge(LabPlayer.Edge.Right) == LabPlayer.EdgeState.Hot2, 2f);

        var magmas = root.GetComponentsInChildren<LabMagma>();
        Assert.IsFalse(magmas[0].Broken, "合并碰撞体的低速接触也不破坏岩浆块");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Left),
            "只加热真正接触的那条边");
    }

    [UnityTest]
    public IEnumerator MergedTerrainBreaksMagmaGroupAtV3()
    {
        MakeFloor();
        MakeMagmaLevel(3, 1, 1);
        var magmas = root.GetComponentsInChildren<LabMagma>();
        Assert.AreEqual(3, magmas.Length, "正式生成路径每格一块岩浆");

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(lab.MagmaBreakSpeed + 4f, 0f));
        yield return WaitUntil(() => magmas[0].Broken, 2f);

        Assert.IsTrue(magmas[0].Broken, "合并碰撞体也要能定位并破坏被撞的那一块");
        for (int i = 1; i < magmas.Length; i++)
            Assert.IsTrue(magmas[i].Broken, "同组岩浆一起碎");
    }

    [UnityTest]
    public IEnumerator MergedTerrainStopsSupportingAfterMagmaBreak()
    {
        MakeFloor();
        MakeMagmaLevel(3, 1);
        var magmas = root.GetComponentsInChildren<LabMagma>();

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        magmas[0].BreakGroup();
        yield return WaitUntil(() => a.transform.position.y < 1f, 2f);

        Assert.Less(a.transform.position.y, 1f, "碎掉的岩浆块必须从合并碰撞里移除，玩家应掉下去");
    }

    [UnityTest]
    public IEnumerator WalkingAcrossMagmaRowDoesNotStopOrHeatSideEdges()
    {
        MakeFloor();
        MakeMagmaLevel(12, 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        float startX = a.transform.position.x;
        for (int i = 0; i < 60; i++)
        {
            // 等价于一直按住走路方向：每步保持走路速度，但碰撞仍由物理结算。
            a.SetVelocity(new Vector2(lab.walkSpeed, a.Velocity.y));
            yield return new WaitForFixedUpdate();
        }

        Assert.Greater(a.transform.position.x, startX + 5f,
            "持续向右 1.2 秒应走过至少 5 格；实际位移 " + (a.transform.position.x - startX));
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom),
            "走在岩浆上表面只应强制底边热二");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Left),
            "走上表面不应把左边加热");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Right),
            "走上表面不应把右边加热");
    }

    [UnityTest]
    public IEnumerator WalkingFromSolidOntoMagmaAtSameHeightDoesNotStopOrHeatSide()
    {
        MakeMixedLevel(6, 8);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        float startX = a.transform.position.x;
        for (int i = 0; i < 60; i++)
        {
            a.SetVelocity(new Vector2(lab.walkSpeed, a.Velocity.y));
            yield return new WaitForFixedUpdate();
        }

        Assert.Greater(a.transform.position.x, startX + 5f,
            "从地形走上同高度的岩浆不应卡住；实际位移 " + (a.transform.position.x - startX));
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom),
            "走上岩浆后底边应强制热二");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Left),
            "地形和岩浆的接缝不应把左边加热");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Right),
            "地形和岩浆的接缝不应把右边加热");
    }

    [UnityTest]
    public IEnumerator WalkingFromMagmaOntoSolidAtSameHeightDoesNotStopOrHeatSide()
    {
        MakeMixedLevel(6, 8);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(7.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        float startX = a.transform.position.x;
        for (int i = 0; i < 50; i++)
        {
            a.SetVelocity(new Vector2(-lab.walkSpeed, a.Velocity.y));
            yield return new WaitForFixedUpdate();
        }

        Assert.Less(a.transform.position.x, startX - 5f,
            "从岩浆走回同高度的地形不应卡住；实际位移 " + (a.transform.position.x - startX));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Left),
            "岩浆和地形的接缝不应把左边加热");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Right),
            "岩浆和地形的接缝不应把右边加热");
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

    LabMagma MakeMagma(Vector2Int cell, int groupId)
    {
        var spec = new LabMagmaSpec(cell, groupId);
        var go = new GameObject("Magma");
        go.transform.SetParent(root.transform, false);
        go.transform.position = spec.Center;

        var magma = go.AddComponent<LabMagma>();
        magma.cell = cell;
        magma.groupId = groupId;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null) body.sharedMaterial = slick;

        magma.ApplyShape();
        return magma;
    }

    /// <summary>用正式生成路径铺一排岩浆，走 LabLevel 的合并碰撞。</summary>
    LabLevel MakeMagmaLevel(int count, int groupId, int startX = 0)
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x < count; x++)
            level.Data.PlaceMagma(new Vector2Int(startX + x, 0), groupId);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        return level;
    }

    /// <summary>用正式生成路径铺一排地形再接一排同高度的岩浆。</summary>
    LabLevel MakeMixedLevel(int solidCells, int magmaCells)
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x < solidCells; x++) level.Data.AddCell(new Vector2Int(x, 0));
        for (int x = 0; x < magmaCells; x++)
            level.Data.PlaceMagma(new Vector2Int(solidCells + x, 0), 1);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        return level;
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
