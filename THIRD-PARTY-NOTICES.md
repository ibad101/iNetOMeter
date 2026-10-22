# iNetOMeter — Third-Party Notices & Attributions

iNetOMeter is a zero-external-NuGet application. It relies directly on the built-in Microsoft .NET 8 runtime and Windows operating system APIs.

---

## 1. Microsoft .NET Runtime and Windows Forms
- **Copyright**: © .NET Foundation and Contributors
- **License**: MIT License
- **URL**: https://github.com/dotnet/runtime and https://github.com/dotnet/winforms

---

## 2. Speed Test Endpoints: Cloudflare Anycast Network
- **Service**: Cloudflare Speed Test (`speed.cloudflare.com`)
- **Usage**: Diagnostic speed measurement via `GET /__down` and `POST /__up`.
- **Terms**: https://speed.cloudflare.com/terms
- **Privacy**: https://www.cloudflare.com/privacypolicy/
- **Data Policy**: No personal data, client IP, or location telemetry is stored, logged, or shared by iNetOMeter.

---

## 3. Windows Shell & Win32 APIs
- Interfaces used: `IShellLinkW`, `IPersistFile`, `Shell_NotifyIcon`, `SetWinEventHook`.
- **Copyright**: © Microsoft Corporation
