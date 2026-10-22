# iNetOMeter — Quickstart & User Guide

**iNetOMeter** is a free, lightweight, privacy-first Windows taskbar utility that displays live download and upload network throughput directly in the taskbar near the system clock, and includes a full Internet Speed Test dashboard.

---

## Quickstart

### Running Portable
1. Extract `iNetOMeter-1.0.0-win-x64-portable.zip` into any user-writable folder (e.g. `C:\Users\<User>\AppData\Local\Programs\iNetOMeter` or desktop folder).
2. Double-click `iNetOMeter.exe`.
3. The compact bandwidth meter will immediately dock in your Windows taskbar adjacent to the notification area (clock / tray icons).

### Installing via PowerShell Installer (Zero Prerequisites)
1. Open PowerShell in the package directory.
2. Run:
   ```powershell
   .\Install-iNetOMeter.ps1
   ```
3. This installs `iNetOMeter` for the current user into `%LOCALAPPDATA%\Programs\iNetOMeter`, adds Start Menu and Desktop shortcuts, and registers the app in Windows **Apps & Features** so you can uninstall it anytime through Windows Settings.

---

## Features & Usage

### 1. Taskbar Meter
- **Download**: Highlighted in red accent (`#FF525C`) with a downward arrow.
- **Upload**: Highlighted in blue accent (`#4CA8FF`) with an upward arrow.
- **Auto-Hide Sync**: If your taskbar has auto-hide enabled, the meter hides and reveals synchronously when the taskbar slides on/off screen.
- **Explorer Restart Resilience**: Automatically re-anchors if Windows Explorer restarts.

### 2. Context Menu & Double-Click
Right-click anywhere on the taskbar meter to open the menu:
- **Run Speed Test**: Opens the speedometer dashboard. (Or double-click the meter).
- **View Last 5 Tests**: Opens your recent test history.
- **Settings**: Opens user preferences.
- **Exit iNetOMeter**: Safely closes the app, disposes native hooks, and clears tray icons.

### 3. Internet Speed Test Dashboard
- Measures true application-layer **Ping (ms)**, **Jitter (ms)**, **Download Throughput (Mbps)**, and **Upload Throughput (Mbps)** against Cloudflare Anycast edge servers.
- Includes a 270° logarithmic animated gauge, live progress indicator, and safe cancellation controls.
- **Zero Simulation**: 100% genuine network telemetry.

### 4. Local Data Storage & Privacy
All persistent data is stored strictly on your local machine under your Windows user profile:
- **History**: `%LOCALAPPDATA%\iNetOMeter\history.json` (Stores up to the 5 newest completed tests).
- **Settings**: `%LOCALAPPDATA%\iNetOMeter\settings.json`
- **Zero Telemetry**: No IP addresses, locations, analytics, credentials, or personal telemetry are collected, stored, or transmitted.

### 5. Settings
- **Start with Windows**: Opt-in toggle that adds/removes a standard per-user shortcut in your Windows Startup folder (`shell:startup`). Requires no administrator rights and modifies no machine-wide registry keys.
- **Speed Format**: Choose between `Auto Bytes` (B/s, KB/s, MB/s, GB/s), `Auto Bits` (bps, Kbps, Mbps, Gbps), `Always Mbps`, or `Always MB/s`.
- **Refresh Frequency**: 500 ms (Fast), 1000 ms (Normal), or 2000 ms (Low CPU).
- **Meter Opacity**: Adjust translucent frosted appearance (70% - 100%).
- **Smart Notifications**: Opt-in desktop alerts for connection loss, connection restoration, and slow speed warnings below a custom threshold.

---

## Uninstallation
To completely remove iNetOMeter:
1. Right-click the taskbar meter and select **Exit iNetOMeter**.
2. If installed via installer: open Windows **Settings** > **Apps & Features**, find **iNetOMeter**, and click **Uninstall** (or run `Uninstall-iNetOMeter.ps1`).
3. If using portable ZIP: simply delete the folder where you extracted the files.
4. Optional: To remove your local test history and preferences, delete the folder `%LOCALAPPDATA%\iNetOMeter`.
