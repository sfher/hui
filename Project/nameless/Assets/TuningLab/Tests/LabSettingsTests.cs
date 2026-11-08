using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 设置存储（Issue #71）：版本字段、临时文件 + 备份、坏档恢复、音量应用，
/// 以及 Esc 设置界面的暂停/恢复是否保留原 timeScale。
/// </summary>
public sealed class LabSettingsTests
{
    string dir;
    GameObject labGo;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "hui-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        LabSettingsStore.DirectoryOverride = dir;
        Time.timeScale = 1f;
        AudioListener.volume = 1f;
        LabAudio.MasterVolume = 1f;
        LabAudio.SfxVolume = 1f;
        LabAudio.MusicVolume = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        if (labGo != null) UnityEngine.Object.DestroyImmediate(labGo);
        LabSettingsStore.DirectoryOverride = null;
        Time.timeScale = 1f;
        AudioListener.volume = 1f;
        LabAudio.MasterVolume = 1f;
        LabAudio.SfxVolume = 1f;
        LabAudio.MusicVolume = 1f;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    static LabSettingsData Sample(float master, float music, float sfx)
    {
        return new LabSettingsData
        {
            masterVolume = master,
            musicVolume = music,
            sfxVolume = sfx,
            fullscreen = true,
            screenWidth = 1600,
            screenHeight = 900,
            brightness = 0.66f,
        };
    }

