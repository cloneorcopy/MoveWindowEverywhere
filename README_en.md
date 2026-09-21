# Move Window Everywhere

<div align="center">

**A zero-dependency, ultra-lightweight Windows desktop utility to instantly move any window to the monitor where your cursor rests.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?logo=windows&logoColor=white)](#system-requirements)
[![Target Framework](https://img.shields.io/badge/.NET-8.0%20(WPF)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/Tests-139%20Passed-brightgreen.svg)](#unit-tests)
[![Single File](https://img.shields.io/badge/Release-Single%20Executable%20(~63MB)-orange.svg)](#build--publish)
[![Zero Telemetry](https://img.shields.io/badge/Telemetry-None%20(100%25%20Offline)-success.svg)](#known-limitations--design-philosophy)

[English](README_en.md) | [简体中文](README.md)

</div>

---

## Overview

**Move Window Everywhere** lives quietly in your Windows system tray. Press a global hotkey (default `Alt + Z`), pick any window from the popup selector, and it instantly teleports to the **center of the work area on whichever monitor your mouse cursor was on at the exact moment of the keystroke**.

- **Single Executable**: Ships as a self-contained single `.exe` (~63 MB). No .NET runtime installation required.
- **Zero Overhead**: No background daemon services, no web requests, no telemetry. The only registry write is the opt-in `Run` entry, and only when you turn on "Start with Windows".
- **DPI-Aware & Multi-Monitor**: Fully supports **Per-Monitor V2 DPI Awareness**, mixed scaling ratios (100%, 125%, 150%, 200%), and negative coordinate monitor layouts.

---

## Core Workflow

```text
[Mouse on Target Monitor] + [Press Alt + Z]
                 │
                 ▼
 1. Instantly capture cursor monitor (Target Monitor)
                 │
                 ▼
 2. Popup Window Selector at the center of Target Monitor
                 │
                 ▼
 3. Search / Select desired window (Keyboard ↑/↓/Enter or Mouse Double-Click)
                 │
                 ▼
 4. Selected window moves to Target Monitor's work area (Centered, clamped if oversized)
                 │
                 ▼
 5. Target window activated & brought to foreground; Selector closes automatically
```

### Key Design Decisions

- **Monitor-based targeting, not mouse coordinates**: "Cursor location" means the **monitor** where the cursor sits, not snapping the window top-left corner directly under the cursor. Windows are always neatly centered within the monitor's work area (respecting taskbar boundaries).
- **Instant snapshot at keystroke**: The target monitor is determined **at the exact millisecond the hotkey is triggered**. Moving your mouse afterwards or dragging the selector window around will never change the target destination.
- **HWND-based identity**: Windows are tracked by their Win32 handle (`HWND`), not title text. Multiple windows with identical titles (e.g. several browser or terminal windows) will never be confused.

---

## Features

### 1. System Tray Menu

Right-click the tray icon to access:

| Menu Item | Action |
| --- | --- |
| **Open Window Selector (`<Hotkey>`)** | Equivalent to pressing the global hotkey |
| **Settings…** | Change the hotkey and all behaviour switches (autostart, thumbnails, …) |
| **Open Config Directory** | Open `%LOCALAPPDATA%\MoveWindowEverywhere\` in File Explorer |
| **About** | View version, active hotkey, autostart state, configuration, and log paths |
| **Exit** | Unregister hotkey, remove tray icon, clean up resources, and exit |

*Double-clicking the tray icon opens the Window Selector.*

### 2. Window Selector

- **Instant Filter**: The search box is automatically focused upon opening. Type to filter by window title or process name.
- **Monitor Badges**: Displays each window's thumbnail, icon, title, process name, PID, and a badge indicating its current monitor (e.g. `[Screen 2]`).
- **Thumbnails**: Captured on a background thread and streamed in one by one, so the list appears instantly. Minimized, unresponsive, or blank-rendering windows show *No preview* instead of a black tile. Can be turned off entirely in Settings.
- **Target Indicator**: Top bar clearly displays the destination monitor (index and device name).
- **Keyboard & Mouse Navigation**: `↑` / `↓` to navigate, `Enter` to confirm, `Esc` to cancel. Single-click to select, double-click to confirm.
- **Auto-Dismiss**: By default, closing on focus lost prevents the selector from cluttering your workspace (configurable via `CloseSelectorOnFocusLost`).
- **Self-Exclusion**: The selector itself never appears in the candidate list.
- **Fresh, Two-Stage Enumeration**: Re-enumerates top-level windows each time it is opened. Cheap in-process queries decide which windows qualify first, and the two expensive fields (process name, window icon) are then fetched only for the survivors, so opening the list does not pay for the thousand-plus top-level handles that never make it in.

**Search Matching Hierarchy** (Case-insensitive, multi-keyword split by space, all keywords must match):
1. Title starts with keyword (Highest priority)
2. Title contains keyword
3. Process name starts with keyword
4. Process name contains keyword

### 3. Smart Window Positioning & State Preservation

When moving a window:

| Original State | Post-Move State |
| --- | --- |
| **Normal Window** | Remains normal; centered within target monitor's work area |
| **Maximized Window** | Restored, positioned, and **re-maximized on the target monitor** |
| **Minimized Window** | **Restored to normal visible state** (`SW_RESTORE`) and centered |

> **Why restore minimized windows?** If you are moving a minimized window to another monitor, the primary intent is to use it immediately on that monitor. This behavior is controlled by `IncludeMinimizedWindows` in settings (default `true`).

**Placement Guarantees**:
- **Work Area Aware (`rcWork`)**: Centers inside the visible workspace, respecting taskbar positions (bottom, top, left, right, or auto-hide).
- **Size Clamping**: If a window is larger than the target work area (e.g. moving from a 4K display to a 1080p display), its dimensions are clamped to fit.
- **Position Verification & Clamping Retry**: Re-verifies actual coordinates after `SetWindowPos` to handle third-party applications that self-adjust coordinates.
- **Foreground Activation**: Calls `SetForegroundWindow`, with fallback to `AttachThreadInput` + `BringWindowToTop` if blocked by Windows foreground lock.

### 4. Settings Dialog

Tray menu → **Settings…** exposes every option in one place:

| Option | Default | Description |
| --- | --- | --- |
| Global hotkey | `Alt + Z` | Click *Re-record*, then press the new combination. Validated immediately. |
| Start with Windows | Off | Writes a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at this executable. **No administrator rights needed.** |
| Include minimized windows | On | When off, minimized windows are excluded from the candidate list. |
| Close selector on focus lost | On | When off, the selector stays until you press `Esc` or pick an item. |
| Auto-fallback when the hotkey is taken | On | See *Hotkey conflicts* below. |
| Show window thumbnails | On | When off, only icons are shown and the selector opens faster. |

Every switch except the hotkey is applied only after you press **Save**; cancelling leaves nothing behind.

### 5. Hotkey Conflicts

A given combination can only be held by one process at a time. The application applies a two-stage strategy:

1. **Register exactly what you asked for.** Nothing changes if it succeeds.
2. **If it is taken and auto-fallback is on**, alternatives are tried in order:
   - **Keep the key, change the modifiers**: `Alt + Z` tries `Alt + Shift + Z`, `Ctrl + Alt + Z`, `Ctrl + Shift + Z`, `Ctrl + Alt + Shift + Z`. The key is the part you remember, so keeping it preserves muscle memory — and it avoids stealing other applications' `Alt` + letter menu mnemonics.
   - **Only if none of those work, keep your modifiers and change the key**, choosing exclusively from `X`, `J`, `K`, `Q`, `N`, `B` — letters that are almost never used as menu mnemonics.
   - `Win`-based combinations are excluded: the shell occupies most of them, and grabbing one would cost you a system shortcut.

The result is written back to the configuration file, the tray tooltip is refreshed, and a balloon tells you which combination is now active. The next startup uses the working combination directly. Auto-fallback can be disabled, in which case the application simply reports the conflict and lets you choose manually.

---

## Hotkey & Settings Customization

### Option 1: Via the Settings Dialog (Recommended)

1. Right-click tray icon → **Settings…**
2. Click **Re-record** and press your desired shortcut combination (e.g. `Ctrl + Alt + Shift + K`).
   - Requires at least **one modifier** (`Ctrl`, `Alt`, `Shift`, or `Win`) + **one non-modifier key**.
3. Click **Save**. The program validates and registers the hotkey immediately:
   - **Success**: Hotkey takes effect instantly and is saved to disk.
   - **Conflict (Win32 Error 1409)**: Displays *"The hotkey is already registered by another application"*, and automatically **restores the previous hotkey**.
4. Click **Reset to default hotkey** to revert to `Alt + Z`.
5. The same dialog also holds all behaviour switches — autostart, thumbnails, minimized windows, focus-loss closing, auto-fallback.

> The hotkey is registered **live** while the dialog is open, so a combination you previewed does take effect temporarily. If you then press Cancel, the original hotkey is registered back. All other switches only apply after Save.

### Option 2: Edit Configuration File Directly

Exit the application, then edit `%LOCALAPPDATA%\MoveWindowEverywhere\settings.json`:

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

- `Modifiers`: Bit flags (`Alt=1`, `Ctrl=2`, `Shift=4`, `Win=8`, `NoRepeat=16384`). `16385` = `Alt` + `NoRepeat`.
- `VirtualKey`: Win32 virtual-key code (`90` = VK_Z).

---

## Window Filter Policy

Filtering rules are centralized in `Services/WindowFilterPolicy.cs`:

| # | Rule | Reason |
|---|---|---|
| 1 | Invalid Handle | `HWND == 0` or invalid |
| 2 | Not Visible | `IsWindowVisible(HWND) == false` |
| 3 | Empty Title | Title is null, empty, or whitespace only |
| 4 | Self Process | Owned by this application |
| 5 | DWM Cloaked | Suspended UWP apps, hidden virtual desktop windows (`DWMWA_CLOAKED`) |
| 6 | Tool Windows | Has `WS_EX_TOOLWINDOW` style (floating bars, IME popups) |
| 7 | Non-activatable | Has `WS_EX_NOACTIVATE` style |
| 8 | Owned Windows | Has an owner window (`GetWindow(GW_OWNER)`), avoiding child popups |
| 9 | System Class Names | Excludes `Shell_TrayWnd`, `Progman`, `WorkerW`, `NotifyIconOverflowWindow`, `XamlExplorerHostIslandWindow`, `ForegroundStaging`, etc. |
| 10 | Minimized Windows | Configurable via `IncludeMinimizedWindows` (included by default) |

**Anti-Hang Protection**: Window icon extraction uses `SendMessageTimeout` (`SMTO_ABORTIFHUNG`, 120ms timeout) combined with `IsHungAppWindow`. Thumbnails are captured on a background thread, never on the UI thread, and unresponsive windows are skipped before any cross-process call is made. Unresponsive or frozen background apps will **never freeze the UI thread**.

---

## User Interface Privilege Isolation (UIPI)

Windows UIPI prevents lower-integrity processes from controlling higher-integrity windows:

- When running under **standard user rights**, windows belonging to **elevated applications** (e.g. Task Manager, Registry Editor, elevated terminals) will appear in the selector list, but `SetWindowPos` will be denied with `ERROR_ACCESS_DENIED (5)`.
- The application detects this and provides a clear notification:
  > *"Target window has higher privileges or the handle is invalid. Please run this tool as Administrator."*
- **Solution**: Right-click `MoveWindowEverywhere.exe` → **Run as administrator**. When elevated, the tool can seamlessly manipulate both standard and elevated windows.

---

## System Requirements

- **OS**: Windows 10 / Windows 11 (x64).
- **Runtime**: None required for published single-file builds (bundled .NET 8 runtime).
- **Displays**: At least one display; dual or multi-monitor setup recommended.
- **Building from source**: .NET SDK 8.0 or later (.NET 9 SDK supported).

---

## Project Structure

```text
Move window everywhere/
├─ MoveWindowEverywhere.sln             # Solution file
├─ README.md                            # Chinese documentation & test records
├─ README_en.md                         # English documentation (this file)
├─ .gitignore                           # Git ignore rules
├─ src/
│  └─ MoveWindowEverywhere/
│     ├─ MoveWindowEverywhere.csproj    # net8.0-windows, x64, WPF + WinForms tray
│     ├─ App.xaml / App.xaml.cs         # Entry point, single-instance mutex, lifecycle
│     ├─ GlobalUsings.cs                # Global imports
│     ├─ Native/
│     │  ├─ Win32.cs                    # P/Invoke signatures & Win32 constants
│     │  └─ Win32Types.cs               # Structs (RECT, POINT, MONITORINFO, etc.)
│     ├─ Models/
│     │  ├─ AppSettings.cs              # JSON config model
│     │  ├─ HotkeySettings.cs           # Modifiers & VirtualKey definitions
│     │  ├─ MonitorInfo.cs              # Monitor boundaries & work area
│     │  ├─ WindowFilterOptions.cs      # Filtering options
│     │  └─ WindowInfo.cs                # Snapshot data for an enumerated window
│     ├─ Services/
│     │  ├─ AppPaths.cs                 # %LOCALAPPDATA% directories
│     │  ├─ AppLogger.cs                # Rolling file logger
│     │  ├─ SettingsService.cs          # Settings persistence (JSON)
│     │  ├─ StartupRegistrar.cs         # Opt-in autostart (HKCU Run entry, injectable key path)
│     │  ├─ MonitorService.cs           # Display enumeration & point-to-monitor
│     │  ├─ ProcessNameResolver.cs      # PID to process name with caching
│     │  ├─ WindowEnumerator.cs         # EnumWindows snapshot collection
│     │  ├─ WindowFilterPolicy.cs       # 10-point window filter rules
│     │  ├─ WindowSearcher.cs           # Multi-keyword ranking and matching
│     │  ├─ WindowPlacementCalculator.cs# Geometry calculation (centered & clamped)
│     │  ├─ WindowMonitorAnnotator.cs   # Annotate windows with current monitor index
│     │  ├─ WindowMover.cs              # SetWindowPos & activation logic
│     │  ├─ WindowThumbnailService.cs   # Thumbnail capture (PrintWindow + GDI)
│     │  ├─ ThumbnailCapturePolicy.cs   # Capturable-window rules & thumbnail sizing
│     │  ├─ ThumbnailLoader.cs          # Background capture loop feeding the UI
│     │  ├─ HiddenMessageWindow.cs      # Invisible window for WM_HOTKEY / IPC
│     │  ├─ HotkeyService.cs            # RegisterHotKey lifecycle & conflict handling
│     │  ├─ HotkeyFallbackPlanner.cs    # Alternative-combination planning
│     │  ├─ IconHelper.cs / IconFactory.cs # App icon extraction & tray GDI+ drawing
│     │  └─ TrayIconService.cs          # NotifyIcon management & context menu
│     ├─ ViewModels/
│     │  ├─ SelectorViewModel.cs        # Selector window state & navigation
│     │  └─ SettingsViewModel.cs        # Settings dialog state
│     ├─ Views/
│     │  ├─ SelectorWindow.xaml(.cs)    # Window picker interface
│     │  └─ SettingsWindow.xaml(.cs)    # Settings dialog
│     └─ Resources/
│        └─ app.manifest                # Per-Monitor V2 DPI, supportedOS, asInvoker
└─ tests/
   └─ MoveWindowEverywhere.Tests/       # xUnit test suite (139 test cases)
```

---

## Build & Publish

### 1. Build from Source

```bash
# Check .NET environment
dotnet --info

# Restore dependencies
dotnet restore

# Build Release
dotnet build -c Release
```

### 2. Run Unit Tests

```bash
dotnet test -c Release
```

### 3. Publish Self-Contained Single Executable

```bash
dotnet publish src/MoveWindowEverywhere/MoveWindowEverywhere.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o publish/win-x64
```

Output: `publish/win-x64/MoveWindowEverywhere.exe` (~63 MB, ready to run anywhere).

---

## Configuration & Logs

All user data is isolated inside `%LOCALAPPDATA%\MoveWindowEverywhere\`:

- **Settings**: `%LOCALAPPDATA%\MoveWindowEverywhere\settings.json`
- **Logs**: `%LOCALAPPDATA%\MoveWindowEverywhere\logs\app-YYYYMMDD.log`

Logs track startup, hotkey registration and auto-fallback, autostart registration, invocation triggers, selector window counts, `SetWindowPos` failures with Win32 error codes, clamping adjustments, thumbnail skip reasons, and settings changes. No sensitive or private data is ever collected.

---

## Unit Tests

The test suite in `tests/MoveWindowEverywhere.Tests` contains **139 unit tests** covering:

- `WindowFilterPolicyTests.cs`: Comprehensive coverage for all 10 filtering rules.
- `WindowSearcherTests.cs`: Prefix and substring matching across titles and process names, multi-keyword queries, empty queries.
- `WindowPlacementCalculatorTests.cs`: Work area centering, negative monitor coordinates, cross-monitor dimension clamping.
- `WindowMoverTests.cs`: State restoration, invalid handle handling, empty monitor targets.
- `HotkeySettingsTests.cs`: Key validity, bitwise modifier serialization, display formatting.
- `HotkeyFallbackPlannerTests.cs`: Primary combination first, key-preserving before key-changing, no duplicates or overflow, no `Win`-based or system-reserved combinations.
- `SettingsServiceTests.cs`: Resilient JSON serialization, corrupt config fallbacks, omitting read-only properties, defaults and round-trip for every switch.
- `SettingsViewModelTests.cs`: All switches collected and change-notified, autostart switch stays off when registration is impossible, results are copies.
- `SelectorViewModelTests.cs`: Selection tracking, list filtering, edge cases.
- `WindowMonitorAnnotatorTests.cs`: Monitor annotation across displays, no annotation on single-monitor setups, no guessing on unmatched handles, empty-label default.
- `ThumbnailCapturePolicyTests.cs`: Minimized / invisible / cloaked / zero-handle windows are skipped, capture cap and ordering, aspect-ratio-preserving sizing.
- `ThumbnailLoaderTests.cs`: Only eligible windows captured, failures not applied, a single bad window does not stall the batch, concurrent starts ignored, dispose stops work immediately.
- `StartupRegistrarTests.cs`: Quoting, `dotnet` host rejection, matching quoted / unquoted / argument-suffixed commands, enable and disable idempotence, stale-path repair, config-off never deletes an entry.
- `HiddenMessageWindowTests.cs`: Message pump, `WM_HOTKEY` dispatch, IPC wake-up messages.
- `DispatcherMessageLoopTests.cs`: WPF Dispatcher message routing.

Autostart tests do touch the registry, but only through a dynamically generated throwaway subkey (`HKCU\Software\MoveWindowEverywhere.Tests\<GUID>`) that is deleted afterwards — your real startup entries are never touched.

---

## Known Limitations & Design Philosophy

1. **Procedural Tray Icon**: The tray icon is drawn dynamically via GDI+ at startup. No binary `.ico` files are embedded, keeping the repository 100% clean and transparent.
2. **Thumbnails Depend on the Target Window**: Thumbnails are captured by asking each window to render itself via `PrintWindow`. Consequently minimized windows, unresponsive windows, and protected-content windows (some players, DRM surfaces) show *No preview* rather than a black tile; at most the first 24 windows of a list are captured, as a deliberate limit on memory and time.
3. **Autostart Registers the Current Process Path**: Launched via `dotnet run`/`dotnet exec`, the process path points at `dotnet.exe`, which is meaningless as a startup entry — the switch is therefore disabled and explains why. Run the published executable directly.
4. **Hotkey Auto-Fallback Has Limits**: At most 11 candidate combinations are attempted. If none is free, the app still asks you to choose manually. The candidate key list is deliberately small (only letters that are almost never menu mnemonics), so auto-fallback covers the common case, not every case. It can be turned off.
5. **Foreground Lock**: If Windows blocks `SetForegroundWindow` due to system foreground lock policies, the window is still moved correctly, and the app attempts thread attachment fallback.
6. **Elevated Windows**: Windows UIPI prevents a lower-integrity process from repositioning a higher-integrity window. This is a security boundary, not a defect.
7. **No Network Features**: No telemetry, accounts, or auto-update.

---

## License

This project is open-sourced under the [MIT License](LICENSE).
