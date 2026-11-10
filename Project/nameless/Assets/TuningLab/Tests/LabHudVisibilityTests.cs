using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 调试台（LabHud）的显示开关。
///
/// 发布包里左右两个框（左：手感调试台面板，右：读数/标尺）不该出现，
/// 但它们是运行时 add 上去的，原来没有任何开关能关。这里钉死：
/// 默认跟着 Debug.isDebugBuild 走，正式构建里是关的。
/// </summary>
public sealed class LabHudVisibilityTests
{
    GameObject go;

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
    }

    LabHud Make()
    {
        go = new GameObject("Hud");
        return go.AddComponent<LabHud>();
    }

    [Test]
    public void DefaultsToDebugBuildFlag()
    {
        // AddComponent 会同步跑 Awake，所以这里读到的就是 Awake 里赋的值。
        var hud = Make();
        Assert.AreEqual(Debug.isDebugBuild, hud.showTuningPanel,
            "默认值要跟 Debug.isDebugBuild 一致：编辑器/dev build 显示，正式包隐藏");
    }

    [Test]
    public void ReleaseBuild_HidesTuningPanel()
    {
        var hud = Make();
        // 模拟正式构建：isDebugBuild 为 false。
        hud.showTuningPanel = false;

        Assert.IsFalse(hud.showTuningPanel, "正式包里不该画调试台");
    }

    [Test]
    public void CanBeForcedOn()
    {
        var hud = Make();
        hud.showTuningPanel = true;

        Assert.IsTrue(hud.showTuningPanel, "需要调参时可以手动打开");
    }
}
