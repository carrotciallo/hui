using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LevelFlowTests
{
    static readonly Rect WorldBounds = new Rect(-30f, -10f, 80f, 40f);

    GameObject root;
    RunStateHost host;
    World world;
    Region l0;
    Region l1;
    PlayerLife p1;
    PlayerLife p2;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("TestRoot");

        var hostGo = new GameObject("RunStateHost");
        hostGo.transform.SetParent(root.transform, false);
        host = hostGo.AddComponent<RunStateHost>();

        var worldGo = new GameObject("World");
        worldGo.transform.SetParent(root.transform, false);
        world = worldGo.AddComponent<World>();

        l0 = CreateRegion(worldGo.transform, 0, "L0", new Vector2(0f, 5f),
                          new Rect(-10f, -10f, 20f, 40f));
        l1 = CreateRegion(worldGo.transform, 1, "L1", new Vector2(40f, 5f),
                          new Rect(30f, -10f, 20f, 40f));
        world.Configure(new[] { l0, l1 }, WorldBounds, -100f);

        p1 = CreatePlayer("P1", new Vector2(0f, 5f));
        p2 = CreatePlayer("P2", new Vector2(1f, 5f));
        p1.Configure(0, p2);
        p2.Configure(1, p1);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayersSpawnAliveAtRegionSpawn()
    {
        yield return null;

        Assert.IsTrue(p1.IsAlive);
        Assert.IsTrue(p2.IsAlive);
        Assert.That(Vector2.Distance(p1.transform.position, l0.SpawnPositionFor(0)), Is.LessThan(1f));
        Assert.That(Vector2.Distance(p2.transform.position, l0.SpawnPositionFor(1)), Is.LessThan(1f));
    }

    [UnityTest]
    public IEnumerator FallKillsThenRespawnsAtRecordedPoint()
    {
        world.Configure(new[] { l0, l1 }, WorldBounds, -10f);
        var point = new Vector2(6f, 5f);
        host.State.SetRespawn(0, point);
        host.State.AddKey();

        p1.GetComponent<PlayerMotor>().Teleport(new Vector2(6f, -20f));

        yield return null;
        Assert.IsFalse(p1.IsAlive, "低于 FallFloor 应死亡");

        yield return WaitUntil(() => p1.IsAlive, 3f);

        Assert.IsTrue(p1.IsAlive);
        Assert.That(Vector2.Distance(p1.transform.position, point), Is.LessThan(1f));
        Assert.AreEqual(1, host.State.Keys, "死亡不应清空运行状态");
    }

    [UnityTest]
    public IEnumerator KillZoneKillsOnContact()
    {
        AddTrigger<KillZone>("KillZone", p1.transform.position);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(p1.IsAlive);
    }

    [UnityTest]
    public IEnumerator TeammateRevivesDeadPartnerAtRespawnPoint()
    {
        var point = new Vector2(6f, 5f);
        p2.Kill(DeathCause.Hazard);
        Assert.IsFalse(p2.IsAlive);

        AddTrigger<RespawnPoint>("RespawnPoint", point);
        p1.GetComponent<PlayerMotor>().Teleport(point);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(p2.IsAlive, "队友接触重生点应立即复活对方");
        Assert.That(Vector2.Distance(p2.transform.position, point), Is.LessThan(1f));

        Assert.IsTrue(host.State.TryGetRespawn(0, out var p1Point));
        Assert.IsTrue(host.State.TryGetRespawn(1, out var p2Point));
        Assert.That(Vector2.Distance(p1Point, point), Is.LessThan(0.01f));
        Assert.That(Vector2.Distance(p2Point, point), Is.LessThan(0.01f));
    }

    [UnityTest]
    public IEnumerator BothDeadReturnToTheirOwnRespawns()
    {
        var first = new Vector2(1f, 5f);
        var second = new Vector2(3f, 5f);
        host.State.SetRespawn(0, first);
        host.State.SetRespawn(1, second);

        p1.Kill(DeathCause.Hazard);
        p2.Kill(DeathCause.Fall);

        yield return WaitUntil(() => p1.IsAlive && p2.IsAlive, 3f);

        Assert.IsTrue(p1.IsAlive);
        Assert.IsTrue(p2.IsAlive);
        Assert.That(Vector2.Distance(p1.transform.position, first), Is.LessThan(1f));
        Assert.That(Vector2.Distance(p2.transform.position, second), Is.LessThan(1f));
    }

    [UnityTest]
    public IEnumerator CameraStaysInsideWorldBounds()
    {
        var cam = CreateCamera(out var camGo);
        var follow = camGo.AddComponent<CoopCamera>();
        follow.Configure(p1.transform, p2.transform, p1, p2, WorldBounds);

        p1.GetComponent<PlayerMotor>().Teleport(new Vector2(-28f, 5f));
        p2.GetComponent<PlayerMotor>().Teleport(new Vector2(-26f, 5f));

        for (int i = 0; i < 40; i++) yield return null;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 position = camGo.transform.position;

        Assert.GreaterOrEqual(position.x - halfWidth, WorldBounds.xMin - 0.05f);
        Assert.LessOrEqual(position.x + halfWidth, WorldBounds.xMax + 0.05f);
        Assert.GreaterOrEqual(position.y - halfHeight, WorldBounds.yMin - 0.05f);
        Assert.LessOrEqual(position.y + halfHeight, WorldBounds.yMax + 0.05f);
        Assert.That(cam.orthographicSize, Is.InRange(5f, 9f));
    }

    [UnityTest]
    public IEnumerator CameraFollowsLivingPlayerAndIgnoresDeadOne()
    {
        p2.Configure(1, p1, 30f);

        var cam = CreateCamera(out var camGo);
        var follow = camGo.AddComponent<CoopCamera>();
        follow.Configure(p1.transform, p2.transform, p1, p2, WorldBounds);

        p2.Kill(DeathCause.Hazard);
        p1.GetComponent<PlayerMotor>().Teleport(new Vector2(20f, 5f));

        for (int i = 0; i < 60; i++) yield return null;

        Assert.Less(Mathf.Abs(camGo.transform.position.x - 20f), 1.5f);
        Assert.LessOrEqual(cam.orthographicSize, 6f, "只剩一人时应收敛到最小尺寸");
    }

    [UnityTest]
    public IEnumerator TeleportLandsInTargetRegion()
    {
        p1.GetComponent<PlayerMotor>().Teleport(l1.SpawnPositionFor(0));

        yield return null;

        var region = world.RegionOf(p1.transform.position);
        Assert.IsNotNull(region);
        Assert.AreEqual(1, region.Index);
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

    static Region CreateRegion(Transform parent, int index, string label, Vector2 spawnPosition,
                               Rect area)
    {
        var go = new GameObject($"L{index}");
        go.transform.SetParent(parent, false);

        var spawn = new GameObject("Spawn");
        spawn.transform.SetParent(go.transform, false);
        spawn.transform.position = spawnPosition;

        var region = go.AddComponent<Region>();
        region.Configure(index, label, spawn.transform, area);
        return region;
    }

    PlayerLife CreatePlayer(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * GameConstants.PlayerSize;

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<SpeedTierProvider>();
        go.AddComponent<PlayerMotor>();

        return go.AddComponent<PlayerLife>();
    }

    T AddTrigger<T>(string name, Vector2 position) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(2f, 2f);

        return go.AddComponent<T>();
    }

    Camera CreateCamera(out GameObject camGo)
    {
        camGo = new GameObject("TestCamera");
        camGo.transform.SetParent(root.transform, false);

        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.aspect = 16f / 9f;

        return cam;
    }
}
