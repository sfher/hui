using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 纯图标设置界面。Esc 打开/关闭；打开时 Time.timeScale = 0，所有固定步长物理和
/// 依赖缩放时间的逻辑一起冻结，关闭时把暂停前的 timeScale 原样还回去。
///
/// 图标从 Resources/&lt;IconResourceFolder&gt;/&lt;name&gt;.png 读取；替换同名 PNG 即可换图，
/// 缺图时使用运行时生成的占位棋盘，不会让界面缺一块。
/// 界面只用 IMGUI 和程序生成的色块，不依赖场景、prefab 或字体。
/// </summary>
public class LabSettingsUI : MonoBehaviour
{
    /// <summary>换图标只改这里和 Resources 下的同名文件。</summary>
    public static string IconResourceFolder = "SettingsIcons";

    public const float SaveDelay = 0.25f;

    // 翻页按钮：放在卡片外侧左边，竖直两个方按钮。
    const float PageBtnSize = 48f;
    const float PageBtnGap = 12f;
    const float PageBtnMargin = 16f;

    const float CardW = 560f;
    const float CardH = 432f;
    const float Pad = 16f;
    const float Chip = 40f;
    const float RowH = 48f;
    const float RowStart = 64f;
    const int VolumeRowMaster = 0;
    const int VolumeRowMusic = 1;
    const int VolumeRowSfx = 2;

    // 像素风调色板：深蓝底、青色高亮、米色图标牌。
    static readonly Color OverlayColor = new Color(0.015f, 0.02f, 0.04f, 0.78f);
    static readonly Color CardColor = new Color(0.06f, 0.08f, 0.13f, 1f);
    static readonly Color PanelColor = new Color(0.10f, 0.13f, 0.20f, 1f);
    static readonly Color TrackColor = new Color(0.16f, 0.20f, 0.29f, 1f);
    static readonly Color ChipColor = new Color(0.91f, 0.89f, 0.80f, 1f);
    static readonly Color ChipDimColor = new Color(0.58f, 0.57f, 0.52f, 1f);
    static readonly Color ChipHoverColor = new Color(1f, 0.98f, 0.88f, 1f);
    static readonly Color AccentColor = new Color(0.36f, 0.89f, 1f, 1f);
    static readonly Color AccentDimColor = new Color(0.20f, 0.48f, 0.58f, 1f);
    static readonly Color ReservedFillColor = new Color(0.30f, 0.34f, 0.44f, 1f);
    static readonly Color BorderColor = new Color(0.02f, 0.03f, 0.05f, 1f);
    static readonly Color ReservedColor = new Color(0.62f, 0.66f, 0.74f, 1f);
    /// <summary>冲突标记的红色。</summary>
    static readonly Color ConflictColor = new Color(1f, 0.32f, 0.32f, 1f);
    /// <summary>直接沿用游戏里两个玩家的本体颜色（TuningLab.ColorA / ColorB）。</summary>

    static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();
    static Texture2D placeholderIcon;

    TuningLab lab;
    LabSettingsData data;
    bool open;
    bool dirty;
    float dirtySince;
    int activeSlider = -1;
    float scale = 1f;

    /// <summary>设置界面的两页。</summary>
    enum Page { Basic, Controls }

    Page page = Page.Basic;

    /// <summary>操控页里正在等待改键的位置；null 表示没有选中。</summary>
    sealed class PendingRebind
    {
        public bool isBig;
        public LabAction action;
    }
    PendingRebind pendingRebind;

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    bool resolutionsReady;
    int resolutionIndex;

    public bool IsOpen => open;

    /// <summary>界面对外暴露的页枚举。</summary>
    public enum SettingsPage { Basic, Controls }

    public SettingsPage CurrentPage => page == Page.Basic ? SettingsPage.Basic : SettingsPage.Controls;

    /// <summary>当前是否有键在等待改键。</summary>
    public bool IsRebinding => pendingRebind != null;

    /// <summary>整套绑定里有没有冲突。界面据此在底部提示。</summary>
    public bool HasConflicts => data != null && data.bindings != null
                                && data.bindings.ConflictedKeys().Count > 0;

    public void ShowBasicPage() { page = Page.Basic; pendingRebind = null; }
    public void ShowControlsPage() { page = Page.Controls; }

