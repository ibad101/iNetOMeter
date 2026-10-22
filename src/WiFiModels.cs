using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace iNetOMeter;

public class WiFiInterfaceInfo
{
    public string InterfaceName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public string Bssid { get; set; } = string.Empty;
    public int SignalPercent { get; set; }
    public string RadioType { get; set; } = string.Empty;
    public int Channel { get; set; }
    public string ProfileName { get; set; } = string.Empty;
}

public class WiFiCandidate
{
    public string Ssid { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int SignalPercent { get; set; }
    public string RadioType { get; set; } = string.Empty;
    public int SignalAdvantage { get; set; }
}

public class WiFiSwitchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string PreviousSsid { get; set; } = string.Empty;
    public string TargetSsid { get; set; } = string.Empty;
    public string ActualSsid { get; set; } = string.Empty;
    public bool InternetReachable { get; set; }

    public static WiFiSwitchResult Succeeded(string prevSsid, string newSsid, bool reachable) => new()
    {
        Success = true,
        PreviousSsid = prevSsid,
        TargetSsid = newSsid,
        ActualSsid = newSsid,
        InternetReachable = reachable
    };

    public static WiFiSwitchResult Failed(string prevSsid, string targetSsid, string error, string? actualSsid = null, bool reachable = false) => new()
    {
        Success = false,
        ErrorMessage = error,
        PreviousSsid = prevSsid,
        TargetSsid = targetSsid,
        ActualSsid = actualSsid ?? prevSsid,
        InternetReachable = reachable
    };
}

public interface IWiFiService
{
    Task<WiFiInterfaceInfo?> GetCurrentInterfaceAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetSavedProfilesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WiFiCandidate>> ScanAvailableSavedNetworksAsync(CancellationToken ct = default);
    Task<WiFiSwitchResult> SwitchToProfileAsync(string profileName, string? ssid, CancellationToken ct = default);
}
