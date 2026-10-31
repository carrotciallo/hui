using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class MaterialTests
{
    const int Ground = 6;
    const int Player = 7;
    const int Hazard = 8;
    const int Interactable = 9;
    const float SpawnY = GameConstants.PlayerSize * 0.5f + 0.02f;

    GameObject root;
    RunStateHost host;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("MaterialTestRoot");

        var hostGo = new GameObject("RunStateHost");
        hostGo.transform.SetParent(root.transform, false);
        host = hostGo.AddComponent<RunStateHost>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) Object.Destroy(root);
        yield return null;
        yield return null;
    }

    // 竖绳 -------------------------------------------------------------

    [UnityTest]
    public IEnumerator VerticalRopeLetsV0PassWithoutEffect()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        CreateTrigger<VerticalRope>("Rope", new Vector2(0.6f, 1.5f), new Vector2(0.5f, 3f));

        Launch(motor, SpeedTier.V0, Vector2.right);
        yield return Physics(2);

        Assert.AreEqual(SpeedTier.V0, motor.Tier);
        Assert.GreaterOrEqual(motor.Velocity.x, 0f, "低挡位不应被弹回");
    }

    [UnityTest]
    public IEnumerator VerticalRopeLetsV1PassThrough()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        CreateTrigger<VerticalRope>("Rope", new Vector2(0.6f, 1.5f), new Vector2(0.5f, 3f));

        Launch(motor, SpeedTier.V1, Vector2.right);
        yield return Physics(3);

        Assert.AreEqual(SpeedTier.V1, motor.Tier);
        Assert.Greater(motor.Velocity.x, 0f);
        Assert.Greater(motor.transform.position.x, 0.55f, "v1 应直接穿过");
    }

    [UnityTest]
    public IEnumerator VerticalRopeLetsV2PassThrough()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        CreateTrigger<VerticalRope>("Rope", new Vector2(0.6f, 1.5f), new Vector2(0.5f, 3f));

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return Physics(3);

        Assert.AreEqual(SpeedTier.V2, motor.Tier);
        Assert.Greater(motor.Velocity.x, 0f);
        Assert.Greater(motor.transform.position.x, 0.6f, "v2 应直接穿过");
    }

    [UnityTest]
    public IEnumerator VerticalRopeBouncesV3UpOneTier()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        CreateTrigger<VerticalRope>("Rope", new Vector2(2f, 1.5f), new Vector2(0.5f, 3f));

        Launch(motor, SpeedTier.V3, Vector2.right);
        yield return WaitUntil(() => motor.Tier == SpeedTier.V4, 2f);

        Assert.AreEqual(SpeedTier.V4, motor.Tier, "v3 撞竖绳应升到 v4");
        Assert.Less(motor.Velocity.x, 0f, "反弹方向应反向");
    }

    [UnityTest]
    public IEnumerator VerticalRopeKeepsV4AtV4()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        CreateTrigger<VerticalRope>("Rope", new Vector2(2f, 1.5f), new Vector2(0.5f, 3f));

        Launch(motor, SpeedTier.V4, Vector2.right);
        yield return WaitUntil(() => motor.Velocity.x < -1f, 2f);

        Assert.IsTrue(motor.Velocity.x < 0f, "v4 撞竖绳应被弹回");
        Assert.AreEqual(SpeedTier.V4, motor.Tier, "v4 是封顶挡位");
    }

    // 地面绳 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator GroundRopeIsStandableAndPassableFromBelow()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(2f, 0.5f));
        CreateGroundRope(new Vector2(2f, 2f));

        Launch(motor, SpeedTier.V1, Vector2.up);
        yield return WaitUntil(() => motor.transform.position.y > 2.4f, 1.5f);
        Assert.Greater(motor.transform.position.y, 2.4f, "从下往上应穿过地绳");

        yield return WaitUntil(() => motor.Grounded && motor.transform.position.y > 2.1f, 2f);
        Assert.IsTrue(motor.Grounded, "落回后应站住");
        Assert.Greater(motor.transform.position.y, 2.1f);
    }

    [UnityTest]
    public IEnumerator GroundRopeDropsThroughForV2()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(2f, 3f));
        CreateGroundRope(new Vector2(2f, 2f));

        Launch(motor, SpeedTier.V2, Vector2.down);
        yield return Physics(60);

        Assert.Less(motor.transform.position.y, 1.5f, "v2 从上方接触应穿下去");
    }

    [UnityTest]
    public IEnumerator GroundRopeBouncesV3UpwardOneTier()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(2f, 3f));
        CreateGroundRope(new Vector2(2f, 2f));

        Launch(motor, SpeedTier.V3, Vector2.down);
        yield return WaitUntil(() => motor.Tier == SpeedTier.V4, 2f);

        Assert.AreEqual(SpeedTier.V4, motor.Tier, "v3 踩地绳应升到 v4");
        Assert.Greater(motor.Velocity.y, 0f, "应向上反弹");
    }

    // 冰 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator IceSlidesFartherThanNormalGround()
    {
        CreateFloor(-5f, 10f);
        CreateFloor(20f, 60f, ice: true);

        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return Physics(90);
        float groundDistance = motor.transform.position.x;

        motor.Teleport(new Vector2(21f, SpawnY));
        yield return Physics(2);

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return Physics(90);
        float iceDistance = motor.transform.position.x - 21f;

        Assert.Greater(iceDistance, groundDistance + 3f,
                       $"冰面滑行 {iceDistance:F2} 应明显长于普通地面 {groundDistance:F2}");
    }

    [UnityTest]
    public IEnumerator IcePanelBreaksAtV2ButNotV1()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        var panel = CreateSolid<IcePanel>("Panel", new Vector2(2f, 0.5f), new Vector2(1f, 1f));
        panel.Configure(GameConstants.IceBreakTier);

        Launch(motor, SpeedTier.V1, Vector2.right);
        yield return Physics(40);
        Assert.IsTrue(panel != null, "v1 不应撞碎冰面板");

        motor.Teleport(new Vector2(0f, SpawnY));
        yield return Physics(2);

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return WaitUntil(() => panel == null, 2f);
        Assert.IsTrue(panel == null, "v2 应撞碎冰面板");
    }

    // 可破坏墙 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator BreakableWallBreaksAtV2ButNotV1()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        var wall = CreateSolid<BreakableWall>("Wall", new Vector2(2f, 1.5f), new Vector2(1f, 3f));
        wall.Configure(GameConstants.WallBreakTier);

        Launch(motor, SpeedTier.V1, Vector2.right);
        yield return Physics(40);
        Assert.IsTrue(wall != null, "v1 不应撞碎墙");

        motor.Teleport(new Vector2(0f, SpawnY));
        yield return Physics(2);

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return WaitUntil(() => wall == null, 2f);
        Assert.IsTrue(wall == null, "v2 应撞碎墙");
    }

    // 钥匙与门 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator PlayerPicksUpKey()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        var carrier = motor.gameObject.AddComponent<KeyCarrier>();
        var key = CreateTrigger<KeyPickup>("Key", new Vector2(1f, 0.8f), new Vector2(0.5f, 0.5f));

        motor.Teleport(new Vector2(1f, SpawnY));
        yield return Physics(3);

        Assert.IsTrue(carrier.HasKey, "碰到钥匙应进入携带状态");
        Assert.IsTrue(key == null, "钥匙应被拿走");
        Assert.AreEqual(1, host.State.Keys);
    }

    [UnityTest]
    public IEnumerator GateBlocksWithoutKeyAndOpensWithOne()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        var carrier = motor.gameObject.AddComponent<KeyCarrier>();
        var gate = CreateSolid<KeyGate>("Gate", new Vector2(2f, 1.5f), new Vector2(1f, 3f));

        Launch(motor, SpeedTier.V1, Vector2.right);
        yield return Physics(30);
        Assert.IsFalse(gate.IsOpen, "无钥匙应被挡住");
        Assert.Less(motor.transform.position.x, 1.6f);

        carrier.PickUp();
        motor.Teleport(new Vector2(0f, SpawnY));
        yield return Physics(2);

        Launch(motor, SpeedTier.V1, Vector2.right);
        yield return WaitUntil(() => gate.IsOpen, 2f);

        Assert.IsTrue(gate.IsOpen, "带钥匙接触应开门");
        Assert.IsFalse(carrier.HasKey, "开门应消耗钥匙");
        Assert.IsTrue(host.State.IsGateOpen(gate.Id), "开门状态应写入 RunState");
        Assert.AreEqual(0, host.State.Keys);
    }

    [UnityTest]
    public IEnumerator HeavyPlayerBreaksGateAtV2()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY));
        var gate = CreateSolid<KeyGate>("Gate", new Vector2(2f, 1.5f), new Vector2(1f, 3f));

        host.State.SetBMass(GameConstants.GateBreakMass);

        Launch(motor, SpeedTier.V2, Vector2.right);
        yield return WaitUntil(() => gate.IsOpen, 2f);

        Assert.IsTrue(gate.IsOpen, "质量达标时 v2 应撞穿过关门");
        Assert.AreEqual(0, host.State.Keys, "破门不消耗钥匙");
    }

    // 刺 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator SpikesKillOnContact()
    {
        CreateFloor(-5f, 10f);
        var motor = CreatePlayer("P1", new Vector2(0f, SpawnY), withLife: true);
        var life = motor.GetComponent<PlayerLife>();
        CreateTrigger<KillZone>("Spikes", new Vector2(1f, 0.2f), new Vector2(1f, 0.4f), Hazard);

        motor.Teleport(new Vector2(1f, SpawnY));
        yield return Physics(3);

        Assert.IsFalse(life.IsAlive, "踩刺应死亡");
    }

    // 传送门 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator PortalLightsOnContactAndCounts()
    {
        CreateFloor(-5f, 10f);
        CreatePlayer("P1", new Vector2(2f, SpawnY));
        var portal = CreateTrigger<Portal>("Portal", new Vector2(2f, 1.5f), new Vector2(2f, 3f));

        yield return Physics(3);

        Assert.IsTrue(portal.IsLit, "接触应点亮传送门");
        Assert.IsTrue(host.State.IsPortalLit(portal.Id));
        Assert.AreEqual(1, host.State.LitPortalCount);
    }

    [UnityTest]
    public IEnumerator PortalNeedsBothPlayersPressing()
    {
        CreateFloor(-5f, 20f);
        var p1 = CreatePlayer("P1", new Vector2(2f, SpawnY));
        CreatePlayer("P2", new Vector2(10f, SpawnY), PlayerMotor.Scheme.Arrows);
        p1.gameObject.AddComponent<KeyCarrier>().PickUp();
        var portal = CreateTrigger<Portal>("Portal", new Vector2(2f, 1.5f), new Vector2(2f, 3f));

        yield return Physics(3);
        yield return PressKeys(Key.S);
        yield return Physics(3);

        Assert.IsFalse(portal.IsActive, "只有一人按下不应激活");
    }

    [UnityTest]
    public IEnumerator PortalActivatesWhenBothPressWithKey()
    {
        CreateFloor(-5f, 20f);
        var p1 = CreatePlayer("P1", new Vector2(2f, SpawnY));
        CreatePlayer("P2", new Vector2(2.5f, SpawnY), PlayerMotor.Scheme.Arrows);
        var carrier = p1.gameObject.AddComponent<KeyCarrier>();
        carrier.PickUp();
        var portal = CreateTrigger<Portal>("Portal", new Vector2(2.5f, 1.5f), new Vector2(2f, 3f));

        yield return Physics(3);
        yield return PressKeys(Key.S, Key.DownArrow);
        yield return Physics(3);

        Assert.IsTrue(portal.IsActive, "两人同帧按下应激活");
        Assert.IsFalse(carrier.HasKey, "激活应消耗钥匙");
        Assert.AreEqual(0, host.State.Keys);
    }

    [UnityTest]
    public IEnumerator PortalDoesNotActivateWithoutKey()
    {
        CreateFloor(-5f, 20f);
        CreatePlayer("P1", new Vector2(2f, SpawnY));
        CreatePlayer("P2", new Vector2(2.5f, SpawnY), PlayerMotor.Scheme.Arrows);
        var portal = CreateTrigger<Portal>("Portal", new Vector2(2.5f, 1.5f), new Vector2(2f, 3f));

        yield return Physics(3);
        yield return PressKeys(Key.S, Key.DownArrow);
        yield return Physics(3);

        Assert.IsFalse(portal.IsActive, "没有钥匙不能激活");
    }

    // 工具 -------------------------------------------------------------

    static void Launch(PlayerMotor motor, SpeedTier tier, Vector2 direction)
    {
        motor.ReceiveBounce(direction, tier, 2f);
    }

    static IEnumerator PressKeys(params Key[] keys)
    {
        var kb = Keyboard.current;
        if (kb == null) yield break;

        InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        InputSystem.Update();
        yield return null;

        InputSystem.QueueStateEvent(kb, new KeyboardState());
        InputSystem.Update();
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
                             PlayerMotor.Scheme scheme = PlayerMotor.Scheme.WASD,
                             bool withLife = false)
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

        if (withLife) go.AddComponent<PlayerLife>();
        return motor;
    }

    GameObject CreateFloor(float xMin, float xMax, float top = 0f, bool ice = false)
    {
        var go = new GameObject(ice ? "IceFloor" : "Floor");
        go.transform.SetParent(root.transform, false);
        go.layer = Ground;
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, top - 1f, 0f);
        go.transform.localScale = new Vector3(xMax - xMin, 2f, 1f);

        go.AddComponent<BoxCollider2D>();
        if (ice) go.AddComponent<IceSurface>();
        return go;
    }

    GroundRope CreateGroundRope(Vector2 position)
    {
        var go = new GameObject("GroundRope");
        go.transform.SetParent(root.transform, false);
        go.layer = Ground;
        go.transform.position = position;
        go.transform.localScale = new Vector3(4f, 0.25f, 1f);

        go.AddComponent<BoxCollider2D>();

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useSideBounce = false;
        effector.useSideFriction = false;
        effector.colliderMask = -1;

        return go.AddComponent<GroundRope>();
    }

    T CreateTrigger<T>(string name, Vector2 position, Vector2 size,
                       int layer = Interactable) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.layer = layer;
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>().isTrigger = true;
        return go.AddComponent<T>();
    }

    T CreateSolid<T>(string name, Vector2 position, Vector2 size,
                     int layer = Ground) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.layer = layer;
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>();
        return go.AddComponent<T>();
    }
}
