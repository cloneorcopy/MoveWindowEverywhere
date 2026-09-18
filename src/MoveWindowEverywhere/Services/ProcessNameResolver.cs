using System.ComponentModel;
using System.Diagnostics;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 进程名解析。对无权限或已退出的进程返回「未知进程」，并带缓存避免重复查询。
/// </summary>
public sealed class ProcessNameResolver
{
    public const string UnknownProcessName = "未知进程";

    private readonly Dictionary<uint, string> _cache = new();
    private readonly AppLogger _logger;

    public ProcessNameResolver(AppLogger logger)
    {
        _logger = logger;
    }

    public string Resolve(uint processId)
    {
        if (processId == 0)
        {
            return UnknownProcessName;
        }

        if (_cache.TryGetValue(processId, out string? cached))
        {
            return cached;
        }

        string name = UnknownProcessName;
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            _logger.Warn($"进程 {processId} 已退出，进程名记为未知");
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"无法读取进程 {processId} 的信息：{ex.Message}");
        }
        catch (Win32Exception ex)
        {
            _logger.Warn($"读取进程 {processId} 名称被拒绝：{ex.Message}");
        }

        _cache[processId] = name;
        return name;
    }
}
