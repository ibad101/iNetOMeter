# iNetOMeter — Quickstart & User Guide

**iNetOMeter** is a free, lightweight, privacy-first Windows taskbar utility that displays live download and upload network throughput directly in the taskbar near the system clock, includes a full Internet Speed Test dashboard, and provides smart Wi-Fi assistance.

---

## Quickstart

### Running Portable
1. Extract `iNetOMeter-1.0.0-win-x64-portable.zip` into any user-writable folder (e.g. `C:\Users\<User>\AppData\Local\Programs\iNetOMeter` or a desktop folder).
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

### 2. Context Menu & Actions
Right-click anywhere on the taskbar meter to open the menu:
- **Run Speed Test**: Opens the speedometer dashboard. (Or double-click the meter).
- **View Last 5 Tests**: Opens your recent test history.
- **Settings**: Opens user preferences.
- **Uninstall iNetOMeter**: Safely initiates uninstallation from the taskbar:
  - **In Installed Mode**: Displays a confirmation dialog reminding you that user settings and history in `%LOCALAPPDATA%\iNetOMeter` are preserved. On confirmation, safely launches the uninstaller out-of-process and closes the application so no file-lock conflicts occur.
  - **In Portable Mode**: Informs you that the app is running portably without system uninstaller entries, explains how to delete the extracted folder, and offers to exit the application.
- **Exit iNetOMeter**: Safely closes the app, disposes native hooks, and clears tray icons.

### 3. Internet Speed Test Dashboard
- Measures true application-layer **Ping (ms)**, **Jitter (ms)**, **Download Throughput (Mbps)**, and **Upload Throughput (Mbps)** against Cloudflare Anycast edge servers.
- Includes a 270° logarithmic animated gauge, live progress indicator, and safe cancellation controls.
- **Zero Simulation**: 100% genuine network telemetry.

### 4. Smart Saved Wi-Fi Assistant & Switching (Opt-in)
- **Separate Measurements**: Independently evaluates Wi-Fi link status, signal percentage, and internet reachability.
- **Saved Networks Only**: When your current Wi-Fi signal drops below the configured weak-signal threshold (default: 30%) or internet reachability is lost, the assistant inspects nearby networks and identifies networks **already saved in Windows** that are currently in range.
- **No False Claims**: A stronger radio signal is a recommendation, not a guarantee of superior internet bandwidth. Networks are only recommended if their observed signal advantage meets or exceeds your configured hysteresis (default: +10%).
- **User-Approved Switching**: Never switches automatically without your explicit approval. An interactive dark-themed action window appears with an explicit **"Switch to [SSID]"** button and a **"Dismiss"** option.
- **Safe Windows Credential Management**: On Switch, Windows connects to the selected saved profile using native Windows-managed credentials. It verifies the resulting Wi-Fi association and performs an internet reachability check, honestly reporting success or failure.
- **Disruption Prevention**: Prompts are suppressed during active speed tests and are throttled by a configurable cooldown timer (default: 300 seconds) to prevent nagging.
- **Privacy Guarantees**: Network scanning and profile inspection are strictly local. Wi-Fi passwords and profile keys are never dumped or stored (`key=clear` is never run). No SSIDs, BSSIDs, or network telemetry are transmitted to Cloudflare or any external server.

### 5. Local Data Storage & Privacy
All persistent data is stored strictly on your local machine under your Windows user profile:
- **History**: `%LOCALAPPDATA%\iNetOMeter\history.json` (Stores up to the 5 newest completed tests).
- **Settings**: `%LOCALAPPDATA%\iNetOMeter\settings.json`
- **Zero Telemetry**: No IP addresses, locations, analytics, credentials, or personal telemetry are collected, stored, or transmitted.

### 6. Settings & Customization
- **Start with Windows**: Opt-in toggle that adds/removes a standard per-user shortcut in your Windows Startup folder (`shell:startup`). Requires no administrator rights.
- **Speed Format**: Choose between `Auto Bytes` (B/s, KB/s, MB/s, GB/s), `Auto Bits` (bps, Kbps, Mbps, Gbps), `Always Mbps`, or `Always MB/s`.
- **Refresh Frequency**: 500 ms (Fast), 1000 ms (Normal), or 2000 ms (Low CPU).
- **Meter Opacity**: Adjust translucent frosted appearance (70% - 100%).
- **Smart Notifications**: Opt-in desktop alerts for connection loss, connection restoration, and slow speed warnings below a custom threshold.
- **Smart Wi-Fi Assistant**: Opt-in toggle, configurable weak signal threshold (15% - 60%), signal advantage threshold (5% - 30%), and assistant cooldown (60s - 1800s).

---

## Manual Wi-Fi Testing Steps

To verify the Smart Wi-Fi Assistant manually in your environment:
1. Open **Settings** (right-click taskbar meter > Settings) and enable **"Enable Smart Wi-Fi Assistant (Opt-in)"**.
2. Set **Weak signal alert threshold** to a value higher than your current signal (e.g. 50% or 99% for immediate testing), and ensure you have at least one other saved Wi-Fi profile visible in Windows.
3. Click **Save Preferences**.
4. Within 25 seconds, the assistant detects the condition and displays the **Smart Wi-Fi Assistant** action card near the bottom-right taskbar.
5. Click **Dismiss**: Verify the card closes without making any connection changes and respects the cooldown.
6. Trigger the card again (or test with internet disconnected by unplugging your upstream router cable while Wi-Fi remains linked): Click **Switch to [SSID]**.
7. Observe that the button indicates "Connecting...", associates with the target saved network using Windows credentials, verifies reachability, and reports the outcome.

---

## Uninstallation

### Option A: From the Taskbar Menu
1. Right-click the taskbar meter.
2. Select **Uninstall iNetOMeter**.
3. Confirm uninstallation in the prompt. The app launches the uninstaller out-of-process and closes cleanly.
4. Your settings and test history in `%LOCALAPPDATA%\iNetOMeter` remain preserved.

### Option B: From Windows Settings
1. Open Windows **Settings** > **Apps** > **Apps & Features**.
2. Locate **iNetOMeter** in the list.
3. Click **Uninstall**.

### Option C: Portable Mode
1. Right-click the taskbar meter and select **Exit iNetOMeter**.
2. Delete the folder containing the extracted application files.
3. (Optional) To delete your saved settings and test history, delete `%LOCALAPPDATA%\iNetOMeter`.
