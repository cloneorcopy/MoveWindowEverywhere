using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;

namespace MoveWindowEverywhere.ViewModels;

/// <summary>列表中的一行：缩略图、标题、进程名、PID、状态与图标。</summary>
public sealed class WindowEntryViewModel : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _iconResolved;
    private ImageSource? _thumbnail;

    public WindowEntryViewModel(WindowInfo info)
    {
        Info = info;
    }

    public WindowInfo Info { get; }

    public string Title => string.IsNullOrWhiteSpace(Info.Title) ? "（无标题）" : Info.Title;

    public string ProcessText => $"{Info.ProcessName} · PID {Info.ProcessId}";

    public string StateText => Info.State switch
    {
        WindowStateKind.Minimized => "最小化",
        WindowStateKind.Maximized => "最大化",
        _ => string.Empty,
    };

    /// <summary>窗口所在显示器编号，0 表示不标注（单显示器或无法确定）。</summary>
    public int MonitorIndex => Info.MonitorIndex;

    /// <summary>列表右侧展示的显示器标签，未标注时为空串。</summary>
    public string MonitorLabel => Info.MonitorLabel;

    /// <summary>窗口是否已经在目标显示器上，用于把标签显示成强调色。</summary>
    public bool IsOnTargetMonitor => Info.IsOnTargetMonitor;

    /// <summary>
    /// 窗口缩略图。截取在后台线程进行，完成后逐张回填；
    /// 尚未截取、窗口无法截取（最小化、无响应、渲染为纯色）时保持为 null，界面显示占位图。
    /// </summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value))
            {
                return;
            }

            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => _thumbnail is not null;

    public ImageSource? Icon
    {
        get
        {
            if (!_iconResolved)
            {
                _iconResolved = true;
                _icon = IconHelper.ToImageSource(Info.IconHandle);
            }

            return _icon;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 窗口选择器视图模型：维护窗口列表、搜索筛选与选中项。
/// 目标显示器由调用方在快捷键触发瞬间捕获后传入，选择过程中不再重新计算。
/// </summary>
public sealed class SelectorViewModel : INotifyPropertyChanged
{
    private readonly List<WindowEntryViewModel> _all = new();
    private readonly Dictionary<WindowInfo, WindowEntryViewModel> _entryByWindow = new();
    private string _searchText = string.Empty;

    public SelectorViewModel(MonitorInfo? targetMonitor)
    {
        TargetMonitor = targetMonitor;
    }

    public ObservableCollection<WindowEntryViewModel> Windows { get; } = new();

    public MonitorInfo? TargetMonitor { get; }

    public string TargetMonitorText => TargetMonitor?.ShortDescription ?? "未捕获到目标显示器";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSearchEmpty));
            ApplyFilter();
        }
    }

    public bool IsSearchEmpty => SearchText.Length == 0;

    public bool IsEmpty => Windows.Count == 0;

    public WindowEntryViewModel? SelectedEntry { get; set; }

    public WindowInfo? SelectedWindow => SelectedEntry?.Info;

    /// <summary>按窗口信息取回对应的列表行，供后台截取完成后回填缩略图。</summary>
    public WindowEntryViewModel? FindEntry(WindowInfo window) =>
        window is not null && _entryByWindow.TryGetValue(window, out WindowEntryViewModel? entry) ? entry : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetWindows(IEnumerable<WindowInfo> windows)
    {
        _all.Clear();
        _entryByWindow.Clear();

        foreach (WindowInfo window in windows)
        {
            var entry = new WindowEntryViewModel(window);
            _all.Add(entry);
            _entryByWindow[window] = entry;
        }

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        IReadOnlyList<WindowInfo> filtered = WindowSearcher.Search(_all.Select(static e => e.Info), SearchText);

        Windows.Clear();
        foreach (WindowInfo window in filtered)
        {
            if (_entryByWindow.TryGetValue(window, out WindowEntryViewModel? entry))
            {
                Windows.Add(entry);
            }
        }

        if (Windows.Count > 0)
        {
            if (SelectedEntry is null || !Windows.Contains(SelectedEntry))
            {
                SelectedEntry = Windows[0];
            }
        }
        else
        {
            SelectedEntry = null;
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SelectedEntry));
    }

    /// <summary>键盘上下移动选中项。</summary>
    public void MoveSelection(int delta)
    {
        if (Windows.Count == 0)
        {
            return;
        }

        int currentIndex = SelectedEntry is null ? -1 : Windows.IndexOf(SelectedEntry);
        int nextIndex = currentIndex + delta;
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }
        else if (nextIndex >= Windows.Count)
        {
            nextIndex = Windows.Count - 1;
        }

        SelectedEntry = Windows[nextIndex];
        OnPropertyChanged(nameof(SelectedEntry));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
