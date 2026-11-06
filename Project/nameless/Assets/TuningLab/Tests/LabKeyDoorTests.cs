using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabKeyDoorTests
{
    GameObject root;
    TuningLab lab;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("KeyDoorTestRoot");
        root.SetActive(false);
        lab = root.AddComponent<TuningLab>();
        lab.autoBuild = false;
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void ACanCollectKeysInEveryForm()
    {
        var a = MakePlayer("A", true);

        AssertFormCollects(a, LabForm.Normal, null);

        // 碎一条边 = u 大炮：反馈里"A 拿不起钥匙"卡的就是这一档
        AssertFormCollects(a, LabForm.Cannon, new[] { LabPlayer.Edge.Top });

        AssertFormCollects(a, LabForm.Converter, new[] { LabPlayer.Edge.Top, LabPlayer.Edge.Right });
        AssertFormCollects(a, LabForm.Elevator, new[] { LabPlayer.Edge.Top, LabPlayer.Edge.Bottom });
        AssertFormCollects(a, LabForm.Spear,
            new[] { LabPlayer.Edge.Top, LabPlayer.Edge.Right, LabPlayer.Edge.Bottom });

        AssertFormCollects(a, LabForm.Spirit,
            new[] { LabPlayer.Edge.Top, LabPlayer.Edge.Bottom, LabPlayer.Edge.Left, LabPlayer.Edge.Right });
    }

    /// <summary>按给定碎边组合摆好形态，再验一次"这形态下 A 捡得起钥匙"。</summary>
    void AssertFormCollects(LabPlayer a, LabForm expected, LabPlayer.Edge[] broken)
    {
        a.ResetEdges();
        if (broken != null)
            for (int i = 0; i < broken.Length; i++)
                a.SetEdge(broken[i], LabPlayer.EdgeState.Spirit);
        Assert.AreEqual(expected, a.Form, $"用例前提没成立：这组碎边应该是 {expected}");

        // 持钥上限 1：上一档形态捡的那把要先清掉，否则这一档必然捡不动。
        a.ClearKeys();
        Assert.IsTrue(MakeKey().TryCollect(a), $"{expected} 形态下 A 应该捡得起钥匙");
        Assert.AreEqual(1, a.KeyCount, $"{expected} 形态下钥匙数量要真的加上去");
    }

    [Test]
    public void HeldKeyCapIsOneAndSecondPickupIsRefused()
    {
        var a = MakePlayer("A", true);
        var first = MakeKey();
        var second = MakeKey();

        Assert.IsTrue(first.TryCollect(a), "空手时第一把捡得起");
        Assert.IsFalse(second.TryCollect(a), "身上已经有一把，第二把应当拒绝");
        Assert.IsFalse(second.Collected, "捡不动的第二把不能被标记成已拾取");
        Assert.AreEqual(1, a.KeyCount, "被拒绝的拾取不加数量");

        Assert.IsTrue(a.ConsumeKey(), "开门消耗掉一把");
        Assert.IsTrue(second.TryCollect(a), "空手后同一把还能再捡");
        Assert.AreEqual(1, a.KeyCount);
    }

    [Test]
    public void BCanCollectInAnyFormAndAnyKeyOpensAnyDoor()
    {
        var b = MakePlayer("B", false);
        var keyOne = MakeKey();
        var keyTwo = MakeKey();
        var doorOne = MakeDoor();
        var doorTwo = MakeDoor();

        Assert.IsTrue(keyOne.TryCollect(b));
        Assert.IsTrue(doorOne.TryOpen(b), "第一把钥匙开第一扇门");
        Assert.AreEqual(0, b.KeyCount, "门只消耗一把");

        Assert.IsTrue(keyTwo.TryCollect(b), "空手后捡第二把");
        Assert.IsTrue(doorTwo.TryOpen(b), "第二把钥匙开第二扇门：钥匙不认门");
        Assert.AreEqual(0, b.KeyCount);
        Assert.IsFalse(doorOne.TryOpen(b));
    }

    [Test]
    public void DoorConsumesOnlyOneKeyAndCanBeReset()
    {
        var a = MakePlayer("A", true);
        var key = MakeKey();
        var door = MakeDoor();

        Assert.IsTrue(key.TryCollect(a));
        Assert.IsTrue(door.TryOpen(a));
        Assert.AreEqual(0, a.KeyCount);
        Assert.IsFalse(door.TryOpen(a));

        door.ResetDoor();
        Assert.IsFalse(door.IsOpen);
        Assert.IsFalse(door.TryOpen(a));
    }

    [UnityTest]
    public IEnumerator LevelBuildPlacesKeysAndDoorsAndMapResetRestoresThem()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.keys.Add(new LabKeySpec(new Vector2(-11.5f, 0.5f)));
        level.Data.doors.Add(new LabDoorSpec(new Vector2(-9.5f, 1.5f), Color.red));
        level.Data.Normalize();

        level.BuildRuntime(root.transform, null);

        string generated = LabLevel.GeneratedRootName;
        Transform keyTransform = root.transform.Find(generated + "/Key");
        Transform doorTransform = root.transform.Find(generated + "/Door");
        Assert.IsNotNull(keyTransform, "关卡里的钥匙被生成");
        Assert.IsNotNull(doorTransform, "关卡里的门被生成");
        Assert.AreEqual(new Vector3(-11.5f, 0.5f, 0f), keyTransform.position);
        Assert.AreEqual(new Vector3(-9.5f, 1.5f, 0f), doorTransform.position);

        var door = doorTransform.GetComponent<LabDoor>();
        Assert.AreEqual(Color.red, door.closedColor);
        Assert.AreEqual(Color.red, doorTransform.GetComponent<SpriteRenderer>().color);

        var player = MakePlayer("A", true);
        Assert.IsTrue(keyTransform.GetComponent<LabKey>().TryCollect(player));
        Assert.IsTrue(door.TryOpen(player));
        Assert.IsTrue(door.IsOpen);

        yield return null;  // 让钥匙的 Destroy 落地

        lab.ResetMap();

        Assert.IsFalse(door.IsOpen, "地图重置后门关回去");
        Assert.AreEqual(Color.red, doorTransform.GetComponent<SpriteRenderer>().color);
        Assert.IsNotNull(root.transform.Find(generated + "/Key"), "地图重置后被捡走的钥匙长回来");
    }

    LabPlayer MakePlayer(string name, bool frame)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        var player = go.AddComponent<LabPlayer>();
        player.lab = lab;
        player.canBeRedHot = frame;
        player.scheme = frame ? LabPlayer.Scheme.P1 : LabPlayer.Scheme.P2;
        return player;
    }

    LabKey MakeKey()
    {
        var go = new GameObject("Key");
        go.transform.SetParent(root.transform, false);
        return go.AddComponent<LabKey>();
    }

    LabDoor MakeDoor()
    {
        var go = new GameObject("Door");
        go.transform.SetParent(root.transform, false);
        return go.AddComponent<LabDoor>();
    }
}
