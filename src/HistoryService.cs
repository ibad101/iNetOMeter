using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace iNetOMeter;

public record SpeedTestHistoryRecord(
    DateTime Timestamp,
    double LatencyMs,
    double JitterMs,
    double DownloadMbps,
    double UploadMbps,
    string ServerName,
    double DurationSeconds
);

public class HistoryService
{
    private static HistoryService? _instance;
    private static readonly object _syncLock = new();

    public static HistoryService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new HistoryService();
                }
            }
            return _instance;
        }
    }

    private readonly string _historyFilePath;
    private readonly List<SpeedTestHistoryRecord> _records = new();
    private readonly object _fileLock = new();

    public event Action? HistoryChanged;

    public HistoryService(string? customPath = null)
    {
        if (customPath != null)
        {
            _historyFilePath = customPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "iNetOMeter");
            Directory.CreateDirectory(folder);
            _historyFilePath = Path.Combine(folder, "history.json");
        }

        LoadHistory();
    }

    public IReadOnlyList<SpeedTestHistoryRecord> GetHistory()
    {
        lock (_fileLock)
        {
            return _records.ToList();
        }
    }

    public void AddResult(SpeedTestResult result)
    {
        if (result == null || !result.Success)
        {
            return; // Only successful, valid tests are saved
        }

        // Validate metrics: must be plausible positive numbers
        if (result.DownloadMbps < 0 || result.UploadMbps < 0 || result.LatencyMs < 0 || result.JitterMs < 0)
        {
            return;
        }

        var record = new SpeedTestHistoryRecord(
            Timestamp: result.Timestamp == default ? DateTime.Now : result.Timestamp,
            LatencyMs: Math.Round(result.LatencyMs, 1),
            JitterMs: Math.Round(result.JitterMs, 1),
            DownloadMbps: Math.Round(result.DownloadMbps, 2),
            UploadMbps: Math.Round(result.UploadMbps, 2),
            ServerName: string.IsNullOrWhiteSpace(result.ServerLocation) ? "Cloudflare Anycast" : result.ServerLocation,
            DurationSeconds: Math.Round(result.DurationSeconds, 1)
        );

        lock (_fileLock)
        {
            _records.Insert(0, record);

            // Keep only the 5 newest completed tests
            while (_records.Count > 5)
            {
                _records.RemoveAt(_records.Count - 1);
            }

            SaveHistory();
        }

        HistoryChanged?.Invoke();
    }

    public void ClearHistory()
    {
        lock (_fileLock)
        {
            _records.Clear();
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    File.Delete(_historyFilePath);
                }
            }
            catch
            {
                // Ignore file system errors on deletion
            }
        }

        HistoryChanged?.Invoke();
    }

    private void LoadHistory()
    {
        lock (_fileLock)
        {
            _records.Clear();

            if (!File.Exists(_historyFilePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_historyFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var list = JsonSerializer.Deserialize<List<SpeedTestHistoryRecord>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (list != null)
                {
                    // Validate each entry to prevent corrupt/fabricated values
                    foreach (var item in list)
                    {
                        if (item.LatencyMs >= 0 && item.JitterMs >= 0 &&
                            item.DownloadMbps >= 0 && item.UploadMbps >= 0 &&
                            !string.IsNullOrWhiteSpace(item.ServerName))
                        {
                            _records.Add(item);
                        }
                    }

                    // Enforce newest first ordering and cap at 5
                    var sorted = _records.OrderByDescending(r => r.Timestamp).Take(5).ToList();
                    _records.Clear();
                    _records.AddRange(sorted);
                }
            }
            catch
            {
                // If the history file is corrupted or unreadable, safely clear without crashing
                _records.Clear();
            }
        }
    }

    private void SaveHistory()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tmpPath = _historyFilePath + ".tmp";
            string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _historyFilePath, overwrite: true);
        }
        catch
        {
            // Fail silently on storage error without crashing application
        }
    }
}
