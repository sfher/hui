using UnityEngine;

/// <summary>
/// 设置界面的像素字体。
///
/// 用的是 **Fusion Pixel Font 10px**（SIL OFL 1.1），已按界面实际用到的字符做过子集化，
/// 所以只有 13KB。授权原文见同目录 LICENSE-OFL.txt。
///
/// 子集化的代价：**界面新增文字时要重新生成字库**，否则新字会被 Unity 回退成方块/空白。
/// 重新生成的命令（在仓库根目录跑）：
///
///   python3 -m fontTools.subset &lt;原字体&gt; --text-file=&lt;字符集&gt; \
///       --output-file=Assets/TuningLab/Resources/Fonts/FusionPixelUI.ttf --no-hinting
///
/// 10px 像素字体只有 10、20、30… 这些整数倍字号是清晰的，
/// 所以 <see cref="SnapSize"/> 会把任意字号吸到最近的整数倍。
/// </summary>
public static class LabPixelFont
{
    /// <summary>Resources 下的字体路径（不含扩展名）。</summary>
    public const string ResourceName = "Fonts/FusionPixelUI";

    /// <summary>字体设计尺寸。像素字体只有它的整数倍清晰。</summary>
    public const int BaseSize = 10;

    static Font cached;
    static bool resolved;

    /// <summary>界面字体。找不到资源时返回 null，调用方退回内置字体。</summary>
    public static Font Font
    {
        get
        {
            if (resolved) return cached;
            resolved = true;

            cached = Resources.Load<Font>(ResourceName);
            if (cached == null)
                Debug.LogWarning($"LabPixelFont: Resources/{ResourceName} 缺失，设置界面退回内置字体。");
            return cached;
        }
    }

    /// <summary>是否拿到了像素字体。</summary>
    public static bool IsPixel => Font != null;

    /// <summary>界面实际使用的字号。改这里一处即可。</summary>
    public const int UiSize = 18;

    /// <summary>
    /// 把期望字号吸到像素字体的清晰尺寸（BaseSize 的整数倍）。
    ///
    /// 例外：正好等于 <see cref="UiSize"/> 时原样保留 —— 18 不是 10 的整数倍，
    /// 但这是界面上定下来的字号，吸附会改变观感。
    /// 非整数倍字号仍能看，只是边缘不如 10/20 那么锐利。
    /// </summary>
    public static int SnapSize(int size)
    {
        if (!IsPixel) return Mathf.Max(8, size);
        if (size == UiSize) return UiSize;
        int multiple = Mathf.Max(1, Mathf.RoundToInt(size / (float)BaseSize));
        return multiple * BaseSize;
    }
}
