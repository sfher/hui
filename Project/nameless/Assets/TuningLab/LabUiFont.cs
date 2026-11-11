using UnityEngine;

/// <summary>
/// IMGUI 用的中文字体。Unity 自带 GUI 字体没有中文字形，只能拿系统字体。
/// 名单必须按平台拆开给：CreateDynamicFontFromOSFont 拿到混合名单时，
/// 查字形会逐个名字往下试，名单里本平台不存在的字体每被试一次就刷
/// 一行 Unable to load font face（2026-11-11 实测：一份玩家日志里
/// 刷出 35 万行 Microsoft YaHei UI）。各平台只给本平台真有的名字。
/// </summary>
public static class LabUiFont
{
    static readonly string[] WindowsNames =
        { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Arial" };
    static readonly string[] LinuxNames =
        { "Noto Sans CJK SC", "Noto Sans CJK", "Source Han Sans SC", "WenQuanYi Zen Hei" };
    static readonly string[] MacNames =
        { "PingFang SC", "Hiragino Sans GB", "Arial" };
    static readonly string[] FallbackNames =
    {
        "Microsoft YaHei UI", "Microsoft YaHei",
        "Noto Sans CJK SC", "Noto Sans CJK", "PingFang SC",
        "SimHei", "SimSun", "Arial",
    };

    static Font cached;

    public static Font Get()
    {
        if (cached == null)
            cached = Font.CreateDynamicFontFromOSFont(PlatformNames(), 14);
        return cached;
    }

    static string[] PlatformNames()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor)
            return WindowsNames;
        if (Application.platform == RuntimePlatform.LinuxPlayer ||
            Application.platform == RuntimePlatform.LinuxEditor)
            return LinuxNames;
        if (Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.OSXEditor)
            return MacNames;
        return FallbackNames;
    }
}
