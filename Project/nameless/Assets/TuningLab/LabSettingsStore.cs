using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>只包含可持久字段的设置 DTO；不序列化任何 GameObject 或场景对象。</summary>
[Serializable]
public sealed class LabSettingsData
{
    public int version = LabSettingsStore.CurrentVersion;
    // 默认 50%；应用时映射为原来的满音量。
    public float masterVolume = 0.5f;
    public float musicVolume = 0.5f;
    public float sfxVolume = 0.5f;
    public bool fullscreen;
    public int screenWidth;
    public int screenHeight;
    /// <summary>亮度预留位：当前只存取，不参与渲染。</summary>
    public float brightness = 1f;
    /// <summary>两个玩家各自的按键绑定（Issue #94）。</summary>
    public LabBindings bindings = new LabBindings();
}

/// <summary>
/// 设置的磁盘读写。位置固定在 Application.persistentDataPath：
/// settings.json / settings.backup.json / settings.tmp。
///
/// 保存：先完整写临时文件，再 File.Replace 正式文件并留下上一份备份。
/// 读取：正式档 → 备份 → 默认值；解析失败不覆盖原文件，保留现场。
/// 测试用 DirectoryOverride 指向临时目录，避免碰真实存档。
/// </summary>
public static class LabSettingsStore
{
    public const int CurrentVersion = 3;
    const string FileName = "settings.json";
    const string BackupFileName = "settings.backup.json";
    const string TempFileName = "settings.tmp";

    /// <summary>测试/工具可改写到临时目录；为空时使用 Application.persistentDataPath。</summary>
    public static string DirectoryOverride;

    public static string DataDirectory =>
        string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;

    public static string SavePath => Path.Combine(DataDirectory, FileName);
    public static string BackupPath => Path.Combine(DataDirectory, BackupFileName);
    public static string TempPath => Path.Combine(DataDirectory, TempFileName);

    /// <summary>最近一次 Load 是否退到了备份档。</summary>
    public static bool LastLoadUsedBackup { get; private set; }

    public static LabSettingsData CreateDefault()
    {
        int width = Screen.width >= 640 ? Screen.width : 1280;
        int height = Screen.height >= 360 ? Screen.height : 720;
        return new LabSettingsData
        {
            version = CurrentVersion,
            masterVolume = 0.5f,
            musicVolume = 0.5f,
            sfxVolume = 0.5f,
            fullscreen = Screen.fullScreen,
            screenWidth = width,
            screenHeight = height,
            brightness = 1f,
        };
    }

    /// <summary>没有存档时返回默认值；loadedFromDisk 表示是否真的读到过文件。</summary>
    public static LabSettingsData Load(out bool loadedFromDisk)
    {
        LastLoadUsedBackup = false;
        loadedFromDisk = false;

        if (TryRead(SavePath, out var data))
        {
            loadedFromDisk = true;
            return Sanitize(data);
        }

        if (TryRead(BackupPath, out data))
        {
            loadedFromDisk = true;
            LastLoadUsedBackup = true;
            Debug.LogWarning("LabSettingsStore: 正式设置档不可用，已读取备份。");
            return Sanitize(data);
        }

        return CreateDefault();
    }

    /// <summary>写临时文件 → 替换正式档 → 保留备份；失败返回 false 并保留旧档。</summary>
    public static bool Save(LabSettingsData data)
    {
        if (data == null) return false;
        data = Sanitize(data);
        data.version = CurrentVersion;

        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(TempPath, JsonUtility.ToJson(ToSerializable(data), true));

            if (File.Exists(SavePath))
            {
                File.Replace(TempPath, SavePath, BackupPath);
            }
            else
            {
                if (File.Exists(TempPath)) File.Move(TempPath, SavePath);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabSettingsStore: 保存设置失败（{SavePath}）：{e.Message}");
            return false;
        }
    }

