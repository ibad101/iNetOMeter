using System;
using System.IO;
using System.Text.Json;

namespace iNetOMeter;

public enum SpeedFormatOption
{
    AutoBytes = 0,    // B/s, KB/s, MB/s, GB/s (Default)
    AutoBits = 1,     // bps, Kbps, Mbps, Gbps
    AlwaysMbps = 2,   // Always Mbps
    AlwaysMBps = 3    // Always MB/s
}

public class UserSettings
{
    public bool StartWithWindows { get; set; } = false;
    public SpeedFormatOption SpeedFormat { get; set; } = SpeedFormatOption.AutoBytes;
    public double WidgetOpacity { get; set; } = 0.96;
    public int UpdateIntervalMs { get; set; } = 1000;

    // Stage 6 Notifications (default: OFF, opt-in)
    public bool NotificationsEnabled { get; set; } = false;
    public bool NotifyOnDisconnect { get; set; } = false;
    public bool NotifyOnRestore { get; set; } = false;
    public bool NotifyOnSlowSpeed { get; set; } = false;
    public double SlowSpeedThresholdMbps { get; set; } = 5.0;
    public int NotificationCooldownSeconds { get; set; } = 60;

    // Smart Wi-Fi Assistant (default: OFF, opt-in)
    public bool WiFiAssistantEnabled { get; set; } = false;
    public int WiFiWeakSignalThresholdPercent { get; set; } = 30;
    public int WiFiSignalHysteresisPercent { get; set; } = 10;
    public int WiFiCooldownSeconds { get; set; } = 300;

    public UserSettings Clone() => new()
    {
        StartWithWindows = StartWithWindows,
        SpeedFormat = SpeedFormat,
        WidgetOpacity = WidgetOpacity,
        UpdateIntervalMs = UpdateIntervalMs,
        NotificationsEnabled = NotificationsEnabled,
        NotifyOnDisconnect = NotifyOnDisconnect,
        NotifyOnRestore = NotifyOnRestore,
        NotifyOnSlowSpeed = NotifyOnSlowSpeed,
        SlowSpeedThresholdMbps = SlowSpeedThresholdMbps,
        NotificationCooldownSeconds = NotificationCooldownSeconds,
        WiFiAssistantEnabled = WiFiAssistantEnabled,
        WiFiWeakSignalThresholdPercent = WiFiWeakSignalThresholdPercent,
        WiFiSignalHysteresisPercent = WiFiSignalHysteresisPercent,
        WiFiCooldownSeconds = WiFiCooldownSeconds
    };
}

public class SettingsService
{
    private static SettingsService? _instance;
    private static readonly object _syncLock = new();

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    private readonly string _settingsFilePath;
    private UserSettings _current = new();
    private readonly object _lock = new();

    public event Action<UserSettings>? SettingsChanged;

    public UserSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _current.Clone();
            }
        }
    }

    public SettingsService(string? customPath = null)
    {
        if (customPath != null)
        {
            _settingsFilePath = customPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "iNetOMeter");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        LoadSettings();
    }

    public void Save(UserSettings newSettings)
    {
        if (newSettings == null) return;

        // Clamp and validate values
        newSettings.WidgetOpacity = Math.Clamp(newSettings.WidgetOpacity, 0.50, 1.0);
        newSettings.UpdateIntervalMs = newSettings.UpdateIntervalMs switch
        {
            < 500 => 500,
            > 5000 => 5000,
            _ => newSettings.UpdateIntervalMs
        };
        newSettings.SlowSpeedThresholdMbps = double.IsNaN(newSettings.SlowSpeedThresholdMbps)
            ? 5.0
            : Math.Clamp(newSettings.SlowSpeedThresholdMbps, 0.5, 1000.0);
        newSettings.NotificationCooldownSeconds = Math.Clamp(
            newSettings.NotificationCooldownSeconds,
            10,
            3600
        );
        newSettings.WiFiWeakSignalThresholdPercent = Math.Clamp(
            newSettings.WiFiWeakSignalThresholdPercent,
            15,
            60
        );
        newSettings.WiFiSignalHysteresisPercent = Math.Clamp(
            newSettings.WiFiSignalHysteresisPercent,
            5,
            30
        );
        newSettings.WiFiCooldownSeconds = Math.Clamp(
            newSettings.WiFiCooldownSeconds,
            60,
            1800
        );

        lock (_lock)
        {
            _current = newSettings.Clone();

            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tmpPath = _settingsFilePath + ".tmp";
                string json = JsonSerializer.Serialize(_current, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, _settingsFilePath, overwrite: true);

                // Update Windows Startup shortcut
                StartupManager.SetStartWithWindows(_current.StartWithWindows);
            }
            catch
            {
                // Fail gracefully without crashing
            }
        }

        SettingsChanged?.Invoke(Current);
    }

    private void LoadSettings()
    {
        lock (_lock)
        {
            if (!File.Exists(_settingsFilePath))
            {
                // Synchronize startup flag with actual shortcut presence
                _current = new UserSettings
                {
                    StartWithWindows = StartupManager.IsStartWithWindowsEnabled()
                };
                return;
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loaded != null)
                {
                    loaded.WidgetOpacity = Math.Clamp(loaded.WidgetOpacity, 0.50, 1.0);
                    loaded.UpdateIntervalMs = Math.Clamp(loaded.UpdateIntervalMs, 500, 5000);
                    loaded.SlowSpeedThresholdMbps = double.IsNaN(loaded.SlowSpeedThresholdMbps)
                        ? 5.0
                        : Math.Clamp(loaded.SlowSpeedThresholdMbps, 0.5, 1000.0);
                    loaded.NotificationCooldownSeconds = Math.Clamp(
                        loaded.NotificationCooldownSeconds,
                        10,
                        3600
                    );
                    loaded.WiFiWeakSignalThresholdPercent = Math.Clamp(
                        loaded.WiFiWeakSignalThresholdPercent > 0 ? loaded.WiFiWeakSignalThresholdPercent : 30,
                        15,
                        60
                    );
                    loaded.WiFiSignalHysteresisPercent = Math.Clamp(
                        loaded.WiFiSignalHysteresisPercent > 0 ? loaded.WiFiSignalHysteresisPercent : 10,
                        5,
                        30
                    );
                    loaded.WiFiCooldownSeconds = Math.Clamp(
                        loaded.WiFiCooldownSeconds > 0 ? loaded.WiFiCooldownSeconds : 300,
                        60,
                        1800
                    );
                    _current = loaded;
                }
                else
                {
                    _current = new UserSettings();
                }
            }
            catch
            {
                // Fall back to clean defaults on corrupt file
                _current = new UserSettings();
            }

            // Sync with actual filesystem shortcut state
            _current.StartWithWindows = StartupManager.IsStartWithWindowsEnabled();
        }
    }
}
