using System.IO;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 按键绑定系统（Issue #94）的规格：
///   默认值和 issue 定案一致；显示名规则（L/R 前缀、Num 前缀、大写字母）；
///   冲突检测不阻止保存；存档按名字写、老档能补默认值。
/// </summary>
public sealed class LabBindingsTests
{
    // ---- 默认值 -------------------------------------------------------

    [Test]
    public void DefaultBigMatchesIssueDecision()
    {
        var set = LabBindings.DefaultBig();
        Assert.AreEqual(Key.W, set[LabAction.Up]);
        Assert.AreEqual(Key.S, set[LabAction.Down]);
        Assert.AreEqual(Key.A, set[LabAction.Left]);
        Assert.AreEqual(Key.D, set[LabAction.Right]);
        Assert.AreEqual(Key.Space, set[LabAction.Jump]);
        Assert.AreEqual(Key.LeftShift, set[LabAction.Dash]);
        Assert.AreEqual(Key.LeftCtrl, set[LabAction.Recall]);
    }

    [Test]
    public void DefaultSmallMatchesIssueDecision()
    {
        var set = LabBindings.DefaultSmall();
        Assert.AreEqual(Key.I, set[LabAction.Up]);
        Assert.AreEqual(Key.K, set[LabAction.Down]);
        Assert.AreEqual(Key.J, set[LabAction.Left]);
        Assert.AreEqual(Key.L, set[LabAction.Right]);
        Assert.AreEqual(Key.B, set[LabAction.Jump]);
        Assert.AreEqual(Key.Semicolon, set[LabAction.Dash]);
        Assert.AreEqual(Key.Enter, set[LabAction.Recall]);
    }

    [Test]
    public void DefaultsHaveNoConflict()
    {
        var bindings = new LabBindings();
        Assert.AreEqual(0, bindings.ConflictedKeys().Count, "默认两套不能互相冲突");
        Assert.IsTrue(bindings.IsDefault);
    }

    // ---- 显示名 -------------------------------------------------------

    [Test]
    public void KeyNameUsesSidePrefixes()
    {
        Assert.AreEqual("LShift", LabBindings.KeyName(Key.LeftShift));
        Assert.AreEqual("RShift", LabBindings.KeyName(Key.RightShift));
        Assert.AreEqual("LCtrl", LabBindings.KeyName(Key.LeftCtrl));
        Assert.AreEqual("RCtrl", LabBindings.KeyName(Key.RightCtrl));
        Assert.AreEqual("LAlt", LabBindings.KeyName(Key.LeftAlt));
    }

    [Test]
    public void KeyNameUsesNumPrefixForNumpad()
    {
        Assert.AreEqual("Num0", LabBindings.KeyName(Key.Numpad0));
        Assert.AreEqual("Num5", LabBindings.KeyName(Key.Numpad5));
        Assert.AreEqual("NumEnter", LabBindings.KeyName(Key.NumpadEnter));
    }

    [Test]
    public void KeyNameIsSingleUppercaseLetter()
    {
        Assert.AreEqual("W", LabBindings.KeyName(Key.W));
        Assert.AreEqual("I", LabBindings.KeyName(Key.I));
        Assert.AreEqual("B", LabBindings.KeyName(Key.B));
        Assert.AreEqual("L", LabBindings.KeyName(Key.L));
    }

    [Test]
    public void KeyNameForNamedKeysAndNone()
    {
        Assert.AreEqual("Space", LabBindings.KeyName(Key.Space));
        Assert.AreEqual("Enter", LabBindings.KeyName(Key.Enter));
        Assert.AreEqual("Esc", LabBindings.KeyName(Key.Escape));
        Assert.AreEqual("-", LabBindings.KeyName(Key.None));
    }

    [Test]
    public void LeftAndRightShiftAreDistinctNames()
    {
        Assert.AreNotEqual(LabBindings.KeyName(Key.LeftShift), LabBindings.KeyName(Key.RightShift),
            "左右 Shift 必须能分清");
        Assert.AreNotEqual(LabBindings.KeyId(Key.LeftShift), LabBindings.KeyId(Key.RightShift));
    }

    // ---- 冲突 ---------------------------------------------------------

    [Test]
    public void SameKeyOnTwoActionsIsConflict()
    {
        var bindings = new LabBindings();
        bindings.Assign(true, LabAction.Recall, Key.Space);   // 和跳跃撞了
        var keys = bindings.ConflictedKeys();
        Assert.IsTrue(keys.Contains(Key.Space), "Space 同时绑给跳跃和召回，应判冲突");
        Assert.IsTrue(bindings.IsConflicted(Key.Space));
    }

    [Test]
    public void CrossSideConflictIsAlsoDetected()
    {
        var bindings = new LabBindings();
        bindings.Assign(false, LabAction.Jump, Key.W);        // 小块跳用大块的上
        Assert.IsTrue(bindings.IsConflicted(Key.W), "大块和小块共用一个键也算冲突");
    }