    /// <summary>把设置应用到运行中的游戏。亮度是预留字段，暂不应用。</summary>
    public static void Apply(LabSettingsData data, bool applyWindow)
    {
        if (data == null) return;
        data = Sanitize(data);

        // 按键绑定跟着设置一起生效：玩家每帧从 TuningLab.Bindings 读。
        var lab = UnityEngine.Object.FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.ApplyBindings(data.bindings);

        // 50% 对应原来的 100%；DSP 滤镜实现 0-2 倍增益，避免被 Unity 音量属性钳掉。
        AudioListener.volume = 1f;
        LabAudio.MasterVolume = data.masterVolume * 2f;
        LabAudio.SfxVolume = data.sfxVolume * 2f;
        LabAudio.MusicVolume = data.musicVolume * 2f;

        var listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
        if (listener != null && listener.GetComponent<LabAudioGain>() == null)
            listener.gameObject.AddComponent<LabAudioGain>();

        if (!applyWindow) return;

        int width = Mathf.Max(320, data.screenWidth);
        int height = Mathf.Max(180, data.screenHeight);

        if (data.fullscreen)
        {
            // 全屏时表面尺寸必须等于窗口所在显示器的原生尺寸，不能直接
            // 请求所选分辨率：player 对非原生全屏只做缩放视口，渲染面与
            // 指针的坐标系就此错位，IMGUI 按钮会全部点不动（实测矩阵：
            // 原生全屏正常、非原生全屏全灭、所有窗口化正常），且 player
            // 合成的模式请求本身就是畸形的（跨显示器拼尺寸、0Hz）。
            // 改法：表面恒为原生，指针与 UI 始终在原生坐标系里，每个
            // 档位的全屏都可操作。所选档位改作世界渲染分辨率，由
            // LabRenderScale 走游戏侧中间纹理缩放（#153）：主相机先
            // 渲染到所选分辨率的纹理，再等比贴满原生表面；IMGUI 直
            // 接画在后端缓冲上，不经纹理，始终原生清晰、命中不变。
            // 管线侧的两条缩放路线（动态分辨率、URP renderScale）
            // 在本工程都实测不成立，见 #153，不再走。
            // 原生尺寸不能只问 mainWindowDisplayInfo 一处：双屏环境下
            // 它实测会错位/拼接（2026-11-11：窗口在横屏主屏上，问到的
            // 却是另一块竖屏的 1600×2560），须与 currentResolution
            // 交叉核对，选法见 PickNativeSize，并打一行日志留证。
            var display = Screen.mainWindowDisplayInfo;
            var current = Screen.currentResolution;
            var native = PickNativeSize(display.width, display.height,
                current.width, current.height, width, height);
            Debug.Log($"[LabSettings] 全屏钉原生：窗口屏 {display.width}x{display.height}，currentResolution {current.width}x{current.height}，选定 {native.x}x{native.y}（档位 {width}x{height}）");
            if (native.x > 0 && native.y > 0)
            {
                if (Screen.width != native.x || Screen.height != native.y || !Screen.fullScreen)
                    Screen.SetResolution(native.x, native.y, true);
                LabRenderScale.SetTarget(width, height, native.x, native.y);
                return;
            }

            // 拿不到原生尺寸时退回旧行为，不让全屏设置整体失效。
            LabRenderScale.Disable();
            if (Screen.width != width || Screen.height != height || !Screen.fullScreen)
                Screen.SetResolution(width, height, true);
            return;
        }

        LabRenderScale.Disable();
        if (Screen.width != width || Screen.height != height || Screen.fullScreen)
            Screen.SetResolution(width, height, false);
    }

    /// <summary>
    /// 全屏钉原生时选哪个尺寸作准。两处来源一致就用窗口屏的结果；
    /// 不一致时选与所选档位横竖方向一致的那个（本作档位全是横屏，
    /// 问到竖屏尺寸即是错位）；方向也分不出时保持窗口屏的结果。
    /// 任一来源无效（≤0）时用另一个；都无效返回 (0,0)，调用方走兜底。
    /// </summary>
    public static Vector2Int PickNativeSize(
        int windowWidth, int windowHeight,
        int currentWidth, int currentHeight,
        int selectedWidth, int selectedHeight)
    {
        bool windowValid = windowWidth > 0 && windowHeight > 0;
        bool currentValid = currentWidth > 0 && currentHeight > 0;
        if (!windowValid) return new Vector2Int(currentWidth, currentHeight);
        if (!currentValid || (windowWidth == currentWidth && windowHeight == currentHeight))
            return new Vector2Int(windowWidth, windowHeight);

        bool wantLandscape = selectedWidth >= selectedHeight;
        bool windowLandscape = windowWidth >= windowHeight;
        bool currentLandscape = currentWidth >= currentHeight;
        if (windowLandscape != wantLandscape && currentLandscape == wantLandscape)
            return new Vector2Int(currentWidth, currentHeight);
        return new Vector2Int(windowWidth, windowHeight);
    }

