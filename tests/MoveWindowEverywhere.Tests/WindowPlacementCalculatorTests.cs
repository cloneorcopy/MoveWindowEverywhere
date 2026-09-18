using System.Windows;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 目标工作区内的窗口位置计算测试，包含负坐标显示器、上下排列、
/// 尺寸超出工作区以及边界限制四类场景。
/// </summary>
public sealed class WindowPlacementCalculatorTests
{
    [Fact]
    public void 窗口应居中到工作区中央且保持原尺寸()
    {
        var workArea = new Rect(0, 0, 1920, 1080);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 800, 600);

        Assert.Equal(new Int32Rect(560, 240, 800, 600), target);
    }

    [Fact]
    public void 负坐标显示器应正确居中()
    {
        // 显示器位于主屏左侧：工作区 x 从 -1920 到 0
        var workArea = new Rect(-1920, 0, 1920, 1080);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 800, 600);

        Assert.Equal(-1360, target.X);
        Assert.Equal(240, target.Y);
        Assert.Equal(800, target.Width);
        Assert.Equal(600, target.Height);
    }

    [Fact]
    public void 上下排列显示器应正确居中()
    {
        // 显示器位于主屏上方
        var workArea = new Rect(0, -1080, 2560, 1440);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 1000, 800);

        Assert.Equal(780, target.X);
        Assert.Equal(-760, target.Y);
    }

    [Fact]
    public void 窗口大于工作区时应限制尺寸并贴到工作区左上角()
    {
        var workArea = new Rect(0, 0, 1366, 768);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 3000, 2000);

        Assert.Equal(1366, target.Width);
        Assert.Equal(768, target.Height);
        Assert.Equal(0, target.X);
        Assert.Equal(0, target.Y);
    }

    [Fact]
    public void 窗口高度超出时应限制高度并垂直居中失效后靠顶()
    {
        var workArea = new Rect(100, 200, 800, 600);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 700, 900);

        Assert.Equal(700, target.Width);
        Assert.Equal(600, target.Height);
        Assert.Equal(150, target.X);
        Assert.Equal(200, target.Y);
    }

    [Fact]
    public void 窗口部分越界时应被夹紧回工作区内()
    {
        var workArea = new Rect(0, 0, 1920, 1080);
        var window = new Int32Rect(-100, -50, 800, 600);

        Int32Rect clamped = WindowPlacementCalculator.ClampToWorkArea(window, workArea);

        Assert.Equal(new Int32Rect(0, 0, 800, 600), clamped);
    }

    [Fact]
    public void 窗口右侧与下侧越界时应向左上平移()
    {
        var workArea = new Rect(0, 0, 1920, 1080);
        var window = new Int32Rect(1800, 1000, 800, 600);

        Int32Rect clamped = WindowPlacementCalculator.ClampToWorkArea(window, workArea);

        Assert.Equal(1120, clamped.X);
        Assert.Equal(480, clamped.Y);
        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
    }

    [Fact]
    public void 负坐标工作区夹紧应保留在工作区内()
    {
        var workArea = new Rect(-1920, -200, 1920, 1080);
        var window = new Int32Rect(-2500, -600, 800, 600);

        Int32Rect clamped = WindowPlacementCalculator.ClampToWorkArea(window, workArea);

        Assert.Equal(-1920, clamped.X);
        Assert.Equal(-200, clamped.Y);
        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
    }

    [Fact]
    public void 夹紧时窗口尺寸大于工作区应被限制为工作区大小()
    {
        var workArea = new Rect(-800, -200, 800, 600);
        var window = new Int32Rect(-1200, -500, 2000, 1500);

        Int32Rect clamped = WindowPlacementCalculator.ClampToWorkArea(window, workArea);

        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
        Assert.Equal(-800, clamped.X);
        Assert.Equal(-200, clamped.Y);
    }

    [Fact]
    public void 任务栏占据部分区域时应以工作区为基准()
    {
        // 底部任务栏 40 像素，工作区高度 1040
        var workArea = new Rect(0, 0, 1920, 1040);

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(workArea, 800, 600);

        Assert.Equal(220, target.Y);
    }
}
