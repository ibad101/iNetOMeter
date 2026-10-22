# iNetOMeter — Privacy & Security Statement

**iNetOMeter** is engineered with a strict privacy-first, zero-telemetry architecture. This document provides a transparent audit of all data handling, network communications, and Windows system interactions.

---

## 1. Summary of Principles

- **Zero Analytics or Telemetry**: No tracking libraries, telemetry SDKs, or background crash reporters exist in the codebase.
- **Zero Cloud Accounts**: No login, user registration, license validation, or cloud profiles are required or supported.
- **100% Local Storage**: All settings and speed-test logs reside exclusively on your local storage drive under your Windows user profile.
- **Zero Wi-Fi Password Extraction**: Wi-Fi credentials are never accessed, retrieved, logged, or transmitted.
- **Minimal System Privileges**: Operates entirely in user mode with no administrative (UAC) elevation required.

---

## 2. Network Communications Audit

iNetOMeter only makes outbound network requests for two explicit purposes:

### A. Reachability Probes (Connectivity Monitor)
- **Target Endpoint**: `https://speed.cloudflare.com/__down?bytes=0`
- **Method**: HTTP `GET` with 0-byte payload (headers only).
- **Frequency**: Every 30 seconds when idle, or immediately upon a Windows network state change event (`NetworkChange.NetworkAvailabilityChanged`, `NetworkChange.NetworkAddressChanged`, or system resume).
- **Data Sent**: Standard HTTP request headers (`User-Agent: iNetOMeter/1.0`). **No identifying user tokens, IP history, or device IDs are transmitted.**

### B. Speed Test Transfers (On-Demand Only)
- **Target Endpoints**: Cloudflare Anycast edge servers (`https://speed.cloudflare.com/__down` and `https://speed.cloudflare.com/__up`).
- **Trigger**: Strictly user-initiated when you click **START SPEED TEST**.
- **Data Sent**: Synthetic, random binary buffers during the upload phase. No personal or filesystem data is ever transmitted.

---

## 3. Wi-Fi Security & Credential Protection

The Smart Wi-Fi Assistant is built to uphold your security:
- **No Password Dumping**: Does **not** execute `netsh wlan show profile name="..." key=clear`. Wi-Fi security keys remain protected inside the Windows credential store.
- **Windows-Managed Authentication**: Network association is delegated entirely to Windows via `netsh wlan connect name="<profile>"`.
- **Saved Networks Only**: Only networks you have previously joined and saved in Windows are considered. The app will never attempt to connect to unfamiliar, open, or rogue access points.
- **No External Wi-Fi Telemetry**: Nearby SSIDs, BSSIDs, MAC addresses, and signal strengths are never sent to external servers or logged to the cloud.

---

## 4. Local Data Storage

All persistent data is stored strictly in your local user profile:

| Data Type | Path | Purpose |
| :--- | :--- | :--- |
| **Settings** | `%LOCALAPPDATA%\iNetOMeter\settings.json` | Stores your UI preferences (opacity, interval, unit formats, notification toggles). |
| **History** | `%LOCALAPPDATA%\iNetOMeter\history.json` | Stores up to the 5 most recent completed speed test records (timestamp, ping, download, upload). |
| **Startup Shortcut** | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\iNetOMeter.lnk` | Created only when "Start iNetOMeter with Windows" is checked. |

### How to Inspect or Delete Local Data
1. Press <kbd>Win</kbd> + <kbd>R</kbd>, type `%LOCALAPPDATA%\iNetOMeter`, and press Enter.
2. You can inspect both JSON files with any plain text editor.
3. Deleting this folder removes all stored history and returns the app to default settings.

---

## 5. Third-Party Dependencies

iNetOMeter uses **zero third-party NuGet packages**. The complete dependency chain consists only of:
- **Microsoft.NETCore.App** (CoreCLR runtime)
- **Microsoft.WindowsDesktop.App.WindowsForms** (Standard Windows Forms GUI framework)
- **IPHlpApi.dll & User32.dll** (Official native Windows system DLLs)

Because there are no third-party package dependencies, there is zero risk of supply-chain dependency injection or hidden tracking packages.
