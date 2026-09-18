using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>窗口过滤规则测试：覆盖默认过滤策略的每一条判定。</summary>
public sealed class WindowFilterPolicyTests
{
    private static WindowInfo CreateWindow(Action<WindowInfoBuilder>? configure = null)
    {
        var builder = new WindowInfoBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public void 普通可见窗口应保留()
    {
        WindowInfo window = CreateWindow();
        var policy = new WindowFilterPolicy();

        WindowFilterDecision decision = policy.Evaluate(window);

        Assert.True(decision.IsIncluded);
        Assert.True(policy.IsIncluded(window));
    }

    [Fact]
    public void 不可见窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.IsVisible = false);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("不可见", decision.Reason);
    }

    [Fact]
    public void 无标题窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.Title = "   ");

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("标题", decision.Reason);
    }

    [Fact]
    public void 工具窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.ExtendedStyle = 0x00000080L);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("工具窗口", decision.Reason);
    }

    [Fact]
    public void 不可激活窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.ExtendedStyle = 0x08000000L);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("不可激活", decision.Reason);
    }

    [Fact]
    public void 被系统隐藏的窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.IsCloaked = true);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("cloaking", decision.Reason);
    }

    [Fact]
    public void 有宿主窗口的从属窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.HasOwner = true);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("从属", decision.Reason);
    }

    [Fact]
    public void 本程序自身窗口应排除()
    {
        WindowInfo window = CreateWindow(b => b.IsOwnProcess = true);

        WindowFilterDecision decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
        Assert.Contains("本程序", decision.Reason);
    }

    [Fact]
    public void 关闭自身排除选项后本程序窗口可保留()
    {
        WindowInfo window = CreateWindow(b => b.IsOwnProcess = true);
        var options = new WindowFilterOptions { ExcludeOwnProcess = false };

        WindowFilterDecision decision = new WindowFilterPolicy(options).Evaluate(window);

        Assert.True(decision.IsIncluded);
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("NotifyIconOverflowWindow")]
    public void 系统窗口应按类名排除(string className)
    {
        WindowInfo window = CreateWindow(b => b.ClassName = className);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
    }

    [Fact]
    public void 最小化窗口默认保留()
    {
        WindowInfo window = CreateWindow(b => b.State = WindowStateKind.Minimized);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.True(decision.IsIncluded);
    }

    [Fact]
    public void 配置为排除最小化窗口时应排除()
    {
        WindowInfo window = CreateWindow(b => b.State = WindowStateKind.Minimized);
        var options = new WindowFilterOptions { IncludeMinimizedWindows = false };

        var decision = new WindowFilterPolicy(options).Evaluate(window);

        Assert.False(decision.IsIncluded);
    }

    [Fact]
    public void 无效句柄应排除()
    {
        WindowInfo window = CreateWindow(b => b.Handle = IntPtr.Zero);

        var decision = new WindowFilterPolicy().Evaluate(window);

        Assert.False(decision.IsIncluded);
    }

    [Fact]
    public void Apply_应只保留通过规则的窗口()
    {
        var windows = new List<WindowInfo>
        {
            CreateWindow(),
            CreateWindow(b =>
            {
                b.Title = "工具窗口";
                b.ExtendedStyle = 0x00000080L;
            }),
            CreateWindow(b =>
            {
                b.Title = "隐藏窗口";
                b.IsVisible = false;
            }),
            CreateWindow(b =>
            {
                b.Title = "系统窗口";
                b.ClassName = "Shell_TrayWnd";
            }),
        };

        IReadOnlyList<WindowInfo> kept = new WindowFilterPolicy().Apply(windows);

        Assert.Single(kept);
        Assert.Equal("记事本 - 无标题", kept[0].Title);
    }

    private sealed class WindowInfoBuilder
    {
        public IntPtr Handle { get; set; } = new IntPtr(0x1234);

        public string Title { get; set; } = "记事本 - 无标题";

        public uint ProcessId { get; set; } = 4242;

        public string ProcessName { get; set; } = "notepad";

        public string ClassName { get; set; } = "Notepad";

        public WindowStateKind State { get; set; } = WindowStateKind.Normal;

        public IntPtr MonitorHandle { get; set; } = new IntPtr(0x00010001);

        public bool IsVisible { get; set; } = true;

        public bool IsCloaked { get; set; }

        public bool HasOwner { get; set; }

        public bool IsOwnProcess { get; set; }

        public long Style { get; set; } = 0x16CF0000L;

        public long ExtendedStyle { get; set; } = 0x00000100L;

        public IntPtr IconHandle { get; set; } = IntPtr.Zero;

        public WindowInfo Build() => new()
        {
            Handle = Handle,
            Title = Title,
            ProcessId = ProcessId,
            ProcessName = ProcessName,
            ClassName = ClassName,
            State = State,
            MonitorHandle = MonitorHandle,
            IsVisible = IsVisible,
            IsCloaked = IsCloaked,
            HasOwner = HasOwner,
            IsOwnProcess = IsOwnProcess,
            Style = Style,
            ExtendedStyle = ExtendedStyle,
            IconHandle = IconHandle,
        };
    }
}
