using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class FormTests
{
    const int Ground = 6;
    const int Player = 7;
    const int Hazard = 8;
    const int Interactable = 9;
    const float SpawnY = GameConstants.PlayerSize * 0.5f + 0.02f;

    GameObject root;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("FormTestRoot");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) Object.Destroy(root);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator FormKeysSwitchOnlyA()
    {
        var a = CreatePlayer("A", new Vector2(0f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        var b = CreatePlayer("B", new Vector2(1f, SpawnY), PlayerMotor.Scheme.Arrows);
        var form = a.GetComponent<FormController>();

        yield return PressKeys(Key.Digit2);
        Assert.AreEqual(AForm.Cannon, form.CurrentForm, "数字键 2 应切到大炮");

        yield return PressKeys(Key.Digit5);
        Assert.AreEqual(AForm.Raft, form.CurrentForm, "数字键 5 应切到木筏");

        Assert.IsNull(b.GetComponent<FormController>(), "B 不应有形态系统");
    }

    [UnityTest]
    public IEnumerator CannonAbsorbsRiderAndLaunchesOneTierHigher()
    {
        CreateFloor(-5f, 30f);
        var a = CreatePlayer("A", new Vector2(5f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        var b = CreatePlayer("B", new Vector2(5f, SpawnY + GameConstants.PlayerSize + 0.02f),
                             PlayerMotor.Scheme.Arrows);

        var form = a.GetComponent<FormController>();
        form.SetForm(AForm.Cannon);
        b.ForceTier(SpeedTier.V1, 5f);

        yield return Physics(8);
        Assert.AreEqual(a, b.RideTarget, "B 应站在 A 上");

        yield return PressKeys(Key.DownArrow);
        yield return null;

        Assert.AreEqual(b, form.Carried, "B 按「下」应进入大炮");
        Assert.IsTrue(b.IsSuspended, "进仓后 B 的物理应暂停");

        yield return PressKeys(Key.Period);
        yield return Physics(2);

        Assert.IsNull(form.Carried, "射出后不应还携带 B");
        Assert.IsFalse(b.IsSuspended, "射出后 B 应恢复物理");
        Assert.AreEqual(SpeedTier.V2, b.Tier, "进入 v1 + 1 应射出 v2");
        Assert.Greater(b.Velocity.x, 5f, "应按 A 的朝向向右射出");
    }

    [UnityTest]
    public IEnumerator FormChangeEjectsCarriedRider()
    {
        CreateFloor(-5f, 30f);
        var a = CreatePlayer("A", new Vector2(5f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        var b = CreatePlayer("B", new Vector2(5f, SpawnY + GameConstants.PlayerSize + 0.02f),
                             PlayerMotor.Scheme.Arrows);

        var form = a.GetComponent<FormController>();
        form.SetForm(AForm.Cannon);
        yield return Physics(8);

        yield return PressKeys(Key.DownArrow);
        yield return null;
        Assert.AreEqual(b, form.Carried);

        form.SetForm(AForm.Normal);
        yield return Physics(2);

        Assert.IsNull(form.Carried, "切形态应把 B 弹出");
        Assert.IsFalse(b.IsSuspended);
        Assert.Greater(b.transform.position.y, a.transform.position.y, "B 应被弹到 A 上方");
    }

    [UnityTest]
    public IEnumerator RiderMovesWithA()
    {
        CreateFloor(-5f, 40f);
        var a = CreatePlayer("A", new Vector2(2f, SpawnY), PlayerMotor.Scheme.WASD);
        var b = CreatePlayer("B", new Vector2(2f, SpawnY + GameConstants.PlayerSize + 0.02f),
                             PlayerMotor.Scheme.Arrows);

        yield return Physics(8);

        yield return HoldKeys(new[] { Key.D }, 40);

        Assert.Greater(a.transform.position.x, 3.5f, "A 应向右移动");
        Assert.That(Mathf.Abs(b.transform.position.x - a.transform.position.x), Is.LessThan(0.8f),
                    "B 应跟着 A 一起走");
        Assert.Greater(b.transform.position.y, a.transform.position.y, "B 应仍在 A 上方");
    }

    [UnityTest]
    public IEnumerator ConverterTurnsHorizontalHitIntoUp()
    {
        CreateFloor(-5f, 30f);
        var a = CreatePlayer("A", new Vector2(8f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        // 反弹速度会按地面加速度衰减，起点太远会在撞到转换区之前停下
        var b = CreatePlayer("B", new Vector2(5.6f, SpawnY), PlayerMotor.Scheme.Arrows);
        a.GetComponent<FormController>().SetForm(AForm.Converter);

        Launch(b, SpeedTier.V2, Vector2.right);
        yield return WaitUntil(() => b.Velocity.y > 1f, 2f);

        Assert.Greater(b.Velocity.y, 1f, "横向撞转换器应被转成向上");
        Assert.AreEqual(SpeedTier.V2, b.Tier, "转换不应改变挡位");
        Assert.Less(b.transform.position.x, 8f, "应在撞到 A 实体前完成转换");
    }

    [UnityTest]
    public IEnumerator ConverterIgnoresSlowContact()
    {
        CreateFloor(-5f, 30f);
        var a = CreatePlayer("A", new Vector2(8f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        var b = CreatePlayer("B", new Vector2(7.1f, SpawnY), PlayerMotor.Scheme.Arrows);
        a.GetComponent<FormController>().SetForm(AForm.Converter);

        yield return Physics(10);

        Assert.Less(b.Velocity.y, 0.5f, "v0 接触不应被转换");
        Assert.AreEqual(SpeedTier.V0, b.Tier);
    }

    [UnityTest]
    public IEnumerator ConverterTurnsVerticalHitIntoHorizontal()
    {
        CreateFloor(-5f, 30f);
        var a = CreatePlayer("A", new Vector2(8f, 3f), PlayerMotor.Scheme.WASD, withForm: true);
        a.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        a.GetComponent<FormController>().SetForm(AForm.Converter);

        var b = CreatePlayer("B", new Vector2(8f, SpawnY), PlayerMotor.Scheme.Arrows);

        Launch(b, SpeedTier.V2, Vector2.up);
        yield return WaitUntil(() => Mathf.Abs(b.Velocity.x) > 1f, 2f);

        Assert.Greater(Mathf.Abs(b.Velocity.x), 1f, "竖直撞转换器应被转成水平");
        Assert.AreEqual(SpeedTier.V2, b.Tier);
    }

    [UnityTest]
    public IEnumerator ClimbFormRisesOnRopeAndDetaches()
    {
        CreateFloor(-5f, 30f);
        CreateRope(new Vector2(10f, 1.5f));
        var a = CreatePlayer("A", new Vector2(9.3f, SpawnY), PlayerMotor.Scheme.WASD, withForm: true);
        var form = a.GetComponent<FormController>();
        form.SetForm(AForm.Climb);

        yield return HoldKeys(new[] { Key.W }, 40);

        Assert.IsTrue(a.IsClimbing, "在绳边按 W 应附着");
        Assert.Greater(a.transform.position.y, 1f, "附着后应向上爬");

        yield return HoldKeys(new[] { Key.D }, 3);
        Assert.IsFalse(a.IsClimbing, "按左右应脱离绳子");
    }

    [UnityTest]
    public IEnumerator RaftFloatsAndProtectsRiderWhileNormalDies()
    {
        CreateLava(new Vector2(0f, 1f), new Vector2(8f, 2f));

        var a = CreatePlayer("A", new Vector2(0f, 3f), PlayerMotor.Scheme.WASD,
                             withForm: true, withLife: true);
        var lifeA = a.GetComponent<PlayerLife>();
        var formA = a.GetComponent<FormController>();

        formA.SetForm(AForm.Raft);
        a.Teleport(new Vector2(0f, 2.5f));

        yield return Physics(12);

        Assert.IsTrue(a.IsFloating, "木筏应漂浮");
        Assert.IsTrue(lifeA.IsAlive, "木筏形态接触岩浆不应死");
        Assert.That(a.transform.position.y, Is.EqualTo(2.4f).Within(0.35f), "应浮在液面");

        var b = CreatePlayer("B", new Vector2(2f, 3f), PlayerMotor.Scheme.Arrows, withLife: true);
        var lifeB = b.GetComponent<PlayerLife>();

        b.Teleport(new Vector2(2f, 2.5f));
        yield return Physics(2);

        Assert.IsFalse(lifeB.IsAlive, "普通玩家接触岩浆应死");

        formA.SetForm(AForm.Normal);
        yield return Physics(3);

        Assert.IsFalse(lifeA.IsAlive, "在岩浆里切出木筏形态应死");
    }

    [UnityTest]
    public IEnumerator EmptyFormPassesWallCollectsKeyAndDies()
    {
        CreateFloor(-5f, 30f);
        var wall = CreateWall(new Vector2(5f, 2f), new Vector2(1f, 4f));
        CreateKey(new Vector2(6.5f, 0.6f));

        var a = CreatePlayer("A", new Vector2(2f, SpawnY), PlayerMotor.Scheme.WASD,
                             withForm: true, withLife: true);
        var life = a.GetComponent<PlayerLife>();
        var form = a.GetComponent<FormController>();
        var carrier = a.gameObject.AddComponent<KeyCarrier>();

        form.SetForm(AForm.Empty);
        yield return Physics(2);

        yield return HoldKeys(new[] { Key.D }, 70);

        Assert.Greater(a.transform.position.x, 5.5f, "空形态应穿过墙");
        Assert.IsTrue(wall.GetComponent<Collider2D>().enabled, "穿过后墙仍然存在");
        Assert.IsTrue(carrier.HasKey, "应能拿到墙后的钥匙");

        yield return WaitUntil(() => !life.IsAlive, 2.5f);
        Assert.IsFalse(life.IsAlive, "空形态超时应死亡");

        yield return WaitUntil(() => life.IsAlive, 2f);
        Assert.IsTrue(life.IsAlive, "应在出生点复活");
        Assert.AreEqual(AForm.Normal, form.CurrentForm, "复活后应回到普通形态");
    }

    // 工具 -------------------------------------------------------------

    static void Launch(PlayerMotor motor, SpeedTier tier, Vector2 direction)
    {
        motor.ReceiveBounce(direction, tier, 2f);
    }

    /// 按键边沿由 wasPressedThisFrame 判定，必须让下一帧的动态输入更新来处理事件，
    /// 手动 InputSystem.Update() 会把边沿记在上一帧，MonoBehaviour.Update 里读不到。
    static IEnumerator PressKeys(params Key[] keys)
    {
        var kb = Keyboard.current;
        if (kb == null) yield break;

        InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        yield return null;

        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
    }

    static IEnumerator HoldKeys(Key[] keys, int steps)
    {
        var kb = Keyboard.current;
        if (kb == null) yield break;

        InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        yield return null;

        yield return Physics(steps);

        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
    }

    static IEnumerator Physics(int steps)
    {
        for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    PlayerMotor CreatePlayer(string name, Vector2 position,
                             PlayerMotor.Scheme scheme,
                             bool withForm = false, bool withLife = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.layer = Player;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * GameConstants.PlayerSize;

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<SpeedTierProvider>();
        var motor = go.AddComponent<PlayerMotor>();
        motor.SetScheme(scheme);

        if (withForm) go.AddComponent<FormController>();
        if (withLife) go.AddComponent<PlayerLife>();

        return motor;
    }

    GameObject CreateFloor(float xMin, float xMax, float top = 0f)
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.layer = Ground;
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, top - 1f, 0f);
        go.transform.localScale = new Vector3(xMax - xMin, 2f, 1f);

        go.AddComponent<BoxCollider2D>();
        return go;
    }

    void CreateRope(Vector2 center)
    {
        var go = new GameObject("VerticalRope");
        go.transform.SetParent(root.transform, false);
        go.layer = Interactable;
        go.transform.position = center;
        go.transform.localScale = new Vector3(0.5f, 3f, 1f);

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<VerticalRope>();
    }

    GameObject CreateWall(Vector2 center, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(root.transform, false);
        go.layer = Ground;
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>();
        return go;
    }

    void CreateKey(Vector2 position)
    {
        var go = new GameObject("KeyPickup");
        go.transform.SetParent(root.transform, false);
        go.layer = Interactable;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<KeyPickup>();
    }

    void CreateLava(Vector2 center, Vector2 size)
    {
        var go = new GameObject("Lava");
        go.transform.SetParent(root.transform, false);
        go.layer = Hazard;
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<Lava>();
    }
}
