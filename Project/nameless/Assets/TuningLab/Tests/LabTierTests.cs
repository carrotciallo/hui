using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 速度等级与上限的纯数学规格：
///   默认 6 级，碰撞读数到顶档封顶；下落 / 冰面加速共用同一个由等级表派生的上限。
/// 只锁 TuningLab 的公开读数，不跑物理。
/// </summary>
public sealed class LabTierTests
{
    GameObject labGo;
    TuningLab lab;

    [SetUp]
    public void SetUp()
    {
        labGo = new GameObject("TierLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        if (labGo != null) Object.DestroyImmediate(labGo);
    }

    [Test]
    public void DefaultsToSixTiersAtFourCellSpacing()
    {
        Assert.AreEqual(6, lab.tierCount, "默认 6 级（不含零级）");
        CollectionAssert.AreEqual(new[] { 4f, 8f, 12f, 16f, 20f, 24f }, lab.Bounds);
    }

    [Test]
    public void SpeedCapStrictlyAboveTopTierThreshold()
    {
        float top = lab.Boundary(lab.MaxTier - 1);
        Assert.Greater(lab.CellsForSpeed(lab.TopTierSpeed), top,
            "上限必须严格高于顶档门槛，否则速度永远差一点进不了顶档");
        Assert.AreEqual(lab.MaxTier, lab.TierOfSpeed(lab.TopTierSpeed - 0.01f),
            "顶档门槛与上限之间是顶档");
    }

    [Test]
    public void SpeedsBeyondTopTierStillReadAsTopTier()
    {
        float far = lab.SpeedForCells(lab.Boundary(lab.MaxTier - 1) * 10f);
        Assert.AreEqual(lab.MaxTier, lab.TierOfSpeed(far),
            "超过顶档门槛多少都只读顶档，碰撞按顶档结算");
    }

    [Test]
    public void CapFollowsTierTableAndGravity()
    {
        lab.tierCount = 4;
        float fourTierCap = lab.TopTierSpeed;

        lab.tierCount = 6;
        Assert.Greater(lab.TopTierSpeed, fourTierCap, "等级多一档，上限跟着抬");

        Assert.AreEqual(lab.Boundary(lab.MaxTier - 1) + lab.capOverTopCells,
            lab.CellsForSpeed(lab.TopTierSpeed), 0.001f, "上限 = 顶档门槛 + capOverTopCells");

        lab.gravity = 100f;
        Assert.AreEqual(lab.Boundary(lab.MaxTier - 1) + lab.capOverTopCells,
            lab.CellsForSpeed(lab.TopTierSpeed), 0.001f, "改重力不改变上限折算的格数");
    }
}
