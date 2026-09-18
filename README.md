# Move Window Everywhere

<div align="center">

**一个零依赖、轻量级、常驻系统托盘的 Windows 桌面小工具：按下快捷键，一键将任意窗口瞬间瞬移至鼠标当前所在的显示器工作区中央。**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?logo=windows&logoColor=white)](#二系统要求)
[![Target Framework](https://img.shields.io/badge/.NET-8.0%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/Tests-139%20Passed-brightgreen.svg)](#十一单元测试)
[![Single File](https://img.shields.io/badge/Release-Single%20Executable%20(~63MB)-orange.svg)](#五发布命令)
[![Zero Telemetry](https://img.shields.io/badge/Telemetry-None%20(100%25%20Offline)-success.svg)](#十已知限制)

[English](README_en.md) | [简体中文](README.md)

</div>

整个工具只有一个可执行文件，不联网、不需要账户、不安装后台服务、不写入启动项。

---

## 一、功能说明

核心工作流：

1. 程序启动后不显示主窗口，只在系统托盘显示图标并后台运行。
2. 按下全局快捷键（默认 `Alt + Z`）。
3. 程序**立即**记录快捷键触发瞬间鼠标所在的显示器（记作「目标显示器」）。
4. 在目标显示器的工作区中央弹出窗口选择器，列出当前可移动的窗口。
5. 用鼠标双击或键盘 `Enter` 选中一个窗口。
6. 该窗口被移动到目标显示器工作区中央，保留原尺寸；若大于工作区则限制到工作区大小。
7. 选择器关闭，目标窗口被激活并成为前台窗口。

设计要点：

- 「鼠标所在位置」指**鼠标所在的显示器**，不是把窗口左上角挪到鼠标坐标。
- 目标显示器在**快捷键触发瞬间**就确定并保存，之后无论鼠标移到哪里、选择器被拖到哪台屏幕，都不会改变。这避免了点选过程中鼠标位置变化导致移动目标错误。
- 移动窗口使用窗口句柄（`HWND`），不依赖窗口标题定位，标题相同也不会认错窗口。

### 托盘菜单

右键点击托盘图标：

| 菜单项 | 作用 |
| --- | --- |
| 打开窗口选择器（当前快捷键） | 等同于按一次快捷键 |
| 设置… | 修改快捷键，以及开机自启、缩略图等全部开关 |
| 打开配置目录 | 在资源管理器中打开 `%LOCALAPPDATA%\MoveWindowEverywhere` |
| 关于 | 显示版本、当前快捷键、开机自启状态、配置与日志路径 |
| 退出 | 注销快捷键、移除托盘图标、释放资源后退出 |

双击托盘图标等同于「打开窗口选择器」。

### 窗口选择器

- **搜索框**：打开后自动聚焦并全选，直接输入即可过滤。
- **窗口列表**：显示窗口缩略图、图标、标题、进程名与 PID，并在右侧用标签标明该窗口**当前所在的显示器**（如「屏 2」）。
- **缩略图**：在后台线程逐张截取，边截边显示，不阻塞列表出现；最小化、无响应或渲染结果为纯色的窗口显示为「无预览」。可在设置中整体关闭。
- **目标显示器提示**：顶部显示本窗口将被移动到哪台显示器（含编号与设备名）。
- **空列表提示**：没有匹配窗口时显示提示文字。
- **键盘导航**：`↑` / `↓` 上下选择，`Enter` 确认，`Esc` 取消。
- **鼠标**：单击选中，双击确认。
- 默认**失去焦点即自动关闭**（相当于取消），避免遮挡工作区；该行为可通过配置项 `CloseSelectorOnFocusLost` 关闭。
- 选择器自身不会出现在待移动窗口列表中（本进程窗口已被过滤规则排除）。
- 每次打开都会**重新枚举一次**窗口，尽量反映当前状态。

搜索匹配规则（不区分大小写，多个关键词用空格分隔，需全部命中）：

1. 标题**以关键词开头**（优先级最高）
2. 标题**包含关键词**
3. 进程名**以关键词开头**
4. 进程名**包含关键词**

同一优先级内保持枚举顺序，即越靠前的匹配越优先显示。

### 移动窗口的完整逻辑

1. 再次确认 `HWND` 仍然有效（`IsWindow`），失效则提示「目标窗口已关闭或句柄失效」。
2. 读取当前窗口状态（普通 / 最大化 / 最小化）。
3. 若最小化或最大化，先 `SW_RESTORE` 恢复为普通窗口，并等待恢复完成。
4. 读取恢复后的窗口尺寸。
5. 计算目标矩形：居中对齐到目标工作区，尺寸不变。
6. 若窗口宽或高大于工作区，则限制到工作区尺寸。
7. `SetWindowPos` 应用位置与尺寸（`SWP_NOZORDER | SWP_NOACTIVATE`）。
8. 移动后再校验一次实际位置，若被目标程序修正或拒绝，则用 `ClampToWorkArea` 夹紧到工作区内并重设一次，保证窗口完全落在目标显示器工作区内。
9. 若原始状态是最大化，则在目标显示器上重新最大化。
10. 激活目标窗口（`SetForegroundWindow`，失败时回退到 `AttachThreadInput` + `BringWindowToTop` 方式重试）。
11. 失败时通过托盘气泡给出简洁可读的提示。

### 状态处理约定

| 原状态 | 移动后状态 |
| --- | --- |
| 普通窗口 | 普通窗口，居中于目标工作区 |
| 最大化窗口 | 在目标显示器上重新最大化 |
| 最小化窗口 | **恢复为可见的普通窗口**（不保持最小化） |

最小化窗口默认恢复为普通窗口，这是有意选择：把最小化的应用移到另一块屏幕通常就是为了接着用它。窗口列表是否包含最小化窗口由配置项 `IncludeMinimizedWindows` 控制，默认为「包含」。

### 设置窗口

托盘菜单 → **设置…**，一个窗口内可调整全部选项：

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| 全局快捷键 | `Alt + Z` | 点击「重新录制」后直接按下新组合，即时校验是否被占用 |
| 随 Windows 启动 | 关 | 在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 登记一条指向本程序的值，**不需要管理员权限** |
| 窗口列表包含最小化窗口 | 开 | 关闭后最小化窗口不再进入候选列表 |
| 选择器失去焦点时自动关闭 | 开 | 关闭后选择器会一直停留，需要 `Esc` 或点选才会消失 |
| 快捷键被占用时自动改用备用组合 | 开 | 见下面的「快捷键被占用时的处理」 |
| 窗口列表显示缩略图 | 开 | 关闭后只显示图标，打开列表更快 |

除快捷键外的开关都只在点击「保存」后生效，取消不会留下任何副作用。

### 快捷键被占用时的处理

`RegisterHotKey` 只能有一个进程持有某个组合。程序按两级策略处理冲突：

1. **优先注册用户设定的组合。** 可用时不做任何改动。
2. **被占用且开启了自动换用**，依次尝试备用组合，命中即用：
   - 第一组：**保留按键，只换修饰键组合**。`Alt + Z` 依次尝试 `Alt + Shift + Z`、`Ctrl + Alt + Z`、`Ctrl + Shift + Z`、`Ctrl + Alt + Shift + Z`。按键是用户最容易记住的部分，保留它能让肌肉记忆继续管用，同时也避免去抢占其他程序的 `Alt` + 字母菜单助记符。
   - 第二组：仍然不可用时，才保留用户设定的修饰键、改换按键，且只从 `X`、`J`、`K`、`Q`、`N`、`B` 这几个极少被用作菜单助记符的字母里挑。
   - 不使用 `Win` 键组合：它被系统外壳大量占用，抢注会让用户失去系统快捷键。

换用结果会写入配置文件、刷新托盘提示文字，并通过气泡明确告知用户改用了哪个组合，下次启动直接使用可用组合，不会再走一遍失败流程。可以在设置中关闭自动换用，此时行为退回「提示用户手动更换」。

---

## 二、系统要求

- **操作系统**：Windows 10 / Windows 11（x64）。
- **运行已发布版本**：无需安装 .NET 运行时。发布产物是 self-contained 单文件，内含所需运行时。
- **从源码构建**：需要 .NET SDK 8.0 或更高版本（本项目在 9.0.316 上验证通过，目标框架为 `net8.0-windows`）。
- 需要至少一块显示器；要发挥完整效果建议两块及以上。

---

## 三、开发环境

- 语言 / 框架：C# + .NET 8（`net8.0-windows`）+ WPF；托盘使用 `System.Windows.Forms.NotifyIcon`。
- Win32 交互：全部通过 P/Invoke（`user32.dll` / `dwmapi.dll` / `shcore.dll` / `kernel32.dll`），无第三方 NuGet 包。
- DPI：应用清单声明 **Per-Monitor V2**，所有坐标运算均基于物理像素，不做主屏假设。
- 平台：`x64`（csproj 中已固定 `PlatformTarget`）。
- 单实例：命名互斥量 `Local\MoveWindowEverywhere.SingleInstance.v1`。
- 应用清单（`Resources/app.manifest`）：声明 DPI 感知、`supportedOS`（Windows 8.1 与 Windows 10/11，按从旧到新排列）与 `asInvoker` 权限级别。

> **清单改动需注意**：`assemblyIdentity` 的 `processorArchitecture` 只能取 `x86` / `amd64` / `ia64` / `msil` / `*` 五个值，**写成 `x64` 会让 Windows 报「应用程序的并行配置不正确」而直接拒绝启动**（`x64` 是 Visual Studio 的平台名，不是 SxS 架构名）。本项目曾因该值写错导致发布产物无法双击运行，已修正为 `amd64`。

### 目录结构

```text
Move window everywhere/
├─ MoveWindowEverywhere.sln
├─ README.md
├─ .gitignore
├─ src/
│  └─ MoveWindowEverywhere/
│     ├─ MoveWindowEverywhere.csproj
│     ├─ App.xaml / App.xaml.cs          应用入口、单实例、服务组装
│     ├─ GlobalUsings.cs
│     ├─ Native/
│     │  ├─ Win32.cs                     P/Invoke 声明与常量
│     │  └─ Win32Types.cs                结构体与委托
│     ├─ Models/                         AppSettings / HotkeySettings / MonitorInfo / WindowInfo / WindowFilterOptions
│     ├─ Services/
│     │  ├─ AppPaths.cs                  配置与日志路径
│     │  ├─ AppLogger.cs                 文件日志
│     │  ├─ SettingsService.cs           配置读写（JSON）
│     │  ├─ StartupRegistrar.cs          开机自启登记（HKCU Run 项，键路径可注入便于测试）
│     │  ├─ MonitorService.cs            显示器枚举与坐标查询
│     │  ├─ ProcessNameResolver.cs       进程名解析（含缓存）
│     │  ├─ WindowEnumerator.cs          EnumWindows 采集窗口快照
│     │  ├─ WindowFilterPolicy.cs        全部过滤规则（集中管理）
│     │  ├─ WindowSearcher.cs            搜索筛选与排序
│     │  ├─ WindowMonitorAnnotator.cs    标注每个窗口当前所在显示器（纯逻辑）
│     │  ├─ WindowPlacementCalculator.cs 居中与夹紧计算（纯逻辑）
│     │  ├─ WindowMover.cs               移动与激活
│     │  ├─ WindowThumbnailService.cs    窗口缩略图截取（PrintWindow + GDI）
│     │  ├─ ThumbnailCapturePolicy.cs    缩略图候选筛选与尺寸计算（纯逻辑）
│     │  ├─ ThumbnailLoader.cs           后台逐张截取并回填界面
│     │  ├─ HiddenMessageWindow.cs       隐藏消息窗口（接收 WM_HOTKEY / 唤醒消息）
│     │  ├─ HotkeyService.cs             RegisterHotKey 封装
│     │  ├─ HotkeyFallbackPlanner.cs     被占用时的备用组合规划（纯逻辑）
│     │  ├─ IconHelper.cs / IconFactory.cs  图标读取与运行时绘制托盘图标
│     │  └─ TrayIconService.cs           托盘图标与右键菜单
│     ├─ ViewModels/                     SelectorViewModel / SettingsViewModel
│     ├─ Views/                          SelectorWindow / SettingsWindow
│     └─ Resources/app.manifest          Per-Monitor V2 DPI、supportedOS
└─ tests/
   └─ MoveWindowEverywhere.Tests/        xunit 单元测试（139 个用例）
```

---

## 四、编译命令

在 `Move window everywhere` 目录下执行：

```bash
# 查看环境
dotnet --info

# 还原依赖
dotnet restore

# Release 构建
dotnet build -c Release

# 运行单元测试
dotnet test -c Release

# 快速运行测试（跳过重新构建）
dotnet test -c Release --no-build
```

也可直接用 Visual Studio 打开 `MoveWindowEverywhere.sln`，切换为 `Release` + `x64` 后构建。

---

## 五、发布命令

推荐使用 self-contained 单文件发布，产物为单个 `.exe`，目标机器无需安装 .NET：

```bash
dotnet publish src/MoveWindowEverywhere/MoveWindowEverywhere.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o publish/win-x64
```

产物路径：`publish/win-x64/MoveWindowEverywhere.exe`（约 63 MB，含运行时，已启用压缩）。

若只想发布框架依赖版本（体积小，但目标机器需装 .NET 8 桌面运行时）：

```bash
dotnet publish src/MoveWindowEverywhere/MoveWindowEverywhere.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64-fd
```

单文件发布在本项目中未出现问题，因此不需要退化为普通多文件发布。

---

## 六、使用方法

1. 将 `publish/win-x64/MoveWindowEverywhere.exe` 放到任意固定目录（例如 `D:\Tools\MoveWindowEverywhere\`）。
2. 双击运行。程序不会显示窗口，只会在托盘出现一个蓝色图标。
3. 把鼠标移到目标显示器上，打开（或切到）你希望搬移的那个窗口，或者在任意位置按下 **`Alt + Z`**。
4. 选择器会出现在鼠标当时所在显示器的中央，顶部提示目标显示器编号，列表里每一行带缩略图与所在屏标签。
5. 输入关键字过滤，用 `↑` / `↓` 选中，按 `Enter`；或直接双击目标项。
6. 窗口被移动到目标显示器并激活。按 `Esc` 或点击选择器之外可取消。

> 想让程序开机自动运行：右键托盘图标 → **设置…** → 勾选 **随 Windows 启动** → 保存。
> 程序会在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下写入一条指向当前 exe 的值，不需要管理员权限。
> 如果之后把 exe 移到了别的目录，下次启动时程序会自动把启动项修正到新路径。

---

## 七、快捷键与设置修改方法

方式一（推荐）：

1. 右键托盘图标 → **设置…**
2. 点击 **重新录制**，直接按下你想用的组合键（例如 `Ctrl + Alt + Shift + K`）。
3. 至少需要 **一个修饰键**（Ctrl / Alt / Shift / Win）+ **一个普通按键**，否则会提示无效。
4. 点击 **保存**。此时会立刻尝试注册：
   - 成功：快捷键立即生效并写入配置文件。
   - 被占用：窗口内显示「快捷键 … 已被其他程序占用，请换一个组合。」，**原快捷键自动恢复**，不会静默失败。
5. 想恢复默认，点 **恢复默认快捷键** 再保存。
6. 同一个窗口里还可以勾选随 Windows 启动、缩略图等开关，具体见第一节的「设置窗口」。

> 需要注意：快捷键是在设置窗口里**即时注册**的，所以录制过程中预览过的组合会真实生效；如果最后点了取消，程序会把原来的组合注册回去。其余开关只在保存后生效。

方式二：直接编辑 `%LOCALAPPDATA%\MoveWindowEverywhere\settings.json`（需先退出程序，否则退出时会覆盖你的修改）。

```json
{
  "Version": 1,
  "Hotkey": {
    "Modifiers": 16385,
    "VirtualKey": 90
  },
  "StartWithWindows": false,
  "IncludeMinimizedWindows": true,
  "CloseSelectorOnFocusLost": true,
  "AutoFallbackHotkey": true,
  "ShowThumbnails": true
}
```

`Modifiers` 为位标志（`Alt=1`、`Ctrl=2`、`Shift=4`、`Win=8`、`NoRepeat=16384`），`VirtualKey` 为 Win32 虚拟键码。上面这组值就是默认的 `Alt + Z`（`16385` 即 `Alt` 加 `NoRepeat`，`90` 即 Z 键）。注意配置文件里只保存这些原始字段，`DisplayText` 等只读属性不会写进去。

> 如果启动时快捷键注册失败（例如已被其它程序占用，Win32 错误码 1409），程序**不会退出**：开启自动换用时它会自己换一个可用的组合并通知你；关闭自动换用时仍会常驻托盘并通过气泡提示快捷键未生效，此时可以从托盘菜单打开选择器，或手动更换快捷键。

---

## 八、窗口过滤规则

全部规则集中在 `Services/WindowFilterPolicy.cs`，UI 代码不参与判断。以下是默认（全部开启）的排除规则：

| # | 规则 | 说明 |
| --- | --- | --- |
| 1 | 句柄无效 | `HWND` 为 0 |
| 2 | 窗口不可见 | `IsWindowVisible` 为假 |
| 3 | 没有有效标题 | 标题为空或只有空白字符 |
| 4 | 本程序自身窗口 | 进程 ID 与本工具相同（选择器不会出现在列表里） |
| 5 | 被 DWM cloaking 的窗口 | 如已挂起的 UWP 应用后台窗口 |
| 6 | `WS_EX_TOOLWINDOW` 工具窗口 | 浮动工具条、部分输入法窗口 |
| 7 | `WS_EX_NOACTIVATE` 窗口 | 无法激活的窗口，移过去也无法使用 |
| 8 | 有 owner 的从属窗口 | 避免大量弹窗与内部窗口污染列表 |
| 9 | 系统窗口（按类名） | `Shell_TrayWnd`（任务栏）、`Shell_SecondaryTrayWnd`（副屏任务栏）、`Progman` / `WorkerW`（桌面）、`NotifyIconOverflowWindow`（通知溢出区）、`XamlExplorerHostIslandWindow`、`ForegroundStaging` 等 |
| 10 | 最小化窗口 | **默认不排除**，由配置项 `IncludeMinimizedWindows` 控制（默认 `true`） |

其它容错行为：

- 进程名读取失败不会让整个枚举失败，该窗口显示为 `未知进程`。
- 单个窗口采集信息抛异常时只记录日志并跳过，不影响其余窗口。
- 刷新列表时窗口已关闭的情况由「句柄无效 / 窗口已关闭」路径处理，不会崩溃。

---

## 九、管理员权限限制

Windows 的 UIPI（用户界面特权隔离）限制**低完整性级别的进程不能操作高完整性级别的窗口**。因此：

- 如果本工具以**普通权限**运行，而目标窗口属于以**管理员权限运行的程序**（例如任务管理器、注册表编辑器、以管理员启动的终端），则：
  - 该窗口**仍会出现在列表中**（枚举通常可以成功）；
  - 但 `SetWindowPos` 会失败并返回 `ERROR_ACCESS_DENIED (5)`，此时程序会提示：
    **「目标窗口权限高于本程序或句柄已失效，请尝试以管理员身份运行本工具。」**
- 解决办法：右键 exe → **以管理员身份运行**。以管理员身份运行时，可以移动普通权限窗口与提权窗口。

本工具**不会**尝试绕过 Windows 的权限保护（不做注入、不修改目标进程、不请求调试权限）。

另外有一类与权限无关的情况：少数程序在收到外部移动请求后会立刻把窗口修正回原位，或在 `WM_WINDOWPOSCHANGING` 中拒绝外部尺寸。本工具会对这种窗口做一次夹紧重试，但仍可能无法保持位置，这属于目标程序自身行为，无法在工具侧解决。

---

## 十、已知限制

1. **托盘图标为运行时绘制**。项目不附带 `.ico` 资源文件（避免引入二进制资源与版权问题），启动时用 GDI+ 绘制一个简洁图标。因此托盘图标在系统托盘小尺寸下观感较朴素。
2. **缩略图受目标窗口配合程度影响**。缩略图通过 `PrintWindow` 请目标窗口自己渲染，因此：
   - 最小化窗口没有可渲染内容，显示为「无预览」；
   - 无响应的窗口会被直接跳过（不等待），显示为「无预览」；
   - 使用硬件覆盖层或受保护内容（部分播放器、DRM 窗口）的窗口可能返回纯色，程序检测到整幅纯色后同样退回「无预览」，而不是显示一块黑；
   - 一次最多截取列表前 24 个窗口，超出的显示「无预览」，这是为控制内存与耗时有意设置的上限。
3. **开机自启写入的是当前进程路径**。通过 `dotnet run` 或 `dotnet exec` 启动时，进程路径指向 `dotnet.exe`，写进启动项没有意义，此时设置里的开关会被禁用并说明原因。请直接运行发布后的 exe。
4. **单实例唤醒的端到端验证不充分**。重复启动时唤醒已有实例的逻辑（`EnumWindows` 定位隐藏窗口 → `SendMessageTimeout` → 失败回退 `PostMessage`）已实现并被单元测试覆盖消息分发链路，但在本机验证环境中后台进程会被冻结，无法完成「两个真实实例并发」的端到端验证。若唤醒失败，程序会弹出一个提示框，不会静默无响应。
5. **快捷键冲突的自动换用有边界**。程序最多尝试 11 个候选组合（原组合 + 4 组修饰键变体 + 6 个备用按键），全部不可用时仍会提示需要手动指定。备用按键只从极少被用作菜单助记符的字母里挑，因此候选数量有限；如果你需要某个特定组合，仍应在设置里手动指定。自动换用可以在设置中关闭。
6. **前台激活可能被系统拒绝**。`SetForegroundWindow` 受 Windows 前台锁限制：如果触发快捷键时本工具自身不在前台，或运行在无交互输入的桌面会话中，该调用可能失败并返回错误码 0（无具体错误信息）。此时程序已回退到 `AttachThreadInput` + `BringWindowToTop` 方式重试。窗口的**移动本身仍然成功**，只是可能不会立刻跳到最前面；日志中会出现「移动成功，但未能设置为前台窗口」的记录。
7. **提权窗口无法移动**（见上一节），这是 Windows 的安全边界，不是缺陷。
8. **无边框窗口**可以移动，但某些游戏 / 全屏独占应用会忽略外部尺寸变更。
9. 不提供联网、遥测、账户、自动更新功能。

配置与日志位置：

```text
%LOCALAPPDATA%\MoveWindowEverywhere\settings.json
%LOCALAPPDATA%\MoveWindowEverywhere\logs\
```

日志会记录启动、快捷键注册结果与自动换用、开机自启登记结果、触发来源、选择器打开与窗口数量、`SetWindowPos` 失败及 Win32 错误码、位置夹紧、缩略图跳过原因、设置变更等信息，不记录敏感数据。P/Invoke 失败时一律保留 Win32 错误码便于诊断。

---

## 十一、单元测试

测试项目：`tests/MoveWindowEverywhere.Tests`（xunit）。覆盖范围：

| 测试文件 | 覆盖内容 |
| --- | --- |
| `WindowFilterPolicyTests.cs` | 全部过滤规则（不可见、无标题、工具窗口、从属窗口、系统类名、自身进程、最小化开关等） |
| `WindowSearcherTests.cs` | 标题 / 进程名匹配、多关键词、排序优先级、空查询 |
| `WindowPlacementCalculatorTests.cs` | 居中计算、尺寸超限限制、**负坐标显示器**、左右与上下排列、夹紧逻辑 |
| `WindowMoverTests.cs` | 真实窗口移动、最小化窗口恢复、最大化窗口处理、**已关闭句柄错误路径**、目标显示器为空的错误路径 |
| `HotkeySettingsTests.cs` | 快捷键有效性判定、修饰键位标志、默认值与显示文本 |
| `HotkeyFallbackPlannerTests.cs` | 首选组合排在最前、按键不变优先于改按键、候选不重复不超上限、不使用 Win 键与系统保留键 |
| `SettingsServiceTests.cs` | 配置持久化、缺失字段默认值、损坏文件容错、**只读计算属性不写入配置文件**、含冗余字段的旧配置仍可读取、新增开关的默认值与读写 |
| `SettingsViewModelTests.cs` | 全部开关的收集与变更通知、无法登记开机自启时该开关保持关闭、结果对象为副本 |
| `SelectorViewModelTests.cs` | 选择器筛选、上下移动、选中项维护、空列表 |
| `WindowMonitorAnnotatorTests.cs` | 多显示器下标注窗口所在显示器、**单显示器不标注**、句柄无匹配时不猜测、句柄为零跳过、空参数不抛异常、未标注时标签为空 |
| `ThumbnailCapturePolicyTests.cs` | 最小化 / 不可见 / cloaked / 空句柄不截图、候选数量上限与顺序、缩略图尺寸保持宽高比且至少 1 像素 |
| `ThumbnailLoaderTests.cs` | 只截合格窗口、失败不回填、单个窗口异常不中断整轮、运行期间重复启动被忽略、释放后立即停止且不再回填 |
| `StartupRegistrarTests.cs` | 命令行加引号、dotnet 宿主不可登记、带引号 / 不带引号 / 带参数三种写法的匹配、开启与关闭幂等、指向其他程序时不算已启用、关闭配置不删除现有登记项、换目录后自动修复 |
| `HiddenMessageWindowTests.cs` | 隐藏窗口创建、`WM_HOTKEY` 与唤醒消息的分发与消息泵行为、窗口类名参数化隔离 |
| `DispatcherMessageLoopTests.cs` | WPF `Dispatcher` 消息循环下两种触发消息的实际分发 |

运行：

```bash
dotnet test -c Release
```

测试全部使用临时目录与唯一窗口类名，不写入真实配置、也不与开发机上正在运行的实例相互干扰，因此本机跑着本工具时同样可以正常执行测试。

开机自启的测试会真实读写注册表，但只使用每次动态生成的临时子键 `HKCU\Software\MoveWindowEverywhere.Tests\<GUID>`，测试结束后整个临时键被删除，不会触碰真实的启动项。

## 十二、本机构建验证记录

### 构建 / 测试 / 发布

| 项目 | 结果 |
| --- | --- |
| `dotnet --version` | `9.0.316` |
| `dotnet build -c Release` | 已成功生成，`0 个警告`、`0 个错误`，退出码 `0` |
| `dotnet test -c Release` | `已通过! - 失败: 0，通过: 139，已跳过: 0，总计: 139`，退出码 `0` |
| `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` | 成功，退出码 `0` |
| 发布产物 | `publish/win-x64/MoveWindowEverywhere.exe`（约 63 MB，单文件 self-contained） |
| 内嵌清单静态校验 | `processorArchitecture="amd64"` |

### 实际运行验证

发布产物已实际启动运行，判据取自程序自身的日志（`%LOCALAPPDATA%\MoveWindowEverywhere\logs\app-20260918.log`）：

```text
[10:12:46] [INFO] 已注册全局快捷键 Ctrl + Alt + M
[WARN] SetForegroundWindow 失败，句柄 0xC0EE8，Win32 错误码 0，尝试线程附加方式激活
[WARN] 窗口 0xC0EE8 移动成功，但未能设置为前台窗口
[10:21:57] [INFO] 启动 Move Window Everywhere（PID 50528，.NET 8.0.29）
[10:21:59] [INFO] 已注册全局快捷键 Alt + Z
[13:36:01] [INFO] 启动 Move Window Everywhere（PID 41928，.NET 8.0.29）
[13:36:02] [INFO] 已注册全局快捷键 Alt + Z
```

- 发布产物可正常启动并常驻，启动记录之后没有出现退出记录。
- 全局快捷键注册成功；默认组合调整为 `Alt + Z` 后已重新验证通过；最近一次（13:36）的启动走的是新增的「自动换用备用组合」注册路径，同样成功。
- 通过快捷键触发选择并执行了多次真实窗口移动，`SetWindowPos` 均成功（日志中「移动成功」）。
- `SetForegroundWindow` 在无交互输入的验证环境下被系统前台锁拒绝（错误码 0），已按设计回退重试并记录日志，不影响移动结果。
- 开机自启所用的注册表位置已单独验证可写：向 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入一个临时值名并读回、删除均成功，验证结束后该临时值已清除，**未留下任何启动项**。

判据说明：本机环境中 `tasklist` 等进程查询看不到该 GUI 进程，因此「启动是否成功」一律以程序自身日志为准，即出现启动记录且其后没有紧跟退出记录。

### 已修复的启动故障（清单 SxS 错误）

首次交付的发布产物在目标机器上双击时报错：

> 应用程序无法启动，因为应用程序的并行配置不正确。有关详细信息，请参阅应用程序事件日志，或使用命令行 sxstrace.exe 工具。

原因：`Resources/app.manifest` 中 `<assemblyIdentity>` 的 `processorArchitecture` 被写成 `x64`。该属性只接受 `x86` / `amd64` / `ia64` / `msil` / `*`，`x64` 是 Visual Studio 的平台名而非 SxS 架构名，非法取值使 Windows 无法解析清单并直接拒绝启动。

修复：改为 `amd64`，同时按从旧到新重排了 `supportedOS`。修复后静态校验 `grep -a -o 'processorArchitecture="[^"]*"' publish/win-x64/MoveWindowEverywhere.exe` 返回 `amd64`，启动验证通过。

### 已修复的界面卡死故障（枚举窗口阻塞）

现象：按下快捷键后程序完全没有反应，托盘图标的右键菜单也点不动，但程序既没有崩溃也没有退出。日志恰好停在「触发来源：xxx」这一行，之后什么都没有。

原因：`WindowEnumerator` 用同步的 `SendMessage(WM_GETICON)` 向每个窗口索取图标。`SendMessage` 会一直等到目标窗口把消息处理完，一旦列表里存在无响应的窗口（后台挂起的程序、被冻结的 UWP 应用），这个调用就会永久阻塞。而枚举执行在 UI 线程上，UI 线程被占住之后，选择器无法创建、托盘菜单也无法响应。日志停在「触发来源」正是因为下一条日志写在枚举完成之后。

修复：

- 图标获取全部改为带超时的 `SendMessageTimeout`（`SMTO_ABORTIFHUNG`，120ms），并用 `IsHungAppWindow` 先做零成本判断，窗口已无响应就直接跳过消息查询、回退到窗口类图标（纯查询，不会阻塞）。
- 枚举耗时写入日志，日后若某次明显偏长即可直接发现。
- 一并修掉两个会让快捷键「彻底失效」的隐患：残留的隐藏选择器窗口改为先关闭再重建；确认选择后无论移动成功与否都关闭窗口。
- 新增单元测试守住「枚举必须在有限时间内返回」这条底线，防止将来有人改回同步调用。

调试用日志与配置均写入 `%LOCALAPPDATA%\MoveWindowEverywhere\`，不污染项目目录。

### 窗口列表标注所在显示器

需求：在选择器里不知道某个窗口当前在哪块屏上，来回切换容易选错。

实现：新增纯逻辑服务 `WindowMonitorAnnotator`，在枚举完成后把每个窗口的 `MonitorHandle` 与 `MonitorService` 返回的显示器列表比对，写入 `MonitorIndex` 与 `IsOnTargetMonitor`；界面模板据此在每行右侧显示「屏 N」标签，已在目标显示器上的窗口标签用强调色标出。

三条取舍：

- **显示器只有一台时不做任何标注**。此时「屏 1」是废话，只会增加噪音，`Annotate` 直接返回。
- **句柄匹配不上时不猜测**。查不到对应显示器就保持未标注状态，标签折叠隐藏，不拿编号最小的显示器顶替。
- **不参与窗口过滤与排序**。标注只影响展示，候选窗口集合与排序规则维持原样，避免因为多一块屏就改变用户已经熟悉的选择顺序。

因为 `Annotate` 不碰任何 Win32 调用、只做集合比对，它可以完全用单元测试覆盖（见第十一节 `WindowMonitorAnnotatorTests.cs` 六个用例），不需要真实多屏环境。

### 补齐四项曾标注为「未实现」的功能

本节记录把已知限制中四项从未实现状态补齐的过程。每一节都遵循同一条原则：**先看这个功能会不会把界面拖死**，再决定怎么写。

#### 一、随 Windows 启动（`StartupRegistrar`）

原来配置项 `StartWithWindows` 只被持久化、从不生效，设置窗口里是一个禁用的勾选框，属于「文档写了但没做」。

实现选择 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 而不是启动文件夹：写 HKCU 不需要管理员权限，也不会弹提权窗口；写注册表值比创建并解析 `.lnk` 少一层依赖。

三个容易出错的点：

- **路径必须加引号**。不加引号时，含空格的路径会被 Windows 当成「可执行文件名 + 参数」，启动项静默失效。
- **`dotnet run` 启动时不能登记**。这种情况下 `Environment.ProcessPath` 指向 `dotnet.exe`，写进启动项毫无意义。此时 `CanRegister` 为假，设置里的开关整体禁用并说明原因。
- **读取时要容忍四种历史写法**：带引号、不带引号、路径含空格但未加引号、后面还跟着参数。匹配逻辑逐一尝试候选路径，避免把一条正确的启动项误判为「指向别的程序」。

启动时的同步策略是有意不对称的：配置为开启时确保登记项存在且指向当前 exe（程序被移动过目录也能自动修复）；配置为关闭时**什么都不做**，不删除既有登记项——那可能是用户手工添加的。

注册表路径与值名都可在构造时注入，因此全部 12 个用例都跑在临时子键 `HKCU\Software\MoveWindowEverywhere.Tests\<GUID>` 上，不触碰真实启动项。

#### 二、快捷键被占用时自动换用（`HotkeyFallbackPlanner`）

原来只能提示用户手动更换。现在按两级策略：先**保留按键只换修饰键**（`Alt + Z` → `Alt + Shift + Z` → `Ctrl + Alt + Z` → …），全部不可用才**保留修饰键换按键**，且只从 `X`、`J`、`K`、`Q`、`N`、`B` 这几个极少被用作菜单助记符的字母里挑。

顺序这样定有两条理由：按键是用户最容易记住的部分，保留它肌肉记忆仍然管用；同时不会去抢占其他程序的 `Alt` + 字母菜单助记符。`Win` 键组合被直接排除——它被系统外壳大量占用，抢注会让用户失去系统快捷键。

实现上有一个必须注意的细节：`HotkeyService` 原来的 `TryRegister` 在失败时会尝试把上一个组合注册回来（避免启动后完全没有可用快捷键）。自动换用的循环里必须关掉这个回滚——否则每一次失败的尝试都会把中间态组合注册上去，干扰后续判断。为此把注册逻辑抽成 `TryRegisterCore(settings, restoreOnFailure)`，只有首次尝试带回滚。

自动换用可以关闭；开启时换用结果会写入配置、刷新托盘提示文字，并通过气泡明确告知用户改用了哪个组合。

#### 三、窗口列表缩略图（`WindowThumbnailService` + `ThumbnailLoader`）

用 `PrintWindow` 请目标窗口把自己渲染到内存位图上，再等比缩放到缩略图尺寸。这个功能最容易踩的恰恰是上一节刚修过的那个坑——`PrintWindow` 本质上是向目标窗口发消息并等它渲染完，与 `SendMessage` 一样会被无响应的窗口拖住。因此：

- **绝不放在 UI 线程上**。截取跑在后台任务里，每张完成后通过 `Dispatcher` 回填属性，列表骨架先出现、缩略图逐张浮现。
- **截取前先 `IsHungAppWindow` 判断**，窗口已无响应直接跳过，连等待都省掉。
- **限制缓冲区大小**。一个 4K 最大化窗口的 32 位位图接近 33 MB，几十个窗口叠加足以吃光内存，因此宽高都设了上限（1280 × 800），超出部分裁掉；一次最多截 24 个窗口。
- **所有 GDI 对象在 `finally` 里释放**。DC、位图、位图的选中状态漏掉任何一个，反复截图后会耗尽进程的 GDI 句柄（上限通常一万个）。
- **全黑检测**。目标窗口没有真正画出内容时会得到一块纯色，抽样比对后判定为空白并退回「无预览」，不显示黑块。截取得到的 `BitmapSource` 一律 `Freeze()`，才能安全跨线程交给 UI。

截取函数通过构造参数注入，`ThumbnailLoader` 因此可以完全不碰 Win32 地测试「哪些窗口会被截」「失败会不会拖垮整轮」「取消后会不会继续截」这三件事（见第十一节 8 个用例）。

#### 四、把全部配置项接进设置界面（`SettingsWindow` / `SettingsViewModel`）

原来设置窗口只能改快捷键，`IncludeMinimizedWindows`、`CloseSelectorOnFocusLost` 这类选项只能手改 JSON，文档写着「可通过配置项关闭」但用户找不到入口。

现在的设置窗口一个界面管全部：快捷键（录制 + 恢复默认）、随 Windows 启动、包含最小化窗口、失焦即关闭、自动换用备用快捷键、显示缩略图。窗口类因此从 `HotkeySettingsWindow` 更名为 `SettingsWindow`，视图模型从 `HotkeySettingsViewModel` 更名为 `SettingsViewModel`（旧的三个文件已删除）。

职责划分保持清晰：

- 快捷键必须在窗口内**即时注册校验**（`RegisterHotKey` 是全局状态），因此预览过的组合会真实生效；点取消时由宿主把原组合注册回去。
- 其余开关只收集选择，等宿主收到 `DialogResult` 之后才落盘生效，取消不留下任何副作用。
- 快捷键**没改动时不再重复注册**。否则一次「只想改开关」的保存会因为此刻组合恰好被占用而失败。
- 开机自启写注册表失败时，把配置回滚到注册表的真实状态，不让配置文件说谎。

### 尚未验证的项

- 多显示器 / 不同 DPI / 负坐标显示器等场景仍需要在真实多屏环境中确认。
- 单实例唤醒（第二个实例唤起已有实例）在本机验证环境中后台进程会被冻结，未能端到端验证；消息分发链路已由单元测试覆盖（见第十节第 4 点）。
- 缩略图的实际观感（内容是否正确、四种退回「无预览」的情形是否都符合预期）需要在真实桌面环境中确认。
- 开机自启的「注销后自动启动」这一环需要重新登录才能确认真实生效；本轮只验证了注册表位置可写、程序写入逻辑有 12 个用例覆盖。
- 快捷键冲突自动换用的真实触发需要另一个程序先占用组合后确认。
