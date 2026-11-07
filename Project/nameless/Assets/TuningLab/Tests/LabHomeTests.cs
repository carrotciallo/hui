using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>主页随机读数、悬停曲线、转场防重入与转场几何。</summary>
public sealed class LabHomeTests
{
    [Test]
    public void FirstPlaytestScaleFeedbackIsPinned()
    {
        Assert.AreEqual(64f, HomeScreenSpec.BackgroundSquareSize, 1e-5f);
        Assert.AreEqual(8f, HomeScreenSpec.BackgroundSquareStroke, 1e-5f);
        Assert.AreEqual(6, HomeScreenSpec.BackgroundSquareCountMin);
        Assert.AreEqual(13, HomeScreenSpec.BackgroundSquareCountMax);
        Assert.AreEqual(144f, HomeScreenSpec.ButtonSize, 1e-5f);
        Assert.AreEqual(60f, HomeScreenSpec.IconSize, 1e-5f);
        Assert.AreEqual(54f, HomeScreenSpec.ButtonGap, 1e-5f);
        Assert.AreEqual(2f, HomeScreenSpec.ArtScaleMin, 1e-5f);
        Assert.AreEqual(2.5f, HomeScreenSpec.ArtScaleMax, 1e-5f);
    }

    [Test]
    public void SecondPlaytestMotionFeedbackIsPinned()
    {
        Assert.AreEqual(80f, HomeScreenSpec.ArtMoveSpeedMin, 1e-5f);
        Assert.AreEqual(220f, HomeScreenSpec.ArtMoveSpeedMax, 1e-5f);
        Assert.AreEqual(0.625f, HomeScreenSpec.ArtBatchDelayMin, 1e-5f);
        Assert.AreEqual(2.5f, HomeScreenSpec.ArtBatchDelayMax, 1e-5f);
    }

    [Test]
    public void RandomRangesStayInsideHomeSpec()
    {
        var random = new System.Random(70);
        for (int i = 0; i < 2000; i++)
        {
            Assert.That(HomeScreenSpec.NextBackgroundSquareCount(random),
                Is.InRange(HomeScreenSpec.BackgroundSquareCountMin,
                    HomeScreenSpec.BackgroundSquareCountMax));
            Assert.That(HomeScreenSpec.NextBatchCount(random),
                Is.InRange(HomeScreenSpec.ArtBatchCountMin, HomeScreenSpec.ArtBatchCountMax));
            Assert.That(HomeScreenSpec.NextBatchDelay(random),
                Is.InRange(HomeScreenSpec.ArtBatchDelayMin, HomeScreenSpec.ArtBatchDelayMax));
            Assert.That(HomeScreenSpec.NextArtScale(random),
                Is.InRange(HomeScreenSpec.ArtScaleMin, HomeScreenSpec.ArtScaleMax));
            Assert.That(HomeScreenSpec.NextArtMoveSpeed(random),
                Is.InRange(HomeScreenSpec.ArtMoveSpeedMin, HomeScreenSpec.ArtMoveSpeedMax));
            Assert.That(HomeScreenSpec.NextArtRotationSpeed(random),
                Is.InRange(HomeScreenSpec.ArtRotationSpeedMin,
                    HomeScreenSpec.ArtRotationSpeedMax));
            Assert.That(HomeScreenSpec.NextArtStagger(random),
                Is.InRange(HomeScreenSpec.ArtStaggerMin, HomeScreenSpec.ArtStaggerMax));
        }
    }

    [Test]
    public void BreathCycleReturnsToBaseScale()
    {
        Assert.AreEqual(1f, HomeScreenSpec.ButtonBreathScale(0f), 1e-5f);
        Assert.AreEqual(HomeScreenSpec.ButtonBreathMaxScale,
            HomeScreenSpec.ButtonBreathScale(HomeScreenSpec.ButtonBreathDuration * 0.5f),
            1e-5f);
        Assert.AreEqual(1f,
            HomeScreenSpec.ButtonBreathScale(HomeScreenSpec.ButtonBreathDuration),
            1e-5f);
    }

    [Test]
    public void FlipStopsAfterThreeRoundTripsThenHolds()
    {
        Assert.AreEqual(1f, HomeScreenSpec.IconFlipScale(0f), 1e-5f);
        Assert.AreEqual(-1f,
            HomeScreenSpec.IconFlipScale(HomeScreenSpec.IconFlipDuration / 6f),
            1e-5f);
        Assert.AreEqual(1f,
            HomeScreenSpec.IconFlipScale(HomeScreenSpec.IconFlipDuration - 1e-4f),
            1e-4f);
        Assert.AreEqual(1f,
            HomeScreenSpec.IconFlipScale(HomeScreenSpec.IconFlipDuration + 0.1f),
            1e-5f);
    }

    [Test]
    public void NavigationAndSettingsAreMutuallyExclusive()
    {
        Assert.IsTrue(HomeScreenSpec.CanOpenSettings(false));
        Assert.IsFalse(HomeScreenSpec.CanOpenSettings(true));
        Assert.IsTrue(HomeScreenSpec.CanStartNavigation(false, false));
        Assert.IsFalse(HomeScreenSpec.CanStartNavigation(true, false));
        Assert.IsFalse(HomeScreenSpec.CanStartNavigation(false, true));
    }

    [Test]
    public void AllHomeArtAndButtonIconsAreAvailableAsTextures()
    {
        Texture2D[] mapArt = Resources.LoadAll<Texture2D>("HuiArt");
        Texture2D[] uiArt = Resources.LoadAll<Texture2D>("SettingsIcons");

        Assert.GreaterOrEqual(mapArt.Length, 23, "HuiArt 下的地图素材必须全部进入漂浮池");
        Assert.GreaterOrEqual(uiArt.Length, 12, "SettingsIcons 下的 UI 素材必须全部进入漂浮池");
        Assert.IsNotNull(Resources.Load<Texture2D>("SettingsIcons/KeyBindings"));
        Assert.IsNotNull(Resources.Load<Texture2D>("SettingsIcons/Resolution"));
        Assert.IsNotNull(Resources.Load<Texture2D>("SettingsIcons/Settings"));
    }

    [Test]
    public void TransitionApertureStaysCenteredAndEndsAsWideLine()
    {
        const float width = 1920f;
        const float height = 1080f;

        Rect start = HomeScreenSpec.TransitionAperture(0f, width, height);
        Rect collapsed = HomeScreenSpec.TransitionAperture(
            HomeScreenSpec.TransitionCollapseDuration, width, height);
        Rect held = HomeScreenSpec.TransitionAperture(
            HomeScreenSpec.TransitionCollapseDuration + HomeScreenSpec.TransitionHoldDuration,
            width, height);
        Rect end = HomeScreenSpec.TransitionAperture(
            HomeScreenSpec.TransitionDuration, width, height);

        Assert.AreEqual(new Rect(0f, 0f, width, height), start);
        Assert.AreEqual(width * 0.5f, collapsed.center.x, 1e-4f);
        Assert.AreEqual(height * 0.5f, collapsed.center.y, 1e-4f);
        Assert.AreEqual(collapsed, held);
        Assert.Greater(end.width, collapsed.width * 9.9f);
        Assert.AreEqual(collapsed.height, end.height, 1e-5f);
    }
}
