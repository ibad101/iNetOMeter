using System;

namespace iNetOMeter;

public enum SpeedTestPhase
{
    Idle,
    Connecting,
    Latency,
    Download,
    Upload,
    Completed,
    Cancelled,
    Failed
}

public record SpeedTestProgress(
    SpeedTestPhase Phase,
    double CurrentSpeedMbps,
    double PercentComplete,
    string StatusMessage,
    double? LatencyMs = null,
    double? JitterMs = null,
    double? DownloadMbps = null,
    double? UploadMbps = null
);

public record SpeedTestResult(
    double LatencyMs,
    double JitterMs,
    double DownloadMbps,
    double UploadMbps,
    string ServerLocation,
    string ServerColo,
    string Asn,
    DateTime Timestamp,
    bool Success,
    string? ErrorMessage = null
)
{
    public static SpeedTestResult Failure(string errorMessage) =>
        new(0, 0, 0, 0, "Unknown", "N/A", "N/A", DateTime.UtcNow, false, errorMessage);

    public static SpeedTestResult Cancelled() =>
        new(0, 0, 0, 0, "Cancelled", "N/A", "N/A", DateTime.UtcNow, false, "Test was cancelled by user.");
}

public static class SpeedConverter
{
    public static double BytesPerSecondToMbps(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return 0.0;
        return (bytesPerSec * 8.0) / 1_000_000.0;
    }

    public static string FormatMbps(double mbps)
    {
        if (mbps <= 0) return "0.00";
        if (mbps < 10.0) return $"{mbps:0.00}";
        if (mbps < 100.0) return $"{mbps:0.0}";
        return $"{mbps:0}";
    }
}
