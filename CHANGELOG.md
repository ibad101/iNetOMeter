# Changelog

All notable changes to the **iNetOMeter** project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] — 2026-10-22

### Added
- **Taskbar Bandwidth Meter**:
  - Anchored floating widget adjacent to the Windows taskbar clock / notification area.
  - Download rate indicator in red accent (`#FF525C`) with down arrow badge.
  - Upload rate indicator in blue accent (`#4CA8FF`) with up arrow badge.
  - Taskbar auto-hide tracking and synchronized reveal/hide via `TaskbarTracker`.
  - Windows Explorer restart resilience listening for the `TaskbarCreated` registered window message.
  - Multi-resolution DPI change (`WM_DPICHANGED`) and display change (`WM_DISPLAYCHANGE`) handling.
- **Internet Speed Test Engine & Dashboard**:
  - Multi-sample latency (ping) and jitter measurement using Cloudflare Anycast edge endpoints.
  - Concurrent multi-stream download and upload throughput testing.
  - Real-time animated 270° logarithmic speedometer gauge with smoothed interpolation.
  - In-flight transfer cancellation with safe socket teardown on form close.
- **Test History**:
  - Automatically records the 5 newest completed speed test results in `%LOCALAPPDATA%\iNetOMeter\history.json`.
  - Dedicated history viewer window with date, latency, download, upload, and "Clear History" action.
  - Corrupt history file recovery with clean initialization.
- **Settings & Preferences**:
  - Persistent settings stored in `%LOCALAPPDATA%\iNetOMeter\settings.json`.
  - Configurable speed unit formatting: Auto Bytes (`B/s`, `KB/s`, `MB/s`, `GB/s`), Auto Bits (`bps`, `Kbps`, `Mbps`, `Gbps`), Always Mbps, Always MB/s.
  - Configurable update interval: 500ms, 1000ms (recommended), or 2000ms.
  - Configurable taskbar meter opacity slider (70% to 100%).
  - Opt-in Start with Windows per-user shortcut toggle (`shell:startup`).
- **Smart Desktop Notifications**:
  - Opt-in desktop alerts for internet connection lost, internet connection restored, and slow speed warnings.
  - Debounced connectivity detection (requires 2 consecutive failed probes before alerting).
  - Cooldown timer (10s - 3600s) to prevent notification spam.
  - Slow-speed alerts trigger strictly after genuine speed-test measurements rather than idle network traffic.
- **Smart Saved Wi-Fi Assistant**:
  - Opt-in assistant evaluating Wi-Fi link status, signal percentage, and internet reachability.
  - Recommends nearby networks *already saved in Windows* when signal is weak (<= 30%) or internet is dropped.
  - Enforces signal advantage hysteresis (+10%) and debouncing (2 consecutive weak readings) to prevent false alerts.
  - Non-focus-stealing native action card (`WiFiSuggestionPopup`) with **"Switch to [SSID]"** and **"Dismiss"** buttons.
  - Windows-managed credential switching without dumping or storing Wi-Fi passwords.
  - Speed test protection: suppresses Wi-Fi prompts during active speed tests.
- **Taskbar Context Menu**:
  - Right-click options: "Run Speed Test", "View Last 5 Tests", "Settings", "Uninstall iNetOMeter", and "Exit iNetOMeter".
  - Double-click taskbar widget to immediately open the speed test dashboard.
  - Guided uninstallation flow distinguishing between installed and portable execution.
- **Packaging & Distribution**:
  - Framework-dependent x64 portable ZIP release (`dist/iNetOMeter-1.0.0-win-x64-portable.zip`, ~198 KB / 0.19 MB).
  - Multi-resolution application icon (`assets/app.ico`) with 7 embedded frames (16x16 to 256x256).
  - Zero-dependency per-user PowerShell installer (`packaging/scripts/Install-iNetOMeter.ps1`) and uninstaller (`packaging/scripts/Uninstall-iNetOMeter.ps1`).
  - Production Inno Setup 6 configuration (`packaging/inno-setup/iNetOMeter.iss`).
  - Master build, test, package, and verification script (`packaging/build-package.ps1`).
  - Automated test suite with **64 tests** passing with 0 failures.

### Fixed
- **Taskbar Meter Visibility Retention**: Fixed an issue where the meter could disappear beneath `Shell_TrayWnd` after taskbar clicks or app switching. Added native window visibility checks (`NativeMethods.IsWindowVisible`), Windows Shell AppBar state detection (`SHAppBarMessage` with `ABM_GETSTATE`), and non-intrusive Z-order recovery (`WindowFromPoint` filtering against Explorer process) to ensure the meter reliably recovers after `Win + D` (Show Desktop), lock screen, or display sleep without interfering with third-party applications.
- **Context Menu Lifecycle Stability**: Resolved an issue where moving the mouse pointer toward menu items (e.g. Settings) could prematurely dismiss the right-click context menu. Removed aggressive `WM_ACTIVATEAPP` dismissal and protected active menu interactions from synchronization timer interference.
