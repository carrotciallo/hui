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
    public void ACanCollectOnlyInNormalOrSpiritForm()
    {
        var a = MakePlayer("A", true);
        var key = MakeKey();

        Assert.IsTrue(key.TryCollect(a));
        Assert.AreEqual(1, a.KeyCount);

        a.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        a.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Spirit);
        a.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Spirit);
        a.SetEdge(LabPlayer.Edge.Right, LabPlayer.EdgeState.Spirit);
        Assert.AreEqual(LabForm.Spirit, a.Form);
        key = MakeKey();
        Assert.IsTrue(key.TryCollect(a));
        Assert.AreEqual(2, a.KeyCount);

        a.ResetEdges();
        a.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        a.SetEdge(LabPlayer.Edge.Right, LabPlayer.EdgeState.Spirit);
        Assert.AreEqual(LabForm.Converter, a.Form);
        var blocked = MakeKey();
        Assert.IsFalse(blocked.TryCollect(a));
        Assert.AreEqual(2, a.KeyCount);
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
        Assert.IsTrue(keyTwo.TryCollect(b));
        Assert.AreEqual(2, b.KeyCount);
        Assert.IsTrue(doorOne.TryOpen(b));
        Assert.IsTrue(doorTwo.TryOpen(b));
        Assert.IsTrue(doorOne.IsOpen);
        Assert.IsTrue(doorTwo.IsOpen);
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
