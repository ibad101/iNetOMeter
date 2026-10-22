using System;
using System.Collections.Generic;
using System.Linq;

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
    double? UploadMbps = null,
    double ElapsedSeconds = 0.0
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
    string? ErrorMessage = null,
    double DurationSeconds = 0.0,
    long TotalBytesDownloaded = 0,
    long TotalBytesUploaded = 0
)
{
    public static SpeedTestResult Failure(string errorMessage, double durationSeconds = 0.0) =>
        new(0, 0, 0, 0, "Unknown", "N/A", "N/A", DateTime.Now, false, errorMessage, durationSeconds);

    public static SpeedTestResult Cancelled(double durationSeconds = 0.0) =>
        new(0, 0, 0, 0, "Cancelled", "N/A", "N/A", DateTime.Now, false, "Test was cancelled by user.", durationSeconds);
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

    /// <summary>
    /// Calculates network jitter in milliseconds according to RFC 3550:
    /// Mean absolute difference between consecutive latency samples.
    /// Returns 0.0 if fewer than 2 samples are available.
    /// </summary>
    public static double CalculateJitter(IReadOnlyList<double> samples)
    {
        if (samples == null || samples.Count < 2) return 0.0;

        double sumDifferences = 0.0;
        int diffCount = 0;

        for (int i = 1; i < samples.Count; i++)
        {
            sumDifferences += Math.Abs(samples[i] - samples[i - 1]);
            diffCount++;
        }

        return diffCount > 0 ? Math.Round(sumDifferences / diffCount, 1) : 0.0;
    }

    /// <summary>
    /// Calculates median latency from a collection of samples.
    /// </summary>
    public static double CalculateMedian(IReadOnlyList<double> samples)
    {
        if (samples == null || samples.Count == 0) return 0.0;

        var sorted = samples.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;
        if (sorted.Count % 2 != 0)
        {
            return Math.Round(sorted[mid], 1);
        }
        return Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, 1);
    }
}
