using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 自由相机的开局规格：Play 第一帧就对着玩家取景点，不从事先摆好的固定点滑过来。
/// </summary>
public sealed class LabCameraTests
{
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
}
