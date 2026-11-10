using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 设置界面两页 + 操控页改键的规格（Issue #94）。
///
/// 界面本身是 IMGUI，测试不模拟鼠标点击，而是直接驱动状态机：
///   翻页 → 选中某个键 → 注入一次按键 → 检查绑定表
/// 像素版式靠截图人工核对，这里只保证逻辑。
/// </summary>
public sealed class LabSettingsControlsPageTests
{
    GameObject labGo;
    TuningLab lab;
    LabSettingsUI ui;

    [SetUp]
    public void SetUp()
    {
        LabSettingsStore.DirectoryOverride =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hui-ui-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(LabSettingsStore.DirectoryOverride);

        Time.timeScale = 1f;
        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        ui = labGo.AddComponent<LabSettingsUI>();
        labGo.SetActive(true);

        ui.Bind(lab);
    }

    [TearDown]
    public void TearDown()
    {
        if (labGo != null) Object.DestroyImmediate(labGo);
        var dir = LabSettingsStore.DirectoryOverride;
        LabSettingsStore.DirectoryOverride = null;
        Time.timeScale = 1f;
        if (dir != null && System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
    }

    [UnityTest]
    public IEnumerator OpeningSettingsShowsBasicPageFirst()
    {
        yield return null;
        ui.Open();
        yield return null;

        Assert.IsTrue(ui.IsOpen);
        Assert.AreEqual(LabSettingsUI.SettingsPage.Basic, ui.CurrentPage, "默认停在基本设置页");
    }

    [UnityTest]
    public IEnumerator CanSwitchToControlsPageAndBack()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;
        Assert.AreEqual(LabSettingsUI.SettingsPage.Controls, ui.CurrentPage);

        ui.ShowBasicPage();
        yield return null;
        Assert.AreEqual(LabSettingsUI.SettingsPage.Basic, ui.CurrentPage);
    }

    [UnityTest]
    public IEnumerator CanSwitchToCreditsPage()
    {
        yield return null;
        ui.Open();
        ui.ShowCreditsPage();
        yield return null;
        Assert.AreEqual(LabSettingsUI.SettingsPage.Credits, ui.CurrentPage,
            "第三页是制作信息页（Issue #131）");

        // 从别的页也能过来，再切回去。
        ui.ShowBasicPage();
        yield return null;
        Assert.AreEqual(LabSettingsUI.SettingsPage.Basic, ui.CurrentPage);

        ui.ShowCreditsPage();
        yield return null;
        Assert.AreEqual(LabSettingsUI.SettingsPage.Credits, ui.CurrentPage);
    }

    /// <summary>
    /// 每个制作人的头像资源都要真的存在。
    /// 之前 cyx 的登录名在代码里少打了一个 m（cyx-weijiheheda vs cyx-weimijiheheda），
    /// Resources.Load 一直返回 null，界面上只剩一块深色底板 —— 排查了很久才
    /// 发现是拼写，所以在这里钉死。
    /// </summary>
    [Test]
    public void EveryCreditedMemberHasAnAvatar()
    {
        var logins = LabSettingsUI.CreditLogins;
        Assert.Greater(logins.Length, 0, "制作人列表不能为空");

        foreach (var login in logins)
        {
            var texture = Resources.Load<Texture2D>("Credits/" + login);
            Assert.IsNotNull(texture, $"缺少头像 Resources/Credits/{login}.png（检查登录名拼写）");
        }
    }

    [UnityTest]
    public IEnumerator CreditsPageIgnoresPendingRebind()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        ui.BeginRebind(true, LabAction.Jump);
        Assert.IsTrue(ui.IsRebinding);

        ui.ShowCreditsPage();
        yield return null;
        Assert.IsFalse(ui.IsRebinding, "切到制作信息页要取消待改键");
    }

    [UnityTest]
    public IEnumerator SelectingAKeyThenPressingItRemapsThatAction()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        // 把大块的跳跃改成 Num0。
        ui.BeginRebind(true, LabAction.Jump);
        Assert.IsTrue(ui.IsRebinding, "进入等待改键状态");

        ui.DebugApplyRebindKey(Key.Numpad0);
        yield return null;

        Assert.IsFalse(ui.IsRebinding, "改完就退出等待状态");
        Assert.AreEqual(Key.Numpad0, lab.Bindings.big[LabAction.Jump], "绑定表要真的改掉");
        Assert.AreEqual(Key.Numpad0, ui.Data.bindings.big[LabAction.Jump], "设置数据也要改掉");
    }

    [UnityTest]
    public IEnumerator OnlyOneKeyCanBeRebindingAtATime()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        ui.BeginRebind(true, LabAction.Jump);
        ui.BeginRebind(false, LabAction.Dash);   // 点了另一个
        yield return null;

        ui.DebugApplyRebindKey(Key.Numpad1);
        yield return null;

        Assert.AreEqual(Key.Numpad1, lab.Bindings.small[LabAction.Dash], "后选的那个被改");
        Assert.AreEqual(Key.Space, lab.Bindings.big[LabAction.Jump], "先选的那个保持原样（大块跳跃默认 Space）");
    }

    [UnityTest]
    public IEnumerator EscapeCancelsRebind()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        ui.BeginRebind(true, LabAction.Left);
        ui.DebugCancelRebind();
        yield return null;

        Assert.IsFalse(ui.IsRebinding);
        Assert.AreEqual(Key.A, lab.Bindings.big[LabAction.Left], "取消不改任何东西");
    }

    [UnityTest]
    public IEnumerator ConflictIsDetectedButStillAllowed()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        // 把召回改成 Space —— 和跳跃撞了。
        ui.BeginRebind(true, LabAction.Recall);
        ui.DebugApplyRebindKey(Key.Space);
        yield return null;

        Assert.AreEqual(Key.Space, lab.Bindings.big[LabAction.Recall]);
        Assert.IsTrue(lab.Bindings.IsConflicted(Key.Space), "冲突要被检出来（界面据此画红框）");
        Assert.IsTrue(ui.HasConflicts, "界面能拿到冲突状态");

        // 关键：冲突状态下仍然能正常关闭设置界面。
        ui.Close();
        yield return null;
        Assert.IsFalse(ui.IsOpen, "有冲突也能退出设置界面");
    }

    [UnityTest]
    public IEnumerator ResetRestoresBothSidesToDefault()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        ui.BeginRebind(true, LabAction.Jump);
        ui.DebugApplyRebindKey(Key.Numpad5);
        yield return null;
        Assert.AreNotEqual(Key.Space, lab.Bindings.big[LabAction.Jump]);

        ui.ResetBindingsToDefault();
        yield return null;

        Assert.AreEqual(Key.Space, lab.Bindings.big[LabAction.Jump], "大块回默认");
        Assert.AreEqual(Key.Quote, lab.Bindings.small[LabAction.Jump], "小块也回默认（#152 起是 ' 键）");
    }

    [UnityTest]
    public IEnumerator SwitchingPageCancelsPendingRebind()
    {
        yield return null;
        ui.Open();
        ui.ShowControlsPage();
        yield return null;

        ui.BeginRebind(true, LabAction.Jump);
        ui.ShowBasicPage();
        yield return null;

        Assert.IsFalse(ui.IsRebinding, "离开操控页要取消待改键状态");
    }
}
