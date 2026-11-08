using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 幻影光桥（Issue #76）的规格：
///   只有电梯形（‖ / =）幻影铺桥；‖ 沿竖直方向、= 沿水平方向铺；
///   撞到第一个障碍物就停，A/B 站在路上不打断；
///   A 永远能穿过桥面，B 只有出生时就被压住的那些方块能穿过；
///   幻影被覆盖（FIFO 淘汰 / MapReset）时桥一起消失。
/// </summary>
public sealed class LabLightBridgeTests
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

        root = new GameObject("Root");
        root.transform.SetParent(labGo.transform, false);

        LabPhantom.All.Clear();
        LabLightBridgeBlock.All.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        LabPhantom.All.Clear();
        LabLightBridgeBlock.All.Clear();
    }

    // 生成规则 -----------------------------------------------------------

    /// <summary>= 幻影（左右两条边碎掉，只剩上下两根横杠）沿水平方向铺桥。</summary>
    [UnityTest]
    public IEnumerator HorizontalPhantomBuildsBridgeLeftAndRight()
    {
        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        Assert.AreEqual(LabForm.Elevator, phantom.Form, "= 是电梯形");
        Assert.IsFalse(phantom.BridgeIsVertical, "= 铺横向光桥");
        Assert.Greater(phantom.Bridge.Count, 0, "铺出了光桥方块");

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.IsTrue(cells.Contains(new Vector2Int(1, 5)), "向右侧铺了一格");
        Assert.IsTrue(cells.Contains(new Vector2Int(-1, 5)), "向左侧铺了一格");
        Assert.IsFalse(cells.Contains(new Vector2Int(0, 6)), "横向光桥不往上下长");
    }

    /// <summary>‖ 幻影（上下两条边碎掉，只剩左右两根竖杠）沿竖直方向铺桥。</summary>
    [UnityTest]
    public IEnumerator VerticalPhantomBuildsBridgeUpAndDown()
    {
        var phantom = SpawnPhantom(LabForms.TopBit | LabForms.BottomBit, new Vector2Int(0, 5));
        yield return null;

        Assert.IsTrue(phantom.BridgeIsVertical, "‖ 铺竖直光桥");

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.IsTrue(cells.Contains(new Vector2Int(0, 6)), "向上铺了一格");
        Assert.IsTrue(cells.Contains(new Vector2Int(0, 4)), "向下铺了一格");
        Assert.IsFalse(cells.Contains(new Vector2Int(1, 5)), "竖直光桥不往左右长");
    }

    /// <summary>非电梯形态（普通 / 炮 / L / 矛 / 灵体）不铺桥。</summary>
    [UnityTest]
    public IEnumerator NonElevatorPhantomsBuildNoBridge()
    {
        var masks = new[]
        {
            0,                                                        // Normal
            LabForms.TopBit,                                          // Cannon
            LabForms.TopBit | LabForms.RightBit,                       // Converter
            LabForms.TopBit | LabForms.BottomBit | LabForms.LeftBit,   // Spear
            LabForms.TopBit | LabForms.BottomBit | LabForms.LeftBit | LabForms.RightBit // Spirit
        };

        for (int i = 0; i < masks.Length; i++)
        {
            var phantom = SpawnPhantom(masks[i], new Vector2Int(0, 5));
            yield return null;

            Assert.AreEqual(0, phantom.Bridge.Count,
                "形态 " + phantom.Form + " 不该铺桥");
            phantom.DestroyPhantom();
            yield return null;
        }
    }

    /// <summary>撞到第一个实体就停，不再往后铺。</summary>
    [UnityTest]
    public IEnumerator BridgeStopsAtFirstObstacle()
    {
        // 在 x=2 立一堵墙，x=1 应该铺到，x=2 及以后不应该有。
        MakeWall(new Vector2Int(2, 5));

        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.Greater(phantom.Bridge.Count, 0,
            "诊断：Form=" + phantom.Form + " HasBridge=" + phantom.HasLightBridge
            + " vertical=" + phantom.BridgeIsVertical + " cell=" + phantom.Cell);
        Assert.IsTrue(cells.Contains(new Vector2Int(1, 5)), "墙前一格铺得到，实际 " + string.Join(",", cells));
        Assert.IsFalse(cells.Contains(new Vector2Int(2, 5)), "撞墙的那一格不铺");
        Assert.IsFalse(cells.Contains(new Vector2Int(3, 5)), "墙后面不再铺");
    }

    /// <summary>
    /// 普通地形不挡光桥：桥压在实体格上继续往前铺（Issue #76）。
    /// 用真实关卡地形建墙，才走的是 LabTerrainField 合并几何那条路径。
    /// </summary>
    [UnityTest]
    public IEnumerator BridgePassesThroughPlainTerrain()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int y = 3; y <= 7; y++) level.Data.AddCell(new Vector2Int(2, y));
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        yield return null;
        Physics2D.SyncTransforms();

        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.IsTrue(cells.Contains(new Vector2Int(2, 5)),
            "地形格上也要铺光桥，实际 " + string.Join(",", cells));
        Assert.IsTrue(cells.Contains(new Vector2Int(3, 5)),
            "穿过地形之后还要继续往右铺，实际 " + string.Join(",", cells));
    }

    /// <summary>
    /// 冰仍然截断光桥。冰虽然也在同一个合并几何里，但按格子反查得到 LabIce，要停。
    /// </summary>
    [UnityTest]
    public IEnumerator BridgeStillStopsAtIce()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int y = 3; y <= 7; y++) level.Data.PlaceIce(new Vector2Int(2, y), 1);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        yield return null;
        Physics2D.SyncTransforms();

        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.IsTrue(cells.Contains(new Vector2Int(1, 5)),
            "冰前一格铺得到，实际 " + string.Join(",", cells));
        Assert.IsFalse(cells.Contains(new Vector2Int(2, 5)), "冰那一格不铺");
        Assert.IsFalse(cells.Contains(new Vector2Int(3, 5)), "冰后面不再铺");
    }

    /// <summary>A/B 站在路上不打断光桥，桥穿过他们继续生成。</summary>
    [UnityTest]
    public IEnumerator BridgePassesThroughPlayers()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(-15.5f, a.HalfHeight));
        b.Teleport(new Vector2(2.5f, b.HalfHeight));
        yield return null;

        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 0), a, b);
        yield return null;

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < phantom.Bridge.Count; i++) cells.Add(phantom.Bridge[i].Cell);

        Assert.IsTrue(cells.Contains(new Vector2Int(2, 0)), "B 站着的那一格照样铺");
        Assert.IsTrue(cells.Contains(new Vector2Int(3, 0)), "穿过 B 继续往后铺");
    }

    // 碰撞 ---------------------------------------------------------------

    /// <summary>A（幻影来源）永远能穿过光桥。</summary>
    [UnityTest]
    public IEnumerator SourcePlayerPassesThroughBridge()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(-15.5f, a.HalfHeight));
        b.Teleport(new Vector2(-20.5f, b.HalfHeight));
        yield return null;

        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5), a, b);
        yield return null;

        var aBody = a.GetComponent<Collider2D>();
        for (int i = 0; i < phantom.Bridge.Count; i++)
        {
            var blockBody = phantom.Bridge[i].GetComponent<Collider2D>();
            Assert.IsTrue(Physics2D.GetIgnoreCollision(blockBody, aBody),
                "A 能穿过自己幻影铺的桥（格 " + phantom.Bridge[i].Cell + "）");
        }
    }

    /// <summary>B 默认被光桥挡住，只有出生时就压在 B 身上的方块对 B 让路。</summary>
    [UnityTest]
    public IEnumerator BridgeBlocksForbiddenPlayerExceptWhereBornOverlapping()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(-15.5f, a.HalfHeight));
        // B 站在 y=0 那一行（脚底踩 y=0），幻影也放在同一行，桥会从 B 身上穿过去。
        b.Teleport(new Vector2(2.5f, b.HalfHeight));
        yield return null;

        var bCell = new Vector2Int(2, 0);
        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 0), a, b);
        yield return null;

        var bBody = b.GetComponent<Collider2D>();
        int passable = 0;
        for (int i = 0; i < phantom.Bridge.Count; i++)
        {
            var block = phantom.Bridge[i];
            var blockBody = block.GetComponent<Collider2D>();
            bool ignores = Physics2D.GetIgnoreCollision(blockBody, bBody);
            if (block.Cell == bCell)
            {
                Assert.IsTrue(block.PassesForbiddenPlayer, "B 站着的那一格要标记为让路");
                Assert.IsTrue(ignores, "出生压在 B 身上的方块（格 " + block.Cell + "）对 B 让路");
            }
            else
            {
                Assert.IsFalse(ignores, "其余方块（格 " + block.Cell + "）挡住 B");
            }
            if (block.PassesForbiddenPlayer) passable++;
        }
        Assert.Greater(passable, 0, "确实有压在 B 身上的方块，用例才有效");
    }

    /// <summary>光桥方块填满整格：一条光柱就是一格。</summary>
    [UnityTest]
    public IEnumerator BridgeBlocksFillWholeCell()
    {
        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        Assert.Greater(phantom.Bridge.Count, 0);
        var block = phantom.Bridge[0];
        var body = block.GetComponent<BoxCollider2D>();

        Assert.IsNotNull(body, "有碰撞箱");
        Assert.IsFalse(body.isTrigger, "是实体碰撞");
        Assert.AreEqual(1f, body.size.x, 0.001f, "宽填满一格");
        Assert.AreEqual(1f, body.size.y, 0.001f, "高填满一格");

        // 碰撞箱要正好压在它自己那一格上。
        Assert.AreEqual(block.Cell.x, body.bounds.min.x, 0.02f, "左缘对齐格子左边");
        Assert.AreEqual(block.Cell.y, body.bounds.min.y, 0.02f, "下缘对齐格子下边");
        Assert.AreEqual(block.Cell.x + 1f, body.bounds.max.x, 0.02f, "右缘对齐格子右边");
        Assert.AreEqual(block.Cell.y + 1f, body.bounds.max.y, 0.02f, "上缘对齐格子上边");
    }

    /// <summary>横向光桥当单向平台：挂 PlatformEffector2D，能站人但只挡从上来。</summary>
    [UnityTest]
    public IEnumerator HorizontalBridgeIsOneWayPlatform()
    {
        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        Assert.Greater(phantom.Bridge.Count, 0);
        var body = phantom.Bridge[0].GetComponent<BoxCollider2D>();
        var effector = body.GetComponent<PlatformEffector2D>();

        Assert.IsNotNull(effector, "横向光桥要有单向平台组件");
        Assert.IsTrue(effector.useOneWay, "走单向");
        Assert.IsTrue(body.usedByEffector, "碰撞箱要交给 effector 处理");
    }

    // 销毁 ---------------------------------------------------------------

    /// <summary>幻影被 FIFO 覆盖时，它的光桥一起拆掉。</summary>
    [UnityTest]
    public IEnumerator OverwrittenPhantomTakesItsBridgeWithIt()
    {
        var phantom = SpawnPhantom(LabForms.LeftBit | LabForms.RightBit, new Vector2Int(0, 5));
        yield return null;

        Assert.Greater(phantom.Bridge.Count, 0, "先有桥");

        phantom.DestroyPhantom();
        yield return null;

        Assert.AreEqual(0, phantom.Bridge.Count, "幻影不再持有桥");
        Assert.AreEqual(0, LabLightBridgeBlock.All.Count, "它的桥方块已经从静态表移除");
    }

    /// <summary>MapReset 清空幻影时，光桥也一起消失。</summary>
    [UnityTest]
    public IEnumerator MapResetClearsBridges()
    {
        SpawnPhantom(LabForms.TopBit | LabForms.BottomBit, new Vector2Int(0, 5));
        yield return null;
        Assert.Greater(LabLightBridgeBlock.All.Count, 0, "先有桥");

        lab.ResetMap();
        yield return null;

        Assert.AreEqual(0, LabLightBridgeBlock.All.Count, "地图重置清空光桥");
    }

    // 时序 ---------------------------------------------------------------

    /// <summary>已有幻影被用过时，召回整次无效（#76 的时序要求）。</summary>
    [UnityTest]
    public IEnumerator RecallIsRejectedAfterAPhantomWasUsed()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(a), "第一次召回成功");
        yield return null;

        // 把留下的大炮幻影标成已使用。
        var phantom = LabPhantom.All[0];
        phantom.MarkConsumedForTest();
        yield return null;

        Assert.IsTrue(LabPhantom.AnyUsed, "确实有一条幻影已被用过");
        Assert.IsFalse(lab.TryRecall(a), "有幻影被用过时，召回无效");
    }

    // 工具 ---------------------------------------------------------------

    LabPhantom SpawnPhantom(int spiritMask, Vector2Int cell)
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(-15.5f, a.HalfHeight));
        b.Teleport(new Vector2(-20.5f, b.HalfHeight));
        return SpawnPhantom(spiritMask, cell, a, b);
    }

    LabPhantom SpawnPhantom(int spiritMask, Vector2Int cell, LabPlayer source, LabPlayer target)
    {
        return LabPhantom.Spawn(lab, source, target, cell, spiritMask,
            source.pixels, source.canBeRedHot);
    }

    void MakeWall(Vector2Int cell)
    {
        var go = new GameObject("Solid");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        bc.sharedMaterial = slick;
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

    static void Link(LabPlayer a, LabPlayer b)
    {
        a.other = b;
        b.other = a;
    }
}
