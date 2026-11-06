using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 灵体态（四条边全碎）的移动规格：不受任何力，速度 = 输入方向 × 移动速度，四向和斜向同速。
/// 冲刺禁用、时间耗尽、吐钥匙等规则见 LabForgeTests / LabKeyDoorTests / LabLevelPlayCheck。
/// 按键接线（WASD → moveX/vertInput）走和普通移动同一条 ReadInput，不在这里重复测；
/// 无头编辑器收不到合成按键，真实按键只能进编辑器手动验收。
/// </summary>
public sealed class LabSpiritTests
{
    const float StartY = 6f;

    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("SpiritRoot");
        root.transform.SetParent(labGo.transform, false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpiritIgnoresAllForcesAndStopsWithoutInput()
    {
        var a = MakeSpirit(out var rb);

        // 模拟残留外力：灵体的移动规则必须在下一个物理步把它覆盖掉。
        a.SetVelocity(new Vector2(12f, -9f));
        yield return StepFixed(1);
        float settledY = a.transform.position.y;

        yield return StepFixed(2);

        Assert.IsTrue(a.IsIntangible);
        Assert.AreEqual(0f, rb.gravityScale, 0.001f, "灵体没有重力");
        Assert.AreEqual(0f, a.Speed, 0.01f, "没有输入时速度直接归零，不受任何力");
        Assert.AreEqual(settledY, a.transform.position.y, 0.001f, "不会被重力或残留速度带走");
    }

    [Test]
    public void SpiritVelocityUsesWalkSpeedInEveryDirection()
    {
        Vector2[] directions =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f), new Vector2(-1f, 1f),
            new Vector2(1f, -1f), new Vector2(-1f, -1f)
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 velocity = LabPlayer.SpiritVelocity(directions[i], 8f);
            Vector2 expected = directions[i].normalized * 8f;
            Assert.AreEqual(8f, velocity.magnitude, 0.0001f,
                "方向 " + directions[i] + " 的速度应等于移动速度");
            Assert.AreEqual(expected.x, velocity.x, 0.0001f,
                "方向 " + directions[i] + " 的 x 分量不对");
            Assert.AreEqual(expected.y, velocity.y, 0.0001f,
                "方向 " + directions[i] + " 的 y 分量不对");
        }
    }

    [Test]
    public void SpiritVelocityIsZeroWithoutInput()
    {
        Assert.AreEqual(Vector2.zero, LabPlayer.SpiritVelocity(Vector2.zero, 8f));
    }

    [UnityTest]
    public IEnumerator LeavingSpiritRestoresGravity()
    {
        var a = MakeSpirit(out var rb);
        float spiritY = a.transform.position.y;

        a.ResetEdges();

        Assert.IsFalse(a.IsIntangible);
        Assert.Greater(rb.gravityScale, 0f, "离开灵体立刻交还重力");

        yield return StepFixed(10);

        Assert.Less(a.transform.position.y, spiritY - 0.05f, "离开灵体后恢复下落");
    }

    // 工具 -------------------------------------------------------------

    LabPlayer MakeSpirit(out Rigidbody2D rb)
    {
        var go = new GameObject("A_Box");
        go.transform.SetParent(root.transform, false);

        float half = TuningLab.A_Pixels / (2f * TuningLab.CellPixels);
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(half * 2f, half * 2f);
        bc.sharedMaterial = slick;

        rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = lab.GravityScale;

        var p = go.AddComponent<LabPlayer>();
        p.lab = lab;
        p.pixels = TuningLab.A_Pixels;
        p.canBeRedHot = true;
        p.scheme = LabPlayer.Scheme.P1;
        p.Teleport(new Vector2(0f, StartY));

        foreach (LabPlayer.Edge e in System.Enum.GetValues(typeof(LabPlayer.Edge)))
        {
            p.StrikeEdge(e, true);
            p.StrikeEdge(e, false);
        }

        Assert.IsTrue(p.IsIntangible, "用例前提：四条边全碎进入灵体");
        return p;
    }

    /// <summary>走 n 个真正的物理步：batchmode 里 yield return null 可能一步都不走。</summary>
    static IEnumerator StepFixed(int n)
    {
        for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
    }
}
