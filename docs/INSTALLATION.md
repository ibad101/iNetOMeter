# iNetOMeter — Installation & Deployment Guide

This guide details all methods for installing, updating, running portably, and uninstalling **iNetOMeter** on Windows 10 x64 and Windows 11.

---

## Prerequisites

- **Operating System**: Windows 10 (Version 2004 or later / Build 19041+, including Windows 10 IoT Enterprise LTSC 2021) or Windows 11.
- **Architecture**: 64-bit (x64).
- **Runtime**: **Microsoft .NET 8.0 Desktop Runtime (x64)**.
  - Download from: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
  - Choose **".NET Desktop Runtime 8.0.x" > "Windows x64"**.

---

## Installation Methods

### Method 1: Portable Release (Recommended for quick testing)

The portable release requires no installation, no registry modifications, and no administrator privileges.

1. Download `iNetOMeter-1.0.0-win-x64-portable.zip` from the GitHub Releases page.
2. Extract the ZIP archive into a local folder:
   - Example: `C:\Users\<YourUsername>\AppData\Local\Programs\iNetOMeter` or `C:\Tools\iNetOMeter`.
3. Double-click `iNetOMeter.exe`.
4. The live speed meter will dock immediately on your Windows taskbar near the clock.

---

### Method 2: Per-User PowerShell Installer (Zero Prerequisites)

The repository provides a native PowerShell installer script that requires no third-party installer compiler or admin rights.

1. Download and extract `iNetOMeter-1.0.0-win-x64-portable.zip`.
2. Open PowerShell in the extracted folder.
3. Run:
   ```powershell
   .\Install-iNetOMeter.ps1
   ```
4. Optional flags:
   - `-CreateDesktopShortcut`: Creates a shortcut on your Windows desktop.
   - `-StartWithWindows`: Creates a shortcut in your Startup folder (`shell:startup`).
   - `-LaunchAfterInstall`: Launches the application immediately after installation.

#### What this installer does:
- Copies program files into `%LOCALAPPDATA%\Programs\iNetOMeter`.
- Creates a Start Menu shortcut: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\iNetOMeter.lnk`.
- Registers the application in Windows **Settings > Apps & Features** (`HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\iNetOMeter`).

---

### Method 3: Inno Setup Executable

If the standalone setup executable (`iNetOMeter-1.0.0-win-x64-Setup.exe`) was generated using Inno Setup 6:
1. Double-click the setup executable.
2. Select desired desktop and startup shortcut tasks.
3. Complete the setup wizard.

---

## Updating iNetOMeter

### Updating Portable Installations
1. Right-click the taskbar meter and select **Exit iNetOMeter**.
2. Download the new portable ZIP release.
3. Overwrite the files in your existing portable folder.
4. Launch `iNetOMeter.exe`.
5. Your settings and test history in `%LOCALAPPDATA%\iNetOMeter` are preserved automatically.

### Updating Installed Versions
1. Exit iNetOMeter.
2. Run `Install-iNetOMeter.ps1` from the new version package (or run the new setup executable).
3. The installer overwrites program binaries while keeping existing user data intact.

---

## Uninstallation

### Option 1: Via the Taskbar Meter
1. Right-click the taskbar meter.
2. Select **Uninstall iNetOMeter**.
3. Confirm the prompt. The application safely launches the uninstaller out-of-process and terminates immediately.

### Option 2: Via Windows Apps & Features
1. Open Windows **Settings** (<kbd>Win</kbd> + <kbd>I</kbd>).
2. Go to **Apps** > **Apps & Features**.
3. Scroll or search for **iNetOMeter**.
4. Click **Uninstall**.

### Option 3: Manual Removal (Portable Mode)
1. Right-click the meter and click **Exit iNetOMeter**.
2. Delete the folder containing the extracted portable files.
3. (Optional) To delete your test records and preferences, delete `%LOCALAPPDATA%\iNetOMeter`.
