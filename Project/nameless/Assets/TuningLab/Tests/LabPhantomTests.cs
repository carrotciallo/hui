using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 幻影（Issue #69）的选择性碰撞：只忽略来源玩家，另一名玩家保持实体接触。
/// </summary>
public sealed class LabPhantomTests
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

    [UnityTest]
    public IEnumerator PhantomOnlyIgnoresItsSource()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        a.other = b;
        b.other = a;
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(b), "B 召回 A，留下 A 幻影");
        yield return new WaitForFixedUpdate();

        var phantom = LabPhantom.All[0];
        var phantomBody = phantom.GetComponent<Collider2D>();
        var aBody = a.GetComponent<Collider2D>();
        var bBody = b.GetComponent<Collider2D>();

        Assert.IsTrue(Physics2D.GetIgnoreCollision(phantomBody, aBody),
            "来源 A 可以穿过自己的幻影");
        Assert.IsFalse(Physics2D.GetIgnoreCollision(phantomBody, bBody),
            "另一名玩家 B 与幻影保持实体碰撞");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SourceColliderReenableKeepsIgnorePair()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        a.other = b;
        b.other = a;
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        b.Teleport(new Vector2(5.5f, b.HalfHeight));
        yield return null;

        lab.UnlockRecall();
        Assert.IsTrue(lab.TryRecall(b));
        yield return new WaitForFixedUpdate();

        var phantom = LabPhantom.All[0];
        var phantomBody = phantom.GetComponent<Collider2D>();
        var aBody = a.GetComponent<Collider2D>();

        aBody.enabled = false;
        yield return new WaitForFixedUpdate();
        aBody.enabled = true;
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(Physics2D.GetIgnoreCollision(phantomBody, aBody),
            "来源碰撞箱关掉再打开后仍然互相忽略");
        yield return null;
    }

    // 幻影炮 / L 幻影 ----------------------------------------------------

    /// <summary>
    /// 玻璃大炮（#76）：炮底进入，不需要冲刺态，进膛同一步就沿原炮口射出，幻影随即自毁。
    /// 速度只换方向不加能量。
    /// </summary>
    [UnityTest]
    public IEnumerator GlassCannonFiresImmediatelyAndSelfDestructs()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        b.Teleport(new Vector2(0.5f, -0.45f));
        var phantom = SpawnPhantom(a, b, LabForms.TopBit);
        yield return null;

        // 不用冲刺态，直接给一个向上的普通速度。
        b.SetVelocity(Vector2.up * EntrySpeed);
        yield return WaitUntil(() => !b.Suspended || phantom.Leaving, 2f);

        // 进膛立刻出膛：不会停在膛里等手动开炮。
        yield return WaitUntil(() => !b.Suspended || phantom.Leaving, 1f);
        Assert.IsFalse(b.Suspended, "玻璃大炮不留在膛里");
        Assert.AreEqual(EntrySpeed, b.Velocity.magnitude, 1.5f,
            "出膛速度取进入速度的大小，不凭空加能量");
        Assert.Greater(b.Velocity.y, 0f, "炮口朝上，向上射出");
        Assert.Greater(b.transform.position.y, phantom.transform.position.y, "从炮口外侧出膛");

        yield return null;
        Assert.IsTrue(phantom.Consumed || phantom == null || phantom.Leaving,
            "发射后幻影被消耗");
    }

    [UnityTest]
    public IEnumerator CannonPhantomRejectsNonBottomEntry()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        b.Teleport(new Vector2(-0.55f, 0.5f));
        var phantom = SpawnPhantom(a, b, LabForms.TopBit);
        yield return null;

        b.ApplyDash(Dash(Vector2.right, EntrySpeed));
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(phantom.TryAbsorb(b), "只有炮底和炮口能进，侧面不行");
        Assert.IsFalse(phantom.Loaded);
        yield return null;
    }

    /// <summary>#76 起大炮不需要冲刺态，普通速度从炮底进去也会被玻璃大炮吃掉。</summary>
    [UnityTest]
    public IEnumerator CannonPhantomNoLongerRequiresDashState()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        b.Teleport(new Vector2(0.5f, -0.45f));
        var phantom = SpawnPhantom(a, b, LabForms.TopBit);
        yield return null;

        b.SetVelocity(Vector2.up * EntrySpeed);
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(b.InDashState, "普通速度不是冲刺态");
        Assert.IsTrue(phantom.TryAbsorb(b), "玻璃大炮不要求冲刺态");
        yield return null;
    }

    /// <summary>幻影大炮的炮口也是入口（#76）。</summary>
    [UnityTest]
    public IEnumerator CannonPhantomAcceptsMouthEntryToo()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        // 炮口朝上：把 B 摆在幻影正上方，从炮口那一侧接触。
        b.Teleport(new Vector2(0.5f, 1.55f));
        var phantom = SpawnPhantom(a, b, LabForms.TopBit);
        yield return null;

        b.SetVelocity(Vector2.down * EntrySpeed);
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(phantom.TryAbsorb(b), "炮口也能进");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ConverterPhantomEntersFromBottomAndAutoFiresSideways()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        b.Teleport(new Vector2(0.5f, -0.45f));
        int mask = LabForms.TopBit | LabForms.RightBit;   // L 开口 ↗，水平灵体边 = 右
        var phantom = SpawnPhantom(a, b, mask);
        yield return null;

        b.ApplyDash(Dash(Vector2.up, EntrySpeed));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // #76：L 转向是瞬时的，不再等 1 s。
        Assert.IsFalse(b.Suspended, "进膛当步就射出，不留在膛里");
        Assert.AreEqual(EntrySpeed, b.Velocity.magnitude, 0.2f, "L 幻影等速转向");
        Assert.Greater(b.Velocity.x, 0f, "从右开口射出");
        Assert.IsTrue(b.InDashState, "进入时的冲刺态被保留");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ConverterPhantomExitEdgeIsDeterministicForAllLOrientations()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));

        var masks = new[]
        {
            LabForms.TopBit | LabForms.RightBit,
            LabForms.TopBit | LabForms.LeftBit,
            LabForms.BottomBit | LabForms.LeftBit,
            LabForms.BottomBit | LabForms.RightBit
        };
        var expectedSign = new[] { 1f, -1f, -1f, 1f };

        for (int i = 0; i < masks.Length; i++)
        {
            b.Teleport(new Vector2(0.5f, -0.45f));
            var phantom = SpawnPhantom(a, b, masks[i]);
            yield return null;

            b.ApplyDash(Dash(Vector2.up, EntrySpeed));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(expectedSign[i], Mathf.Sign(b.Velocity.x), 0.001f,
                "L 组合 " + i + " 的出口方向");
            phantom.DestroyPhantom();
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator PlainPhantomsNeverAbsorb()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));

        var masks = new[]
        {
            0,                                            // Normal
            LabForms.TopBit | LabForms.BottomBit,         // Elevator
            LabForms.TopBit | LabForms.BottomBit | LabForms.LeftBit, // Spear（2048）
            LabForms.TopBit | LabForms.BottomBit | LabForms.LeftBit | LabForms.RightBit // Spirit
        };

        for (int i = 0; i < masks.Length; i++)
        {
            b.Teleport(new Vector2(0.5f, -0.45f));
            var phantom = SpawnPhantom(a, b, masks[i]);
            yield return null;

            b.ApplyDash(Dash(Vector2.up, EntrySpeed));
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(phantom.TryAbsorb(b), "形态 " + phantom.Form + " 没有入口");
            phantom.DestroyPhantom();
            yield return null;
        }
    }

    LabPhantom SpawnPhantom(LabPlayer source, LabPlayer target, int spiritMask)
    {
        return LabPhantom.Spawn(lab, source, target, new Vector2Int(0, 0),
            spiritMask, source.pixels, source.canBeRedHot);
    }

    static LabPlayer.DashState Dash(Vector2 dir, float speed)
    {
        return new LabPlayer.DashState
        {
            active = true,
            dir = dir,
            speed = speed,
            exitSpeed = speed,
            timer = 2f / speed
        };
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

    static void Link(LabPlayer a, LabPlayer b)
    {
        a.other = b;
        b.other = a;
    }

    const float EntrySpeed = 24f;

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
}
