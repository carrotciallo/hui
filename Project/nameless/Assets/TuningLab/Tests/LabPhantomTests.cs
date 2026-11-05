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
