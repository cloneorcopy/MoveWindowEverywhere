using System.Diagnostics;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 极简文件日志：写入 %LOCALAPPDATA%\MoveWindowEverywhere\logs\app-yyyyMMdd.log。
/// 只记录关键失败与状态变化，不记录窗口标题等可能敏感的内容。
/// </summary>
public sealed class AppLogger : IDisposable
{
    private readonly object _sync = new();
    private bool _disposed;

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public void Dispose()
    {
        _disposed = true;
    }

    private void Write(string level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            string file = Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            if (exception is not null)
            {
                line += $"{Environment.NewLine}    {exception.GetType().Name}: {exception.Message}";
            }

            lock (_sync)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // 日志系统本身已经无处再记录，只能输出到调试通道，绝不能因为写日志失败影响主流程。
            Debug.WriteLine($"日志写入失败：{ex.Message}");
        }
    }
}
