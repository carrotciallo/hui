using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 隐藏方块的运行时规格：任一存活且未装填的玩家完全进入组并集后，整组永久半透明。
/// 全部通过公开状态和真实玩家驱动物理步，不依赖键盘输入或私有实现。
/// </summary>
public sealed class LabHiddenBlockTests
{
    static readonly Vector2 SafeA = new Vector2(20f, 12f);
    static readonly Vector2 SafeB = new Vector2(24f, 12f);

    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        LabHiddenBlock.All.Clear();

        labGo = new GameObject("HiddenBlockLab");
        labGo.SetActive(false);

        var level = labGo.AddComponent<LabLevel>();
        var data = new LabLevelData
        {
            spawnA = SafeA,
            spawnB = SafeB,
            bounds = new RectInt(-100, -100, 300, 300)
        };
        data.AddPartition(data.bounds, "Hidden blocks");
        level.CopyFrom(data);

        lab = labGo.AddComponent<TuningLab>();
        lab.hiddenRevealedAlpha = 0.35f;
        labGo.SetActive(true);

        Physics2D.gravity = Vector2.zero;
        MakeKinematic(lab.PlayerA);
        MakeKinematic(lab.PlayerB);
        MovePlayer(lab.PlayerA, SafeA);
        MovePlayer(lab.PlayerB, SafeB);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Camera cam = lab != null ? lab.Cam : null;
        if (labGo != null) Object.Destroy(labGo);
        if (cam != null) Object.Destroy(cam.gameObject);
        yield return null;
        yield return null;
        LabHiddenBlock.All.Clear();
        Physics2D.gravity = previousGravity;
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator PlayerFullyInsideGroupUnionRevealsWholeGroup()
    {
        var first = MakeHidden(new Vector2Int(0, 0), 3);
        var second = MakeHidden(new Vector2Int(1, 0), 3);

        // A 横跨两格：单独任一格都装不下它，但两格并集可以。
        MovePlayer(lab.PlayerA, new Vector2(1f, 0.5f));
        yield return StepFixed(2);

        AssertRevealed(first, lab.hiddenRevealedAlpha);
        AssertRevealed(second, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator EitherLivingPlayerCanRevealAGroup()
    {
        var block = MakeHidden(new Vector2Int(0, 0), 4);
        MovePlayer(lab.PlayerA, SafeA);
        MovePlayer(lab.PlayerB, new Vector2(0.5f, 0.5f));
        yield return StepFixed(2);

        AssertRevealed(block, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator RevealSurvivesLeavingAndMapReset()
    {
        var first = MakeHidden(new Vector2Int(0, 0), 2);
        var second = MakeHidden(new Vector2Int(1, 0), 2);
        MovePlayer(lab.PlayerA, new Vector2(1f, 0.5f));
        yield return StepFixed(2);
        AssertRevealed(first, lab.hiddenRevealedAlpha);

        MovePlayer(lab.PlayerA, SafeA);
        lab.ResetMap();
        yield return StepFixed(2);

        AssertRevealed(first, lab.hiddenRevealedAlpha);
        AssertRevealed(second, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator PlayerPartiallyOutsideDoesNotReveal()
    {
        var block = MakeHidden(new Vector2Int(0, 0), 1);
        MovePlayer(lab.PlayerA, new Vector2(0.1f, 0.5f));
        Physics2D.SyncTransforms();
        Assert.Less(lab.PlayerA.BodyBounds.min.x, 0f, "测试前置：A 必须有一部分越出格子");

        yield return StepFixed(2);

        Assert.IsFalse(block.Revealed);
        Assert.AreEqual(1f, block.GetComponent<SpriteRenderer>().color.a, 0.0001f);
    }

    [UnityTest]
    public IEnumerator CellEdgeToleranceAllowsTinyPenetration()
    {
        var block = MakeHidden(new Vector2Int(0, 0), 1);
        float halfWidth = lab.PlayerA.BodyBounds.extents.x;
        MovePlayer(lab.PlayerA, new Vector2(halfWidth - 0.005f, 0.5f));
        Assert.AreEqual(-0.005f, lab.PlayerA.BodyBounds.min.x, 0.0001f,
            "测试前置：包围盒只越过格边界 0.005，小于 0.01 容差");

        yield return StepFixed(2);

        AssertRevealed(block, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator DeadPlayerDoesNotReveal()
    {
        MovePlayer(lab.PlayerA, new Vector2(0.5f, 0.5f));
        lab.PlayerA.Kill();
        var block = MakeHidden(new Vector2Int(0, 0), 1);

        yield return StepFixed(2);

        Assert.IsFalse(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive, "保留存活队友，让 A 处于死亡等待");
        Assert.IsFalse(block.Revealed);
    }

    [UnityTest]
    public IEnumerator SuspendedPlayerDoesNotReveal()
    {
        yield return LoadBIntoA();
        Assert.IsTrue(lab.PlayerB.Suspended, "测试前置：B 必须真实进入装填态");

        // 宿主 A 部分越界，不能触发；B 的零尺寸 BodyBounds 位于格内，若漏掉
        // Suspended 过滤就会错误揭示，因此这里能独立区分两条规则。
        MakeKinematic(lab.PlayerA);
        MoveBody(lab.PlayerA, new Vector2(0.1f, 0.5f));
        var block = MakeHidden(new Vector2Int(0, 0), 1);
        yield return StepFixed(2);

        Assert.IsTrue(lab.PlayerB.Suspended);
        Assert.AreSame(lab.PlayerA, lab.PlayerB.Host);
        Assert.Greater(lab.PlayerB.BodyBounds.center.x, 0f);
        Assert.Less(lab.PlayerB.BodyBounds.center.x, 1f);
        Assert.Less(lab.PlayerA.BodyBounds.min.x, 0f, "A 自身不满足完全进入条件");
        Assert.IsFalse(block.Revealed);
    }

    [UnityTest]
    public IEnumerator GroupsSharingACellAreEvaluatedIndependently()
    {
        var smallGroup = MakeHidden(new Vector2Int(0, 0), 5);
        var nestedShared = MakeHidden(new Vector2Int(0, 0), 6);
        var nestedOther = MakeHidden(new Vector2Int(1, 0), 6);

        MovePlayer(lab.PlayerA, new Vector2(1f, 0.5f));
        yield return StepFixed(2);

        Assert.IsFalse(smallGroup.Revealed, "只含 (0,0) 的组装不下横跨两格的 A");
        AssertRevealed(nestedShared, lab.hiddenRevealedAlpha);
        AssertRevealed(nestedOther, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator GroupZeroBehavesLikeAnyOtherGroup()
    {
        var first = MakeHidden(new Vector2Int(0, 0), 0);
        var second = MakeHidden(new Vector2Int(1, 0), 0);
        MovePlayer(lab.PlayerA, new Vector2(1f, 0.5f));
        yield return StepFixed(2);

        AssertRevealed(first, lab.hiddenRevealedAlpha);
        AssertRevealed(second, lab.hiddenRevealedAlpha);
    }

    [UnityTest]
    public IEnumerator SingleCellGroupIsValid()
    {
        var block = MakeHidden(new Vector2Int(0, 0), 9);
        MovePlayer(lab.PlayerB, new Vector2(0.5f, 0.5f));
        yield return StepFixed(2);

        Assert.AreEqual(1, LabHiddenBlock.GroupOf(9).Count);
        AssertRevealed(block, lab.hiddenRevealedAlpha);
    }

    [Test]
    public void EditorPreviewDoesNotRegisterRuntimeBlocks()
    {
        Assert.Ignore("PlayMode 中 Application.isPlaying 恒为 true，无法表达编辑器预览门控；留作编辑器预览验收。");
    }

    LabHiddenBlock MakeHidden(Vector2Int cell, int groupId)
    {
        var spec = new LabHiddenBlockSpec(cell, groupId);
        var go = new GameObject("HiddenBlock");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = spec.Center;
        var block = go.AddComponent<LabHiddenBlock>();
        block.cell = cell;
        block.groupId = groupId;
        block.ApplyShape();
        return block;
    }

    IEnumerator LoadBIntoA()
    {
        var a = lab.PlayerA;
        var b = lab.PlayerB;
        a.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        b.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;

        a.StrikeEdge(LabPlayer.Edge.Top, true);
        a.StrikeEdge(LabPlayer.Edge.Top, false);
        Assert.AreEqual(LabForm.Cannon, a.Form, "测试前置：A 形成炮口朝上的大炮");

        b.Teleport((Vector2)a.transform.position +
                   Vector2.up * (a.HalfHeight + b.HalfHeight + 1.5f));
        const float speed = 22f;
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = speed,
            exitSpeed = speed,
            timer = 2f / speed
        });
        yield return WaitUntil(() => b.Suspended, 3f);
    }

    static void AssertRevealed(LabHiddenBlock block, float alpha)
    {
        Assert.IsTrue(block.Revealed);
        Assert.AreEqual(alpha, block.GetComponent<SpriteRenderer>().color.a, 0.0001f,
            "揭示后渲染透明度取 TuningLab.hiddenRevealedAlpha");
    }

    static void MakeKinematic(LabPlayer player)
    {
        player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    static void MovePlayer(LabPlayer player, Vector2 position)
    {
        player.Teleport(position);
        Physics2D.SyncTransforms();
    }

    /// <summary>移动炮宿主不能走 Teleport：重置形态会把膛里的 B 退出来。</summary>
    static void MoveBody(LabPlayer player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = position;
        body.linearVelocity = Vector2.zero;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    static IEnumerator StepFixed(int count)
    {
        for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
    {
        int steps = 0;
        int maxSteps = Mathf.CeilToInt(timeoutSeconds / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        while (!condition() && steps < maxSteps)
        {
            yield return new WaitForFixedUpdate();
            steps++;
        }
        Assert.IsTrue(condition(), $"等待条件超时（{timeoutSeconds}s / {steps} 个物理步）");
    }
}
