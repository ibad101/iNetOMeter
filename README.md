<p align="center">
  <img src="assets/logo.svg" alt="iNetOMeter Logo" width="640"/>
</p>

<p align="center">
  <strong>Lightweight, privacy-first Windows taskbar network speed meter and internet speed test dashboard.</strong>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT"/></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-0078D6.svg" alt="Platform: Windows 10/11 x64"/>
  <img src="https://img.shields.io/badge/.NET-8.0%20Desktop%20Runtime-512BD4.svg" alt=".NET 8.0"/>
  <img src="https://img.shields.io/badge/tests-64%20passing-brightgreen.svg" alt="Tests: 64 passing"/>
  <img src="https://img.shields.io/badge/dependencies-0%20external%20NuGet-success.svg" alt="Dependencies: 0 NuGet"/>
</p>

---

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Architecture & Honest Technical Details](#architecture--honest-technical-details)
- [System Requirements](#system-requirements)
- [Interface & Visual Layout](#interface--visual-layout)
- [Installation Options](#installation-options)
  - [Option A: Portable Release (Zero Install)](#option-a-portable-release-zero-install)
  - [Option B: Per-User PowerShell Installer](#option-b-per-user-powershell-installer)
  - [Option C: Inno Setup Executable](#option-c-inno-setup-executable)
- [User Guide & First Launch](#user-guide--first-launch)
  - [Taskbar Context Menu](#taskbar-context-menu)
  - [Internet Speed Test Dashboard](#internet-speed-test-dashboard)
  - [Smart Wi-Fi Assistant](#smart-wi-fi-assistant)
  - [Customization & Preferences](#customization--preferences)
- [Uninstallation](#uninstallation)
- [Privacy & Security](#privacy--security)
- [Known Limitations](#known-limitations)
- [Project Structure](#project-structure)
- [Building & Testing from Source](#building--testing-from-source)
- [Documentation Index](#documentation-index)
- [License & Attributions](#license--attributions)
- [Contributing & Support](#contributing--support)

---

## Overview

**iNetOMeter** is a free, open-source Windows desktop utility designed for users who want accurate, real-time insight into their internet connectivity without system bloat, ads, background telemetry, or account sign-ups.

It seamlessly integrates live download and upload indicators directly into your Windows taskbar adjacent to the notification area clock, offers a genuine high-precision speed-test speedometer, preserves test history locally, and includes an intelligent, user-approved Wi-Fi switching assistant.

---

## Key Features

- **Live Taskbar Speed Meter**: Displays live download (red accent `#FF525C`) and upload (blue accent `#4CA8FF`) rates anchored directly near the system clock.
- **Auto-Hide & Explorer Synchronization**: Automatically matches Windows taskbar auto-hide animations and re-anchors immediately if `explorer.exe` restarts.
- **Internet Speed Test Dashboard**: Full-fledged dashboard measuring true ping, jitter, download, and upload throughput against Cloudflare Anycast edge servers with a logarithmic speedometer gauge.
- **Smart Saved Wi-Fi Assistant (Opt-in)**: Detects weak Wi-Fi signals or connection dropouts and suggests stronger networks *already saved on your PC*. Never switches without your explicit approval.
- **Zero Third-Party NuGet Dependencies**: Built strictly against .NET 8 Windows Forms and native Win32 APIs for minimal RAM/CPU footprint.
- **Privacy by Default**: Zero telemetry, no cloud sync, no tracking, and local storage of preferences and test history in `%LOCALAPPDATA%\iNetOMeter`.

---

## Architecture & Honest Technical Details

### 1. Taskbar Meter Implementation
iNetOMeter's taskbar widget is implemented as a lightweight, non-activating floating utility window (`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`). 
- It uses Windows event hooks (`SetWinEventHook` on `EVENT_OBJECT_LOCATIONCHANGE`) and window message listeners (`TaskbarCreated`, `WM_DISPLAYCHANGE`, `WM_DPICHANGED`) to track the exact bounding box of the taskbar notification area (`Shell_TrayWnd` -> `TrayNotifyWnd`).
- It is **not** an in-process Explorer DLL band object (DeskBand), ensuring complete stability: it can never crash `explorer.exe`.

### 2. Smart Wi-Fi Assistant Behavior
- **Separate Measurements**: Wi-Fi radio signal quality and internet reachability are measured separately. A high radio signal indicates link quality to your router, but does *not* guarantee external internet throughput.
- **Strictly Saved Profiles**: The assistant only inspects profiles already registered in Windows (`netsh wlan show profiles`). It never attempts to connect to unfamiliar or public networks.
- **Credential Protection**: Passwords and WLAN profile keys are never dumped or extracted (`key=clear` is never run). Windows handles authentication internally.
- **Zero Automation**: Switching is never forced. The assistant presents a native action card where the user can click **"Switch"** or **"Dismiss"**.
- **Hysteresis & Debouncing**: Requires sustained weak signal (2 consecutive checks) and enforces a minimum signal advantage (+10%) to prevent rapid toggling between networks.
- **Speed Test Protection**: Network evaluation is suppressed while a speed test is actively streaming.

### 3. Uninstall Behavior
- **Installed Mode**: When installed via setup, clicking **"Uninstall iNetOMeter"** launches the uninstaller out-of-process and cleanly terminates the running application to avoid file-lock conflicts.
- **Portable Mode**: Informs the user that the app is portable, guides them to delete the extracted folder, and offers to exit immediately.
- **Data Protection**: Neither uninstall method purges `%LOCALAPPDATA%\iNetOMeter` unless explicitly requested, preserving your past speed-test history and preferences.

---

## System Requirements

| Requirement | Specification |
| :--- | :--- |
| **Operating System** | Windows 10 (Version 2004 / Build 19041+, including Windows 10 IoT Enterprise LTSC 2021) or Windows 11 |
| **Architecture** | 64-bit (x64) |
| **Runtime** | **Microsoft .NET 8.0 Desktop Runtime (x64)** ([Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)) |
| **Hardware** | Intel Core i3 / AMD equivalent or higher, 512 MB available RAM, 10 MB free disk space |
| **Network** | Active network interface (Ethernet or 802.11 Wi-Fi) |

---

## Interface & Visual Layout

iNetOMeter uses a clean, high-contrast dark color palette designed for high legibility and native Windows aesthetics:

- **Taskbar Bandwidth Meter**: Compact dark charcoal widget (`#111827`) anchored immediately adjacent to the Windows taskbar clock. Displays live download rates in vibrant red (`#FF525C`, downward arrow) and upload rates in sky blue (`#4CA8FF`, upward arrow) with clean Segoe UI typography and a subtle 1px border.
- **Speed Test Dashboard**: Immersive deep navy interface (`#0B111E`) featuring an animated 270° logarithmic speedometer gauge, live metric cards for Latency (Ping in ms), Jitter (ms), Download (Mbps), and Upload (Mbps), and real-time progress indicators.
- **Speed Test History**: Dedicated dark modal displaying the 5 most recent completed speed test records (date/time, ping, download, upload) with a "Clear History" action.
- **Settings & Preferences**: Scrollable dark-themed dialog for configuring speed units (Auto Bytes, Auto Bits, Always Mbps, Always MB/s), refresh frequency (500ms, 1000ms, 2000ms), meter opacity (70%–100%), startup shortcut, notifications, and Wi-Fi assistant rules.
- **Smart Wi-Fi Action Card**: Non-focus-stealing floating toast prompt appearing above the tray when a stronger saved network is available, providing direct **"Switch"** and **"Dismiss"** actions.

---

## Installation Options

### Option A: Portable Release (Zero Install)
*Best for users who prefer not to install software.*
1. Download `iNetOMeter-1.0.0-win-x64-portable.zip` from the GitHub **Releases** tab (tag `v1.0.0`).
2. Extract the archive into any user folder (e.g. `C:\Users\<User>\AppData\Local\Programs\iNetOMeter` or a folder on your Desktop).
3. Run `iNetOMeter.exe`.

### Option B: Per-User PowerShell Installer
*Zero prerequisites, requires no administrator privileges.*
1. Download and extract the portable ZIP.
2. Open PowerShell in the extracted folder and run:
   ```powershell
   .\Install-iNetOMeter.ps1
   ```
3. Installs iNetOMeter to `%LOCALAPPDATA%\Programs\iNetOMeter`, adds Start Menu/Desktop shortcuts, and registers in Windows **Apps & Features**.

### Option C: Inno Setup Executable
*Traditional Windows installer wizard.*
- If built using Inno Setup 6, run `iNetOMeter-1.0.0-win-x64-Setup.exe` and follow the on-screen setup prompts.

---

## User Guide & First Launch

When launched, iNetOMeter docks automatically in your Windows taskbar adjacent to the notification area clock.

### Taskbar Context Menu
Right-click the meter at any time:
- **Run Speed Test**: Opens the speed test dashboard (or simply **double-click** the taskbar meter).
- **View Last 5 Tests**: Inspects previous speed test records.
- **Settings**: Opens user preferences.
- **Uninstall iNetOMeter**: Safely triggers guided uninstallation.
- **Exit iNetOMeter**: Closes the application and cleans up all system hooks.

### Internet Speed Test Dashboard
1. Open the dashboard by double-clicking the taskbar meter.
2. Click **START SPEED TEST**.
3. Telemetry stages:
   - **Ping & Jitter**: Multi-sample HTTP latency probes against Cloudflare Anycast edge servers.
   - **Download Test**: Multi-stream concurrent payload streaming.
   - **Upload Test**: Multi-stream payload transmission.
4. Results are automatically recorded to your local 5-entry history log.

### Smart Wi-Fi Assistant
1. Open **Settings** and check **Enable Smart Wi-Fi Assistant (Opt-in)**.
2. If your Wi-Fi signal drops below the threshold (default: 30%) or internet connectivity drops, a card will appear near your taskbar if a stronger saved network is in range.
3. Click **Switch to [SSID]** to switch networks, or click **Dismiss** to ignore.

---

## Uninstallation

### Method 1: Taskbar Context Menu
Right-click the taskbar meter > select **Uninstall iNetOMeter** > confirm the prompt.

### Method 2: Windows Settings
Open **Settings** > **Apps** > **Apps & Features** > find **iNetOMeter** > click **Uninstall**.

### Method 3: Portable Mode
Exit iNetOMeter and delete the extracted folder.

> **Note on Data Preservation**: User settings and test records in `%LOCALAPPDATA%\iNetOMeter` are intentionally kept so re-installing won't lose your data. To completely delete them, remove `%LOCALAPPDATA%\iNetOMeter`.

---

## Privacy & Security

- **100% Local Execution**: All bandwidth counting, signal evaluation, and profile comparisons happen strictly on your PC.
- **Zero Analytics / Telemetry**: iNetOMeter does not collect, log, or transmit any user metrics, IP addresses, or device IDs.
- **Zero Credential Access**: Wi-Fi passwords are never retrieved or stored.
- **Zero Third-Party Trackers**: No third-party tracking libraries, advertising SDKs, or cloud backends.

---

## Known Limitations

1. **Multi-Monitor Taskbars**: The taskbar widget anchors to the primary Windows taskbar notification area. Secondary display taskbars do not display independent meters.
2. **Third-Party Shell Replacements**: Custom Windows shells (e.g. StartAllBack, RetroBar) may position the tray differently; iNetOMeter detects standard `Shell_TrayWnd`.
3. **Wi-Fi Hardware Support**: Wi-Fi features require an active WLAN adapter managed by the Windows Wireless AutoConfig service (`wlansvc`). Ethernet-only systems automatically ignore Wi-Fi suggestions.

---

## Project Structure

```text
iNetOMeter/
├── assets/                  # Application branding (app.ico, logo.svg)
├── docs/                    # Detailed technical and user documentation
│   ├── DEVELOPMENT.md       # Architecture, build, and contribution guide
│   ├── FEATURES.md          # Comprehensive feature catalog & specs
│   ├── INSTALLATION.md      # Deployment, silent flags, and troubleshooting
│   ├── PRIVACY-AND-SECURITY.md # Security audit and data flow details
│   ├── TROUBLESHOOTING.md   # Diagnostics and common error resolutions
│   └── USER-GUIDE.md        # Complete walkthrough of all screens
├── packaging/               # Release packaging scripts & installer definitions
│   ├── build-package.ps1    # Master build, test, publish, and packaging pipeline
│   ├── inno-setup/          # Inno Setup 6 installer definition
│   └── scripts/             # Native PowerShell installer & uninstaller
├── src/                     # C# .NET 8 Windows Forms application
│   ├── ConnectivityMonitor.cs # Internet reachability monitoring
│   ├── HistoryService.cs    # 5-test local JSON history persistence
│   ├── NetworkMonitor.cs    # Win32 GetIfTable2 network interface monitor
│   ├── SettingsService.cs   # Persistent user preferences
│   ├── SpeedTestService.cs  # Cloudflare Anycast speed test engine
│   ├── TaskbarBandForm.cs   # Taskbar widget and event hook tracking
│   ├── WiFiAssistant.cs     # Smart Wi-Fi coordination & debounce engine
│   ├── WiFiService.cs       # Windows WLAN netsh interface & parser
│   └── WiFiSuggestionPopup.cs # Non-focus-stealing floating action card
├── tests/                   # Automated unit & integration test suite (64 tests)
│   └── Program.cs           # Test runner and assertion framework
├── CHANGELOG.md             # Version history and release notes
├── LICENSE                  # MIT License
├── QUICKSTART.md            # Quick user onboarding guide
└── THIRD-PARTY-NOTICES.md   # Open-source third-party attributions
```

---

## Building & Testing from Source

### Prerequisites
- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Windows 10 x64 / Windows 11
- PowerShell 5.1+ or PowerShell 7+

### 1. Build Solution
```powershell
dotnet build iNetOMeter.sln -c Release
```

### 2. Run Automated Test Suite
```powershell
dotnet run --project tests/iNetOMeter.Tests.csproj -c Release
```

### 3. Package Release Archive
```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-package.ps1
```
Output artifacts are saved to `dist/`.

---

## Documentation Index

- [Quickstart Guide](QUICKSTART.md)
- [Complete Feature Catalog](docs/FEATURES.md)
- [Installation & Deployment Guide](docs/INSTALLATION.md)
- [Detailed User Guide](docs/USER-GUIDE.md)
- [Privacy & Security Audit](docs/PRIVACY-AND-SECURITY.md)
- [Developer & Contribution Guide](docs/DEVELOPMENT.md)
- [Troubleshooting & Diagnostics](docs/TROUBLESHOOTING.md)
- [Changelog & Release Notes](CHANGELOG.md)

---

## License & Attributions

This project is licensed under the **[MIT License](LICENSE)**.

- **Microsoft .NET 8 & Windows Forms**: Licensed under the MIT License.
- **Cloudflare Speed Test Endpoints**: Diagnostic payload endpoints used in compliance with public diagnostic terms.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for full legal text and attributions.

---

## Contributing & Support

Contributions are welcome! Please review [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for architectural guidelines, coding style, and pull request workflows.

If you encounter a bug or have a suggestion, please open an issue on the repository GitHub **Issues** tab.
