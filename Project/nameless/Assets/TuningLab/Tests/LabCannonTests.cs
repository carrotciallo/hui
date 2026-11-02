using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Issue #15 大炮形态功能在实验台上的规格。
/// 进入：有且仅能以冲刺态进入，速度多少不管。
/// 装填：记 V前，B 不可视、跟着 A、没有碰撞，不算"处于地面"、不回复充能。
/// 发射：v' = √(Vb² + 一个等级间距的 v²)，不看 A 自己的速度；方向取进入方向的反向。
/// 退出：射出、A 结构改变（u→L / u→=）、A 死亡。
/// </summary>
public sealed class LabCannonTests
{
    const float AHalf = TuningLab.A_Pixels / (2f * TuningLab.CellPixels);
    const float BHalf = TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
    const float EntrySpeed = 22f;

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

        root = new GameObject("CannonRoot");
        root.transform.SetParent(labGo.transform, false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    // 进入条件 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator OnlyADashEntryLoadsTheCannon()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        int hits = 0;
        lab.PlayerHit += info => hits++;

        Shatter(a, LabPlayer.Edge.Bottom);
        Assert.AreEqual(LabForm.Cannon, a.Form);

        // 有速度、但不在冲刺态：照旧走进动量互换，不进膛
        b.Teleport(new Vector2(0f, a.transform.position.y + AHalf + BHalf + 0.1f));
        b.SetVelocity(new Vector2(0f, -3f));
        yield return WaitUntil(() => hits > 0 || b.Suspended, 3f);

        Assert.IsFalse(a.Loaded, "不冲刺就不装填");
        Assert.IsFalse(b.Suspended);

        // 同样的方向、同样的入口，换成冲刺态就进膛
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);
        Assert.IsTrue(a.Loaded, "有且仅能以冲刺态进入");
        Assert.IsTrue(b.Suspended);
    }

    [UnityTest]
    public IEnumerator NormalFormDoesNotAbsorb()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        int hits = 0;
        lab.PlayerHit += info => hits++;

        Vector2 start = (Vector2)a.transform.position + Vector2.up * (AHalf + BHalf + 1.5f);
        b.Teleport(start);
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = EntrySpeed,
            exitSpeed = EntrySpeed,
            timer = 2f / EntrySpeed
        });

        yield return WaitUntil(() => hits > 0, 3f);

        Assert.IsFalse(a.Loaded, "普通形态没有口，不装填");
        Assert.AreEqual(LabForm.Normal, a.Form);
        Assert.Greater(hits, 0, "退回原来的动量互换");
    }

    // 装填态 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator LoadedBLosesItsBodyAndFollowsA()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);

        Shatter(a, LabPlayer.Edge.Bottom);
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Assert.IsFalse(b.GetComponent<Collider2D>().enabled, "膛内没有碰撞");
        Assert.AreEqual(RigidbodyType2D.Kinematic, b.GetComponent<Rigidbody2D>().bodyType);
        Assert.IsFalse(b.GetComponentInChildren<SpriteRenderer>().enabled, "膛内不可视");
        Assert.IsFalse(b.Touching, "膛内不算处于地面，所以不回复充能");
        Assert.AreEqual(a, b.Host);

        // A 带着膛里的 B 走
        lab.groundBrake = 0f;
        a.SetVelocity(new Vector2(6f, 0f));
        yield return null;
        yield return null;

        Assert.AreEqual(a.transform.position.x, b.transform.position.x, 0.05f);
        Assert.AreEqual(a.transform.position.y, b.transform.position.y, 0.05f);
    }

    // 发射 -------------------------------------------------------------

    [UnityTest]
    public IEnumerator FireAddsOneTierSpacingAndReversesEntry()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);

        Shatter(a, LabPlayer.Edge.Bottom);
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        float vBefore = a.ChamberVelocity.magnitude;
        Assert.AreEqual(EntrySpeed, vBefore, 0.01f, "冲刺期间速度恒定，进膛记的就是它");

        a.Fire();

        float expected = Mathf.Sqrt(vBefore * vBefore + 2f * lab.gravity * lab.tierSpacingCells);
        Assert.AreEqual(expected, b.Velocity.magnitude, 0.05f);
        Assert.Greater(b.Velocity.y, 0f, "从上面冲进来 → 往上面打出去");
        Assert.IsFalse(a.Loaded);
        Assert.IsFalse(b.Suspended);
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);
    }

    [UnityTest]
    public IEnumerator FallThenDashInThenFireGainsFourCells()
    {
        // 验收：B 自由落体 x 格，最后冲刺进入 A，直接射出，B 大约能到 x+4 格
        const float drop = 6f;

        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Bottom);

        float topOfA = a.transform.position.y + AHalf;
        float startY = topOfA + BHalf + drop;
        b.Teleport(new Vector2(0f, startY));

        // 落到还剩约 1 格时进入冲刺态：冲刺不改速度，只是把 v前 定下来并关掉重力
        yield return WaitUntil(() => b.FootY <= startY - (drop - 1f), 3f);
        float v = b.VerticalSpeed;
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = v,
            exitSpeed = v,
            timer = 3f / Mathf.Max(0.01f, v)      // 冲刺位移 3 格，足够覆盖剩下的 1 格多
        });

        yield return WaitUntil(() => a.Loaded, 3f);
        a.Fire();

        float vOut = b.Velocity.magnitude;
        Assert.AreEqual(Mathf.Sqrt(v * v + 2f * lab.gravity * lab.tierSpacingCells), vOut, 0.05f);

        float fireFootY = b.FootY;
        float apex = fireFootY;
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            yield return null;
            apex = Mathf.Max(apex, b.FootY);
            elapsed += Time.deltaTime;
        }

        float gained = apex - fireFootY;
        Assert.AreEqual(lab.CellsForSpeed(vOut), gained, 0.1f, "上升高度 = 出膛速度的折算格数");
        // 自由落体只有 drop - 1 格（最后 1 格是定速冲刺，不涨速度），
        // 再加一个等级间距的 4 格，所以是 drop + 3 ≈ "x + 4 格（大约）"。
        Assert.AreEqual(drop + 3f, gained, 0.6f, "出膛后上升 ≈ 落体高度 + 一个等级间距");
    }

    [UnityTest]
    public IEnumerator HorizontalShotIgnoresGravityUntilTierZero()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Left);

        yield return LoadInto(a, b, Vector2.right, EntrySpeed);
        a.Fire();

        Assert.Less(b.Velocity.x, 0f, "从左边冲进来 → 往左边打出去");
        Assert.AreEqual(Mathf.Abs(b.Velocity.x),
            Mathf.Sqrt(EntrySpeed * EntrySpeed + 2f * lab.gravity * lab.tierSpacingCells), 0.05f);

        yield return null;
        Assert.AreEqual(0f, b.GetComponent<Rigidbody2D>().gravityScale, 0.0001f, "水平射出：不受重力");

        float startY = b.transform.position.y;
        yield return WaitUntil(() => b.HorizontalSpeed < lab.V0Speed, 3f);
        Assert.AreEqual(startY, b.transform.position.y, 0.2f, "免重力期间不掉高度");
        Assert.AreEqual(lab.GravityScale, b.GetComponent<Rigidbody2D>().gravityScale, 0.0001f,
                        "降到零级之后重力照常");
    }

    // 退出装填态 -------------------------------------------------------

    [UnityTest]
    public IEnumerator ChangingFormReleasesLoadedB()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Bottom);
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Shatter(a, LabPlayer.Edge.Top);        // u → L：二者分开

        Assert.AreEqual(LabForm.Converter, a.Form);
        Assert.IsFalse(a.Loaded);
        Assert.IsFalse(b.Suspended);
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);
        Assert.AreEqual(RigidbodyType2D.Dynamic, b.GetComponent<Rigidbody2D>().bodyType);
        Assert.IsTrue(b.GetComponentInChildren<SpriteRenderer>().enabled);
    }

    [UnityTest]
    public IEnumerator DeathReleasesLoadedB()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Bottom);
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Heat(a, LabPlayer.Edge.Top);
        a.StrikeEdge(LabPlayer.Edge.Top, true);        // 热二遇红热 = A 死亡

        Assert.IsFalse(b.Suspended, "膛里的人不能跟着宿主一起没");
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);

        yield return null;
        yield return null;
        Assert.IsNull(a, "A 已销毁");
        Assert.IsNotNull(b);
    }

    // 工具 -------------------------------------------------------------

    /// <summary>把 B 以冲刺态送进膛里：dir = 进入方向，speed = 进入速度。</summary>
    IEnumerator LoadInto(LabPlayer a, LabPlayer b, Vector2 dir, float speed, float gap = 1.5f)
    {
        Vector2 start = (Vector2)a.transform.position - dir * (a.HalfHeight + b.HalfHeight + gap);
        b.Teleport(start);
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = dir,
            speed = speed,
            exitSpeed = speed,
            timer = (gap + 0.5f) / Mathf.Max(0.01f, speed)
        });

        yield return WaitUntil(() => a.Loaded || !b.InDashState, 3f);
        Assert.IsTrue(a.Loaded, "冲刺态撞进 u 形口应装填");
    }

    LabPlayer MakeFrame()
    {
        return MakePlayer("A_Box", TuningLab.A_Pixels, true, TuningLab.ColorA);
    }

    LabPlayer MakeChunk()
    {
        return MakePlayer("B_Chunk", TuningLab.B_Pixels, false, TuningLab.ColorB);
    }

    LabPlayer MakePlayer(string name, int pixels, bool isFrame, Color color)
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
        p.visual = LabVisual.Build(go.transform, pixels, isFrame, color);
        return p;
    }

    void Link(LabPlayer a, LabPlayer b)
    {
        a.other = b;
        b.other = a;
    }

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    static void Heat(LabPlayer p, LabPlayer.Edge edge)
    {
        p.StrikeEdge(edge, true);
        p.StrikeEdge(edge, true);
    }

    static void Shatter(LabPlayer p, LabPlayer.Edge edge)
    {
        Heat(p, edge);
        p.StrikeEdge(edge, false);
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