    /// <summary>进入某个键的改键状态。</summary>
    public void BeginRebind(bool isBig, LabAction action)
        => pendingRebind = new PendingRebind { isBig = isBig, action = action };

    /// <summary>把指定的键写进当前待改位置。给规格用；真实输入走 CaptureRebind。</summary>
    public void DebugApplyRebindKey(Key key)
    {
        if (pendingRebind == null || data == null) return;
        data.bindings ??= new LabBindings();
        data.bindings.Assign(pendingRebind.isBig, pendingRebind.action, key);
        lab?.ApplyBindings(data.bindings);
        pendingRebind = null;
        MarkDirty();
    }

    public void DebugCancelRebind() { pendingRebind = null; }

    /// <summary>两边一起恢复默认。</summary>
    public void ResetBindingsToDefault()
    {
        if (data == null) return;
        data.bindings = new LabBindings();
        lab?.ApplyBindings(data.bindings);
        pendingRebind = null;
        MarkDirty();
    }

    /// <summary>仅供验证工具切页。</summary>
    public void DebugShowControlsPage() => ShowControlsPage();
    public LabSettingsData Data => data;

    void Start()
    {
        if (lab == null) lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) Bind(lab);
    }

    public void Bind(TuningLab target)
    {
        lab = target;
        if (data != null) return;

        data = LabSettingsStore.Load(out bool loadedFromDisk);
        LabSettingsStore.Apply(data, loadedFromDisk);
    }

    public void Open()
    {
        if (open) return;
        if (lab == null) lab = FindAnyObjectByType<TuningLab>();
        if (data == null) Bind(lab);
        EnsureResolutions();
        open = true;
        activeSlider = -1;
        if (lab != null) lab.SetPaused(true);
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        activeSlider = -1;
        if (lab != null) lab.SetPaused(false);
        if (dirty) SaveNow();
    }

    public void Toggle()
    {
        if (open) Close();
        else Open();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            // 传送门选择仍由 TuningLab 用 Esc 取消；动画期间 Esc 继续无效。
            if (open) Close();
            else if (lab == null || !lab.TeleportBusy) Open();
        }

        if (dirty && Time.unscaledTime - dirtySince >= SaveDelay) SaveNow();
    }

    void OnApplicationQuit()
    {
        if (dirty) SaveNow();
    }

    void OnDestroy()
    {
        if (open)
        {
            open = false;
            if (lab != null) lab.SetPaused(false);
        }
        if (dirty) SaveNow();
    }

    bool SaveNow()
    {
        if (data == null) return false;
        if (!LabSettingsStore.Save(data)) return false;
        dirty = false;
        return true;
    }

    void MarkDirty()
    {
        dirty = true;
        dirtySince = Time.unscaledTime;
    }

    void ApplyAudio()
    {
        LabSettingsStore.Apply(data, false);
    }

    void ApplyWindow()
    {
        LabSettingsStore.Apply(data, true);
    }

    static void PlayFeedback()
    {
        LabAudio.Play(LabAudio.Cue.PortalUi);
    }

    // ------------------------------------------------------------------
    // 分辨率档位

    void EnsureResolutions()
    {
        if (resolutionsReady) return;
        resolutionsReady = true;
        resolutions.Clear();

        int maxWidth = Display.main.systemWidth;
        int maxHeight = Display.main.systemHeight;
        var seen = new HashSet<long>();

        foreach (var r in Screen.resolutions)
        {
            if (r.width < 800 || r.height < 450) continue;
            if (maxWidth > 0 && r.width > maxWidth + 1) continue;
            if (maxHeight > 0 && r.height > maxHeight + 1) continue;

            float aspect = r.width / (float)r.height;
            if (aspect < 1.2f || aspect > 2.1f) continue;

            long key = ((long)r.width << 32) | (uint)r.height;
            if (seen.Add(key)) resolutions.Add(new Vector2Int(r.width, r.height));
        }

        if (resolutions.Count == 0) AddFallbackResolutions(maxWidth, maxHeight);

        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        TrimResolutions(6);

        // 当前/存档分辨率不一定出现在 Screen.resolutions 里；补进去，档位点才不会指错。
        var current = new Vector2Int(data.screenWidth, data.screenHeight);
        if (resolutions.Count == 0) resolutions.Add(current);
        else if (!resolutions.Contains(current))
        {
            resolutions[FindResolutionIndex(current.x, current.y)] = current;
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        resolutionIndex = resolutions.IndexOf(current);
        if (resolutionIndex < 0) resolutionIndex = FindResolutionIndex(current.x, current.y);
    }

    void AddFallbackResolutions(int maxWidth, int maxHeight)
    {
        AddFallback(1280, 720, maxWidth, maxHeight);
        AddFallback(1366, 768, maxWidth, maxHeight);
        AddFallback(1600, 900, maxWidth, maxHeight);
        AddFallback(1920, 1080, maxWidth, maxHeight);
        AddFallback(2560, 1440, maxWidth, maxHeight);
        AddFallback(3840, 2160, maxWidth, maxHeight);
        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(
            data.screenWidth >= 640 ? data.screenWidth : 1280,
            data.screenHeight >= 360 ? data.screenHeight : 720));
    }

    void AddFallback(int width, int height, int maxWidth, int maxHeight)
    {
        if (maxWidth > 0 && width > maxWidth + 1) return;
        if (maxHeight > 0 && height > maxHeight + 1) return;
        resolutions.Add(new Vector2Int(width, height));
    }

    /// <summary>档位太多时按等距取样，保留首尾，避免箭头要按几十次。</summary>
    void TrimResolutions(int max)
    {
        if (resolutions.Count <= max) return;
        var keep = new List<Vector2Int>(max);
        for (int i = 0; i < max; i++)
        {
            int index = Mathf.RoundToInt(i * (resolutions.Count - 1) / (float)(max - 1));
            var value = resolutions[index];
            if (!keep.Contains(value)) keep.Add(value);
        }
        resolutions.Clear();
        resolutions.AddRange(keep);
    }

    int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < resolutions.Count; i++)
            if (resolutions[i].x == width && resolutions[i].y == height) return i;

        int best = 0;
        long bestDelta = long.MaxValue;
        for (int i = 0; i < resolutions.Count; i++)
        {
            long dx = resolutions[i].x - width;
            long dy = resolutions[i].y - height;
            long delta = dx * dx + dy * dy;
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }

    void ChangeResolution(int step)
    {
        if (resolutions.Count == 0) return;
        int next = Mathf.Clamp(resolutionIndex + step, 0, resolutions.Count - 1);
        if (next == resolutionIndex) return;

        resolutionIndex = next;
        data.screenWidth = resolutions[next].x;
        data.screenHeight = resolutions[next].y;
        ApplyWindow();
        MarkDirty();
    }

    // ------------------------------------------------------------------
    // IMGUI

    void OnGUI()
    {
        if (!open || data == null) return;

        GUI.depth = -1000;
        scale = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Min(Screen.width / 600f, Screen.height / 470f)), 1, 3);

        var card = new Rect(
            Mathf.Round((Screen.width - CardW * scale) * 0.5f),
            Mathf.Round((Screen.height - CardH * scale) * 0.5f),
            CardW * scale,
            CardH * scale);

        DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), OverlayColor);
        DrawFrame(card, CardColor, AccentColor, 3f);

        DrawPageButtons(card);
        DrawTitle(card);
        DrawClose(card);

        if (page == Page.Basic) DrawBasicPage(card);
        else DrawControlsPage(card);
    }

    /// <summary>基本设置页：就是原来那一页。</summary>
    void DrawBasicPage(Rect card)
    {
        DrawVolumeRow(card, RowStart + RowH * 0f, VolumeRowMaster, "MasterVolume", ref data.masterVolume, false);
        DrawVolumeRow(card, RowStart + RowH * 1f, VolumeRowMusic, "MusicVolume", ref data.musicVolume, false);
        DrawVolumeRow(card, RowStart + RowH * 2f, VolumeRowSfx, "SfxVolume", ref data.sfxVolume, false);

        DrawSeparator(card, RowStart + RowH * 3f - 4f);

        DrawResolutionRow(card, RowStart + RowH * 3f);
        DrawFullscreenRow(card, RowStart + RowH * 4f);
        DrawReservedBrightnessRow(card, RowStart + RowH * 5f);
    }

    void DrawControlsPage(Rect card)
    {
        var bindings = data.bindings ?? (data.bindings = new LabBindings());

        // 左右两栏：大块在左，小块在右。每栏顶部是角色头像 + 名字。
        float colW = (CardW - Pad * 2f - 12f) * 0.5f;
        float leftX = Pad;
        float rightX = Pad + colW + 12f;
        float top = RowStart - 8f;

        DrawSideColumn(card, leftX, top, colW, true, bindings);
        DrawSideColumn(card, rightX, top, colW, false, bindings);

        // 底部：冲突提示 + 恢复默认。
        DrawControlsFooter(card, bindings);

        // 正在等改键：吃掉这一帧的按键。
        if (pendingRebind != null) CaptureRebind(bindings);
    }

    /// <summary>一栏：角色头像 + 名字 + 7 个动作的按键框。</summary>
    void DrawSideColumn(Rect card, float x, float y, float w, bool isBig, LabBindings bindings)
    {
        var set = bindings.For(isBig);

        // ---- 角色标识：直接用游戏里的方块素材，不写字 ----
        var avatar = At(card, x, y, 40f, 40f);
        DrawSideAvatar(avatar, isBig);

        // 头像右边画一条同色横线，视觉上把这一栏和对面那栏隔开。
        DrawSolid(At(card, x + 48f, y + 12f, w - 48f, 3f),
            isBig ? TuningLab.ColorA : TuningLab.ColorB);

        y += 52f;

        // ---- 7 个动作 ----
        var conflicted = bindings.ConflictedKeys();
        for (int a = 0; a < LabBindingSet.ActionCount; a++)
        {
            var action = (LabAction)a;
            DrawKeyRow(card, x, y, w, isBig, action, set[action], conflicted, bindings);
            y += 40f;
        }
    }

    /// <summary>一行：动作名 + 按键框。</summary>
    void DrawKeyRow(Rect card, float x, float y, float w, bool isBig, LabAction action,
        Key key, HashSet<Key> conflicted, LabBindings bindings)
    {
        const float labelW = 92f;
        const float gap = 8f;

        var label = At(card, x, y + 4f, labelW, 24f);
        GUI.Label(label, LabBindings.ActionName(action), PixelLabel(1));

        var box = At(card, x + labelW + gap, y, w - labelW - gap, 32f);

        bool waiting = pendingRebind != null
                       && pendingRebind.isBig == isBig
                       && pendingRebind.action == action;
        bool conflict = conflicted.Contains(key);

        bool clicked = KeyBox(box, key, waiting, conflict);
        if (clicked)
        {
            // 同时只允许改一个：点新的就换掉旧的。
            pendingRebind = new PendingRebind { isBig = isBig, action = action };
        }
    }

    /// <summary>
    /// 按键框。三种状态：
    ///   白色边框 = 正常
    ///   淡蓝边框 = 正在等改键
    ///   红色边框 + 感叹号 = 冲突
    /// </summary>
    bool KeyBox(Rect rect, Key key, bool waiting, bool conflict)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked) PlayFeedback();

        Color border = conflict ? ConflictColor
            : waiting ? AccentColor
            : ChipColor;

        Color fill = waiting ? new Color(0.08f, 0.18f, 0.24f, 1f)
            : hover ? new Color(0.16f, 0.21f, 0.30f, 1f)
            : PanelColor;

        DrawFrame(rect, fill, border, 2f);

        string text = waiting ? "..." : LabBindings.KeyName(key);
        var style = PixelLabel(1);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = conflict ? ConflictColor : ChipColor;
        GUI.Label(rect, text, style);

        // 冲突时右上角画一个感叹号。
        if (conflict) DrawBang(new Rect(rect.xMax - 14f * scale, rect.y - 6f * scale, 14f * scale, 14f * scale));

        return clicked;
    }

    /// <summary>冲突标记：深色底 + 红色感叹号。</summary>
    void DrawBang(Rect rect)
    {
        DrawFrame(rect, ConflictColor, BorderColor, 1f);
        var style = PixelLabel(1);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(0.05f, 0.02f, 0.03f, 1f);
        GUI.Label(rect, "!", style);
    }

    /// <summary>底部：冲突数量提示 + 恢复默认按钮。</summary>
    void DrawControlsFooter(Rect card, LabBindings bindings)
    {
        float y = CardH - Pad - 32f;
        DrawSeparator(card, y - 10f);

        var conflicted = bindings.ConflictedKeys();
        var info = PixelLabel(1);
        if (conflicted.Count > 0)
        {
            info.normal.textColor = ConflictColor;
            GUI.Label(At(card, Pad, y + 4f, 320f, 24f),
                "按键冲突 " + conflicted.Count + " 处（仍可退出）", info);
        }
        else
        {
            info.normal.textColor = ReservedColor;
            GUI.Label(At(card, Pad, y + 4f, 320f, 24f), "无冲突", info);
        }

        var reset = At(card, CardW - Pad - 96f, y, 96f, 32f);
        if (GUI.Button(reset, GUIContent.none, GUIStyle.none))
        {
            ResetBindingsToDefault();
            PlayFeedback();
        }
        DrawFrame(reset, PanelColor, ChipColourForHover(reset, ChipColor), 2f);
        var rs = PixelLabel(1);
        rs.alignment = TextAnchor.MiddleCenter;
        rs.normal.textColor = ChipColor;
        GUI.Label(reset, "恢复默认", rs);
    }

    Color ChipColourForHover(Rect rect, Color normal)
        => rect.Contains(Event.current.mousePosition) ? ChipHoverColor : normal;

    /// <summary>可以被绑定的键。InputSystem 的 Key 枚举本身就覆盖全部物理键。</summary>
    static readonly Key[] AllAssignableKeys = BuildKeyList();

    static Key[] BuildKeyList()
    {
        var list = new List<Key>();
        foreach (Key k in System.Enum.GetValues(typeof(Key)))
            if (k != Key.None) list.Add(k);
        return list.ToArray();
    }

    /// <summary>把这一帧读到的按键写进绑定表。</summary>
    void CaptureRebind(LabBindings bindings)
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Esc 取消，不占用 Esc 本身。
        if (kb.escapeKey.wasPressedThisFrame)
        {
            pendingRebind = null;
            return;
        }

        foreach (Key candidate in AllAssignableKeys)
        {
            if (candidate == Key.Escape || candidate == Key.None) continue;
            var control = kb[candidate];
            if (control == null || !control.wasPressedThisFrame) continue;

            bindings.Assign(pendingRebind.isBig, pendingRebind.action, candidate);
            lab?.ApplyBindings(bindings);
            pendingRebind = null;
            MarkDirty();
            PlayFeedback();
            return;
        }
    }

    // ------------------------------------------------------------------
    // 翻页按钮：卡片外侧左边，竖直两个。不写字，只用图标。

    void DrawPageButtons(Rect card)
    {
        float size = PageBtnSize * scale;
        float gap = PageBtnGap * scale;
        float x = card.x - PageBtnMargin * scale - size;
        float totalH = size * 2f + gap;
        float y = Mathf.Round(card.y + (card.height - totalH) * 0.5f);

        if (IconButton(new Rect(x, y, size, size), "Settings",
                page == Page.Basic, true, out _))
        {
            page = Page.Basic;
            pendingRebind = null;
        }

        if (IconButton(new Rect(x, y + size + gap, size, size), "MoveArrows",
                page == Page.Controls, true, out _))
        {
            page = Page.Controls;
        }
    }

    Rect At(Rect card, float x, float y, float w, float h)
        => new Rect(card.x + x * scale, card.y + y * scale, w * scale, h * scale);

    void DrawTitle(Rect card)
    {
        DrawIconChip(At(card, Pad, 10f, Chip + 8f, Chip + 8f), "Settings", 1f);
        DrawSolid(At(card, 80f, 26f, 396f, 4f), AccentDimColor);
        DrawSolid(At(card, 80f, 34f, 250f, 4f), PanelColor);
    }

    void DrawClose(Rect card)
    {
        if (IconButton(At(card, CardW - Pad - 48f, 8f, 48f, 48f), "Close", false, true, out _))
            Close();
    }

    void DrawSeparator(Rect card, float y)
    {
        DrawSolid(At(card, Pad, y, CardW - Pad * 2f, 2f), PanelColor);
    }

    void DrawVolumeRow(Rect card, float y, int id, string icon, ref float value, bool reserved)
    {
        DrawIconChip(At(card, Pad, y + 4f, Chip, Chip), icon, reserved ? 0.35f : 1f);

        float sliderWidth = reserved ? 424f : CardW - Pad * 2f - 56f;
        var rect = At(card, 72f, y, sliderWidth, RowH);
        bool changed = PixelSlider(rect, id, ref value, !reserved, out bool released);

        if (reserved)
        {
            DrawLock(At(card, CardW - Pad - 40f, y + 4f, 40f, 40f));
            return;
        }

        if (changed)
        {
            ApplyAudio();
            MarkDirty();
        }
        if (released) PlayFeedback();
    }

    void DrawResolutionRow(Rect card, float y)
    {
        DrawIconChip(At(card, Pad, y + 4f, Chip, Chip), "Resolution", 1f);

        if (IconButton(At(card, 72f, y + 4f, Chip, Chip), "ArrowLeft", false, resolutionIndex > 0, out _))
            ChangeResolution(-1);

        float pipsX = 124f;
        float pipsW = 360f;
        int count = Mathf.Max(1, resolutions.Count);
        float pipW = Mathf.Min(32f, (pipsW - (count - 1) * 10f) / count);
        float total = count * pipW + (count - 1) * 10f;
        float start = pipsX + (pipsW - total) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var pip = At(card, start + i * (pipW + 10f), y + 19f, pipW, 10f);
            DrawFrame(pip, i == resolutionIndex ? AccentColor : TrackColor, BorderColor, 1f);
            if (i == resolutionIndex)
                DrawSolid(At(card, start + i * (pipW + 10f) + 3f, y + 22f, pipW - 6f, 4f), ChipColor);
        }

        bool canRight = resolutionIndex < resolutions.Count - 1;
        if (IconButton(At(card, CardW - Pad - Chip, y + 4f, Chip, Chip), "ArrowRight", false, canRight, out _))
            ChangeResolution(1);
    }

    void DrawFullscreenRow(Rect card, float y)
    {
        DrawIconChip(At(card, Pad, y + 4f, Chip, Chip), data.fullscreen ? "Fullscreen" : "Windowed", 1f);

        if (IconButton(At(card, 72f, y + 4f, Chip, Chip), "Fullscreen", data.fullscreen, true, out _)
            && !data.fullscreen)
        {
            data.fullscreen = true;
            ApplyWindow();
            MarkDirty();
        }

        if (IconButton(At(card, 120f, y + 4f, Chip, Chip), "Windowed", !data.fullscreen, true, out _)
            && data.fullscreen)
        {
            data.fullscreen = false;
            ApplyWindow();
            MarkDirty();
        }

        DrawModePreview(At(card, 184f, y + 6f, CardW - Pad - 184f, Chip - 4f), data.fullscreen);
    }

    void DrawReservedBrightnessRow(Rect card, float y)
    {
        DrawIconChip(At(card, Pad, y + 4f, Chip, Chip), "Brightness", 0.35f);
        float brightness = data.brightness;
        PixelSlider(At(card, 72f, y, 424f, RowH), VolumeRowMaster + 10, ref brightness, false, out _);
        DrawLock(At(card, CardW - Pad - Chip, y + 4f, Chip, Chip));
    }

    void DrawReservedKeybindRow(Rect card, float y)
    {
        DrawIconChip(At(card, Pad, y + 4f, Chip, Chip), "KeyBindings", 0.35f);

        for (int i = 0; i < 3; i++)
        {
            var key = At(card, 72f + i * 36f, y + 12f, 28f, 24f);
            DrawFrame(key, ChipDimColor, BorderColor, 2f);
            DrawSolid(At(card, 83f + i * 36f, y + 21f, 6f, 4f), BorderColor);
        }

        DrawLock(At(card, CardW - Pad - Chip, y + 4f, Chip, Chip));
    }

    void DrawModePreview(Rect rect, bool fullscreen)
    {
        DrawFrame(rect, PanelColor, BorderColor, 2f);
        float pad = (fullscreen ? 3f : 9f) * scale;
        var screen = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);
        DrawFrame(screen, fullscreen ? AccentDimColor : TrackColor, BorderColor, 2f);

        if (!fullscreen)
            DrawSolid(new Rect(screen.x, screen.y, screen.width, Mathf.Max(2f, 3f * scale)), AccentDimColor);

        // 屏幕里的"画面"块：让预览读起来像显示器，而不是一根纯色条。
        var content = new Rect(
            screen.x + screen.width * 0.14f,
            screen.y + screen.height * 0.34f,
            screen.width * 0.72f,
            screen.height * 0.40f);
        DrawSolid(content, fullscreen ? ChipColor : ReservedColor);
        DrawSolid(new Rect(content.x, content.y + content.height * 0.62f,
            content.width, Mathf.Max(1f, 2f * scale)), BorderColor);
    }

    /// <summary>像素风文字样式。字体由 LabPixelFont 提供（缺字体时退回内置）。</summary>
    /// <summary>像素字体的文字样式。字号会吸到 10 的整数倍，保证硬边。</summary>
    static GUIStyle PixelLabel(int size)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            richText = false,
            wordWrap = false,
            padding = new RectOffset(0, 0, 0, 0),
        };

        var font = LabPixelFont.Font;
        if (font != null) style.font = font;

        // size 是"倍率"：1 → 10px，2 → 20px。
        style.fontSize = LabPixelFont.IsPixel
            ? LabPixelFont.SnapSize(size * LabPixelFont.BaseSize)
            : Mathf.RoundToInt(style.fontSize * 0.75f);

        style.normal.textColor = ChipColor;
        return style;
    }

    /// <summary>
    /// 角色标识：照搬游戏里玩家本体的样子。
    ///   A（大块）在 LabVisual 里是四条边围出来的空心框，颜色 TuningLab.ColorA；
    ///   B（小块）是一整块实心方块，颜色 TuningLab.ColorB。
    /// 这是代码画的，没有缩放游戏素材导致的模糊。
    /// </summary>
    void DrawSideAvatar(Rect rect, bool isBig)
    {
        float pad = 6f * scale;
        var inner = PixelSnap(new Rect(rect.x + pad, rect.y + pad,
            rect.width - pad * 2f, rect.height - pad * 2f));

        if (isBig)
        {
            // 空心框：上下两条横边 + 左右两条竖边。
            float t = Mathf.Max(2f * scale, Mathf.Round(inner.height * 0.16f));
            Color c = TuningLab.ColorA;
            DrawSolid(new Rect(inner.x, inner.y, inner.width, t), c);
            DrawSolid(new Rect(inner.x, inner.yMax - t, inner.width, t), c);
            DrawSolid(new Rect(inner.x, inner.y, t, inner.height), c);
            DrawSolid(new Rect(inner.xMax - t, inner.y, t, inner.height), c);
            return;
        }

        // 实心块。
        DrawSolid(inner, TuningLab.ColorB);
    }

    /// <summary>把矩形对齐到整数像素，避免像素图被抗锯齿糊掉。</summary>
    static Rect PixelSnap(Rect r)
        => new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));

    void DrawIconChip(Rect rect, string icon, float alpha)
    {
        bool dim = alpha < 0.99f;
        float inset = IconInset(rect);
        DrawFrame(rect, dim ? ChipDimColor : ChipColor, BorderColor, 2f);
        DrawIcon(new Rect(rect.x + inset, rect.y + inset,
            rect.width - inset * 2f, rect.height - inset * 2f), icon, dim ? 0.55f : 1f);
    }

    float IconInset(Rect rect) => (rect.width / scale > 44f ? 8f : 4f) * scale;

    bool IconButton(Rect rect, string icon, bool selected, bool enabled, out bool clicked)
    {
        clicked = false;
        bool hover = enabled && rect.Contains(Event.current.mousePosition);

        if (enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            clicked = true;
            PlayFeedback();
        }

        Color chip = selected ? AccentColor
            : !enabled ? ChipDimColor
            : hover ? ChipHoverColor
            : ChipColor;
        Color border = selected ? ChipHoverColor : BorderColor;
        float inset = IconInset(rect);
        DrawFrame(rect, chip, border, 2f);
        DrawIcon(new Rect(rect.x + inset, rect.y + inset,
            rect.width - inset * 2f, rect.height - inset * 2f), icon, enabled ? 1f : 0.45f);
        return clicked;
    }

    bool PixelSlider(Rect rect, int id, ref float value, bool enabled, out bool released)
    {
        released = false;
        float trackH = 8f * scale;
        var track = new Rect(rect.x, rect.y + (rect.height - trackH) * 0.5f, rect.width, trackH);
        float x = Mathf.Lerp(track.x, track.xMax, Mathf.Clamp01(value));

        DrawFrame(track, TrackColor, BorderColor, 1f);
        DrawSolid(new Rect(track.x, track.y, x - track.x, track.height),
            enabled ? AccentColor : ReservedFillColor);

        float knobW = 12f * scale;
        float knobH = 24f * scale;
        var knob = new Rect(x - knobW * 0.5f, rect.y + (rect.height - knobH) * 0.5f, knobW, knobH);
        DrawFrame(knob, enabled ? ChipColor : ReservedColor, BorderColor, 2f);

        if (!enabled) return false;

        var e = Event.current;
        bool changed = false;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            activeSlider = id;
            changed = SetSliderValue(track, e.mousePosition.x, ref value);
            e.Use();
        }
        else if (activeSlider == id && e.type == EventType.MouseDrag)
        {
            changed = SetSliderValue(track, e.mousePosition.x, ref value);
            e.Use();
        }
        else if (activeSlider == id && e.type == EventType.MouseUp)
        {
            activeSlider = -1;
            released = true;
            e.Use();
        }

        return changed;
    }

    static bool SetSliderValue(Rect track, float mouseX, ref float value)
    {
        float next = Mathf.Clamp01((mouseX - track.x) / Mathf.Max(1f, track.width));
        next = Mathf.Round(next * 100f) / 100f;
        if (Mathf.Approximately(next, value)) return false;
        value = next;
        return true;
    }

    void DrawLock(Rect rect)
    {
        float u = scale;
        float cx = rect.x + rect.width * 0.5f;
        float y = rect.y;
        Color color = ReservedColor;

        DrawSolid(new Rect(cx - 5f * u, y + 9f * u, 10f * u, 2f * u), color);
        DrawSolid(new Rect(cx - 6f * u, y + 11f * u, 2f * u, 8f * u), color);
        DrawSolid(new Rect(cx + 4f * u, y + 11f * u, 2f * u, 8f * u), color);
        DrawSolid(new Rect(cx - 7f * u, y + 19f * u, 14f * u, 12f * u), color);
        DrawSolid(new Rect(cx - 1f * u, y + 23f * u, 2f * u, 5f * u), PanelColor);
    }

    void DrawSolid(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void DrawFrame(Rect rect, Color fill, Color border, float thickness)
    {
        float t = Mathf.Max(1f, thickness * scale);
        DrawSolid(rect, fill);
        DrawSolid(new Rect(rect.x, rect.y, rect.width, t), border);
        DrawSolid(new Rect(rect.x, rect.yMax - t, rect.width, t), border);
        DrawSolid(new Rect(rect.x, rect.y, t, rect.height), border);
        DrawSolid(new Rect(rect.xMax - t, rect.y, t, rect.height), border);
    }

    static void DrawIcon(Rect rect, string name, float alpha)
    {
        var texture = Icon(name);
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }

    /// <summary>图标入口：Resources/&lt;IconResourceFolder&gt;/&lt;name&gt;.png；缺失时用占位图。</summary>
    public static Texture2D Icon(string name)
    {
        string path = IconResourceFolder + "/" + name;
        if (iconCache.TryGetValue(path, out var cached) && cached != null) return cached;

        var texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning($"LabSettingsUI: 缺少设置图标 Resources/{path}.png，使用占位图。");
            texture = PlaceholderIcon();
        }

        iconCache[path] = texture;
        return texture;
    }

    static Texture2D PlaceholderIcon()
    {
        if (placeholderIcon != null) return placeholderIcon;

        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                bool checker = ((x / 4) + (y / 4)) % 2 == 0;
                bool mark = (x >= 4 && x <= 11 && y >= 4 && y <= 5)
                    || (x >= 9 && x <= 11 && y >= 5 && y <= 9)
                    || (x >= 7 && x <= 8 && y >= 9 && y <= 10)
                    || (x >= 7 && x <= 8 && y >= 12 && y <= 13);
                Color32 color = edge || mark
                    ? new Color32(20, 24, 34, 255)
                    : checker ? new Color32(240, 236, 222, 255) : new Color32(196, 190, 176, 255);
                pixels[y * size + x] = color;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        placeholderIcon = tex;
        return tex;
    }
}
