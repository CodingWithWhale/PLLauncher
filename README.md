# PLLauncher - Windows 11 Automation Launcher (Avalonia UI)

A modern Windows 11 desktop automation launcher built with **C# Avalonia UI**.
Builds in **VS Code** with just `dotnet build` — no Visual Studio needed!

## Quick Start

```bash
# 1. Make sure you have .NET 8 SDK
dotnet --version   # Should show 8.x

# 2. Restore packages
dotnet restore

# 3. Build and run
dotnet run

# 4. Publish as single-file .exe
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The published .exe will be in: `bin/Release/net8.0/win-x64/publish/PLLauncher.exe`

## Features

- **Global Keybinds** — System-wide hotkeys (Alt+Shift+X, etc.)
- **Task Automation** — Shutdown, restart, sleep, lock PC on schedule
- **Time Limits** — Daily app usage limits with 12-hour cooldown lock
- **Scheduler** — Recurring actions (daily/weekday/weekly)
- **Anti-Sleep Mode** — Prevents PC from sleeping
- **Windows Notifications** — 1-minute warnings with Cancel/Delay buttons
- **System Tray** — Runs in background
- **Dark Mode** — Windows 11 Fluent Design look
- **Offline** — No internet, no login, no subscriptions

## Project Structure

```
PLLauncher/
├── PLLauncher.sln
└── PLLauncher/
    ├── Program.cs                    # Entry point
    ├── App.axaml / .cs               # App config, theme, service init
    ├── MainWindow.axaml / .cs        # Main window + sidebar navigation
    ├── PLLauncher.csproj             # Project config (Avalonia packages)
    ├── Models/                       # Data models (ObservableObject)
    │   ├── KeybindItem.cs
    │   ├── TaskItem.cs
    │   ├── TimeLimitItem.cs
    │   ├── ScheduleItem.cs
    │   └── AppSettings.cs
    ├── ViewModels/                   # MVVM ViewModels
    │   ├── DashboardViewModel.cs
    │   ├── KeybindsViewModel.cs
    │   ├── TasksViewModel.cs
    │   ├── TimeLimitsViewModel.cs
    │   ├── SchedulerViewModel.cs
    │   └── SettingsViewModel.cs
    ├── Views/                        # Avalonia XAML Pages
    │   ├── DashboardPage.axaml / .cs
    │   ├── KeybindsPage.axaml / .cs
    │   ├── TasksPage.axaml / .cs
    │   ├── TimeLimitsPage.axaml / .cs
    │   ├── SchedulerPage.axaml / .cs
    │   └── SettingsPage.axaml / .cs
    ├── Services/                     # Backend services
    │   ├── DataService.cs            # JSON persistence
    │   ├── HotkeyService.cs          # Win32 global hotkeys
    │   ├── TaskSchedulerService.cs   # Task execution + countdown
    │   ├── TimeTrackingService.cs    # App usage tracking
    │   ├── ScheduleService.cs        # Recurring schedules
    │   ├── NotificationService.cs    # Windows notifications
    │   ├── ProcessMonitorService.cs  # Process control
    │   └── SystemTrayService.cs      # System tray
    └── Helpers/
        ├── NativeMethods.cs          # Win32 P/Invoke
        └── RelayCommand.cs           # ICommand
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | Avalonia UI 11.2 |
| Language | C# .NET 8 |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Theme | Avalonia.Themes.Fluent (Windows 11 look) |
| Storage | Local JSON files |
| Hotkeys | Win32 P/Invoke (RegisterHotKey) |

## Build Requirements

- **.NET 8 SDK** (you already have this!)
- **Windows 11** (for runtime — hotkeys, shutdown APIs, etc.)
- That's it! No Visual Studio needed.

## Creating the Installer .exe

```bash
# Single-file self-contained exe (no .NET install needed on target)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Smaller size (requires .NET 8 on target machine)
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```
