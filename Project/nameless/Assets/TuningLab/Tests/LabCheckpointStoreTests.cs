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
    public void SaveWithoutSceneIsRejected()
    {
        Assert.IsFalse(LabCheckpointStore.Save("", Vector2.zero));
        Assert.IsFalse(LabCheckpointStore.Exists);
    }
}