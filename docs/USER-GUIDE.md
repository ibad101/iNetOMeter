# iNetOMeter — User Guide

This guide covers day-to-day usage of **iNetOMeter**, including the taskbar widget, speed test dashboard, history viewer, settings, and Wi-Fi assistant.

---

## 1. The Taskbar Speed Meter

When launched, iNetOMeter docks automatically in your Windows taskbar adjacent to the notification area clock.

- **Download Row**: Displays the current downstream throughput with a red down-arrow badge (`#FF525C`).
- **Upload Row**: Displays the current upstream throughput with a blue up-arrow badge (`#4CA8FF`).
- **Auto-Hide Compatibility**: If your taskbar is configured to auto-hide, the widget smoothly hides and reveals synchronously with the taskbar.

### Primary Mouse Actions
- **Single Left Click**: Focuses the taskbar meter.
- **Double Left Click**: Immediately launches or brings the **Internet Speed Test** dashboard to the foreground.
- **Right Click**: Opens the context menu with all application actions.

---

## 2. Context Menu Actions

Right-click anywhere on the taskbar meter to open the context menu:

| Menu Item | Action |
| :--- | :--- |
| **iNetOMeter** (Header) | Application title header (informational, disabled). |
| **Run Speed Test** | Opens the speed test speedometer dashboard. |
| **View Last 5 Tests** | Opens the test history window. |
| **Settings** | Opens the preferences window. |
| **Uninstall iNetOMeter** | Initiates safe uninstallation with confirmation. |
| **Exit iNetOMeter** | Closes the application and releases system hooks. |

---

## 3. Running an Internet Speed Test

1. Open the speed test dashboard by double-clicking the taskbar meter or selecting **Run Speed Test** from the context menu.
2. Click **START SPEED TEST**.
3. The test progresses through three distinct stages:
   - **Ping & Jitter**: Sends multi-sample HTTP probes to measure baseline latency and jitter.
   - **Download Test**: Streams concurrent payloads from Cloudflare Anycast edge servers, animating the speedometer gauge in real time.
   - **Upload Test**: Transmits concurrent payload buffers to measure outbound line capacity.
4. When finished, your final Ping, Jitter, Download, and Upload speeds are displayed and automatically saved to your local history log.
5. If you need to stop early, click **CANCEL** at any time.

---

## 4. Viewing Test History

1. Right-click the taskbar meter and select **View Last 5 Tests**.
2. A window opens showing up to 5 of your most recent completed speed tests.
3. Each entry includes:
   - Date and local timestamp.
   - Ping ($ms$) and Jitter ($ms$).
   - Download speed ($Mbps$).
   - Upload speed ($Mbps$).
4. To remove all stored test records, click **Clear History**.

---

## 5. Configuring Preferences

Right-click the meter and select **Settings** to open the preferences window:

### Taskbar Settings
- **Start iNetOMeter with Windows**: Opt-in checkbox that adds or removes a per-user shortcut in your Windows Startup folder (`shell:startup`). Requires no admin privileges.
- **Taskbar Speed Unit Formatting**: Choose between `Auto Bytes` (default: B/s, KB/s, MB/s, GB/s), `Auto Bits` (bps, Kbps, Mbps, Gbps), `Always Mbps`, or `Always MB/s`.
- **Taskbar Refresh Frequency**: 500 ms (Fast), 1000 ms (Normal / Recommended), or 2000 ms (Low CPU footprint).
- **Taskbar Meter Opacity**: Adjust transparency slider between 70% and 100%.

### Smart Desktop Notifications
- **Enable Windows Desktop Notifications**: Master opt-in toggle (disabled by default).
- **Alert when internet connection is lost**: Shows a balloon tip when external internet reachability drops.
- **Alert when internet connection is restored**: Shows a notification when internet access recovers.
- **Alert if speed test download speed is below threshold**: Triggers after a completed speed test if the result is lower than your configured threshold.
- **Slow speed threshold (Mbps)**: Minimum acceptable speed (0.5 to 1000.0 Mbps, default 5.0 Mbps).
- **Notification cooldown (seconds)**: Rate-limiting interval (10 to 3600 seconds, default 60s) to prevent repeated alerts.

### Smart Wi-Fi Assistant
- **Enable Smart Wi-Fi Assistant**: Master opt-in toggle (disabled by default).
- **Weak signal alert threshold (%)**: Triggers suggestion when signal drops to or below this percentage (15% to 60%, default 30%).
- **Required signal advantage (%)**: Candidate network must be at least this much stronger than the current network to qualify for recommendation (5% to 30%, default 10%).
- **Assistant cooldown (seconds)**: Time to wait before repeating a suggestion (60 to 1800 seconds, default 300s).

---

## 6. Using the Smart Wi-Fi Assistant

When enabled in Settings:
1. If your Wi-Fi signal drops below the threshold for two consecutive evaluations, or if internet reachability is lost while Wi-Fi remains connected:
2. The assistant scans for nearby networks that match **profiles already saved on your PC**.
3. If an eligible network is found with a sufficient signal advantage, a sleek dark action card appears near your taskbar:
   - Displays the current network and signal.
   - Recommends the alternative saved network and its signal advantage.
   - Reminds you that Windows-managed saved credentials are used.
4. **Your Options**:
   - Click **Switch to [SSID]**: Windows connects to the saved network, verifies association and internet reachability, and reports the result.
   - Click **Dismiss**: Closes the card and activates the cooldown timer.
   - Ignore: Automatically closes after 15 seconds.