    [Test]
    public void ConflictingBindingsAreStillAllowed()
    {
        // 需求：冲突只标记，不影响退出/保存。
        var bindings = new LabBindings();
        bindings.Assign(true, LabAction.Jump, Key.A);
        Assert.IsTrue(bindings.IsConflicted(Key.A));
        Assert.IsFalse(bindings.IsDefault, "改过就不是默认了");

        // 存进去再读回来，冲突关系原样保留。
        var roundTrip = RoundTrip(bindings);
        Assert.AreEqual(Key.A, roundTrip.big[LabAction.Jump]);
        Assert.IsTrue(roundTrip.IsConflicted(Key.A));
    }

    [Test]
    public void WhoUsesListsEveryOwner()
    {
        var bindings = new LabBindings();
        bindings.Assign(true, LabAction.Jump, Key.A);

        var owners = bindings.WhoUses(Key.A);
        Assert.AreEqual(2, owners.Count, "A 被大块的左移和跳跃同时占用");
    }

    // ---- 存档 ---------------------------------------------------------

    [Test]
    public void BindingsSurviveSaveAndLoad()
    {
        var bindings = new LabBindings();
        bindings.Assign(true, LabAction.Jump, Key.Numpad0);
        bindings.Assign(true, LabAction.Dash, Key.RightCtrl);
        bindings.Assign(false, LabAction.Recall, Key.LeftArrow);

        var restored = RoundTrip(bindings);
        Assert.AreEqual(Key.Numpad0, restored.big[LabAction.Jump]);
        Assert.AreEqual(Key.RightCtrl, restored.big[LabAction.Dash]);
        Assert.AreEqual(Key.LeftArrow, restored.small[LabAction.Recall]);
        // 没改过的保持默认。
        Assert.AreEqual(Key.B, restored.small[LabAction.Jump]);
    }

    [Test]
    public void SaveWritesKeyNamesNotNumbers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hui-bindings-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var saved = LabSettingsStore.DirectoryOverride;
        LabSettingsStore.DirectoryOverride = dir;
        try
        {
            var data = LabSettingsStore.CreateDefault();
            data.bindings = new LabBindings();
            data.bindings.Assign(true, LabAction.Jump, Key.Space);
            LabSettingsStore.Save(data);

            string json = File.ReadAllText(LabSettingsStore.SavePath);
            StringAssert.Contains("Space", json, "按键要按名字存，不能是数字");
            StringAssert.Contains("bigKeys", json);
        }
        finally
        {
            LabSettingsStore.DirectoryOverride = saved;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Test]
    public void OldSaveWithoutBindingsGetsDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hui-bindings-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var saved = LabSettingsStore.DirectoryOverride;
        LabSettingsStore.DirectoryOverride = dir;
        try
        {
            // v2 的存档：没有 bindings 字段。
            File.WriteAllText(LabSettingsStore.SavePath,
                "{\"version\":2,\"masterVolume\":0.5,\"musicVolume\":0.5,\"sfxVolume\":0.5," +
                "\"fullscreen\":false,\"screenWidth\":1280,\"screenHeight\":720,\"brightness\":1.0}");

            var data = LabSettingsStore.Load(out bool fromDisk);
            Assert.IsTrue(fromDisk, "老档要能正常读");
            Assert.IsNotNull(data.bindings, "缺字段要补默认值，不能是 null");
            Assert.AreEqual(Key.W, data.bindings.big[LabAction.Up]);
            Assert.AreEqual(Key.I, data.bindings.small[LabAction.Up]);
            Assert.AreEqual(3, data.version, "迁移到 v3");
        }
        finally
        {
            LabSettingsStore.DirectoryOverride = saved;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    // ---- 接到玩家 -----------------------------------------------------

    [UnityTest]
    public IEnumerator AppliedBindingsDrivePlayerInput()
    {
        var labGo = new GameObject("Lab");
        labGo.SetActive(false);
        var lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);
        yield return null;

        // 默认：W 是"上"。
        Assert.AreEqual(Key.W, lab.Bindings.big[LabAction.Up]);

        // 换一套：把大块的跳跃改成 Num0，并且故意制造冲突。
        var custom = new LabBindings();
        custom.Assign(true, LabAction.Jump, Key.Numpad0);
        custom.Assign(true, LabAction.Dash, Key.Numpad0);
        lab.ApplyBindings(custom);

        Assert.AreEqual(Key.Numpad0, lab.Bindings.big[LabAction.Jump]);
        Assert.IsTrue(lab.Bindings.IsConflicted(Key.Numpad0), "同一个键绑两个动作，要能检出来");

        Object.DestroyImmediate(labGo);
    }

    static LabBindings RoundTrip(LabBindings bindings)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hui-bindings-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var saved = LabSettingsStore.DirectoryOverride;
        LabSettingsStore.DirectoryOverride = dir;
        try
        {
            var data = LabSettingsStore.CreateDefault();
            data.bindings = bindings;
            LabSettingsStore.Save(data);
            var loaded = LabSettingsStore.Load(out bool fromDisk);
            Assert.IsTrue(fromDisk, "存档应该写得进去读得出来");
            return loaded.bindings;
        }
        finally
        {
            LabSettingsStore.DirectoryOverride = saved;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
