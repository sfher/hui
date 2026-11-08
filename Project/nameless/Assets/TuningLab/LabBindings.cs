using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>一个玩家的可重映射动作。</summary>
public enum LabAction
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
    Jump = 4,
    Dash = 5,
    Recall = 6,
}

/// <summary>
/// 一个玩家的按键绑定。索引与 <see cref="LabAction"/> 一一对应。
///
/// 存的是 <see cref="Key"/> 而不是字符串：序列化到磁盘时用
/// <see cref="LabBindings.KeyId"/> 换取稳定的名字，避免枚举顺序变化让存档错位。
/// </summary>
[Serializable]
public sealed class LabBindingSet
{
    public const int ActionCount = 7;

    /// <summary>每个动作一个键；顺序同 <see cref="LabAction"/>。</summary>
    public Key[] keys = new Key[ActionCount];

    public LabBindingSet() { }

    public LabBindingSet(params Key[] initial)
    {
        for (int i = 0; i < ActionCount; i++)
            keys[i] = i < initial.Length ? initial[i] : Key.None;
    }

    public Key this[LabAction action]
    {
        get => keys[(int)action];
        set => keys[(int)action] = value;
    }

    public LabBindingSet Clone()
    {
        var copy = new LabBindingSet();
        for (int i = 0; i < ActionCount; i++) copy.keys[i] = keys[i];
        return copy;
    }

    public bool SameAs(LabBindingSet other)
    {
        if (other == null) return false;
        for (int i = 0; i < ActionCount; i++)
            if (keys[i] != other.keys[i]) return false;
        return true;
    }
}

/// <summary>
/// 两个玩家的按键绑定 + 冲突检测 + 按键显示名。
///
/// 默认值按 Issue #94 的定案：
///   大块（P1）：WASD 指向，Space 跳，LeftShift 冲刺，LeftCtrl 召回
///   小块（P2）：IJKL 指向，B 跳，:/; 冲刺，Enter 召回
///
/// 冲突是**允许存在**的（同一个键绑到两个动作，运行时两个动作同时响应），
/// 这里只负责把它们找出来给界面标红，不阻止玩家保存。
/// </summary>
[Serializable]
public sealed class LabBindings
{
    /// <summary>大块（A，方形）。</summary>
    public LabBindingSet big = DefaultBig();
    /// <summary>小块（B，红块）。</summary>
    public LabBindingSet small = DefaultSmall();

    // ---- 默认值（Issue #94 定案）----

    public static LabBindingSet DefaultBig() => new LabBindingSet(
        Key.W, Key.S, Key.A, Key.D,        // 指向
        Key.Space,                          // 跳跃
        Key.LeftShift,                      // 冲刺
        Key.LeftCtrl);                      // 召回

    public static LabBindingSet DefaultSmall() => new LabBindingSet(
        Key.I, Key.K, Key.J, Key.L,        // 指向
        Key.B,                              // 跳跃
        Key.Semicolon,                      // 冲刺（:/;）
        Key.Enter);                         // 召回

    public LabBindings() { }

    public LabBindings(LabBindingSet big, LabBindingSet small)
    {
        this.big = big ?? DefaultBig();
        this.small = small ?? DefaultSmall();
    }

    public LabBindingSet For(bool isBig) => isBig ? big : small;

    public LabBindings Clone() => new LabBindings(big.Clone(), small.Clone());

    public void ResetToDefault()
    {
        big = DefaultBig();
        small = DefaultSmall();
    }

    /// <summary>是否与默认值完全一致。</summary>
    public bool IsDefault =>
        big.SameAs(DefaultBig()) && small.SameAs(DefaultSmall());

    // ---- 冲突 ----

    /// <summary>
    /// 一个键被绑到了哪些动作。返回值带上"是谁的"，因为大块和小块可以共用一个键
    /// （那也算冲突：同一个物理键同时驱动两个人的动作）。
    /// </summary>
    public readonly struct Owning
    {
        public readonly bool isBig;
        public readonly LabAction action;
        public Owning(bool isBig, LabAction action) { this.isBig = isBig; this.action = action; }
    }

    /// <summary>这个键在整套绑定里出现的所有位置。</summary>
    public List<Owning> WhoUses(Key key, List<Owning> buffer = null)
    {
        var result = buffer ?? new List<Owning>();
        result.Clear();
        if (key == Key.None) return result;

        for (int a = 0; a < LabBindingSet.ActionCount; a++)
        {
            if (big.keys[a] == key) result.Add(new Owning(true, (LabAction)a));
            if (small.keys[a] == key) result.Add(new Owning(false, (LabAction)a));
        }
        return result;
    }

    /// <summary>这个键是否被多个动作占用。</summary>
    public bool IsConflicted(Key key)
    {
        var hits = WhoUses(key);
        return hits.Count > 1;
    }

