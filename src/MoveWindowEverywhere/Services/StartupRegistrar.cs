using System.IO;
using System.Security;
using Microsoft.Win32;

namespace MoveWindowEverywhere.Services;

/// <summary>开机自启的登记结果。</summary>
public sealed record StartupRegistrationResult(bool Success, bool Enabled, string? ErrorMessage)
{
    public static StartupRegistrationResult Ok(bool enabled) => new(true, enabled, null);

    public static StartupRegistrationResult Fail(string message, bool enabled) => new(false, enabled, message);
}

/// <summary>
/// 随 Windows 启动的登记与解除。实现方式为在
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> 下写入一条指向本程序 exe 的值。
/// </summary>
/// <remarks>
/// 选择 HKCU 而非 HKLM，是因为写入 HKCU 不需要管理员权限，也不需要提权弹窗；
/// 选择 Run 键而非启动文件夹，是为了避免创建快捷方式与解析 .lnk 带来的额外依赖。
/// 注册表路径与值名都可在构造时注入，单元测试因此可以使用临时的键路径，
/// 不会触碰用户真实的启动项。
/// </remarks>
public sealed class StartupRegistrar
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string DefaultValueName = "MoveWindowEverywhere";

    private readonly string _runKeyPath;
    private readonly string _valueName;
    private readonly string _executablePath;

    public StartupRegistrar(string? runKeyPath = null, string? valueName = null, string? executablePath = null)
    {
        _runKeyPath = string.IsNullOrWhiteSpace(runKeyPath) ? DefaultRunKeyPath : runKeyPath;
        _valueName = string.IsNullOrWhiteSpace(valueName) ? DefaultValueName : valueName;
        _executablePath = string.IsNullOrWhiteSpace(executablePath) ? ResolveExecutablePath() : executablePath;
    }

    /// <summary>被登记的可执行文件路径。</summary>
    public string ExecutablePath => _executablePath;

    /// <summary>
    /// 当前进程是否适合写进启动项。通过 <c>dotnet run</c> 或 <c>dotnet exec</c> 启动时，
    /// 进程路径指向 dotnet.exe 而不是本程序，写进启动项只会得到一条无意义且有害的记录。
    /// </summary>
    public bool CanRegister => IsRegisterableExecutable(_executablePath);

    /// <summary>读取当前进程的可执行文件路径。单文件发布时即为该 exe 自身的路径。</summary>
    public static string ResolveExecutablePath()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Environment.GetCommandLineArgs().FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// 生成写进注册表的命令行。路径一律加引号，否则含空格的路径会被 Windows 当成
    /// 可执行文件名 + 参数，启动项静默失效。
    /// </summary>
    public static string BuildCommandLine(string executablePath)
    {
        string trimmed = (executablePath ?? string.Empty).Trim().Trim('"');
        return $"\"{trimmed}\"";
    }

    public static bool IsRegisterableExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string fileName = Path.GetFileName(executablePath.Trim().Trim('"'));
        return !fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            && !fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断注册表里的命令行是否就指向给定的可执行文件。
    /// 兼容四种历史写法：带引号、不带引号、路径含空格但未加引号、后面还跟着参数。
    /// </summary>
    public static bool CommandMatches(string? registeredCommand, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(registeredCommand) || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string expected = executablePath.Trim().Trim('"');
        string expectedName = Path.GetFileName(expected);
        string expectedDirectory = Path.GetFileName(Path.GetDirectoryName(expected) ?? string.Empty);

        foreach (string candidate in EnumeratePathCandidates(registeredCommand))
        {
            if (string.Equals(Normalize(candidate), Normalize(expected), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 路径可能因为短名或相对写法不同而无法直接比较，退一步只比文件名与所在目录名
            if (string.Equals(Path.GetFileName(candidate), expectedName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    Path.GetFileName(Path.GetDirectoryName(candidate) ?? string.Empty),
                    expectedDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从注册表的命令行里剥离出候选的可执行文件路径。
    /// 带引号时取引号内的内容；不带引号时既可能是完整路径（路径本身含空格），
    /// 也可能是「可执行文件 + 参数」的写法，两种都给出，由调用方逐一比对。
    /// </summary>
    private static IEnumerable<string> EnumeratePathCandidates(string command)
    {
        string trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            yield break;
        }

        if (trimmed[0] == '"')
        {
            int closing = trimmed.IndexOf('"', 1);
            if (closing > 1)
            {
                yield return trimmed[1..closing];
            }

            yield break;
        }

        yield return trimmed;

        int firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0)
        {
            yield return trimmed[..firstSpace];
        }
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>读取已登记的启动命令行；不存在或无法读取时返回 null。</summary>
    public string? GetRegisteredCommand()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: false);
            return key?.GetValue(_valueName, null) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>当前是否已登记为开机自启，且指向的就是本程序。</summary>
    public bool IsEnabled()
    {
        string? command = GetRegisteredCommand();
        return command is not null && CommandMatches(command, _executablePath);
    }

    /// <summary>登记开机自启。失败原因（权限、路径不可用等）会原样返回给调用方展示。</summary>
    public StartupRegistrationResult Enable()
    {
        if (!CanRegister)
        {
            return StartupRegistrationResult.Fail(
                $"当前进程不是可登记的可执行文件（{_executablePath}），无法设置为开机自启。请直接运行发布后的 exe。",
                false);
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
            if (key is null)
            {
                return StartupRegistrationResult.Fail("无法打开开机自启的注册表项。", false);
            }

            key.SetValue(_valueName, BuildCommandLine(_executablePath), RegistryValueKind.String);
            return StartupRegistrationResult.Ok(true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return StartupRegistrationResult.Fail($"写入开机自启项失败：{ex.Message}", false);
        }
        catch (Exception ex)
        {
            return StartupRegistrationResult.Fail($"写入开机自启项失败：{ex.Message}", false);
        }
    }

    /// <summary>解除开机自启。登记项本来就不存在时同样视为成功（幂等）。</summary>
    public StartupRegistrationResult Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
            key?.DeleteValue(_valueName, throwOnMissingValue: false);
            return StartupRegistrationResult.Ok(false);
        }
        catch (Exception ex)
        {
            return StartupRegistrationResult.Fail($"移除开机自启项失败：{ex.Message}", true);
        }
    }

    public StartupRegistrationResult Apply(bool enabled) => enabled ? Enable() : Disable();

    /// <summary>
    /// 启动时把注册表里的状态对齐到配置。<br/>
    /// 配置为开启：确保登记项存在且指向当前 exe（程序被移动过位置时自动修复）。<br/>
    /// 配置为关闭：什么都不做。此时注册表里若残存本程序的值，可能是用户手动添加的，
    /// 程序不应该在启动时悄悄删掉它，删除只由用户在设置里显式关闭时执行。
    /// </summary>
    public StartupRegistrationResult EnsureConsistent(bool desired)
    {
        if (!desired)
        {
            return StartupRegistrationResult.Ok(false);
        }

        return IsEnabled() ? StartupRegistrationResult.Ok(true) : Enable();
    }

    /// <summary>供日志与「关于」窗口展示的可读状态。</summary>
    public string DescribeState()
    {
        string? command = GetRegisteredCommand();
        if (command is null)
        {
            return "未设置开机自启";
        }

        return IsEnabled()
            ? "已设置开机自启"
            : $"注册表中的启动项指向其他程序（{command}）";
    }
}
