using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabChargeFeedbackTests
{
    GameObject labGo;
    TuningLab lab;

    [SetUp]
    public void SetUp()
    {
        labGo = new GameObject("ChargeFeedbackLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        labGo.SetActive(true);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReadyFeedbackFlashesOnceAndBurstsAboveHead()
    {
        lab.chargeReadyFlashDuration = 0.12f;
        lab.chargeReadyParticleCount = 5;
        var feedback = lab.PlayerA.GetComponentInChildren<LabChargeFeedback>();

        Assert.IsNotNull(feedback);
        Assert.IsTrue(feedback.Play());
        Assert.IsTrue(feedback.IsActive);
        Assert.AreEqual(5, feedback.BurstParticleCount);

        var particles = feedback.GetComponentInChildren<ParticleSystem>();
        Assert.IsNotNull(particles, "蓄力完成反馈应有头顶粒子");
        Assert.Greater(particles.transform.localPosition.y,
            lab.PlayerA.pixels / (2f * TuningLab.CellPixels),
            "粒子应在角色头顶");

        yield return new WaitForSecondsRealtime(0.2f);
        Assert.IsFalse(feedback.IsActive, "单次高亮应在持续时间内结束");
    }

    [Test]
    public void DisabledFeedbackDoesNotPlay()
    {
        lab.chargeReadyFeedbackEnabled = false;
        var feedback = lab.PlayerA.GetComponentInChildren<LabChargeFeedback>();

        Assert.IsNotNull(feedback);
        Assert.IsFalse(feedback.Play());
        Assert.IsFalse(feedback.IsActive);
    }

    [Test]
    public void PausedFeedbackDoesNotPlay()
    {
        var feedback = lab.PlayerA.GetComponentInChildren<LabChargeFeedback>();
        lab.SetPaused(true);

        Assert.IsFalse(feedback.Play());
        Assert.IsFalse(feedback.IsActive);

        lab.SetPaused(false);
    }

    [Test]
    public void DeathStopsActiveFeedback()
    {
        var feedback = lab.PlayerA.GetComponentInChildren<LabChargeFeedback>();
        Assert.IsTrue(feedback.Play());

        lab.PlayerA.Kill();

        Assert.IsFalse(feedback.IsActive);
    }

    [Test]
    public void TeleportAnimationBlocksFeedback()
    {
        var feedback = lab.PlayerA.GetComponentInChildren<LabChargeFeedback>();
        var source = MakeTeleport(new Vector2(-4f, 6f));
        var target = MakeTeleport(new Vector2(4f, 6f));
        lab.MarkTeleportPoint(source);
        lab.MarkTeleportPoint(target);

        lab.EnterTeleportSelection(source);

        Assert.IsTrue(lab.TeleportBusy);
        Assert.IsFalse(feedback.Play());
        Assert.IsFalse(feedback.IsActive);
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
}