    [Test]
    public void SaveThenLoadRoundTripsAllFields()
    {
        Assert.IsTrue(LabSettingsStore.Save(Sample(0.33f, 0.44f, 0.55f)));
        Assert.IsTrue(File.Exists(LabSettingsStore.SavePath));
        Assert.IsFalse(File.Exists(LabSettingsStore.TempPath), "临时文件替换后不应留下");

        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsTrue(fromDisk);
        Assert.IsFalse(LabSettingsStore.LastLoadUsedBackup);
        Assert.AreEqual(LabSettingsStore.CurrentVersion, loaded.version);
        Assert.AreEqual(0.33f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual(0.44f, loaded.musicVolume, 1e-4f);
        Assert.AreEqual(0.55f, loaded.sfxVolume, 1e-4f);
        Assert.IsTrue(loaded.fullscreen);
        Assert.AreEqual(1600, loaded.screenWidth);
        Assert.AreEqual(900, loaded.screenHeight);
        Assert.AreEqual(0.66f, loaded.brightness, 1e-4f);
    }

    [Test]
    public void SaveKeepsPreviousFileAsBackup()
    {
        Assert.IsTrue(LabSettingsStore.Save(Sample(0.20f, 0.20f, 0.20f)));
        Assert.IsTrue(LabSettingsStore.Save(Sample(0.80f, 0.80f, 0.80f)));
        Assert.IsTrue(File.Exists(LabSettingsStore.BackupPath));

        var backup = LabSettingsStore.Load(out _);
        Assert.AreEqual(0.80f, backup.masterVolume, 1e-4f);
    }

    [Test]
    public void CorruptMainFallsBackToBackupAndKeepsCorruptFile()
    {
        Assert.IsTrue(LabSettingsStore.Save(Sample(0.25f, 0.25f, 0.25f)));
        Assert.IsTrue(LabSettingsStore.Save(Sample(0.75f, 0.75f, 0.75f)));
        File.WriteAllText(LabSettingsStore.SavePath, "{ this is not json");

        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsTrue(fromDisk);
        Assert.IsTrue(LabSettingsStore.LastLoadUsedBackup);
        Assert.AreEqual(0.25f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual("{ this is not json", File.ReadAllText(LabSettingsStore.SavePath),
            "读取失败不能顺手覆盖坏档，保留现场");
    }

    [Test]
    public void BothFilesCorruptFallsBackToDefaultsWithoutThrowing()
    {
        File.WriteAllText(LabSettingsStore.SavePath, "nope");
        File.WriteAllText(LabSettingsStore.BackupPath, "nope too");

        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsFalse(fromDisk);
        Assert.IsFalse(LabSettingsStore.LastLoadUsedBackup);
        Assert.AreEqual(0.5f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual(0.5f, loaded.musicVolume, 1e-4f);
        Assert.AreEqual(0.5f, loaded.sfxVolume, 1e-4f);
        Assert.GreaterOrEqual(loaded.screenWidth, 640);
        Assert.GreaterOrEqual(loaded.screenHeight, 360);
    }

    [Test]
    public void MissingFilesUseDefaultsWithoutCreatingAnything()
    {
        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsFalse(fromDisk);
        Assert.AreEqual(0.5f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual(0.5f, loaded.musicVolume, 1e-4f);
        Assert.AreEqual(0.5f, loaded.sfxVolume, 1e-4f);
        Assert.IsFalse(File.Exists(LabSettingsStore.SavePath));
        Assert.IsFalse(File.Exists(LabSettingsStore.BackupPath));
    }

    [Test]
    public void OutOfRangeValuesAreClampedOnLoad()
    {
        File.WriteAllText(LabSettingsStore.SavePath,
            "{\"version\":2,\"masterVolume\":9,\"musicVolume\":-2," +
            "\"sfxVolume\":0.5,\"fullscreen\":true,\"screenWidth\":0," +
            "\"screenHeight\":0,\"brightness\":4}");

        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsTrue(fromDisk);
        Assert.AreEqual(1f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual(0f, loaded.musicVolume, 1e-4f);
        Assert.AreEqual(0.5f, loaded.sfxVolume, 1e-4f);
        Assert.AreEqual(1f, loaded.brightness, 1e-4f);
        Assert.GreaterOrEqual(loaded.screenWidth, 640);
        Assert.GreaterOrEqual(loaded.screenHeight, 360);
    }

    [Test]
    public void VersionOneVolumesMigrateWithoutChangingLoudness()
    {
        File.WriteAllText(LabSettingsStore.SavePath,
            "{\"version\":1,\"masterVolume\":1,\"musicVolume\":0.5,\"sfxVolume\":0}");

        var loaded = LabSettingsStore.Load(out bool fromDisk);

        Assert.IsTrue(fromDisk);
        Assert.AreEqual(LabSettingsStore.CurrentVersion, loaded.version, "迁移到当前版本");
        Assert.AreEqual(0.5f, loaded.masterVolume, 1e-4f);
        Assert.AreEqual(0.25f, loaded.musicVolume, 1e-4f);
        Assert.AreEqual(0f, loaded.sfxVolume, 1e-4f);
    }

    [Test]
    public void ApplyMapsFiftyPercentToOriginalFullGainAndKeepsHeadroom()
    {
        LabSettingsStore.Apply(new LabSettingsData
        {
            masterVolume = 0.75f,
            musicVolume = 0.5f,
            sfxVolume = 0.25f,
        }, false);

        Assert.AreEqual(1f, AudioListener.volume, 1e-4f, "主音量改由增益滤镜应用");
        Assert.AreEqual(1.5f, LabAudio.MasterVolume, 1e-4f);
        Assert.AreEqual(1f, LabAudio.MusicVolume, 1e-4f);
        Assert.AreEqual(0.5f, LabAudio.SfxVolume, 1e-4f);
    }

    [Test]
    public void SetPausedFreezesAndRestoresExactTimeScale()
    {
        var lab = MakeLab();
        lab.timeScale = 0.4f;
        Time.timeScale = 0.4f;

        lab.SetPaused(true);
        Assert.IsTrue(lab.Paused);
        Assert.AreEqual(0f, Time.timeScale, 1e-4f);

        lab.SetPaused(true);
        Assert.AreEqual(0f, Time.timeScale, 1e-4f, "重复暂停不改变已保存的 timeScale");

        lab.SetPaused(false);
        Assert.IsFalse(lab.Paused);
        Assert.AreEqual(0.4f, Time.timeScale, 1e-4f, "恢复暂停前的值，而不是固定回 1");
    }

    [Test]
    public void SettingsUiOpenAndCloseRestorePrePauseState()
    {
        var lab = MakeLab();
        var ui = labGo.AddComponent<LabSettingsUI>();
        ui.Bind(lab);

        lab.timeScale = 0.6f;
        Time.timeScale = 0.6f;

        ui.Open();
        Assert.IsTrue(ui.IsOpen);
        Assert.IsTrue(lab.Paused);
        Assert.AreEqual(0f, Time.timeScale, 1e-4f);

        ui.Close();
        Assert.IsFalse(ui.IsOpen);
        Assert.IsFalse(lab.Paused);
        Assert.AreEqual(0.6f, Time.timeScale, 1e-4f);
    }

    TuningLab MakeLab()
    {
        labGo = new GameObject("SettingsLab");
        labGo.SetActive(false);
        var lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);
        return lab;
    }
}
