using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using MoveWindowEverywhere.ViewModels;

namespace MoveWindowEverywhere.Views;

/// <summary>
/// 窗口选择器。显示被捕获的目标显示器提示与窗口列表，支持搜索、键盘导航、Enter 或双击移动、Esc 取消。
/// 选择器自身不会进入待移动窗口列表（过滤策略已排除本进程窗口，且 ShowInTaskbar=False）。
/// </summary>
public sealed partial class SelectorWindow : Window
{
    private readonly SelectorViewModel _viewModel;
    private readonly WindowMover _mover;
    private readonly MonitorInfo? _targetMonitor;
    private readonly Action<string, string>? _notify;
    private readonly bool _closeOnFocusLost;
    private readonly bool _showThumbnails;
    private readonly ThumbnailLoader? _thumbnailLoader;
    private bool _isReady;
    private bool _completed;

    public SelectorWindow(
        SelectorViewModel viewModel,
        WindowMover mover,
        MonitorInfo? targetMonitor,
        Action<string, string>? notify = null,
        bool closeOnFocusLost = true,
        bool showThumbnails = true,
        WindowThumbnailService? thumbnailService = null)
    {
        _viewModel = viewModel;
        _mover = mover;
        _targetMonitor = targetMonitor;
        _notify = notify;
        _closeOnFocusLost = closeOnFocusLost;
        _showThumbnails = showThumbnails;

        // 缩略图加载器由窗口自己持有：窗口关闭时连同未完成的截取一起停掉
        if (showThumbnails && thumbnailService is not null)
        {
            _thumbnailLoader = new ThumbnailLoader(thumbnailService.TryCapture);
        }

        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public SelectorViewModel ViewModel => _viewModel;

    /// <summary>缩略图列的可见性。关闭缩略图时整列折叠，避免每行都留一块「无预览」的空白。</summary>
    public Visibility ThumbnailColumnVisibility => _showThumbnails ? Visibility.Visible : Visibility.Collapsed;

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnLoaded;
        ContentRendered -= OnContentRendered;
        Deactivated -= OnDeactivated;
        PreviewKeyDown -= OnPreviewKeyDown;

        _thumbnailLoader?.Dispose();
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Focus();
        Keyboard.Focus(SearchTextBox);
        SearchTextBox.SelectAll();

        if (_viewModel.Windows.Count > 0)
        {
            _viewModel.SelectedEntry ??= _viewModel.Windows[0];
        }

        // 延后一帧再允许「失焦即关闭」，避免窗口刚弹出时的焦点抖动导致立刻关闭
        Dispatcher.BeginInvoke(() => _isReady = true);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        PlaceInsideTargetMonitor();

        // 缩略图放在窗口已经完整呈现之后再开始截取：
        // 首帧先出列表骨架，缩略图随后逐张浮现，打开速度不受截取影响
        StartThumbnailLoading();
    }

    private void StartThumbnailLoading()
    {
        if (_thumbnailLoader is null)
        {
            return;
        }

        _thumbnailLoader.Start(
            _viewModel.Windows.Select(static entry => entry.Info),
            ApplyThumbnail,
            action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action));
    }

    private void ApplyThumbnail(WindowInfo window, BitmapSource bitmap)
    {
        WindowEntryViewModel? entry = _viewModel.FindEntry(window);
        if (entry is not null)
        {
            entry.Thumbnail = bitmap;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_isReady || !_closeOnFocusLost || _completed)
        {
            return;
        }

        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _completed = true;
            Close();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
            return;
        }

        bool searchHasFocus = ReferenceEquals(Keyboard.FocusedElement, SearchTextBox);
        if (!searchHasFocus)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            _viewModel.MoveSelection(1);
            WindowListBox.ScrollIntoView(_viewModel.SelectedEntry);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            _viewModel.MoveSelection(-1);
            WindowListBox.ScrollIntoView(_viewModel.SelectedEntry);
        }
    }

    private void OnWindowItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        WindowInfo? selected = _viewModel.SelectedWindow;
        if (selected is null)
        {
            _completed = true;
            Close();
            return;
        }

        _completed = true;
        Hide();

        // 让选择器先让出前台，待系统完成焦点切换后再移动并激活目标窗口。
        // 无论移动结果如何都必须关闭窗口：如果窗口停在隐藏状态，
        // 宿主会认为它仍然存在，之后按快捷键只会去激活一个看不见的窗口，
        // 表现为快捷键彻底失效。
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                WindowMoveResult result = _mover.MoveToMonitor(selected.Handle, _targetMonitor);
                if (!result.Success)
                {
                    _notify?.Invoke("Move Window Everywhere", result.Message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"移动窗口时发生异常：{ex}");
                _notify?.Invoke("Move Window Everywhere", "移动窗口失败，详情见日志。");
            }
            finally
            {
                Close();
            }
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>把选择器放到捕获到的目标显示器工作区内（按物理像素定位，兼容不同 DPI 与负坐标）。</summary>
    private void PlaceInsideTargetMonitor()
    {
        if (_targetMonitor is null)
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !Win32.GetWindowRect(handle, out RECT current))
        {
            return;
        }

        Int32Rect target = WindowPlacementCalculator.ComputeCenteredRect(
            _targetMonitor.WorkRect,
            current.Width,
            current.Height);

        if (!Win32.SetWindowPos(
                handle,
                IntPtr.Zero,
                target.X,
                target.Y,
                target.Width,
                target.Height,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED))
        {
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"选择器定位失败，Win32 错误码 {error}");
        }
    }
}
