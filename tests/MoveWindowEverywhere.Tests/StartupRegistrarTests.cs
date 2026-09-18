using Microsoft.Win32;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 开机自启登记测试。
/// </summary>
/// <remarks>
/// 这些用例会真实读写注册表，但用的是每次实例动态生成的临时子键
/// （<c>HKCU\Software\MoveWindowEverywhere.Tests\&lt;GUID&gt;</c>），
/// 不触碰真实的启动项，也不会互相干扰，测试结束后整个临时键被删除。
/// </remarks>
public sealed class StartupRegistrarTests : IDisposable
{
    private const string TestRoot = @"Software\MoveWindowEverywhere.Tests";

    private readonly string _keyPath;
    private readonly string _valueName;

    public StartupRegistrarTests()
    {
        string suffix = Guid.NewGuid().ToString("N");
        _keyPath = $@"{TestRoot}\{suffix}";
        _valueName = $"TestApp_{suffix}";
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        }
        catch (Exception)
        {
            // 清理失败不影响用例结论
        }
    }

    private StartupRegistrar CreateRegistrar(string executablePath) =>
        new(_keyPath, _valueName, executablePath);

    [Fact]
    public void 命令行应为带引号的路径()
    {
        Assert.Equal("\"C:\\Tools\\app.exe\"", StartupRegistrar.BuildCommandLine("C:\\Tools\\app.exe"));
        Assert.Equal("\"C:\\Program Files\\Move Window Everywhere\\app.exe\"", StartupRegistrar.BuildCommandLine("C:\\Program Files\\Move Window Everywhere\\app.exe"));
    }

    [Fact]
    public void 命令行生成时应容忍已带引号或首尾有空格的输入()
    {
        Assert.Equal("\"C:\\Tools\\app.exe\"", StartupRegistrar.BuildCommandLine("  \"C:\\Tools\\app.exe\"  "));
    }

    [Fact]
    public void dotnet宿主不应被视为可登记的可执行文件()
    {
        Assert.False(StartupRegistrar.IsRegisterableExecutable(@"C:\Program Files\dotnet\dotnet.exe"));
        Assert.False(StartupRegistrar.IsRegisterableExecutable("dotnet"));
        Assert.False(StartupRegistrar.IsRegisterableExecutable(null));
        Assert.False(StartupRegistrar.IsRegisterableExecutable("   "));
        Assert.True(StartupRegistrar.IsRegisterableExecutable(@"C:\Tools\MoveWindowEverywhere.exe"));
    }

    [Fact]
    public void 命令行匹配应兼容带引号与不带引号以及带参数的写法()
    {
        const string exe = @"C:\Program Files\App\app.exe";

        Assert.True(StartupRegistrar.CommandMatches("\"C:\\Program Files\\App\\app.exe\"", exe));
        Assert.True(StartupRegistrar.CommandMatches("C:\\Program Files\\App\\app.exe", exe));
        Assert.True(StartupRegistrar.CommandMatches("\"C:\\Program Files\\App\\app.exe\" --minimized", exe));
        Assert.True(StartupRegistrar.CommandMatches("c:\\program files\\app\\APP.EXE", exe));

        Assert.False(StartupRegistrar.CommandMatches("\"C:\\Other\\app.exe\"", exe));
        Assert.False(StartupRegistrar.CommandMatches(null, exe));
        Assert.False(StartupRegistrar.CommandMatches("\"C:\\Program Files\\App\\app.exe\"", string.Empty));
    }

    [Fact]
    public void 开启后应登记并被识别为已启用()
    {
        string exe = @"C:\Tools\MoveWindowEverywhere.exe";
        StartupRegistrar registrar = CreateRegistrar(exe);

        Assert.False(registrar.IsEnabled());
        Assert.Null(registrar.GetRegisteredCommand());

        StartupRegistrationResult result = registrar.Enable();

        Assert.True(result.Success);
        Assert.True(result.Enabled);
        Assert.True(registrar.IsEnabled());
        Assert.Equal("\"C:\\Tools\\MoveWindowEverywhere.exe\"", registrar.GetRegisteredCommand());
    }

    [Fact]
    public void 关闭后应移除登记且可重复调用()
    {
        StartupRegistrar registrar = CreateRegistrar(@"C:\Tools\MoveWindowEverywhere.exe");
        registrar.Enable();

        StartupRegistrationResult first = registrar.Disable();
        Assert.True(first.Success);
        Assert.False(first.Enabled);
        Assert.False(registrar.IsEnabled());
        Assert.Null(registrar.GetRegisteredCommand());

        // 再关一次也应当成功，而不是报「找不到值」
        StartupRegistrationResult second = registrar.Disable();
        Assert.True(second.Success);
    }

    [Fact]
    public void 重复开启应当幂等()
    {
        StartupRegistrar registrar = CreateRegistrar(@"C:\Tools\MoveWindowEverywhere.exe");

        Assert.True(registrar.Enable().Success);
        Assert.True(registrar.Enable().Success);
        Assert.True(registrar.IsEnabled());
    }

    [Fact]
    public void 指向其他程序的登记项不应被视为本程序已启用()
    {
        StartupRegistrar ours = CreateRegistrar(@"C:\Tools\MoveWindowEverywhere.exe");
        StartupRegistrar other = CreateRegistrar(@"C:\Other\something-else.exe");

        other.Enable();

        Assert.False(ours.IsEnabled());
        Assert.Contains("指向其他程序", ours.DescribeState());
    }

    [Fact]
    public void 配置为关闭时不应删除已存在的登记项()
    {
        StartupRegistrar registrar = CreateRegistrar(@"C:\Tools\MoveWindowEverywhere.exe");
        registrar.Enable();

        // 配置为关闭时，启动阶段的同步逻辑什么都不做：
        // 注册表里的值可能是用户手工添加的，程序不该悄悄删掉
        StartupRegistrationResult result = registrar.EnsureConsistent(desired: false);

        Assert.True(result.Success);
        Assert.False(result.Enabled);
        Assert.True(registrar.IsEnabled());
    }

    [Fact]
    public void 配置为开启时应补上缺失的登记项()
    {
        string exe = @"C:\Tools\MoveWindowEverywhere.exe";
        StartupRegistrar registrar = CreateRegistrar(exe);

        StartupRegistrationResult result = registrar.EnsureConsistent(desired: true);

        Assert.True(result.Success);
        Assert.True(result.Enabled);
        Assert.True(registrar.IsEnabled());
    }

    [Fact]
    public void 程序被移动到新位置后同步逻辑应修复旧路径()
    {
        string oldPath = @"C:\Old\MoveWindowEverywhere.exe";
        string newPath = @"C:\New\MoveWindowEverywhere.exe";

        CreateRegistrar(oldPath).Enable();

        StartupRegistrar movedRegistrar = CreateRegistrar(newPath);
        Assert.False(movedRegistrar.IsEnabled());

        StartupRegistrationResult result = movedRegistrar.EnsureConsistent(desired: true);

        Assert.True(result.Success);
        Assert.True(movedRegistrar.IsEnabled());
        Assert.Equal("\"C:\\New\\MoveWindowEverywhere.exe\"", movedRegistrar.GetRegisteredCommand());
    }

    [Fact]
    public void 通过dotnet运行时开启应失败且不写入登记项()
    {
        StartupRegistrar registrar = CreateRegistrar(@"C:\Program Files\dotnet\dotnet.exe");

        Assert.False(registrar.CanRegister);

        StartupRegistrationResult result = registrar.Enable();

        Assert.False(result.Success);
        Assert.False(result.Enabled);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(registrar.GetRegisteredCommand());
    }

    [Fact]
    public void 应用开关应转发到对应的操作()
    {
        string exe = @"C:\Tools\MoveWindowEverywhere.exe";
        StartupRegistrar registrar = CreateRegistrar(exe);

        Assert.True(registrar.Apply(true).Enabled);
        Assert.True(registrar.IsEnabled());
        Assert.False(registrar.Apply(false).Enabled);
        Assert.False(registrar.IsEnabled());
    }

    [Fact]
    public void 未设置时应给出可读状态()
    {
        StartupRegistrar registrar = CreateRegistrar(@"C:\Tools\MoveWindowEverywhere.exe");

        Assert.Equal("未设置开机自启", registrar.DescribeState());
    }
}
