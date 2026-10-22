# iNetOMeter — Feature Catalog & Technical Specifications

This document provides a comprehensive breakdown of all features implemented in **iNetOMeter v1.0.0**, including their architectural design, default values, user settings, and constraints.

---

## 1. Live Taskbar Speed Meter

### Purpose
Provides immediate, continuous awareness of real-time network throughput directly on the Windows desktop without taking up valuable screen space or requiring overlay windows.

### Visual Architecture
- **Dimensions**: 94 × 40 physical pixels.
- **Top Row**: Red download badge (`#45252B` background, `#FF525C` downward arrow, bold red `DOWNLOAD` label, bold white numeric speed value).
- **Bottom Row**: Blue upload badge (`#1E354A` background, `#4CA8FF` upward arrow, bold blue `UPLOAD` label, bold white numeric speed value).
- **Divider & Border**: Thin subtle divider line (`#383D48`) and perimeter border (`#303642`).
- **Background**: Translucent frosted dark charcoal (`#20242B`) with configurable opacity (70% - 100%, default 96%).

### Anchoring & Synchronization
- Tracks the Windows notification area (`TrayNotifyWnd`) inside `Shell_TrayWnd`.
- Anchors immediately to the left of the notification area on bottom and top taskbars, or directly above it on vertical (left/right) taskbars.
- Synchronously monitors taskbar auto-hide state using a 150ms tracking loop, sliding out of view when the taskbar auto-hides and reappearing when the taskbar reveals.
- Registers the `"TaskbarCreated"` window message and hooks `WM_DISPLAYCHANGE` and `WM_DPICHANGED` to immediately re-dock if Windows Explorer restarts or monitor resolutions change.

### Network Monitoring Engine
- Invokes native `GetIfTable2` (`IPHlpApi.dll`) to query hardware interface byte counters with sub-millisecond overhead.
- Filters out loopback and non-operational interfaces.
- Computes delta throughput per sampling period:
  $$\text{Speed (Bytes/sec)} = \frac{\Delta \text{Bytes}}{\Delta t}$$

---

## 2. Speed Unit Formatting

### Purpose
Adapts bandwidth readouts to user preferences across both data rate (bits/sec) and transfer size (bytes/sec) conventions.

### Supported Modes
1. **Auto Bytes (Default)**: Automatically selects `B/s`, `KB/s`, `MB/s`, or `GB/s` based on throughput magnitude.
2. **Auto Bits**: Automatically selects `bps`, `Kbps`, `Mbps`, or `Gbps` based on bit rate magnitude.
3. **Always Mbps**: Fixed representation in Megabits per second (e.g. `14.25 Mbps`), ideal for comparing against ISP plan ratings.
4. **Always MB/s**: Fixed representation in Megabytes per second (e.g. `1.78 MB/s`), ideal for file download comparisons.

---

## 3. Internet Speed Test Dashboard

### Purpose
Performs an on-demand, rigorous measurement of internet performance against public Anycast edge infrastructure.

### Test Metrics
- **Ping (Latency)**: Round-trip time in milliseconds ($ms$) using multi-sample HTTP probes.
- **Jitter**: Latency variation ($ms$) computed from consecutive sample deltas:
  $$\text{Jitter} = \frac{1}{N-1} \sum_{i=1}^{N-1} |RTT_{i+1} - RTT_i|$$
- **Download Speed**: Multi-stream concurrent payload streaming over HTTP/2, measuring sustained data transfer in Megabits per second ($Mbps$).
- **Upload Speed**: Multi-stream payload transmission over HTTP/2, measuring throughput in $Mbps$.

### UI Components
- **270° Logarithmic Gauge**: Dynamically spans from 0.1 Mbps up to 1000 Mbps with smoothed animation.
- **Real-Time Progress Indicator**: Visual bar displaying completion percentage across test phases.
- **Safe Cancellation**: Clicking "CANCEL" immediately aborts streaming sockets and frees resources without locking the UI.

