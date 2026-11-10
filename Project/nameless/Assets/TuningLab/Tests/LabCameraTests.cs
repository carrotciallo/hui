using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 自由相机的开局规格：Play 第一帧就对着玩家取景点，不从事先摆好的固定点滑过来。
/// </summary>
public sealed class LabCameraTests
{
    [Test]
    public void DestroyingLabAlsoRemovesItsCameraAndAudioListener()
    {
        var camera = lab.Cam;
        var listener = camera.GetComponent<AudioListener>();
        Object.DestroyImmediate(labGo);
        Assert.IsTrue(camera == null, "规则台销毁后不应遗留跟随旧玩家的相机");
        Assert.IsTrue(listener == null, "遗留监听器会导致重复监听和测试串扰");
    }

    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;

        labGo = new GameObject("CameraLab");
        labGo.SetActive(false);

        var level = labGo.AddComponent<LabLevel>();
        var data = new LabLevelData
        {
            spawnA = new Vector2(28f, 10f),
            spawnB = new Vector2(32f, 10f),
            bounds = new RectInt(-100, -100, 300, 300)
        };
        data.AddPartition(data.bounds, "Level 1");
        level.CopyFrom(data);

        lab = labGo.AddComponent<TuningLab>();
        labGo.SetActive(true);   // Awake → BuildAll → BuildCamera → Configure 时就应该对齐
    }

    [TearDown]
    public void TearDown()
    {
        Camera cam = lab != null ? lab.Cam : null;
        if (labGo != null) Object.DestroyImmediate(labGo);
        if (cam != null) Object.DestroyImmediate(cam.gameObject);
        Physics2D.gravity = previousGravity;
    }

    [Test]
    public void CameraStartsOnSpawnMidpointWithoutFlyingIn()
    {
        Assert.IsNotNull(lab.Cam, "自由相机应已生成");
        var follow = lab.Cam.GetComponent<LabCamera>();
        Assert.IsNotNull(follow, "相机上应挂 LabCamera");

        Vector2 a = lab.PlayerA.transform.position;
        Vector2 b = lab.PlayerB.transform.position;
        Vector3 expected = new Vector3(
            (a.x + b.x) * 0.5f + follow.offset.x,
            (a.y + b.y) * 0.5f + follow.offset.y,
            -10f);

        Assert.AreEqual(expected.x, lab.Cam.transform.position.x, 0.001f,
            "开局相机应直接对准玩家中点，而不是从固定点插值过来");
        Assert.AreEqual(expected.y, lab.Cam.transform.position.y, 0.001f,
            "开局相机应直接对准玩家中点，而不是从固定点插值过来");
        Assert.AreEqual(expected.z, lab.Cam.transform.position.z, 0.001f, "相机 z 保持不变");

        float spread = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        float expectedSize = Mathf.Clamp(
            follow.minSize + spread * follow.sizePerUnit, follow.minSize, follow.maxSize);
        Assert.AreEqual(expectedSize, lab.Cam.orthographicSize, 0.001f,
            "开局尺寸也应一次到位，不应先按固定尺寸再缩放");
    }

    [Test]
    public void SnapToPlayersImmediatelyReframesMovedPlayers()
    {
        var follow = lab.Cam.GetComponent<LabCamera>();
        lab.PlayerA.Teleport(new Vector2(40f, 20f));
        lab.PlayerB.Teleport(new Vector2(50f, 26f));
        lab.Cam.transform.position = new Vector3(-40f, -30f, -10f);
        lab.Cam.orthographicSize = follow.maxSize;

        follow.SnapToPlayers();

        Vector2 a = lab.PlayerA.transform.position;
        Vector2 b = lab.PlayerB.transform.position;
        Vector3 expected = new Vector3(
            (a.x + b.x) * 0.5f + follow.offset.x,
            (a.y + b.y) * 0.5f + follow.offset.y,
            -10f);
        Assert.AreEqual(expected.x, lab.Cam.transform.position.x, 0.001f,
            "SnapToPlayers 返回时位置必须已经对准玩家中点，不能等 LateUpdate 滑入");
        Assert.AreEqual(expected.y, lab.Cam.transform.position.y, 0.001f);
        Assert.AreEqual(expected.z, lab.Cam.transform.position.z, 0.001f);

        float spread = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        float expectedSize = Mathf.Clamp(
            follow.minSize + spread * follow.sizePerUnit, follow.minSize, follow.maxSize);
        Assert.AreEqual(expectedSize, lab.Cam.orthographicSize, 0.001f,
            "正交尺寸也必须在 SnapToPlayers 内一次到位");
    }

    [Test]
    public void SnapToPlayersExcludesPlayerWaitingDead()
    {
        var follow = lab.Cam.GetComponent<LabCamera>();
        lab.PlayerA.Teleport(new Vector2(40f, 20f));
        lab.PlayerB.Teleport(new Vector2(50f, 26f));
        lab.PlayerA.Kill();
        lab.Cam.transform.position = new Vector3(-40f, -30f, -10f);
        lab.Cam.orthographicSize = follow.maxSize;

        follow.SnapToPlayers();

        Assert.IsFalse(lab.PlayerA.IsAlive, "A 应处于死亡等待");
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Vector3 expected = (Vector2)lab.PlayerB.transform.position;
        expected += follow.offset;
        expected.z = -10f;
        Assert.AreEqual(expected.x, lab.Cam.transform.position.x, 0.001f,
            "死亡等待的 A 不应再把相机中点拉走");
        Assert.AreEqual(expected.y, lab.Cam.transform.position.y, 0.001f);
        Assert.AreEqual(expected.z, lab.Cam.transform.position.z, 0.001f);
        Assert.AreEqual(follow.minSize, lab.Cam.orthographicSize, 0.001f,
            "只剩一名存活玩家时取景尺寸立即收到最小值");
    }

    [Test]
    public void SnapToPlayersKeepsCurrentFrameWhenBothPlayersAreDead()
    {
        var follow = lab.Cam.GetComponent<LabCamera>();
        lab.PlayerA.Kill();
        lab.PlayerB.Kill();
        var expectedPosition = new Vector3(7f, 9f, -10f);
        const float expectedSize = 11.25f;
        lab.Cam.transform.position = expectedPosition;
        lab.Cam.orthographicSize = expectedSize;

        follow.SnapToPlayers();

        Assert.IsFalse(lab.PlayerA.IsAlive);
        Assert.IsFalse(lab.PlayerB.IsAlive);
        Assert.AreEqual(expectedPosition.x, lab.Cam.transform.position.x, 0.001f,
            "没有存活玩家时保持当前取景，不跳到原点或死亡玩家位置");
        Assert.AreEqual(expectedPosition.y, lab.Cam.transform.position.y, 0.001f);
        Assert.AreEqual(expectedPosition.z, lab.Cam.transform.position.z, 0.001f);
        Assert.AreEqual(expectedSize, lab.Cam.orthographicSize, 0.001f,
            "没有存活玩家时也不改变正交尺寸");
    }
}
