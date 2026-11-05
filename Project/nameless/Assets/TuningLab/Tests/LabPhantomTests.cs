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

    [UnityTest]
    public IEnumerator CannonPhantomLoadsFromBottomAndFiresThroughMouth()
    {
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, false);
        Link(a, b);
        a.Teleport(new Vector2(8f, a.HalfHeight));
        b.Teleport(new Vector2(0.5f, -0.45f));
        var phantom = SpawnPhantom(a, b, LabForms.TopBit);
        yield return null;

        b.ApplyDash(Dash(Vector2.up, EntrySpeed));
        yield return WaitUntil(() => phantom.Loaded, 2f);

        Assert.IsTrue(b.Suspended, "进入后玩家被幻影收进膛里");
        Assert.AreEqual(phantom, b.PhantomHost, "宿主是幻影");

        phantom.Fire();

        Assert.IsFalse(b.Suspended, "开炮后恢复实体");
        float expected = Mathf.Sqrt(EntrySpeed * EntrySpeed + lab.CannonBoostV2);
        Assert.AreEqual(expected, b.Velocity.magnitude, 0.2f,
            "出膛速度 = √(V前² + 一个等级间距)");
        Assert.Greater(b.Velocity.y, 0f, "炮口朝上，向上射出");
        Assert.Greater(b.transform.position.y, phantom.transform.position.y, "从炮口外侧出膛");
        yield return null;
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

        Assert.IsFalse(phantom.TryAbsorb(b), "只有底面能进，侧面不行");
        Assert.IsFalse(phantom.Loaded);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CannonPhantomRequiresDashState()
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
        Assert.IsFalse(phantom.TryAbsorb(b), "大炮幻影只吃冲刺态");
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
        yield return WaitUntil(() => phantom.ConverterLoaded, 2f);

        yield return WaitUntil(() => !phantom.Loaded, 2.5f);

        Assert.IsFalse(b.Suspended, "1 秒后自动射出");
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
            yield return WaitUntil(() => phantom.ConverterLoaded, 2f);
            yield return WaitUntil(() => !phantom.Loaded, 2.5f);

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
