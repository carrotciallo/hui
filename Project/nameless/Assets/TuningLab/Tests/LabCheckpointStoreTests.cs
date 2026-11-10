using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 重生点自动存档的磁盘读写：往返、备份兜底、覆盖删除。
/// 全部写临时目录，不碰真实存档。
/// </summary>
public sealed class LabCheckpointStoreTests
{
    string dir;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "hui-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        LabCheckpointStore.DirectoryOverride = dir;
        LabCheckpointStore.PendingContinue = null;
    }

    [TearDown]
    public void TearDown()
    {
        LabCheckpointStore.DirectoryOverride = null;
        LabCheckpointStore.PendingContinue = null;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public void SaveLoadRoundTripKeepsSceneAndPoint()
    {
        Assert.IsFalse(LabCheckpointStore.Exists);
        Assert.IsNull(LabCheckpointStore.Load());

        Assert.IsTrue(LabCheckpointStore.Save("REAL FINAL MAP", new Vector2(546.5f, -26.5f)));
        Assert.IsTrue(LabCheckpointStore.Exists);

        var data = LabCheckpointStore.Load();
        Assert.IsNotNull(data);
        Assert.AreEqual("REAL FINAL MAP", data.scene);
        Assert.AreEqual(546.5f, data.checkpoint.x, 1e-4f);
        Assert.AreEqual(-26.5f, data.checkpoint.y, 1e-4f);
    }

    [Test]
    public void CorruptMainSaveFallsBackToBackup()
    {
        LabCheckpointStore.Save("A", new Vector2(1f, 2f));
        LabCheckpointStore.Save("B", new Vector2(3f, 4f));
        Assert.IsTrue(File.Exists(LabCheckpointStore.BackupPath), "第二次保存应留下上一份备份");

        File.WriteAllText(LabCheckpointStore.SavePath, "不是 JSON");
        var data = LabCheckpointStore.Load();
        Assert.IsNotNull(data, "正式档损坏时应退到备份");
        Assert.AreEqual("A", data.scene);
        Assert.AreEqual(1f, data.checkpoint.x, 1e-4f);
    }

    [Test]
    public void DeleteRemovesMainAndBackup()
    {
        LabCheckpointStore.Save("A", Vector2.zero);
        LabCheckpointStore.Save("B", Vector2.one);

        LabCheckpointStore.Delete();

        Assert.IsFalse(LabCheckpointStore.Exists);
        Assert.IsNull(LabCheckpointStore.Load());
    }

    [Test]
    public void PermanentAbilitySurvivesDiskRoundTrip()
    {
        Assert.IsTrue(LabCheckpointStore.Save("REAL FINAL MAP", new Vector2(1034, -180), true));
        Assert.IsTrue(LabCheckpointStore.Load().recallUnlocked);
    }

    [Test]
    public void VersionOneSaveRemainsReadable()
    {
        File.WriteAllText(LabCheckpointStore.SavePath,
            "{\"version\":1,\"scene\":\"REAL FINAL MAP\",\"checkpoint\":{\"x\":4,\"y\":5}}");
        var data = LabCheckpointStore.Load();
        Assert.IsNotNull(data);
        Assert.AreEqual(new Vector2(4, 5), data.checkpoint);
        Assert.IsFalse(data.recallUnlocked, "旧档未记录能力，不能凭空解锁");
    }

    [Test]
    public void UnsupportedVersionFallsBackToKnownBackup()
    {
        LabCheckpointStore.Save("A", Vector2.one, true);
        LabCheckpointStore.Save("B", Vector2.zero);
        File.WriteAllText(LabCheckpointStore.SavePath,
            "{\"version\":999,\"scene\":\"B\",\"checkpoint\":{\"x\":0,\"y\":0}}");
        var data = LabCheckpointStore.Load();
        Assert.AreEqual("A", data.scene);
        Assert.IsTrue(data.recallUnlocked);
    }

    [Test]
    public void NonFinitePositionCannotOverwriteGoodSave()
    {
        LabCheckpointStore.Save("A", Vector2.one, true);
        Assert.IsFalse(LabCheckpointStore.Save("A", new Vector2(float.NaN, 0)));
        Assert.IsFalse(LabCheckpointStore.Save("A", new Vector2(0, float.PositiveInfinity)));
        Assert.AreEqual(Vector2.one, LabCheckpointStore.Load().checkpoint);
    }

    [Test]
    public void SaveWithoutSceneIsRejected()
    {
        Assert.IsFalse(LabCheckpointStore.Save("", Vector2.zero));
        Assert.IsFalse(LabCheckpointStore.Exists);
    }
}
