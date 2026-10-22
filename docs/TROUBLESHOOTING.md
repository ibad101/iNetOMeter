# iNetOMeter — Troubleshooting & Diagnostics

This guide addresses common questions, diagnostic checks, and troubleshooting steps for **iNetOMeter**.

---

## Common Issues & Solutions

### 1. The application fails to start or shows a .NET error dialog
- **Cause**: The .NET 8.0 Windows Desktop Runtime is missing.
- **Solution**: Download and install the **.NET Desktop Runtime 8.0.x (x64)** from Microsoft:
  [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
  *Note: Make sure to install the "Desktop Runtime", not the standard base runtime or ASP.NET runtime.*

---

### 2. The taskbar speed meter does not appear or overlaps taskbar icons
- **Check Taskbar Position**: iNetOMeter anchors to the primary taskbar next to the notification area clock (`TrayNotifyWnd`).
- **If Windows Explorer restarted**: iNetOMeter listens for the `TaskbarCreated` message to automatically re-anchor. If it did not re-anchor, right-click the iNetOMeter tray icon, select **Exit**, and relaunch.
- **Custom Shell Software**: Third-party shells (e.g. StartAllBack, RetroBar, Cairo Desktop) modify native taskbar windows. iNetOMeter targets standard Windows 10/11 taskbars.

---

### 3. Taskbar meter stays hidden when taskbar auto-hide is enabled
- **Behavior**: iNetOMeter checks taskbar visibility every 150ms. When the taskbar slides off-screen, iNetOMeter hides. When your mouse hovers over the screen edge to reveal the taskbar, iNetOMeter reveals with it.
- **Fix**: Move your mouse cursor to the bottom of the screen to reveal the taskbar. If the taskbar fails to reveal, check that Windows Explorer is responding.

---

### 4. Smart Wi-Fi Assistant does not show any network recommendations
- **Assistant Disabled**: By default, the assistant is **off**. Open Settings and verify that **"Enable Smart Wi-Fi Assistant (Opt-in)"** is checked.
- **Saved Networks Only**: The assistant will *only* recommend networks that are **already saved in Windows**. It will never recommend unfamiliar or public networks.
- **Signal Hysteresis**: Even if another saved network is in range, it must be at least **10% stronger** (or your configured hysteresis threshold) than your current connection.
- **Active Speed Test**: The assistant deliberately suppresses prompts during active speed tests to prevent interrupting live network benchmarks.
- **Cooldown Active**: If a recommendation was shown recently, it is suppressed until the cooldown timer (default: 300 seconds) has elapsed.
- **Ethernet Connection**: If your computer is connected via wired Ethernet, Wi-Fi switching is unnecessary and suppressed.

---

### 5. Speed Test shows "Network connection error" or fails to start
- **Firewall / Proxy**: Ensure your firewall allows outbound HTTPS traffic on port 443 to `speed.cloudflare.com`.
- **Offline**: Check that your device has an active internet connection.
- **DNS Resolution**: Verify that your system can resolve `speed.cloudflare.com`. Test in PowerShell:
  ```powershell
  Resolve-DnsName speed.cloudflare.com
  ```

---

### 6. Desktop notifications do not appear
- **Master Notifications Toggle**: Open Settings and ensure **"Enable Windows Desktop Notifications"** is checked.
- **Windows Focus Assist / Do Not Disturb**: Check that Windows **Focus Assist** (Windows 10) or **Do Not Disturb** (Windows 11) is not suppressing notifications:
  - Windows 10: Open Action Center > click **Focus assist** to turn it **Off**.
  - Windows 11: Open Settings > System > Notifications > check Focus / Do Not Disturb settings.
- **Notification Cooldown**: Notifications are rate-limited by the configured cooldown window (default: 60s) to prevent spam.

---

### 7. Settings or history are not saving
- **Permissions**: Verify that your user account has write access to `%LOCALAPPDATA%\iNetOMeter`.
- **Corrupt JSON File**: If a JSON file was edited with syntax errors, rename or delete `%LOCALAPPDATA%\iNetOMeter\settings.json` and `%LOCALAPPDATA%\iNetOMeter\history.json` to allow the application to regenerate fresh default files.

---

## Diagnostic Checklist for Support

When reporting an issue on GitHub, please include:
1. Windows Version and Build (Run `winver` — e.g. Windows 10 Build 19044.x or Windows 11 Build 22631.x).
2. .NET Runtime version installed (Run `dotnet --list-runtimes`).
3. Taskbar configuration: position (bottom/top/left/right), auto-hide enabled/disabled, single or multi-monitor.
4. Network connection type (Wi-Fi model, Ethernet, or both).
5. Exact error message or behavior observed.
