using NUnit.Framework;
using UnityEngine;

/// <summary>绳子三级以上反弹速度规则。</summary>
public sealed class LabRopeTests
{
    [Test]
    public void RopeBouncePreservesMaxFallSpeedAndAddsOneTier()
    {
        var go = new GameObject("RopeBounceLab");
        go.SetActive(false);
        var lab = go.AddComponent<TuningLab>();
        lab.autoBuild = false;
        go.SetActive(true);
        lab.gravity = 50f;
        lab.tierSpacingCells = 4f;
        lab.maxFallSpeed = 42f;

        float incomingCells = lab.CellsForSpeed(lab.maxFallSpeed);
        float bounceCells = lab.CellsForSpeed(lab.RopeBounceSpeed(lab.maxFallSpeed));

        Assert.That(bounceCells, Is.EqualTo(incomingCells + lab.tierSpacingCells).Within(0.001f));
        Assert.That(bounceCells, Is.GreaterThan(incomingCells));

        Object.DestroyImmediate(go);
    }
}
