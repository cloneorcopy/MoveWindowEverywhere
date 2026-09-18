using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>缩略图候选筛选与尺寸计算测试。</summary>
public sealed class ThumbnailCapturePolicyTests
{
    private static WindowInfo CreateWindow(
        bool visible = true,
        bool cloaked = false,
        WindowStateKind state = WindowStateKind.Normal,
        IntPtr? handle = null) => new()
    {
        Handle = handle ?? new IntPtr(0x3000),
        Title = "测试窗口",
        ProcessName = "test",
        IsVisible = visible,
        IsCloaked = cloaked,
        State = state,
    };

    [Fact]
    public void 正常可见窗口应当被截图()
    {
        Assert.True(ThumbnailCapturePolicy.ShouldCapture(CreateWindow()));
    }

    [Fact]
    public void 最小化窗口不应被截图()
    {
        // 最小化窗口没有可渲染内容，截出来只会是一块黑
        Assert.False(ThumbnailCapturePolicy.ShouldCapture(CreateWindow(state: WindowStateKind.Minimized)));
    }

    [Fact]
    public void 最大化窗口应当被截图()
    {
        Assert.True(ThumbnailCapturePolicy.ShouldCapture(CreateWindow(state: WindowStateKind.Maximized)));
    }

    [Fact]
    public void 不可见或被遮挡隐藏的窗口不应被截图()
    {
        Assert.False(ThumbnailCapturePolicy.ShouldCapture(CreateWindow(visible: false)));
        Assert.False(ThumbnailCapturePolicy.ShouldCapture(CreateWindow(cloaked: true)));
    }

    [Fact]
    public void 句柄为零或对象为空时不应被截图()
    {
        Assert.False(ThumbnailCapturePolicy.ShouldCapture(CreateWindow(handle: IntPtr.Zero)));
        Assert.False(ThumbnailCapturePolicy.ShouldCapture(null));
    }

    [Fact]
    public void 挑选候选应跳过不合格项并保持原顺序()
    {
        var windows = new List<WindowInfo>
        {
            CreateWindow(handle: new IntPtr(1)),
            CreateWindow(visible: false, handle: new IntPtr(2)),
            CreateWindow(state: WindowStateKind.Minimized, handle: new IntPtr(3)),
            CreateWindow(handle: new IntPtr(4)),
            CreateWindow(handle: new IntPtr(5)),
        };

        IReadOnlyList<WindowInfo> candidates = ThumbnailCapturePolicy.SelectCandidates(windows);

        Assert.Equal(3, candidates.Count);
        Assert.Equal(new IntPtr(1), candidates[0].Handle);
        Assert.Equal(new IntPtr(4), candidates[1].Handle);
        Assert.Equal(new IntPtr(5), candidates[2].Handle);
    }

    [Fact]
    public void 挑选候选应遵守数量上限()
    {
        List<WindowInfo> windows = Enumerable.Range(1, 50)
            .Select(index => CreateWindow(handle: new IntPtr(index)))
            .ToList();

        Assert.Equal(5, ThumbnailCapturePolicy.SelectCandidates(windows, maxCaptures: 5).Count);
        Assert.Equal(
            ThumbnailCapturePolicy.MaxCapturesPerOpen,
            ThumbnailCapturePolicy.SelectCandidates(windows).Count);
    }

    [Fact]
    public void 空输入或零上限应返回空列表()
    {
        Assert.Empty(ThumbnailCapturePolicy.SelectCandidates(null));
        Assert.Empty(ThumbnailCapturePolicy.SelectCandidates(new List<WindowInfo>()));
        Assert.Empty(ThumbnailCapturePolicy.SelectCandidates(new List<WindowInfo> { CreateWindow() }, maxCaptures: 0));
    }

    [Fact]
    public void 缩略图尺寸应落在目标框内并保持宽高比()
    {
        (int wide, int tall) = ThumbnailCapturePolicy.ComputeThumbnailSize(1920, 1080);

        Assert.True(wide <= ThumbnailCapturePolicy.MaxTargetWidth);
        Assert.True(tall <= ThumbnailCapturePolicy.TargetHeight);
        Assert.Equal(1920d / 1080d, wide / (double)tall, precision: 1);

        (int portraitWidth, int portraitHeight) = ThumbnailCapturePolicy.ComputeThumbnailSize(600, 1200);
        Assert.True(portraitWidth <= ThumbnailCapturePolicy.MaxTargetWidth);
        Assert.True(portraitHeight <= ThumbnailCapturePolicy.TargetHeight);
        Assert.True(portraitHeight > portraitWidth);
    }

    [Fact]
    public void 非法的窗口尺寸应回退到默认缩略图尺寸()
    {
        Assert.Equal(
            (ThumbnailCapturePolicy.MaxTargetWidth, ThumbnailCapturePolicy.TargetHeight),
            ThumbnailCapturePolicy.ComputeThumbnailSize(0, 0));
        Assert.Equal(
            (ThumbnailCapturePolicy.MaxTargetWidth, ThumbnailCapturePolicy.TargetHeight),
            ThumbnailCapturePolicy.ComputeThumbnailSize(-100, 200));
    }

    [Fact]
    public void 极窄或极扁的窗口也至少有一像素()
    {
        (int narrowWidth, int narrowHeight) = ThumbnailCapturePolicy.ComputeThumbnailSize(4000, 3);
        Assert.True(narrowWidth >= 1);
        Assert.True(narrowHeight >= 1);

        (int wideWidth, int wideHeight) = ThumbnailCapturePolicy.ComputeThumbnailSize(3, 4000);
        Assert.True(wideWidth >= 1);
        Assert.True(wideHeight >= 1);
    }
}