---

## 4. Test History Persistence

### Purpose
Maintains a local log of speed test performance over time.

### Implementation Details
- **Storage Location**: `%LOCALAPPDATA%\iNetOMeter\history.json`.
- **Retention**: Strictly retains up to the 5 newest successful completed test records.
- **Integrity**: Excludes aborted, cancelled, or failed tests.
- **Error Recovery**: Automatically reinitializes a clean state if the JSON file is modified or corrupted externally.
- **Clear History**: Users can wipe all saved records at any time using the "Clear History" button in the History window.

---

## 5. Smart Desktop Notifications

### Purpose
Alerts users to network changes and bandwidth anomalies via native Windows balloon tips / toast notifications.

### Configuration & Defaults
- **Master Toggle (`NotificationsEnabled`)**: `false` (Disabled by default / strictly opt-in).
- **Disconnect Alert (`NotifyOnDisconnect`)**: Alerts when internet reachability drops.
- **Restore Alert (`NotifyOnRestore`)**: Alerts when internet reachability returns.
- **Slow Speed Warning (`NotifyOnSlowSpeed`)**: Alerts if a user-initiated speed test falls below a configured threshold.
- **Slow Speed Threshold**: Configurable between 0.5 Mbps and 1000.0 Mbps (default: 5.0 Mbps).
- **Notification Cooldown**: Rate limit configurable between 10 seconds and 3600 seconds (default: 60 seconds) to prevent alert storms.

### Debouncing Architecture
- Requires **2 consecutive failed reachability probes** before declaring a disconnect.
- Requires **2 consecutive successful probes** before declaring internet restored.
- Zero false alerts on initial application startup.

---

## 6. Smart Saved Wi-Fi Assistant & Switching

### Purpose
Recommends stronger Wi-Fi connections when the current connection becomes degraded or drops out, using only profiles already configured on the user's computer.

### Behavior & Constraints
- **Opt-In State**: `WiFiAssistantEnabled = false` by default.
- **Saved Networks Only**: Intersects visible networks against Windows saved profiles (`netsh wlan show profiles`). Never suggests unknown or open public networks.
- **Signal Hysteresis**: Recommends an alternative network only if its signal advantage exceeds the hysteresis threshold (default: candidate must be >= current + 10%).
- **Weak Signal Threshold**: Triggers when current Wi-Fi signal <= 30% (default) across 2 consecutive evaluations.
- **Internet Lost Trigger**: Triggers immediately if internet connectivity drops while Wi-Fi remains associated.
- **User Confirmation Required**: Renders `WiFiSuggestionPopup`, a native dark floating card. The user must explicitly click "Switch to [SSID]" or "Dismiss".
- **Speed Test Guard**: Prompts are suppressed while a speed test is actively streaming.
- **Cooldown**: Throttles suggestions via a 300-second cooldown timer.

---

## 7. Context Menu & Taskbar Actions

### Menu Items
1. **Run Speed Test**: Launches or activates `SpeedTestForm`.
2. **View Last 5 Tests**: Launches or activates `HistoryForm`.
3. **Settings**: Launches or activates `SettingsForm`.
4. **Uninstall iNetOMeter**: Initiates guided uninstallation.
5. **Exit iNetOMeter**: Shuts down the application and cleanly releases all hooks, timers, and tray resources.

---

## 8. Guided Uninstallation

### Portable Execution
If running portably, the app recognizes that no Windows installer registry keys exist. It informs the user where the application folder is located, advises that `%LOCALAPPDATA%\iNetOMeter` data is preserved, and offers an "Exit Now" button so the folder can be deleted.

### Installed Execution
If installed via setup:
1. Prompts for confirmation and explains data preservation.
2. Spawns `Uninstall-iNetOMeter.ps1` (or `unins000.exe`) with a detached process delay.
3. Immediately shuts down `iNetOMeter.exe` so program files are not locked during deletion.
