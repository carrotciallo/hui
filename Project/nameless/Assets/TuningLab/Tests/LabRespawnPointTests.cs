using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabRespawnPointTests
{
    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        labGo = new GameObject("RespawnPointLab");
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
    public IEnumerator OnePlayerEnteringMovesBothToFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        lab.PlayerA.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerB.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerA.Teleport(checkpointPosition);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(checkpoint.IsTouching(lab.PlayerA), "重生点没有记录进入的 A");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f,
            "A 应固定在重生点左侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f,
            "B 应固定在重生点右侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointResetsMapAndBothPlayerStates()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;

        var bodyA = lab.PlayerA.GetComponent<Rigidbody2D>();
        var bodyB = lab.PlayerB.GetComponent<Rigidbody2D>();
        bodyA.bodyType = RigidbodyType2D.Kinematic;
        bodyB.bodyType = RigidbodyType2D.Kinematic;

        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA));
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        lab.PlayerB.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetVelocity(Vector2.right * 4f);
        lab.PlayerB.SetVelocity(Vector2.left * 3f);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        lab.PlayerA.Teleport(checkpointPosition);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "每次进入只应重置地图一次");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
        Assert.AreEqual(0, lab.PlayerB.KeyCount);
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity);
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerA.GetEdge((LabPlayer.Edge)i));
            Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge((LabPlayer.Edge)i));
        }
    }

    [UnityTest]
    public IEnumerator BothPlayersEnteringTogetherTreatItAsOneTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        lab.PlayerA.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerB.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Teleport(checkpointPosition + Vector2.left);
        lab.PlayerB.Teleport(checkpointPosition + Vector2.right);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "两人同时进入仍只视为一次触发");
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
    }

    [UnityTest]
    public IEnumerator CheckpointTriggersAgainOnlyAfterEveryoneLeaves()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        lab.PlayerA.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerB.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, resetCount);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, resetCount, "两人仍在触发器内时不得重复重置");

        lab.PlayerA.Teleport(new Vector2(8f, 8f));
        lab.PlayerB.Teleport(new Vector2(10f, 8f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(2, resetCount, "全部离开后重新进入应再次触发");
    }

    [UnityTest]
    public IEnumerator DeathAfterCheckpointRespawnsBothAtFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        lab.PlayerA.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerB.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, resetCount);

        lab.PlayerA.Teleport(new Vector2(8f, 8f));
        lab.PlayerB.Teleport(new Vector2(10f, 8f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(2, resetCount, "死亡回归也应重置地图一次");
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(checkpointPosition.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointRestoresPickedLevelKey()
    {
        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(labGo.transform, false);
        var level = levelGo.AddComponent<LabLevel>();
        var data = new LabLevelData();
        data.keys.Add(new LabKeySpec(new Vector2(3f, 3f)));
        level.CopyFrom(data);
        level.BuildRuntime(levelGo.transform, null);

        var key = level.GetComponentInChildren<LabKey>();
        Assert.IsNotNull(key);
        Assert.IsTrue(key.TryCollect(lab.PlayerA));
        yield return null;
        Assert.IsNull(level.GetComponentInChildren<LabKey>(), "拾取后世界里的钥匙应消失");

        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        lab.PlayerA.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerB.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsNotNull(level.GetComponentInChildren<LabKey>(), "触发重生点后钥匙应刷新");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
    }

    LabRespawnPoint MakeCheckpoint(Vector2 position)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = position;
        var point = go.AddComponent<LabRespawnPoint>();
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
