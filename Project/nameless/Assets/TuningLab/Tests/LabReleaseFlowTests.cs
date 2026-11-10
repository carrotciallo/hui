using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class LabReleaseFlowTests
{
    GameObject root;
    LabLevelAsset asset;
    string saveDirectory;
    Scene loadedScene;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        saveDirectory = Path.Combine(Path.GetTempPath(), "hui-release-" + Guid.NewGuid().ToString("N"));
        LabCheckpointStore.DirectoryOverride = saveDirectory;
        LabCheckpointStore.PendingContinue = null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (loadedScene.IsValid() && loadedScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(loadedScene);
        if (root != null) Object.Destroy(root);
        if (asset != null) Object.Destroy(asset);
        yield return null;
        Time.timeScale = 1;
        Physics2D.gravity = previousGravity;
        LabCheckpointStore.PendingContinue = null;
        LabCheckpointStore.DirectoryOverride = null;
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
    }

    [UnityTest]
    public IEnumerator StreamingContinueWaitsForDistantTerrainAndRestoresAbility()
    {
        root = new GameObject("StreamingContinueLab");
        root.SetActive(false);
        var level = root.AddComponent<LabLevel>();
        // Deliberately different embedded geometry: the streamed path must use the asset too.
        level.Data.AddCell(new Vector2Int(-20, 0));
        asset = ScriptableObject.CreateInstance<LabLevelAsset>();
        for (int i = 0; i < 600; i++) asset.Data.AddCell(new Vector2Int(i * 3, -1));
        var point = new Vector2(1000, 5);
        asset.Data.respawnPoints.Add(new LabRespawnPointSpec(point));
        asset.Data.spawnA = new Vector2(0, 10);
        asset.Data.spawnB = new Vector2(2, 10);
        level.BindAsset(asset);
        var lab = root.AddComponent<TuningLab>();
        LabCheckpointStore.PendingContinue = new LabCheckpointStore.Data
        {
            scene = root.scene.name, checkpoint = point, recallUnlocked = true
        };
        root.SetActive(true);
        Assert.IsTrue(lab.IsLoadingLevel);
        Assert.IsTrue(lab.Paused, "加载未完成时物理和操作必须冻结");
        lab.SetPaused(false);
        Assert.IsTrue(lab.Paused, "加载中不能通过恢复暂停提前启动物理");
        Assert.IsNotNull(LabCheckpointStore.PendingContinue, "重生点尚未全部注册，不能提前消费存档");

        float deadline = Time.realtimeSinceStartup + 30;
        while (lab.IsLoadingLevel && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(lab.IsLoadingLevel, "分帧构建应结束");
        Assert.IsFalse(lab.Paused);
        Assert.IsNull(LabCheckpointStore.PendingContinue);
        Assert.IsTrue(lab.RecallUnlocked);
        Assert.IsNotNull(lab.ActiveRespawnPoint);
        Assert.AreEqual(point.x - 1, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(point.x + 1, lab.PlayerB.transform.position.x, 0.001f);
        Assert.IsTrue(lab.PlayerA.IsAlive && lab.PlayerB.IsAlive);
        var composite = root.GetComponentInChildren<CompositeCollider2D>();
        Physics2D.SyncTransforms();
        Assert.IsTrue(composite.OverlapPoint(new Vector2(999.5f, -0.5f)));
        Assert.IsFalse(composite.OverlapPoint(new Vector2(-19.5f, 0.5f)));
        Assert.AreEqual(1, root.GetComponentsInChildren<AudioListener>().Length);
    }

    [UnityTest]
    public IEnumerator FormalMapLoadsFullyAndCanPauseRespawnAndSave()
    {
        var operation = SceneManager.LoadSceneAsync(HomeScreen.LevelSceneName, LoadSceneMode.Additive);
        Assert.IsNotNull(operation);
        yield return operation;
        loadedScene = SceneManager.GetSceneByName(HomeScreen.LevelSceneName);
        Assert.IsTrue(loadedScene.IsValid());
        TuningLab lab = null;
        foreach (var go in loadedScene.GetRootGameObjects())
            if (go.TryGetComponent(out TuningLab found)) lab = found;
        Assert.IsNotNull(lab);
        float deadline = Time.realtimeSinceStartup + 60;
        while (lab.IsLoadingLevel && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(lab.IsLoadingLevel);
        Assert.IsTrue(lab.PlayerA.IsAlive && lab.PlayerB.IsAlive);
        Assert.AreEqual(lab.level.Data.respawnPoints.Count,
            lab.GetComponentsInChildren<LabRespawnPoint>().Length);
        Assert.AreEqual(lab.level.Data.teleports.Count, lab.TeleportPointCount);
        Assert.AreEqual(1, lab.GetComponentsInChildren<AudioListener>().Length);
        Assert.IsFalse(lab.showGrid);
        Assert.AreEqual(lab.DebugControlsEnabled, lab.Hud.enabled);

        var ice = lab.GetComponentInChildren<LabIce>();
        Assert.IsNotNull(ice);
        ice.BreakOne();
        Assert.IsTrue(ice.Broken);
        lab.ResetMap();
        Physics2D.SyncTransforms();
        Assert.IsFalse(ice.Broken, "分帧完成后必须登记真实地图的重置链");
        Assert.IsTrue(ice.IsSolidAtCell());

        lab.SettingsUI.Open();
        Assert.IsTrue(lab.Paused);
        Assert.AreEqual(0, Time.timeScale);
        lab.SettingsUI.Close();
        Assert.IsFalse(lab.Paused);
        Assert.Greater(Time.timeScale, 0);

        var checkpoints = lab.GetComponentsInChildren<LabRespawnPoint>();
        Assert.IsNotEmpty(checkpoints);
        var checkpoint = checkpoints[checkpoints.Length - 1];
        lab.ActivateRespawnPoint(checkpoint);
        lab.UnlockRecall();
        checkpoint.ForceRespawn(lab);
        Assert.AreEqual(checkpoint.transform.position.x - 1, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpoint.transform.position.x + 1, lab.PlayerB.transform.position.x, 0.001f);
        var saved = LabCheckpointStore.Load();
        Assert.AreEqual(HomeScreen.LevelSceneName, saved.scene);
        Assert.IsTrue(saved.recallUnlocked);
        Assert.AreEqual((Vector2)checkpoint.transform.position, saved.checkpoint);
    }
}
