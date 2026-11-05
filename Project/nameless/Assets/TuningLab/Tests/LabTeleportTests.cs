using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabTeleportTests
{
    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        labGo = new GameObject("TeleportLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        labGo.SetActive(true);
        Physics2D.gravity = Vector2.zero;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        Physics2D.gravity = previousGravity;
    }

    [UnityTest]
    public IEnumerator SelectionListsEveryOtherTeleportAndConfirmMovesBothPlayers()
    {
        var a = MakeTeleport(new Vector2(-4f, 6f));
        var b = MakeTeleport(new Vector2(0f, 6f));
        var c = MakeTeleport(new Vector2(4f, 6f));
        yield return null;
        lab.MarkTeleportPoint(a);
        lab.MarkTeleportPoint(b);
        lab.MarkTeleportPoint(c);

        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA), "A 应能带一把钥匙进入传送");
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));

        lab.EnterTeleportSelection(a);

        Assert.IsTrue(lab.TeleportBusy, "触发后应先进入离场动画");
        Assert.IsFalse(lab.TeleportSelecting, "离场动画期间不能提前开放选择");
        yield return WaitForSelection(lab);
        Assert.AreEqual(2, lab.TeleportTargetCount, "另外两个传送点都应是候选");
        Assert.AreNotSame(a, lab.TeleportTarget, "候选不应包含进入的那个传送点");
        Assert.IsTrue(lab.TeleportTarget == b || lab.TeleportTarget == c);

        var firstTarget = lab.TeleportTarget;
        lab.SelectTeleport(1);
        var secondTarget = lab.TeleportTarget;
        Assert.AreNotSame(firstTarget, secondTarget, "A/D 应能切换到另一个终点");
        Assert.IsTrue(secondTarget == b || secondTarget == c);

        int resets = 0;
        lab.MapReset += () => resets++;

        lab.ConfirmTeleport();

        Assert.IsFalse(lab.TeleportSelecting, "确认后应退出选择状态");
        Assert.AreEqual(1, resets, "一次传送只重置一次地图");
        Assert.AreEqual(0, lab.PlayerA.KeyCount, "传送应吞掉 A 的钥匙");
        Assert.AreEqual(0, lab.PlayerB.KeyCount, "传送应吞掉 B 的钥匙");
        Assert.AreEqual(secondTarget.transform.position.x - 0.65f,
            lab.PlayerA.transform.position.x, 0.001f, "A 应落在终点左侧");
        Assert.AreEqual(secondTarget.transform.position.x + 0.65f,
            lab.PlayerB.transform.position.x, 0.001f, "B 应落在终点右侧");
        Assert.AreEqual(secondTarget.transform.position.y,
            lab.PlayerA.transform.position.y, 0.001f);
        yield return WaitForIdle(lab);
    }

    [UnityTest]
    public IEnumerator SelectionFreezesPlayersAndTeleportRestoresEntryVelocityAfterArrival()
    {
        var source = MakeTeleport(new Vector2(-4f, 6f));
        var target = MakeTeleport(new Vector2(4f, 6f));
        yield return null;
        lab.MarkTeleportPoint(source);
        lab.MarkTeleportPoint(target);

        Vector2 velocityA = new Vector2(7f, -9f);
        Vector2 velocityB = new Vector2(-6f, 8f);
        lab.PlayerA.SetVelocity(velocityA);
        lab.PlayerB.SetVelocity(velocityB);
        lab.EnterTeleportSelection(source);
        lab.CancelTeleport();
        lab.EnterTeleportSelection(source);

        Assert.IsTrue(lab.TeleportBusy, "离场期间取消和重复触发都不能打断动画");
        Assert.AreEqual(RigidbodyType2D.Kinematic,
            lab.PlayerA.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(RigidbodyType2D.Kinematic,
            lab.PlayerB.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity);
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity);

        lab.PlayerA.enabled = false;
        lab.PlayerB.enabled = false;
        yield return WaitForSelection(lab);
        lab.ConfirmTeleport();

        Assert.IsTrue(lab.TeleportBusy, "到达动画期间仍需保持状态锁");
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity, "到达动画前不能提前写回 A 的速度");
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity, "到达动画前不能提前写回 B 的速度");
        yield return WaitForIdle(lab);
        Assert.AreEqual(velocityA, lab.PlayerA.Velocity, "到达动画后应恢复 A 的完整进入速度");
        Assert.AreEqual(velocityB, lab.PlayerB.Velocity, "到达动画后应恢复 B 的完整进入速度");
    }

    [UnityTest]
    public IEnumerator ConsecutiveTeleportsCleanUpAnimationObjects()
    {
        var a = MakeTeleport(new Vector2(-6f, 6f));
        var b = MakeTeleport(new Vector2(0f, 6f));
        var c = MakeTeleport(new Vector2(6f, 6f));
        yield return null;
        lab.MarkTeleportPoint(a);
        lab.MarkTeleportPoint(b);
        lab.MarkTeleportPoint(c);
        lab.PlayerA.enabled = false;
        lab.PlayerB.enabled = false;

        lab.EnterTeleportSelection(a);
        yield return WaitForSelection(lab);
        Assert.AreSame(b, lab.TeleportTarget);
        lab.ConfirmTeleport();
        yield return WaitForIdle(lab);
        yield return null;
        Assert.IsNull(lab.transform.Find("TeleportAnimation"), "第一次到达后应清理动画根对象");

        lab.EnterTeleportSelection(b);
        yield return WaitForSelection(lab);
        Assert.AreSame(a, lab.TeleportTarget);
        lab.SelectTeleport(1);
        Assert.AreSame(c, lab.TeleportTarget);
        lab.ConfirmTeleport();
        yield return WaitForIdle(lab);
        yield return null;

        Assert.IsNull(lab.transform.Find("TeleportAnimation"), "连续传送后不应残留动画根对象");
        Assert.AreEqual(c.transform.position.x - 0.65f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(c.transform.position.x + 0.65f, lab.PlayerB.transform.position.x, 0.001f);
        Assert.AreEqual(c.transform.position.y, lab.PlayerA.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator OneMarkedTeleportCannotOpenSelection()
    {
        var source = MakeTeleport(new Vector2(-4f, 6f));
        var unmarked = MakeTeleport(new Vector2(4f, 6f));
        yield return null;

        lab.MarkTeleportPoint(source);
        lab.EnterTeleportSelection(source);

        Assert.IsFalse(lab.TeleportSelecting, "只有一个已标记传送点时不应进入选择");
        Assert.IsFalse(lab.TeleportBusy);
        Assert.AreEqual(0, lab.TeleportTargetCount);
        Assert.IsFalse(unmarked.IsMarked, "未访问的传送点不应自动标记");
    }

    [UnityTest]
    public IEnumerator OnlyMarkedTeleportsAreSelectable()
    {
        var source = MakeTeleport(new Vector2(-6f, 6f));
        var target = MakeTeleport(new Vector2(0f, 6f));
        var unmarked = MakeTeleport(new Vector2(6f, 6f));
        yield return null;

        lab.MarkTeleportPoint(source);
        lab.MarkTeleportPoint(target);
        lab.EnterTeleportSelection(source);

        yield return WaitForSelection(lab);
        Assert.AreEqual(1, lab.TeleportTargetCount);
        Assert.AreSame(target, lab.TeleportTarget);
        Assert.AreNotSame(unmarked, lab.TeleportTarget);
        lab.CancelTeleport();
        yield return WaitForIdle(lab);
    }

    [UnityTest]
    public IEnumerator CancellingSelectionRestoresPlayersAndCamera()
    {
        var source = MakeTeleport(new Vector2(-4f, 6f));
        var target = MakeTeleport(new Vector2(4f, 6f));
        yield return null;
        lab.MarkTeleportPoint(source);
        lab.MarkTeleportPoint(target);
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA));
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));
        int resets = 0;
        lab.MapReset += () => resets++;
        lab.PlayerA.SetVelocity(new Vector2(5f, 6f));
        lab.PlayerB.SetVelocity(new Vector2(-4f, 7f));

        lab.EnterTeleportSelection(source);
        yield return WaitForSelection(lab);
        lab.CancelTeleport();

        Assert.IsFalse(lab.TeleportSelecting);
        Assert.IsTrue(lab.TeleportBusy, "取消后应先播放来源点返场动画");
        yield return WaitForIdle(lab);
        Assert.AreEqual(RigidbodyType2D.Dynamic,
            lab.PlayerA.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(RigidbodyType2D.Dynamic,
            lab.PlayerB.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity);
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity);
        Assert.AreEqual(1, lab.PlayerA.KeyCount, "取消不能吞掉 A 的钥匙");
        Assert.AreEqual(1, lab.PlayerB.KeyCount, "取消不能吞掉 B 的钥匙");
        Assert.AreEqual(0, resets, "取消不能重置地图");
        Assert.IsTrue(source.IsMarked && target.IsMarked, "取消不能清除传送门标记");
        Assert.AreEqual(source.transform.position.x - 0.65f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(source.transform.position.x + 0.65f, lab.PlayerB.transform.position.x, 0.001f);
        Assert.AreEqual(0, lab.TeleportTargetCount);

        lab.EnterTeleportSelection(source);
        yield return WaitForSelection(lab);
        lab.ConfirmTeleport();
        yield return WaitForIdle(lab);
        Assert.AreEqual(1, resets, "取消后再次确认应能正常传送");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
        Assert.AreEqual(0, lab.PlayerB.KeyCount);
    }

    [UnityTest]
    public IEnumerator SingleTeleportCannotOpenSelection()
    {
        var only = MakeTeleport(new Vector2(0f, 6f));
        yield return null;

        lab.EnterTeleportSelection(only);

        Assert.IsFalse(lab.TeleportSelecting, "没有其它传送点时不应打开选择");
        Assert.IsFalse(lab.TeleportBusy);
        Assert.AreEqual(0, lab.TeleportTargetCount);
    }

    [UnityTest]
    public IEnumerator LevelDataBuildCreatesRegisteredTeleports()
    {
        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(labGo.transform, false);
        var level = levelGo.AddComponent<LabLevel>();

        var data = new LabLevelData();
        data.teleports.Add(new LabTeleportSpec(new Vector2(-2f, 6f)));
        data.teleports.Add(new LabTeleportSpec(new Vector2(2f, 6f)));
        level.CopyFrom(data);
        level.BuildRuntime(levelGo.transform, null);
        yield return null;

        var points = Object.FindObjectsByType<LabTeleportPoint>();
        Assert.AreEqual(2, points.Length, "关卡数据里的两个传送点都应生成");

        foreach (var point in points)
        {
            var body = point.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(body);
            Assert.IsTrue(body.isTrigger, "传送点应是触发区");
            Assert.AreEqual(3f, body.size.x, 0.001f);
            Assert.AreEqual(3f, body.size.y, 0.001f);
        }

        lab.MarkTeleportPoint(points[0]);
        lab.MarkTeleportPoint(points[1]);
        lab.EnterTeleportSelection(points[0]);
        yield return WaitForSelection(lab);
        Assert.AreEqual(1, lab.TeleportTargetCount);

        var target = lab.TeleportTarget;
        lab.ConfirmTeleport();

        Assert.AreEqual(target.transform.position.x - 0.65f,
            lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(target.transform.position.x + 0.65f,
            lab.PlayerB.transform.position.x, 0.001f);
        yield return WaitForIdle(lab);
    }

    [UnityTest]
    public IEnumerator AwakeTimeLevelBuildRegistersTeleports()
    {
        // 关掉 SetUp 里的调试 Lab，保证 Start 只会找到下面这个正式构建的 TuningLab。
        labGo.SetActive(false);

        var awakeGo = new GameObject("AwakeTeleportLab");
        awakeGo.SetActive(false);
        var level = awakeGo.AddComponent<LabLevel>();
        var data = new LabLevelData();
        data.teleports.Add(new LabTeleportSpec(new Vector2(-3f, 6f)));
        data.teleports.Add(new LabTeleportSpec(new Vector2(3f, 6f)));
        level.CopyFrom(data);
        var awakeLab = awakeGo.AddComponent<TuningLab>();
        awakeGo.SetActive(true);
        yield return null;

        var points = Object.FindObjectsByType<LabTeleportPoint>();
        Assert.AreEqual(2, points.Length, "Awake 时按关卡数据生成两个传送点");
        Assert.AreEqual(2, awakeLab.TeleportPointCount, "Awake 时生成的传送点也要注册");

        awakeLab.MarkTeleportPoint(points[0]);
        awakeLab.MarkTeleportPoint(points[1]);
        awakeLab.EnterTeleportSelection(points[0]);
        yield return WaitForSelection(awakeLab);
        Assert.AreEqual(1, awakeLab.TeleportTargetCount);

        Object.Destroy(awakeGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CameraFocusCanCrossCurrentBoundsDuringTeleportSelection()
    {
        var cameraGo = new GameObject("TeleportCamera");
        cameraGo.AddComponent<Camera>().orthographic = true;
        var camera = cameraGo.AddComponent<LabCamera>();
        camera.Configure(null, null, null, new Rect(-5f, -5f, 10f, 10f), 1f, 5f, 0f);
        cameraGo.transform.position = Vector3.zero;

        camera.FocusOn(new Vector2(20f, 0f), 0.25f);
        yield return new WaitForSecondsRealtime(0.3f);

        Assert.Greater(cameraGo.transform.position.x, 5f,
            "选择跨分区传送点时，相机不应被当前分区 bounds 截回去");

        Object.Destroy(cameraGo);
    }

    [UnityTest]
    public IEnumerator CameraStaysOnSelectedTeleportWhileSelectionRemainsOpen()
    {
        var source = MakeTeleport(new Vector2(-4f, 6f));
        var target = MakeTeleport(new Vector2(20f, 6f));
        yield return null;
        lab.MarkTeleportPoint(source);
        lab.MarkTeleportPoint(target);

        lab.EnterTeleportSelection(source);
        yield return WaitForSelection(lab);
        yield return new WaitForSecondsRealtime(0.4f);

        Assert.AreEqual(target.transform.position.x, lab.Cam.transform.position.x, 1f,
            "选择态持续时，相机应停在当前候选传送点");
    }

    static IEnumerator WaitForSelection(TuningLab owner)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (owner.TeleportBusy && !owner.TeleportSelecting && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.IsTrue(owner.TeleportSelecting, "离场动画结束后应开放传送门选择");
    }

    static IEnumerator WaitForIdle(TuningLab owner)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (owner.TeleportBusy && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.IsFalse(owner.TeleportBusy, "到达或取消返场动画应正常结束");
    }

    LabTeleportPoint MakeTeleport(Vector2 position)
    {
        var go = new GameObject("Teleport");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = position;
        var point = go.AddComponent<LabTeleportPoint>();
        point.ApplyShape();
        return point;
    }

    LabKey MakeKey()
    {
        var go = new GameObject("Key");
        go.transform.SetParent(labGo.transform, false);
        return go.AddComponent<LabKey>();
    }
}