    /// <summary>这一整套里所有发生冲突的键。</summary>
    public HashSet<Key> ConflictedKeys()
    {
        var counts = new Dictionary<Key, int>();
        for (int a = 0; a < LabBindingSet.ActionCount; a++)
        {
            if (big.keys[a] != Key.None)
            {
                counts.TryGetValue(big.keys[a], out int n);
                counts[big.keys[a]] = n + 1;
            }
            if (small.keys[a] != Key.None)
            {
                counts.TryGetValue(small.keys[a], out int n);
                counts[small.keys[a]] = n + 1;
            }
        }

        var result = new HashSet<Key>();
        foreach (var pair in counts)
            if (pair.Value > 1) result.Add(pair.Key);
        return result;
    }

    /// <summary>把某个位置设成指定键。传 Key.None 表示清空。</summary>
    public void Assign(bool isBig, LabAction action, Key key)
    {
        For(isBig)[action] = key;
    }

    /// <summary>只重置一边。</summary>
    public void ResetSide(bool isBig)
    {
        if (isBig) big = DefaultBig();
        else small = DefaultSmall();
    }

    // ---- 按键显示名 ----

    /// <summary>
    /// 按键的显示名。规则（需求原文）：
    ///   - 左右 Shift/Ctrl 用 L/R 前缀区分：LShift / RShift / LCtrl / RCtrl
    ///   - 小键盘加 Num 前缀：Num0 / NumEnter
    ///   - 普通字母是单个大写字母：W / I / B
    ///   - 其余按键用去掉空格的英文名，首字母大写
    /// </summary>
    public static string KeyName(Key key)
    {
        if (key == Key.None) return "-";

        switch (key)
        {
            // ---- 左右区分的修饰键 ----
            case Key.LeftShift: return "LShift";
            case Key.RightShift: return "RShift";
            case Key.LeftCtrl: return "LCtrl";
            case Key.RightCtrl: return "RCtrl";
            case Key.LeftAlt: return "LAlt";
            case Key.RightAlt: return "RAlt";
            case Key.LeftMeta: return "LMeta";
            case Key.RightMeta: return "RMeta";

            // ---- 小键盘：前缀 Num ----
            case Key.Numpad0: return "Num0";
            case Key.Numpad1: return "Num1";
            case Key.Numpad2: return "Num2";
            case Key.Numpad3: return "Num3";
            case Key.Numpad4: return "Num4";
            case Key.Numpad5: return "Num5";
            case Key.Numpad6: return "Num6";
            case Key.Numpad7: return "Num7";
            case Key.Numpad8: return "Num8";
            case Key.Numpad9: return "Num9";
            case Key.NumpadEnter: return "NumEnter";
            case Key.NumpadPlus: return "Num+";
            case Key.NumpadMinus: return "Num-";
            case Key.NumpadMultiply: return "Num*";
            case Key.NumpadDivide: return "Num/";
            case Key.NumpadPeriod: return "Num.";

            // ---- 具名键 ----
            case Key.Space: return "Space";
            case Key.Enter: return "Enter";
            case Key.Escape: return "Esc";
            case Key.Tab: return "Tab";
            case Key.Backspace: return "Backspace";
            case Key.UpArrow: return "Up";
            case Key.DownArrow: return "Down";
            case Key.LeftArrow: return "Left";
            case Key.RightArrow: return "Right";
        }

        // ---- 字母与数字：单个大写 ----
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();
        if (key >= Key.Digit1 && key <= Key.Digit0)
        {
            // Digit1..Digit9, Digit0 顺序特殊，直接取枚举名末位。
            string n = key.ToString();
            return n.Substring(n.Length - 1);
        }

        // ---- 其余：去掉 InputSystem 加的 "Key" 前缀，首字母大写 ----
        string name = key.ToString();
        if (name.StartsWith("Key", StringComparison.Ordinal) && name.Length > 3)
            name = name.Substring(3);
        return name;
    }

    /// <summary>动作的中文名，界面用。</summary>
    public static string ActionName(LabAction action)
    {
        switch (action)
        {
            case LabAction.Up: return "上";
            case LabAction.Down: return "下";
            case LabAction.Left: return "左";
            case LabAction.Right: return "右";
            case LabAction.Jump: return "跳跃";
            case LabAction.Dash: return "冲刺";
            case LabAction.Recall: return "召回";
            default: return action.ToString();
        }
    }

    /// <summary>稳定 ID：写进存档用，避免枚举数值变化导致错位。</summary>
    public static string KeyId(Key key) => key.ToString();

    public static Key ParseKey(string id)
    {
        if (string.IsNullOrEmpty(id)) return Key.None;
        return Enum.TryParse(id, out Key key) ? key : Key.None;
    }
}
