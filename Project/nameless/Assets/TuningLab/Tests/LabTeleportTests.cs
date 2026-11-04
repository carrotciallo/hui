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

        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA), "A 应能带一把钥匙进入传送");
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));

        lab.EnterTeleportSelection(a);

        Assert.IsTrue(lab.TeleportSelecting, "进入传送点后应打开终点选择");
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
    }

    [UnityTest]
    public IEnumerator SingleTeleportCannotOpenSelection()
    {
        var only = MakeTeleport(new Vector2(0f, 6f));
        yield return null;

        lab.EnterTeleportSelection(only);

        Assert.IsFalse(lab.TeleportSelecting, "没有其它传送点时不应打开选择");
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

        lab.EnterTeleportSelection(points[0]);
        Assert.IsTrue(lab.TeleportSelecting, "生成后的传送点应注册到 TuningLab");
        Assert.AreEqual(1, lab.TeleportTargetCount);

        var target = lab.TeleportTarget;
        lab.ConfirmTeleport();

        Assert.AreEqual(target.transform.position.x - 0.65f,
            lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(target.transform.position.x + 0.65f,
            lab.PlayerB.transform.position.x, 0.001f);
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

        awakeLab.EnterTeleportSelection(points[0]);
        Assert.IsTrue(awakeLab.TeleportSelecting, "构建完成后能进入终点选择");
        Assert.AreEqual(1, awakeLab.TeleportTargetCount);

        Object.Destroy(awakeGo);
        yield return null;
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
