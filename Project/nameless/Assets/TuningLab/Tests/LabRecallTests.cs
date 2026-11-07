using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 召回（Issue #69）的规格：道具解锁、头顶空位判定、传送与幻影生成、FIFO 上限。
/// 幻影炮 / L 幻影的交互放在 LabPhantomTests。
/// </summary>
public sealed class LabRecallTests
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
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        LabPhantom.All.Clear();
    }

    // 道具 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator LevelDataBuildsRecallPickupPerCell()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceRecall(new Vector2Int(-4, 0));
        level.Data.PlaceRecall(new Vector2Int(3, 4));
        level.Data.Normalize();

        level.BuildRuntime(root.transform, slick);

        var pickups = root.GetComponentsInChildren<LabRecallPickup>();
        Assert.AreEqual(2, pickups.Length, "一格一个召回道具");
        Assert.AreEqual(new Vector3(-3.5f, 0.5f, 0f), pickups[0].transform.position, "落在格中心");
        Assert.AreEqual(new Vector3(3.5f, 4.5f, 0f), pickups[1].transform.position);

        for (int i = 0; i < pickups.Length; i++)
        {
            var body = pickups[i].GetComponent<BoxCollider2D>();
            Assert.IsTrue(body.isTrigger, "道具是触发区");
            Assert.AreEqual(Vector2.one, body.size, "触发区 1×1");
            Assert.IsNotNull(pickups[i].GetComponent<SpriteRenderer>().sprite, "道具有可见贴图");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator RecallPickupUnlocksTeamAbility()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        Assert.IsFalse(lab.RecallUnlocked, "默认没有召回");
        Assert.IsFalse(lab.TryRecall(a), "未解锁时施法无效");
        Assert.AreEqual(0, LabPhantom.All.Count);

        var pickup = MakePickup(new Vector2(5.5f, 0.5f));
        Assert.IsTrue(pickup.TryCollect(b), "任一玩家都能拾取并解锁");
        Assert.IsTrue(lab.RecallUnlocked, "全队解锁");
        yield return null;

        Assert.IsTrue(lab.TryRecall(a), "解锁后 A 能召回 B");
        Assert.IsTrue(lab.TryRecall(b), "解锁后 B 也能召回 A");
        yield return null;
    }

    // 头顶空位 / 传送 -----------------------------------------------

    [UnityTest]
    public IEnumerator RecallTeleportsOtherOntoCasterHeadAndSpawnsPhantomAtOldCell()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(a), "头顶有空位，召回成功");

        float expectedY = a.BodyBounds.max.y + b.HalfHeight + 0.01f;
        Assert.AreEqual(a.transform.position.x, b.transform.position.x, 0.0001f, "目标落在施法者正上方");
        Assert.AreEqual(expectedY, b.transform.position.y, 0.01f, "目标站在施法者头顶");

        Assert.AreEqual(1, LabPhantom.All.Count, "旧位置留下一个幻影");
        var phantom = LabPhantom.All[0];
        Assert.AreEqual(b, phantom.Source, "幻影复制被召回者");
        Assert.AreEqual(a, phantom.Target, "幻影只对另一名玩家生效");
        Assert.AreEqual(LabForm.Normal, phantom.Form);
        Assert.AreEqual(TuningLab.B_Pixels, phantom.Pixels);
        Assert.AreEqual(new Vector3(5.5f, 0.5f, 0f), phantom.transform.position, "幻影吸附格中心");
        yield return null;
    }

    [UnityTest]
    public IEnumerator RecallBlockedWhenNoClearance()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        // 正上方 1×1 的实心块
        var ceiling = new GameObject("Ceiling");
        ceiling.transform.SetParent(root.transform, false);
        ceiling.transform.position = new Vector3(0.5f, 1.5f, 0f);
        ceiling.AddComponent<BoxCollider2D>().size = Vector2.one;

        lab.UnlockRecall();
        Vector3 before = b.transform.position;
        Assert.IsFalse(lab.TryRecall(a), "头顶没有 1 格空间，拒绝召回");
        Assert.AreEqual(before, b.transform.position, "目标不动");
        Assert.AreEqual(0, LabPhantom.All.Count, "不生成幻影");
        yield return null;
    }

    [UnityTest]
    public IEnumerator RecallIgnoresTriggersButNotSolids()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        // 触发区不挡召回
        var trigger = new GameObject("Trigger");
        trigger.transform.SetParent(root.transform, false);
        trigger.transform.position = new Vector3(0.5f, 1.5f, 0f);
        var triggerBody = trigger.AddComponent<BoxCollider2D>();
        triggerBody.isTrigger = true;
        triggerBody.size = Vector2.one;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(a), "触发器不算占位");

        LabPhantom.ClearAll();
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        // 换成实体碰撞就挡住（先关掉触发器，避免 Destroy 延迟到帧末）
        triggerBody.enabled = false;
        var solid = new GameObject("Solid");
        solid.transform.SetParent(root.transform, false);
        solid.transform.position = new Vector3(0.5f, 1.5f, 0f);
        solid.AddComponent<BoxCollider2D>().size = Vector2.one;

        Assert.IsFalse(lab.TryRecall(a), "实体碰撞挡住召回");
        yield return null;
    }

    [UnityTest]
    public IEnumerator RecallPreservesRecalledPlayersForm()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        a.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        Assert.AreEqual(LabForm.Cannon, a.Form, "A 先进入 u 炮形态");
        yield return null;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(b), "B 召回 A");
        Assert.AreEqual(LabForm.Cannon, a.Form, "被召回者保留形态");
        Assert.AreEqual(1, LabPhantom.All.Count);
        Assert.AreEqual(a, LabPhantom.All[0].Source, "留下的是 A 的幻影");
        Assert.AreEqual(LabForm.Cannon, LabPhantom.All[0].Form, "幻影复制 A 的炮形态");
        yield return null;
    }

    // FIFO / 重置 -------------------------------------------------------

    [UnityTest]
    public IEnumerator PhantomsAreCappedAtTwoAndEvictOldestFifo()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        lab.UnlockRecall();
        for (int i = 0; i < LabPhantom.MaxPhantoms + 1; i++)
        {
            b.Teleport(new Vector2(5.5f + i, b.HalfHeight));
            yield return null;
            Assert.IsTrue(lab.TryRecall(a), "第 " + i + " 次召回");
        }

        Assert.AreEqual(2, LabPhantom.MaxPhantoms, "上限按 #76 定案改成 2");
        Assert.AreEqual(LabPhantom.MaxPhantoms, LabPhantom.All.Count,
            "场上最多 " + LabPhantom.MaxPhantoms + " 个幻影");
        Assert.IsFalse(AllCells().Contains(new Vector2Int(5, 0)), "最旧的幻影被淘汰");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MapResetClearsPhantomsAndKeepsRecallUnlocked()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        var pickup = MakePickup(new Vector2(8.5f, 0.5f));
        Assert.IsTrue(pickup.TryCollect(a));
        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(a));
        Assert.AreEqual(1, LabPhantom.All.Count);
        yield return null;

        lab.ResetMap();
        Assert.AreEqual(0, LabPhantom.All.Count, "地图重置清空幻影");
        Assert.IsTrue(lab.RecallUnlocked, "召回能力保留");
        Assert.AreEqual(0, Object.FindObjectsByType<LabRecallPickup>(FindObjectsSortMode.None).Length,
            "已拾取的道具不会因地图重置刷回来");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MapResetDoesNotRespawnUncollectedPickup()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceRecall(new Vector2Int(8, 0));
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);

        Assert.AreEqual(1, Object.FindObjectsByType<LabRecallPickup>(FindObjectsSortMode.None).Length);
        lab.ResetMap();
        yield return null;
        Assert.AreEqual(1, Object.FindObjectsByType<LabRecallPickup>(FindObjectsSortMode.None).Length,
            "未拾取的道具仍留在场景里");
        yield return null;
    }

    // 工具 ---------------------------------------------------------------

    System.Collections.Generic.List<Vector2Int> AllCells()
    {
        var cells = new System.Collections.Generic.List<Vector2Int>();
        for (int i = 0; i < LabPhantom.All.Count; i++) cells.Add(LabPhantom.All[i].Cell);
        return cells;
    }

    LabRecallPickup MakePickup(Vector2 position)
    {
        var go = new GameObject("RecallPickup");
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        var pickup = go.AddComponent<LabRecallPickup>();
        pickup.ApplyShape();
        return pickup;
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
