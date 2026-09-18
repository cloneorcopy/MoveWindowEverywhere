using System.Text;
using System.Text.Json.Serialization;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace MoveWindowEverywhere.Models;

/// <summary>
/// 全局快捷键定义。持久化到 settings.json，并以 RegisterHotKey 所需的
/// 修饰符位掩码 + 虚拟键码形式保存。
/// </summary>
public sealed record HotkeySettings
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public const uint DefaultVirtualKey = 0x5A; // 'Z'

    public uint Modifiers { get; set; }

    public uint VirtualKey { get; set; }

    /// <summary>默认快捷键：Alt + Z。</summary>
    public static HotkeySettings CreateDefault() => new()
    {
        Modifiers = ModAlt | ModNoRepeat,
        VirtualKey = DefaultVirtualKey,
    };

    /// <summary>是否至少包含一个修饰键且包含有效按键。</summary>
    [JsonIgnore]
    public bool IsValid => VirtualKey != 0 && (Modifiers & (ModAlt | ModControl | ModShift | ModWin)) != 0;

    /// <summary>供 RegisterHotKey 使用的修饰符掩码（保留 MOD_NOREPEAT）。</summary>
    [JsonIgnore]
    public uint RegisterModifiers => Modifiers & (ModAlt | ModControl | ModShift | ModWin | ModNoRepeat);

    /// <summary>Modifier 部分的人类可读名称，例如 "Ctrl + Alt"。</summary>
    [JsonIgnore]
    public string ModifierText
    {
        get
        {
            var text = new StringBuilder();
            Append(text, ModControl, "Ctrl");
            Append(text, ModAlt, "Alt");
            Append(text, ModShift, "Shift");
            Append(text, ModWin, "Win");
            return text.ToString();

            void Append(StringBuilder builder, uint flag, string name)
            {
                if ((Modifiers & flag) != 0)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(" + ");
                    }

                    builder.Append(name);
                }
            }
        }
    }

    /// <summary>按键部分的人类可读名称，例如 "M"。</summary>
    [JsonIgnore]
    public string KeyText => VirtualKeyToName(VirtualKey);

    /// <summary>完整显示文本，例如 "Ctrl + Alt + M"。</summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            if (VirtualKey == 0)
            {
                return "未设置";
            }

            string modifiers = ModifierText;
            return modifiers.Length > 0 ? $"{modifiers} + {KeyText}" : KeyText;
        }
    }

    public HotkeySettings Copy() => new() { Modifiers = Modifiers, VirtualKey = VirtualKey };

    public override string ToString() => DisplayText;

    /// <summary>将虚拟键码转换为可读名称。</summary>
    public static string VirtualKeyToName(uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return "未设置";
        }

        try
        {
            string name = ((WinFormsKeys)virtualKey).ToString();
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("Oem", StringComparison.OrdinalIgnoreCase))
            {
                return CleanKeyName(name);
            }
        }
        catch (ArgumentException)
        {
            // 未定义的虚拟键码，落到下面的兜底分支
        }

        return $"VK 0x{virtualKey:X2}";

        static string CleanKeyName(string raw)
        {
            // 数字键以 D1..D9 形式出现，去掉前缀更符合直觉
            if (raw.Length == 2 && raw[0] == 'D' && char.IsDigit(raw[1]))
            {
                return raw[1].ToString();
            }

            return raw;
        }
    }
}
