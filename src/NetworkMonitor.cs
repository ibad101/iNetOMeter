using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace iNetOMeter;

public record NetworkSpeed(double DownloadBytesPerSec, double UploadBytesPerSec)
{
    public string DownloadFormatted => NetworkMonitor.FormatSpeed(DownloadBytesPerSec, SettingsService.Instance.Current.SpeedFormat);
    public string UploadFormatted => NetworkMonitor.FormatSpeed(UploadBytesPerSec, SettingsService.Instance.Current.SpeedFormat);

    public string GetFormattedDownload(SpeedFormatOption format) => NetworkMonitor.FormatSpeed(DownloadBytesPerSec, format);
    public string GetFormattedUpload(SpeedFormatOption format) => NetworkMonitor.FormatSpeed(UploadBytesPerSec, format);
}

public class NetworkMonitor
{
    private long _previousBytesReceived;
    private long _previousBytesSent;
    private DateTime _previousTimestamp = DateTime.MinValue;
    private bool _isFirstSample = true;

    public NetworkSpeed GetCurrentSpeed()
    {
        long totalReceived = 0;
        long totalSent = 0;
        DateTime now = DateTime.UtcNow;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            foreach (var nic in interfaces)
            {
                var stats = nic.GetIPv4Statistics();
                totalReceived += stats.BytesReceived;
                totalSent += stats.BytesSent;
            }
        }
        catch
        {
            // Fallback gracefully if network interface query fails
            return new NetworkSpeed(0, 0);
        }

        if (_isFirstSample || _previousTimestamp == DateTime.MinValue)
        {
            _previousBytesReceived = totalReceived;
            _previousBytesSent = totalSent;
            _previousTimestamp = now;
            _isFirstSample = false;
            return new NetworkSpeed(0, 0);
        }

        double elapsedSeconds = (now - _previousTimestamp).TotalSeconds;
        if (elapsedSeconds <= 0.001)
        {
            return new NetworkSpeed(0, 0);
        }

        long deltaReceived = totalReceived - _previousBytesReceived;
        long deltaSent = totalSent - _previousBytesSent;

        // Handle counter reset or rollover
        if (deltaReceived < 0) deltaReceived = 0;
        if (deltaSent < 0) deltaSent = 0;

        _previousBytesReceived = totalReceived;
        _previousBytesSent = totalSent;
        _previousTimestamp = now;

        double downSpeed = deltaReceived / elapsedSeconds;
        double upSpeed = deltaSent / elapsedSeconds;

        return new NetworkSpeed(downSpeed, upSpeed);
    }

    public static string FormatSpeed(double bytesPerSec, SpeedFormatOption format = SpeedFormatOption.AutoBytes)
    {
        if (bytesPerSec <= 0)
        {
            return format switch
            {
                SpeedFormatOption.AutoBits => "0.0 Kbps",
                SpeedFormatOption.AlwaysMbps => "0.00 Mbps",
                SpeedFormatOption.AlwaysMBps => "0.00 MB/s",
                _ => "0.0 KB/s"
            };
        }

        switch (format)
        {
            case SpeedFormatOption.AutoBits:
            {
                double bitsPerSec = bytesPerSec * 8.0;
                if (bitsPerSec < 1000)
                {
                    return $"{bitsPerSec:0} bps";
                }
                if (bitsPerSec < 1_000_000)
                {
                    double kbps = bitsPerSec / 1000.0;
                    return kbps < 100 ? $"{kbps:0.0} Kbps" : $"{kbps:0} Kbps";
                }
                if (bitsPerSec < 1_000_000_000)
                {
                    double mbps = bitsPerSec / 1_000_000.0;
                    return mbps < 100 ? $"{mbps:0.0} Mbps" : $"{mbps:0} Mbps";
                }
                double gbps = bitsPerSec / 1_000_000_000.0;
                return $"{gbps:0.00} Gbps";
            }

            case SpeedFormatOption.AlwaysMbps:
            {
                double mbps = (bytesPerSec * 8.0) / 1_000_000.0;
                if (mbps < 10.0) return $"{mbps:0.00} Mbps";
                if (mbps < 100.0) return $"{mbps:0.0} Mbps";
                return $"{mbps:0} Mbps";
            }

            case SpeedFormatOption.AlwaysMBps:
            {
                double mb = bytesPerSec / (1024.0 * 1024.0);
                if (mb < 10.0) return $"{mb:0.00} MB/s";
                if (mb < 100.0) return $"{mb:0.0} MB/s";
                return $"{mb:0} MB/s";
            }

            case SpeedFormatOption.AutoBytes:
            default:
            {
                const double KB = 1024.0;
                const double MB = KB * 1024.0;
                const double GB = MB * 1024.0;

                if (bytesPerSec < KB)
                {
                    return $"{bytesPerSec:0} B/s";
                }
                if (bytesPerSec < MB)
                {
                    double kb = bytesPerSec / KB;
                    return kb < 100 ? $"{kb:0.0} KB/s" : $"{kb:0} KB/s";
                }
                if (bytesPerSec < GB)
                {
                    double mb = bytesPerSec / MB;
                    return mb < 100 ? $"{mb:0.0} MB/s" : $"{mb:0} MB/s";
                }

                double gb = bytesPerSec / GB;
                return $"{gb:0.00} GB/s";
            }
        }
    }
}
