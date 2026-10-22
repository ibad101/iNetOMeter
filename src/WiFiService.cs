using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace iNetOMeter;

public class WiFiService : IWiFiService
{
    private static WiFiService? _instance;
    private static readonly object _syncLock = new();

    public static WiFiService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new WiFiService();
                }
            }
            return _instance;
        }
    }

    private readonly IConnectivityProbe _probe;

    public WiFiService(IConnectivityProbe? probe = null)
    {
        _probe = probe ?? new DefaultConnectivityProbe();
    }

    public async Task<WiFiInterfaceInfo?> GetCurrentInterfaceAsync(CancellationToken ct = default)
    {
        string output = await RunNetshAsync("wlan show interfaces", ct).ConfigureAwait(false);
        return ParseInterfaceInfo(output);
    }

    public async Task<IReadOnlyList<string>> GetSavedProfilesAsync(CancellationToken ct = default)
    {
        string output = await RunNetshAsync("wlan show profiles", ct).ConfigureAwait(false);
        return ParseSavedProfiles(output);
    }

    public async Task<IReadOnlyList<WiFiCandidate>> ScanAvailableSavedNetworksAsync(CancellationToken ct = default)
    {
        var current = await GetCurrentInterfaceAsync(ct).ConfigureAwait(false);
        int currentSignal = current?.IsConnected == true ? current.SignalPercent : 0;
        string currentSsid = current?.Ssid ?? string.Empty;

        var savedProfiles = await GetSavedProfilesAsync(ct).ConfigureAwait(false);
        if (savedProfiles.Count == 0) return Array.Empty<WiFiCandidate>();

        string scanOutput = await RunNetshAsync("wlan show networks mode=bssid", ct).ConfigureAwait(false);
        var visible = ParseVisibleNetworks(scanOutput);

        var savedSet = new HashSet<string>(savedProfiles, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<WiFiCandidate>();

        foreach (var net in visible)
        {
            // Skip the network we are already connected to
            if (!string.IsNullOrEmpty(currentSsid) && string.Equals(net.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Must match a saved profile in Windows
            if (savedSet.Contains(net.Ssid))
            {
                candidates.Add(new WiFiCandidate
                {
                    Ssid = net.Ssid,
                    ProfileName = net.Ssid,
                    SignalPercent = net.SignalPercent,
                    RadioType = net.RadioType,
                    SignalAdvantage = net.SignalPercent - currentSignal
                });
            }
        }

        // Rank by signal strength descending
        return candidates.OrderByDescending(c => c.SignalPercent).ToList();
    }

    public async Task<WiFiSwitchResult> SwitchToProfileAsync(string profileName, string? ssid, CancellationToken ct = default)
    {
        var beforeInterface = await GetCurrentInterfaceAsync(ct).ConfigureAwait(false);
        string prevSsid = beforeInterface?.Ssid ?? "Disconnected";
        string target = string.IsNullOrWhiteSpace(ssid) ? profileName : ssid;

        // Command: netsh wlan connect name="<profile>" [ssid="<target>"]
        string connectArgs = string.IsNullOrWhiteSpace(ssid)
            ? $"wlan connect name=\"{profileName}\""
            : $"wlan connect name=\"{profileName}\" ssid=\"{ssid}\"";

        string connectOutput = await RunNetshAsync(connectArgs, ct).ConfigureAwait(false);

        // Check if Windows rejected the command immediately
        if (connectOutput.Contains("There is no profile", StringComparison.OrdinalIgnoreCase) ||
            connectOutput.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            connectOutput.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return WiFiSwitchResult.Failed(prevSsid, target, connectOutput.Trim());
        }

        // Poll for association (up to 7 seconds)
        DateTime deadline = DateTime.UtcNow.AddSeconds(7);
        bool associated = false;
        WiFiInterfaceInfo? currentInfo = null;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(800, ct).ConfigureAwait(false);
            currentInfo = await GetCurrentInterfaceAsync(ct).ConfigureAwait(false);

            if (currentInfo != null && currentInfo.IsConnected &&
                (string.Equals(currentInfo.Ssid, target, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(currentInfo.ProfileName, profileName, StringComparison.OrdinalIgnoreCase)))
            {
                associated = true;
                break;
            }
        }

        if (!associated)
        {
            string actual = currentInfo?.Ssid ?? "Unknown";
            return WiFiSwitchResult.Failed(prevSsid, target, "Failed to associate with target network within 7 seconds.", actualSsid: actual);
        }

        // Associated successfully! Now verify genuine internet reachability
        using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        bool reachable = await _probe.CheckInternetReachabilityAsync(probeCts.Token).ConfigureAwait(false);

        if (!reachable)
        {
            return WiFiSwitchResult.Failed(prevSsid, target, "Associated with Wi-Fi, but internet reachability could not be verified.", actualSsid: target, reachable: false);
        }

        return WiFiSwitchResult.Succeeded(prevSsid, target, reachable: true);
    }

    #region Static Parsers for Deterministic Testing

    public static WiFiInterfaceInfo? ParseInterfaceInfo(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var info = new WiFiInterfaceInfo();
        bool foundInterface = false;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            string key = line.Substring(0, colonIdx).Trim();
            string val = line.Substring(colonIdx + 1).Trim();

            if (key.Equals("Name", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.InterfaceName))
            {
                info.InterfaceName = val;
                foundInterface = true;
            }
            else if (key.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                info.IsConnected = val.Equals("connected", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
            {
                info.Ssid = val;
            }
            else if (key.Equals("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                info.Bssid = val;
            }
            else if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
            {
                string numOnly = Regex.Match(val, @"\d+").Value;
                if (int.TryParse(numOnly, out int sig))
                {
                    info.SignalPercent = sig;
                }
            }
            else if (key.Equals("Radio type", StringComparison.OrdinalIgnoreCase))
            {
                info.RadioType = val;
            }
            else if (key.Equals("Channel", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(val, out int ch))
                {
                    info.Channel = ch;
                }
            }
            else if (key.Equals("Profile", StringComparison.OrdinalIgnoreCase))
            {
                info.ProfileName = val;
            }
        }

        return foundInterface ? info : null;
    }

    public static IReadOnlyList<string> ParseSavedProfiles(string output)
    {
        var profiles = new List<string>();
        if (string.IsNullOrWhiteSpace(output)) return profiles;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Lines like: "All User Profile     : ProfileName" or "User Profile : ProfileName"
            if (line.Contains("Profile", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    string profile = line.Substring(colonIdx + 1).Trim();
                    if (!string.IsNullOrEmpty(profile) && !profile.Equals("<None>", StringComparison.OrdinalIgnoreCase))
                    {
                        profiles.Add(profile);
                    }
                }
            }
        }

        return profiles;
    }

    public static IReadOnlyList<WiFiCandidate> ParseVisibleNetworks(string output)
    {
        var list = new List<WiFiCandidate>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string currentSsid = string.Empty;
        int maxSignalForCurrent = 0;
        string radioType = string.Empty;

        void CommitCurrent()
        {
            if (!string.IsNullOrEmpty(currentSsid) && maxSignalForCurrent > 0)
            {
                // If this SSID already in list, update with highest signal
                var existing = list.FirstOrDefault(c => string.Equals(c.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (maxSignalForCurrent > existing.SignalPercent)
                    {
                        existing.SignalPercent = maxSignalForCurrent;
                    }
                }
                else
                {
                    list.Add(new WiFiCandidate
                    {
                        Ssid = currentSsid,
                        ProfileName = currentSsid,
                        SignalPercent = maxSignalForCurrent,
                        RadioType = radioType
                    });
                }
            }
        }

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase))
            {
                CommitCurrent();
                int colonIdx = line.IndexOf(':');
                currentSsid = colonIdx >= 0 ? line.Substring(colonIdx + 1).Trim() : string.Empty;
                maxSignalForCurrent = 0;
                radioType = string.Empty;
            }
            else if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    string numOnly = Regex.Match(line.Substring(colonIdx + 1), @"\d+").Value;
                    if (int.TryParse(numOnly, out int sig) && sig > maxSignalForCurrent)
                    {
                        maxSignalForCurrent = sig;
                    }
                }
            }
            else if (line.StartsWith("Radio type", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx >= 0 && string.IsNullOrEmpty(radioType))
                {
                    radioType = line.Substring(colonIdx + 1).Trim();
                }
            }
        }

        CommitCurrent();
        return list;
    }

    #endregion

    private static async Task<string> RunNetshAsync(string arguments, CancellationToken ct)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            proc.Start();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(5));

            string stdout = await proc.StandardOutput.ReadToEndAsync(linkedCts.Token).ConfigureAwait(false);
            await proc.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

            return stdout;
        }
        catch (Exception ex)
        {
            return $"Error executing netsh {arguments}: {ex.Message}";
        }
    }
}
