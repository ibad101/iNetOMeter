using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iNetOMeter;

public class WiFiAssistant : IDisposable
{
    private static WiFiAssistant? _instance;
    private static readonly object _syncLock = new();

    public static WiFiAssistant Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new WiFiAssistant();
                }
            }
            return _instance;
        }
    }

    private readonly IWiFiService _wifiService;
    private readonly ConnectivityMonitor? _connectivityMonitor;
    private readonly System.Threading.Timer _timer;

    private int _consecutiveWeakReadings = 0;
    private DateTime _lastPromptTime = DateTime.MinValue;
    private bool _isEvaluating = false;
    private bool _disposed;

    // Synchronization context to dispatch popup on UI thread
    private SynchronizationContext? _uiContext;

    public DateTime LastPromptTime => _lastPromptTime;
    public int ConsecutiveWeakReadings => _consecutiveWeakReadings;

    public WiFiAssistant(IWiFiService? wifiService = null, ConnectivityMonitor? connectivityMonitor = null)
    {
        _wifiService = wifiService ?? WiFiService.Instance;
        _connectivityMonitor = connectivityMonitor;
        _uiContext = SynchronizationContext.Current;

        if (_connectivityMonitor != null)
        {
            _connectivityMonitor.ConnectivityChanged += OnConnectivityChanged;
        }

        // Periodic check every 25 seconds (initial delay 5 seconds)
        _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(25));
    }

    public void SetUiContext(SynchronizationContext context)
    {
        _uiContext = context;
    }

    private void OnTimerTick(object? state)
    {
        _ = EvaluateAsync();
    }

    private void OnConnectivityChanged(ConnectivityState oldState, ConnectivityState newState)
    {
        if (newState == ConnectivityState.Disconnected)
        {
            // Immediate check when internet reachability drops
            _ = EvaluateAsync();
        }
    }

    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        if (_disposed || _isEvaluating) return;

        var settings = SettingsService.Instance.Current;
        if (!settings.WiFiAssistantEnabled)
        {
            _consecutiveWeakReadings = 0;
            return;
        }

        // Never disturb an active speed test
        if (SpeedTestForm.IsTestActive)
        {
            return;
        }

        // Check cooldown window
        double elapsedSeconds = (DateTime.UtcNow - _lastPromptTime).TotalSeconds;
        if (elapsedSeconds < settings.WiFiCooldownSeconds)
        {
            return;
        }

        _isEvaluating = true;
        try
        {
            var iface = await _wifiService.GetCurrentInterfaceAsync(ct).ConfigureAwait(false);
            if (iface == null || !iface.IsConnected || string.IsNullOrEmpty(iface.Ssid))
            {
                _consecutiveWeakReadings = 0;
                return;
            }

            bool isInternetLost = _connectivityMonitor != null && _connectivityMonitor.CurrentState == ConnectivityState.Disconnected;
            bool isSignalWeak = iface.SignalPercent <= settings.WiFiWeakSignalThresholdPercent;

            if (isSignalWeak)
            {
                _consecutiveWeakReadings++;
            }
            else
            {
                _consecutiveWeakReadings = 0;
            }

            // For weak-signal triggers without internet loss, require 2 consecutive weak readings (debounce)
            if (!isInternetLost && _consecutiveWeakReadings < 2)
            {
                return;
            }

            // If internet is connected and signal is healthy, no action needed
            if (!isInternetLost && !isSignalWeak)
            {
                return;
            }

            // Scan for candidate saved networks
            var candidates = await _wifiService.ScanAvailableSavedNetworksAsync(ct).ConfigureAwait(false);
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            WiFiCandidate? bestCandidate = null;

            if (isInternetLost)
            {
                // When internet is lost, recommend the strongest saved network in range
                bestCandidate = candidates.FirstOrDefault();
            }
            else
            {
                // When signal is weak, require candidate to exceed hysteresis threshold
                bestCandidate = candidates.FirstOrDefault(c => c.SignalAdvantage >= settings.WiFiSignalHysteresisPercent);
            }

            if (bestCandidate == null)
            {
                return;
            }

            // Passed all checks! Update prompt timestamp to enforce cooldown
            _lastPromptTime = DateTime.UtcNow;

            // Dispatch popup to UI thread
            PostPopupToUi(iface.Ssid, iface.SignalPercent, isInternetLost, bestCandidate);
        }
        catch
        {
            // Silently handle any transient shell/WLAN inspection errors
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    private void PostPopupToUi(string currentSsid, int currentSignal, bool isInternetLost, WiFiCandidate candidate)
    {
        void ShowPopup()
        {
            WiFiSuggestionPopup.ShowOrUpdate(
                currentSsid,
                currentSignal,
                isInternetLost,
                candidate,
                async (target) =>
                {
                    var result = await _wifiService.SwitchToProfileAsync(target.ProfileName, target.Ssid).ConfigureAwait(true);
                    if (result.Success && result.InternetReachable && _connectivityMonitor != null)
                    {
                        // Refresh reachability in the monitor
                        _ = _connectivityMonitor.TriggerCheckAsync();
                    }
                    return result;
                }
            );
        }

        if (_uiContext != null)
        {
            _uiContext.Post(_ => ShowPopup(), null);
        }
        else if (Application.OpenForms.Count > 0)
        {
            Application.OpenForms[0]?.BeginInvoke((Action)ShowPopup);
        }
        else
        {
            ShowPopup();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_connectivityMonitor != null)
            {
                _connectivityMonitor.ConnectivityChanged -= OnConnectivityChanged;
            }
            _timer.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
