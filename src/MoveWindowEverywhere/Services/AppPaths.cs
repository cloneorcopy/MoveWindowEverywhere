namespace MoveWindowEverywhere.Services;

/// <summary>应用数据目录。配置与日志均写入 %LOCALAPPDATA%，不写入项目目录或系统根目录。</summary>
public static class AppPaths
{
    public static string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoveWindowEverywhere");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");
}
