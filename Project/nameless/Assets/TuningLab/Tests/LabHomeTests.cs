using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>主页随机读数、悬停曲线、转场防重入与转场几何。</summary>
public sealed class LabHomeTests
{
    [Test]
    public void FirstPlaytestScaleFeedbackIsPinned()
    {
        Assert.AreEqual(64f, HomeScreenSpec.BorderSnakeCellSize, 1e-5f);
        Assert.AreEqual(8f, HomeScreenSpec.BorderSnakeCellStroke, 1e-5f);
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
        Assert.AreEqual(0.9f, HomeScreenSpec.ArtBatchDelayMin, 1e-5f);
        Assert.AreEqual(3.6f, HomeScreenSpec.ArtBatchDelayMax, 1e-5f);
    }

    [Test]
    public void ThirdPlaytestBorderSnakeSpecIsPinned()
    {
        Assert.AreEqual(2, HomeScreenSpec.BorderSnakeCount);
        Assert.AreEqual(3, HomeScreenSpec.BorderSnakeLengthMin);
        Assert.AreEqual(11, HomeScreenSpec.BorderSnakeLengthMax);
        Assert.AreEqual(0.15f, HomeScreenSpec.BorderSnakeStepSeconds, 1e-5f);
        Assert.AreEqual(0.10f, HomeScreenSpec.BorderSnakeFadeSeconds, 1e-5f);
        Assert.AreEqual(1f / 30f, HomeScreenSpec.BorderSnakeTurnChance, 1e-6f);
        Assert.AreEqual(15f, HomeScreenSpec.BorderSnakeLifetimeMin, 1e-5f);
        Assert.AreEqual(25f, HomeScreenSpec.BorderSnakeLifetimeMax, 1e-5f);
    }

    [Test]
    public void RandomRangesStayInsideHomeSpec()
    {
        var random = new System.Random(70);
        for (int i = 0; i < 2000; i++)
        {
            Assert.That(HomeScreenSpec.NextBorderSnakeLength(random),
                Is.InRange(HomeScreenSpec.BorderSnakeLengthMin,
                    HomeScreenSpec.BorderSnakeLengthMax));
            Assert.That(HomeScreenSpec.NextBorderSnakeLifetime(random),
                Is.InRange(HomeScreenSpec.BorderSnakeLifetimeMin,
                    HomeScreenSpec.BorderSnakeLifetimeMax));
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
    public void BorderSnakesStartOnEveryEdgeAndMoveInward()
    {
        var random = new System.Random(70);
        const int columns = 10;
        const int rows = 8;

        foreach (HomeSnakeEdge edge in Enum.GetValues(typeof(HomeSnakeEdge)))
        {
            Vector2Int start = HomeScreenSpec.SnakeStartCell(edge, columns, rows, random);
            HomeSnakeDirection direction = HomeScreenSpec.SnakeStartDirection(edge);
            Vector2Int next = start + HomeScreenSpec.SnakeDirectionDelta(direction);
            Assert.IsTrue(next.x >= 0 && next.x < columns, edge.ToString());
            Assert.IsTrue(next.y >= 0 && next.y < rows, edge.ToString());
            Assert.AreNotEqual(start, next, edge.ToString());
        }
    }

    [Test]
    public void BorderSnakeTurnsOnlyNinetyDegreesAtLowFrequency()
    {
        var random = new System.Random(70);
        HomeSnakeDirection direction = HomeSnakeDirection.Right;
        int turns = 0;
        const int steps = 30000;

        for (int i = 0; i < steps; i++)
        {
            HomeSnakeDirection previous = direction;
            direction = HomeScreenSpec.NextSnakeDirection(direction, random);
            if (direction == previous) continue;
            int delta = ((int)direction - (int)previous + 4) % 4;
            Assert.IsTrue(delta == 1 || delta == 3);
            turns++;
        }

        Assert.That(turns, Is.InRange(850, 1150));
    }

    [Test]
    public void BorderSnakeCellsFadeInAndOutInOneHundredMilliseconds()
    {
        const float fade = HomeScreenSpec.BorderSnakeFadeSeconds;
        Assert.AreEqual(0f, HomeScreenSpec.ActiveSnakeAlpha(0f), 1e-5f);
        Assert.AreEqual(0.5f, HomeScreenSpec.ActiveSnakeAlpha(fade * 0.5f), 1e-5f);
        Assert.AreEqual(1f, HomeScreenSpec.ActiveSnakeAlpha(fade), 1e-5f);
        Assert.AreEqual(1f, HomeScreenSpec.FadingSnakeAlpha(0f, 1f), 1e-5f);
        Assert.AreEqual(0f, HomeScreenSpec.FadingSnakeAlpha(fade, 1f), 1e-5f);
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

    // ---- 场景名解析 ---------------------------------------------------

    /// <summary>
    /// 打包后 Build Settings 里存的是**完整路径**，而主页和存档里用的是短名
    /// （"REAL FINAL MAP"）。直接拿短名去 LoadScene 在打包版本里加载不到，
    /// 表现就是"点开始游戏没反应"。这里钉死：短名、短名+扩展名、完整路径三种写法
    /// 都要能解析到同一个场景。
    /// </summary>
    [Test]
    public void ResolveSceneNameAcceptsShortAndFullForms()
    {
        const string wanted = "REAL FINAL MAP";
        string resolved = HomeScreen.ResolveSceneName(wanted);

        Assert.IsNotNull(resolved, "短名必须能解析出场景路径（打包版本靠这个进关）");
        Assert.IsTrue(resolved.EndsWith(wanted + ".unity"),
            "解析结果要指向 REAL FINAL MAP.unity，实际：" + resolved);
        Assert.IsTrue(resolved.StartsWith("Assets/"), "应该是 Assets 下的相对路径，实际：" + resolved);
    }

    [Test]
    public void ResolveSceneNameRejectsUnknownScene()
    {
        Assert.IsNull(HomeScreen.ResolveSceneName("不存在的场景名"),
            "没有的场景要返回 null，让调用方走报错分支");
        Assert.IsNull(HomeScreen.ResolveSceneName(""), "空名字返回 null");
        Assert.IsNull(HomeScreen.ResolveSceneName(null), "null 返回 null");
    }

    [Test]
    public void HomeSceneNameResolvesToo()
    {
        string resolved = HomeScreen.ResolveSceneName("Home");
        Assert.IsNotNull(resolved, "主界面自己也要能解析");
        Assert.IsTrue(resolved.EndsWith("Home.unity"));
    }
}
