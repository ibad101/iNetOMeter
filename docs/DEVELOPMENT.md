# iNetOMeter — Developer & Contribution Guide

This document outlines the architecture, coding standards, build pipelines, and testing procedures for developers contributing to **iNetOMeter**.

---

## Architecture Overview

iNetOMeter is a 64-bit Windows Forms application written in C# targeting **.NET 8.0-windows**. It adheres to a strict "zero external NuGet dependencies" policy.

```text
src/
├── Program.cs                 # Application bootstrap, mutex check, single-instance enforcement
├── TaskbarBandForm.cs         # Main floating taskbar widget, WinEventHook positioning & context menu
├── TaskbarTracker.cs          # Taskbar edge detection, bounding box calculation, auto-hide state
├── NetworkMonitor.cs          # Native GetIfTable2 interface counter polling and speed formatting
├── ConnectivityMonitor.cs     # Event-driven internet reachability state machine & debouncing
├── ConnectivityModels.cs      # Core connectivity interfaces and enums
├── NotificationService.cs     # Windows notification tray integration & rate limiting
├── WiFiAssistant.cs           # Smart Wi-Fi coordination, debounce, hysteresis, and popup dispatch
├── WiFiService.cs             # Windows WLAN netsh interface, static parsers, and connection driver
├── WiFiModels.cs              # Wi-Fi data transfer objects and IWiFiService interface
├── WiFiSuggestionPopup.cs     # Non-focus-stealing native action card window
├── SpeedTestService.cs        # Multi-stream concurrent speed test engine (Cloudflare Anycast)
├── SpeedTestModels.cs         # Speed test records, math utilities, and unit conversions
├── SpeedTestForm.cs           # Speed test dashboard with 270° logarithmic gauge animation
├── HistoryService.cs          # 5-record JSON history repository
├── HistoryForm.cs             # History viewer window with clear action
├── SettingsService.cs         # Persistent user preferences JSON repository
├── SettingsForm.cs            # Preferences configuration window with scrollable layout
├── StartupManager.cs          # Per-user Startup folder shortcut manager
└── NativeMethods.cs           # P/Invoke signatures for User32, Dwmapi, and IPHlpApi
```

---

## Development Environment Setup

### Required Tools
- Windows 10 x64 / Windows 11
- .NET 8.0 SDK (version 8.0.100 or higher)
- Visual Studio 2022 (Community or higher) with the **.NET Desktop Development** workload, OR VS Code with C# Dev Kit, OR JetBrains Rider.

---

## Build Commands

### Clean & Build (Debug)
```powershell
dotnet clean iNetOMeter.sln
dotnet build iNetOMeter.sln -c Debug
```

### Build (Release)
```powershell
dotnet build iNetOMeter.sln -c Release
```

### Run Locally
```powershell
dotnet run --project src/iNetOMeter.csproj -c Debug
```

---

## Running Automated Tests

The test suite in `tests/` is self-contained with no third-party test runners, allowing instant execution in any environment:

```powershell
dotnet run --project tests/iNetOMeter.Tests.csproj -c Release
```

### Adding New Tests
1. Open `tests/Program.cs`.
2. Add a `RunTest("TestName", () => { ... });` block in `Main`.
3. Use `AssertEqual(expected, actual)` and `AssertTrue(condition)`.

---

## Release Packaging

Run the master packaging script:
```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-package.ps1
```

The script:
1. Executes all 64 automated tests.
2. Publishes an optimized `win-x64` framework-dependent build into `publish/win-x64/`.
3. Strips all `.pdb` debug symbol files.
4. Bundles `LICENSE`, `THIRD-PARTY-NOTICES.md`, `QUICKSTART.md`, `assets/`, and installer scripts.
5. Produces `dist/iNetOMeter-1.0.0-win-x64-portable.zip`.
6. Verifies archive contents to ensure zero PDBs and zero user data files exist in the release.

---

## Coding Standards

- **Zero Third-Party Dependencies**: Do not introduce external NuGet packages without explicit project consensus. Use standard library or clean Win32 P/Invoke.
- **Resource Discipline**: Always dispose brushes, pens, fonts, timers, and native hooks.
- **Thread Safety**: Network I/O and process execution must be asynchronous (`Task.Run` / `async`/`await`). UI updates must be marshaled to the UI thread via `Invoke` or `SynchronizationContext`.
- **Privacy First**: Never log or transmit user credentials, machine names, IP addresses, or local network telemetry.
