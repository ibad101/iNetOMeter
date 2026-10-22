using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace iNetOMeter;

public class DefaultConnectivityProbe : IConnectivityProbe, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public DefaultConnectivityProbe(HttpClient? client = null)
    {
        if (client != null)
        {
            _client = client;
            _ownsClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(3),
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            };
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            _client.DefaultRequestHeaders.Add("User-Agent", "iNetOMeter/1.0");
            _ownsClient = true;
        }
    }

    public async Task<bool> CheckInternetReachabilityAsync(CancellationToken ct)
    {
        try
        {
            // Minimal 0-byte header probe to Cloudflare Anycast edge
            using var response = await _client.GetAsync("https://speed.cloudflare.com/__down?bytes=0", HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}

public class ConnectivityMonitor : IDisposable
{
    private readonly IConnectivityProbe _probe;
    private readonly INotificationService _notificationService;
    private readonly System.Threading.Timer _pollingTimer;

    private ConnectivityState _currentState = ConnectivityState.Unknown;
    private int _consecutiveFailures = 0;
    private int _consecutiveSuccesses = 0;
    private bool _isInitialCheck = true;
    private bool _isChecking = false;
    private bool _disposed;

    public ConnectivityState CurrentState => _currentState;

    public event Action<ConnectivityState, ConnectivityState>? ConnectivityChanged;

    public ConnectivityMonitor(IConnectivityProbe? probe = null, INotificationService? notificationService = null)
    {
        _probe = probe ?? new DefaultConnectivityProbe();
        _notificationService = notificationService ?? WindowsNotificationService.Instance;

        // Register Windows network change notifications
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        // Low-frequency periodic poll (every 30 seconds)
        _pollingTimer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = TriggerCheckAsync();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _ = TriggerCheckAsync();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            // Reset debouncing counters on resume to avoid spurious notifications
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _ = Task.Delay(2000).ContinueWith(_ => TriggerCheckAsync());
        }
    }

    private void OnTimerTick(object? state)
    {
        _ = TriggerCheckAsync();
    }

    public async Task TriggerCheckAsync()
    {
        if (_disposed || _isChecking) return;
        _isChecking = true;

        try
        {
            bool hasLocalInterface = NetworkInterface.GetIsNetworkAvailable();
            bool isReachable = false;

            if (hasLocalInterface)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                isReachable = await _probe.CheckInternetReachabilityAsync(cts.Token).ConfigureAwait(false);
            }

            ProcessProbeResult(isReachable);
        }
        catch
        {
            ProcessProbeResult(false);
        }
        finally
        {
            _isChecking = false;
        }
    }

    /// <summary>
    /// Processes a reachability probe result with debouncing logic.
    /// Internal/public for deterministic automated unit testing.
    /// </summary>
    public void ProcessProbeResult(bool isReachable)
    {
        if (_isInitialCheck)
        {
            _isInitialCheck = false;
            _currentState = isReachable ? ConnectivityState.Connected : ConnectivityState.Disconnected;
            _consecutiveSuccesses = isReachable ? 1 : 0;
            _consecutiveFailures = isReachable ? 0 : 1;
            // No startup notification is sent for initial state
            return;
        }

        if (isReachable)
        {
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;

            if (_currentState != ConnectivityState.Connected && _consecutiveSuccesses >= 2)
            {
                var oldState = _currentState;
                _currentState = ConnectivityState.Connected;

                ConnectivityChanged?.Invoke(oldState, _currentState);

                if (oldState == ConnectivityState.Disconnected)
                {
                    _notificationService.ShowNotification(
                        "Internet Restored",
                        "Internet connection restored.",
                        NotificationType.InternetRestored
                    );
                }
            }
        }
        else
        {
            _consecutiveFailures++;
            _consecutiveSuccesses = 0;

            if (_currentState != ConnectivityState.Disconnected && _consecutiveFailures >= 2)
            {
                var oldState = _currentState;
                _currentState = ConnectivityState.Disconnected;

                ConnectivityChanged?.Invoke(oldState, _currentState);

                if (oldState == ConnectivityState.Connected)
                {
                    _notificationService.ShowNotification(
                        "Internet Lost",
                        "Internet connection lost.",
                        NotificationType.InternetDisconnected
                    );
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            _pollingTimer.Dispose();

            if (_probe is IDisposable disposableProbe)
            {
                disposableProbe.Dispose();
            }

            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
