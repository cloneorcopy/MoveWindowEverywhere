using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 快捷键被占用时的备用组合规划。
/// </summary>
/// <remarks>
/// 规划遵循两条原则，候选顺序也是按这两条原则排的：
/// <list type="number">
/// <item>
/// 先保留用户设定的按键、只替换修饰键组合。按键是用户最容易记住的部分，
/// 换成 Alt + Shift + Z 之后肌肉记忆仍然管用。同时也避免了去抢占其他程序
/// 的 Alt + 字母菜单助记符。
/// </item>
/// <item>
/// 只有在修饰键组合都不可用时，才改换按键，并且只从一份精选字母里挑
/// （见 <see cref="PreferredSecondaryKeys"/>），避开 F / E / V / I / T / H / O / W
/// 这类在 Windows 与各类程序中几乎必然被当作菜单助记符的字母。
/// </item>
/// </list>
/// 不使用 Win 键组合：Win + 字母被系统外壳大量占用，抢注会让用户失去系统快捷键，
/// 而且注册失败率高，作为备选没有意义。
/// </remarks>
public static class HotkeyFallbackPlanner
{
    /// <summary>默认最多规划多少个候选（含用户原本设定的组合）。</summary>
    public const int DefaultMaxCandidates = 12;

    /// <summary>
    /// 备用修饰键组合，按从「与原组合最接近」到「差异最大」排列。
    /// 不含 MOD_NOREPEAT，拼接时统一补上。
    /// </summary>
    private static readonly uint[] AlternativeModifierPresets =
    [
        HotkeySettings.ModAlt | HotkeySettings.ModShift,
        HotkeySettings.ModControl | HotkeySettings.ModAlt,
        HotkeySettings.ModControl | HotkeySettings.ModShift,
        HotkeySettings.ModControl | HotkeySettings.ModAlt | HotkeySettings.ModShift,
    ];

    /// <summary>
    /// 备用按键，只包含极少作为菜单助记符的字母。
    /// J / K / Q / N / B / X 在英文界面里几乎不承担助记符职责，且与 Z 相邻或易于记忆。
    /// </summary>
    private static readonly uint[] PreferredSecondaryKeys =
    [
        0x58, // X
        0x4A, // J
        0x4B, // K
        0x51, // Q
        0x4E, // N
        0x42, // B
    ];

    /// <summary>
    /// 按优先级给出候选组合列表，第一项始终是用户原本设定的组合。
    /// 传入无效快捷键时返回空列表。
    /// </summary>
    public static IReadOnlyList<HotkeySettings> Plan(HotkeySettings? desired, int maxCandidates = DefaultMaxCandidates)
    {
        var candidates = new List<HotkeySettings>();
        if (desired is null || !desired.IsValid || maxCandidates <= 0)
        {
            return candidates;
        }

        uint desiredModifiers = desired.Modifiers & (HotkeySettings.ModAlt
            | HotkeySettings.ModControl
            | HotkeySettings.ModShift
            | HotkeySettings.ModWin);

        var seen = new HashSet<(uint Modifiers, uint VirtualKey)>();

        // 第一项：用户原本设定的组合
        Add(desiredModifiers, desired.VirtualKey);

        // 第二组：保留按键，只换修饰键组合
        foreach (uint preset in AlternativeModifierPresets)
        {
            Add(preset, desired.VirtualKey);
        }

        // 第三组：保留用户设定的修饰键，改换按键
        foreach (uint key in PreferredSecondaryKeys)
        {
            Add(desiredModifiers, key);
        }

        return candidates;

        void Add(uint modifiers, uint virtualKey)
        {
            if (candidates.Count >= maxCandidates || virtualKey == 0 || modifiers == 0)
            {
                return;
            }

            if (!seen.Add((modifiers, virtualKey)))
            {
                return;
            }

            candidates.Add(new HotkeySettings
            {
                Modifiers = modifiers | HotkeySettings.ModNoRepeat,
                VirtualKey = virtualKey,
            });
        }
    }
}
