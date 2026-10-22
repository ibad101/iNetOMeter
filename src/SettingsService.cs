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

    public UserSettings Clone() => new()
    {
        StartWithWindows = StartWithWindows,
        SpeedFormat = SpeedFormat,
        WidgetOpacity = WidgetOpacity,
        UpdateIntervalMs = UpdateIntervalMs
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