    /// <summary>缺字段、越界值和损坏文件都在这里收口，调用方不必再判。</summary>
    public static LabSettingsData Sanitize(LabSettingsData data)
    {
        if (data == null) return CreateDefault();

        data.version = Mathf.Max(1, data.version);
        // v1 的 100% 是 v2 的 50%；迁移时保持实际听感不变。
        if (data.version == 1)
        {
            data.masterVolume *= 0.5f;
            data.musicVolume *= 0.5f;
            data.sfxVolume *= 0.5f;
            data.version = 2;
        }
        // v2 → v3：加入按键绑定。旧档没有这个字段，补默认值。
        if (data.version < 3)
        {
            if (data.bindings == null) data.bindings = new LabBindings();
            data.version = 3;
        }
        if (data.bindings == null) data.bindings = new LabBindings() /* 兜底 */;
        data.bindings.big ??= LabBindings.DefaultBig();
        data.bindings.small ??= LabBindings.DefaultSmall();
        if (data.bindings.big.keys == null || data.bindings.big.keys.Length != LabBindingSet.ActionCount)
            data.bindings.big = LabBindings.DefaultBig();
        if (data.bindings.small.keys == null || data.bindings.small.keys.Length != LabBindingSet.ActionCount)
            data.bindings.small = LabBindings.DefaultSmall();

        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.brightness = Mathf.Clamp01(data.brightness);

        if (data.screenWidth < 640) data.screenWidth = Screen.width >= 640 ? Screen.width : 1280;
        if (data.screenHeight < 360) data.screenHeight = Screen.height >= 360 ? Screen.height : 720;
        return data;
    }

    // ------------------------------------------------------------------
    // 序列化层：Key 枚举不能直接进 JSON。
    //
    // JsonUtility 会把 enum 写成数字，一旦 InputSystem 调整枚举顺序，
    // 老存档里的按键就会指向别的键。所以按键一律按名字存。
    // 其余字段原样透传，DTO 只负责把 bindings 换成字符串。
    // ------------------------------------------------------------------

    [Serializable]
    sealed class SerializableSettings
    {
        public int version;
        public float masterVolume;
        public float musicVolume;
        public float sfxVolume;
        public bool fullscreen;
        public int screenWidth;
        public int screenHeight;
        public float brightness;
        /// <summary>按 LabAction 顺序的按键名：大块在前，小块在后。</summary>
        public string[] bigKeys;
        public string[] smallKeys;

        public LabSettingsData ToData()
        {
            var data = new LabSettingsData
            {
                version = version,
                masterVolume = masterVolume,
                musicVolume = musicVolume,
                sfxVolume = sfxVolume,
                fullscreen = fullscreen,
                screenWidth = screenWidth,
                screenHeight = screenHeight,
                brightness = brightness,
                bindings = new LabBindings(
                    KeysFrom(bigKeys, true),
                    KeysFrom(smallKeys, false)),
            };
            return data;
        }

        static LabBindingSet KeysFrom(string[] ids, bool isBig)
        {
            // 没写就是老档：用默认值，不要让玩家丢键位。
            if (ids == null || ids.Length != LabBindingSet.ActionCount)
                return isBig ? LabBindings.DefaultBig() : LabBindings.DefaultSmall();

            var set = new LabBindingSet();
            for (int i = 0; i < LabBindingSet.ActionCount; i++)
                set.keys[i] = LabBindings.ParseKey(ids[i]);
            return set;
        }
    }

    static SerializableSettings ToSerializable(LabSettingsData data)
    {
        var bindings = data.bindings ?? new LabBindings();
        return new SerializableSettings
        {
            version = data.version,
            masterVolume = data.masterVolume,
            musicVolume = data.musicVolume,
            sfxVolume = data.sfxVolume,
            fullscreen = data.fullscreen,
            screenWidth = data.screenWidth,
            screenHeight = data.screenHeight,
            brightness = data.brightness,
            bigKeys = IdsOf(bindings.big),
            smallKeys = IdsOf(bindings.small),
        };
    }

    static string[] IdsOf(LabBindingSet set)
    {
        var ids = new string[LabBindingSet.ActionCount];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = LabBindings.KeyId(set != null ? set.keys[i] : Key.None);
        return ids;
    }

    static bool TryRead(string path, out LabSettingsData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            var dto = JsonUtility.FromJson<SerializableSettings>(json);
            data = dto != null ? dto.ToData() : null;
            if (data != null) return true;
            Debug.LogWarning($"LabSettingsStore: 设置档为空或不是对象（{path}）。");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabSettingsStore: 读取设置失败（{path}）：{e.Message}");
        }

        data = null;
        return false;
    }
}
