using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace iNetOMeter;

public class SpeedTestService : IDisposable
{
    private const string BaseUrl = "https://speed.cloudflare.com";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public SpeedTestService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.None,
                ConnectCallback = async (context, ct) =>
                {
                    // Prioritize IPv4 to avoid broken IPv6 route hangs on Windows
                    IPAddress[] addresses;
                    try
                    {
                        var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct).ConfigureAwait(false);
                        addresses = entry.AddressList;
                    }
                    catch
                    {
                        var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, ct).ConfigureAwait(false);
                        addresses = entry.AddressList;
                    }

                    var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
                    var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), ct).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "iNetOMeter/1.0");
        }
    }

    public async Task<SpeedTestResult> RunFullTestAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        var totalSw = Stopwatch.StartNew();
        string serverLocation = "Cloudflare Edge";
        string serverColo = "Anycast";
        string serverAsn = "Global";

        try
        {
            // Phase 1: Connect & Query Server Meta (~0.5 - 1s)
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Connecting, 0, 5, "Connecting to Cloudflare Anycast edge...", ElapsedSeconds: totalSw.Elapsed.TotalSeconds));

            try
            {
                var meta = await GetServerMetaAsync(ct).ConfigureAwait(false);
                if (meta != null)
                {
                    serverColo = meta.Colo ?? "Anycast";
                    serverLocation = !string.IsNullOrWhiteSpace(meta.City) && !string.IsNullOrWhiteSpace(meta.Country)
                        ? $"{meta.City}, {meta.Country} ({serverColo})"
                        : $"Edge ({serverColo})";
                    serverAsn = meta.Asn != null ? $"AS{meta.Asn}" : "N/A";
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                serverLocation = "Cloudflare Anycast Edge";
            }

            ct.ThrowIfCancellationRequested();

            // Phase 2: Latency & Jitter (Target: ~2.5 - 3.5 seconds)
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Latency, 0, 10, "Measuring HTTP latency & jitter...", ElapsedSeconds: totalSw.Elapsed.TotalSeconds));
            var (latencyMs, jitterMs) = await MeasureLatencyAsync(progress, totalSw, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Phase 3: Download Throughput (Target: ~15 seconds, safe cap 80 MB)
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Download, 0, 25, "Testing download throughput...", latencyMs, jitterMs, 0, null, ElapsedSeconds: totalSw.Elapsed.TotalSeconds));
            var (downloadMbps, bytesDownloaded) = await MeasureDownloadAsync(progress, latencyMs, jitterMs, totalSw, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Phase 4: Upload Throughput (Target: ~15 seconds, safe cap 30 MB)
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Upload, 0, 65, "Testing upload throughput...", latencyMs, jitterMs, downloadMbps, 0, ElapsedSeconds: totalSw.Elapsed.TotalSeconds));
            var (uploadMbps, bytesUploaded) = await MeasureUploadAsync(progress, latencyMs, jitterMs, downloadMbps, totalSw, ct).ConfigureAwait(false);

            totalSw.Stop();
            double durationSec = Math.Round(totalSw.Elapsed.TotalSeconds, 1);

            // Phase 5: Completed
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Completed,
                0,
                100,
                $"Test complete in {durationSec:0.0}s.",
                latencyMs,
                jitterMs,
                downloadMbps,
                uploadMbps,
                ElapsedSeconds: durationSec
            ));

            return new SpeedTestResult(
                latencyMs,
                jitterMs,
                downloadMbps,
                uploadMbps,
                serverLocation,
                serverColo,
                serverAsn,
                DateTime.Now,
                true,
                null,
                durationSec,
                bytesDownloaded,
                bytesUploaded
            );
        }
        catch (OperationCanceledException)
        {
            totalSw.Stop();
            double durationSec = Math.Round(totalSw.Elapsed.TotalSeconds, 1);
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Cancelled, 0, 0, "Test was cancelled.", ElapsedSeconds: durationSec));
            return SpeedTestResult.Cancelled(durationSec);
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            double durationSec = Math.Round(totalSw.Elapsed.TotalSeconds, 1);
            string errorMessage = ex switch
            {
                HttpRequestException httpEx => $"Network error: {httpEx.Message}",
                TimeoutException => "Connection timed out. Check your internet connection.",
                _ => $"Test failed: {ex.Message}"
            };

            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Failed, 0, 0, errorMessage, ElapsedSeconds: durationSec));
            return SpeedTestResult.Failure(errorMessage, durationSec);
        }
    }

    public async Task<CloudflareMeta?> GetServerMetaAsync(CancellationToken ct)
    {
        string url = $"{BaseUrl}/__down?bytes=0";
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string? city = null;
        string? country = null;
        string? colo = null;
        string? asn = null;

        if (response.Headers.TryGetValues("colo", out var coloVals))
        {
            colo = coloVals.FirstOrDefault();
        }
        if (response.Headers.TryGetValues("cf-meta-colo", out var cfColoVals))
        {
            colo ??= cfColoVals.FirstOrDefault();
        }
        if (response.Headers.TryGetValues("city", out var cityVals))
        {
            city = Uri.UnescapeDataString(cityVals.FirstOrDefault() ?? "");
        }
        if (response.Headers.TryGetValues("country", out var countryVals))
        {
            country = countryVals.FirstOrDefault();
        }
        if (response.Headers.TryGetValues("asn", out var asnVals))
        {
            asn = asnVals.FirstOrDefault();
        }

        return new CloudflareMeta(city, country, colo, asn);
    }

    public async Task<(double LatencyMs, double JitterMs)> MeasureLatencyAsync(IProgress<SpeedTestProgress>? progress, Stopwatch totalSw, CancellationToken ct)
    {
        // 12 probes spaced by ~140ms produces a ~2.5 - 3.5s measurement window
        const int sampleCount = 12;
        var samples = new List<double>(sampleCount);
        var sw = new Stopwatch();

        for (int i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            string url = $"{BaseUrl}/__down?bytes=0&r={Guid.NewGuid()}";

            sw.Restart();
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            sw.Stop();

            double rtt = Math.Max(1.0, sw.Elapsed.TotalMilliseconds);
            samples.Add(rtt);

            double currentMedian = SpeedConverter.CalculateMedian(samples);
            double currentJitter = SpeedConverter.CalculateJitter(samples);
            double percent = 10 + ((i + 1.0) / sampleCount * 15);

            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Latency,
                0,
                percent,
                $"Measuring latency & jitter (probe {i + 1}/{sampleCount})... {currentMedian:0} ms",
                currentMedian,
                currentJitter,
                null,
                null,
                ElapsedSeconds: totalSw.Elapsed.TotalSeconds
            ));

            if (i < sampleCount - 1)
            {
                await Task.Delay(130, ct).ConfigureAwait(false);
            }
        }

        double medianLatency = SpeedConverter.CalculateMedian(samples);
        double jitter = SpeedConverter.CalculateJitter(samples);

        return (medianLatency, jitter);
    }

    public async Task<(double Mbps, long Bytes)> MeasureDownloadAsync(
        IProgress<SpeedTestProgress>? progress,
        double latencyMs,
        double jitterMs,
        Stopwatch totalSw,
        CancellationToken ct)
    {
        // Target: ~15 seconds; safe cap: 80 MB total payload across 2 concurrent streams
        const int streamCount = 2;
        const int chunkSize = 10_000_000; // 10 MB per chunk
        const long maxBytes = 80_000_000;
        const double targetDurationSec = 14.5;

        long totalBytes = 0;
        var phaseSw = Stopwatch.StartNew();
        var lastReport = Stopwatch.StartNew();
        var streamLock = new object();

        var tasks = new List<Task>();

        for (int s = 0; s < streamCount; s++)
        {
            int streamId = s;
            tasks.Add(Task.Run(async () =>
            {
                byte[] buffer = new byte[32768];

                while (phaseSw.Elapsed.TotalSeconds < targetDurationSec && Interlocked.Read(ref totalBytes) < maxBytes)
                {
                    ct.ThrowIfCancellationRequested();
                    string url = $"{BaseUrl}/__down?bytes={chunkSize}&r={Guid.NewGuid()}_{streamId}";

                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    int read;
                    while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                    {
                        lock (streamLock)
                        {
                            totalBytes += read;
                        }

                        if (lastReport.ElapsedMilliseconds >= 75)
                        {
                            double elapsed = phaseSw.Elapsed.TotalSeconds;
                            if (elapsed > 0.1)
                            {
                                double liveMbps = SpeedConverter.BytesPerSecondToMbps(totalBytes / elapsed);
                                double percent = Math.Min(65, 25 + (elapsed / targetDurationSec) * 40);
                                progress?.Report(new SpeedTestProgress(
                                    SpeedTestPhase.Download,
                                    liveMbps,
                                    percent,
                                    $"Testing download... {liveMbps:0.0} Mbps ({elapsed:0.0}s / {targetDurationSec:0}s)",
                                    latencyMs,
                                    jitterMs,
                                    liveMbps,
                                    null,
                                    ElapsedSeconds: totalSw.Elapsed.TotalSeconds
                                ));
                                lastReport.Restart();
                            }
                        }

                        if (phaseSw.Elapsed.TotalSeconds >= targetDurationSec || Interlocked.Read(ref totalBytes) >= maxBytes)
                        {
                            break;
                        }
                    }
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        phaseSw.Stop();

        double finalSeconds = Math.Max(0.1, phaseSw.Elapsed.TotalSeconds);
        double finalMbps = Math.Round(SpeedConverter.BytesPerSecondToMbps(totalBytes / finalSeconds), 2);
        return (finalMbps, totalBytes);
    }

    public async Task<(double Mbps, long Bytes)> MeasureUploadAsync(
        IProgress<SpeedTestProgress>? progress,
        double latencyMs,
        double jitterMs,
        double downloadMbps,
        Stopwatch totalSw,
        CancellationToken ct)
    {
        // Target: ~15 seconds; safe cap: 30 MB total payload
        const int payloadSize = 2_500_000; // 2.5 MB reusable payload
        const long maxBytes = 30_000_000;
        const double targetDurationSec = 14.5;

        byte[] payload = new byte[payloadSize];
        new Random(42).NextBytes(payload);

        long totalBytesUploaded = 0;
        var phaseSw = Stopwatch.StartNew();
        var lastReport = Stopwatch.StartNew();

        while (phaseSw.Elapsed.TotalSeconds < targetDurationSec && totalBytesUploaded < maxBytes)
        {
            ct.ThrowIfCancellationRequested();

            long chunkSent = 0;
            var content = new ProgressableStreamContent(payload, sent =>
            {
                long newDelta = sent - chunkSent;
                chunkSent = sent;
                Interlocked.Add(ref totalBytesUploaded, newDelta);

                if (lastReport.ElapsedMilliseconds >= 75)
                {
                    double elapsed = phaseSw.Elapsed.TotalSeconds;
                    if (elapsed > 0.1)
                    {
                        double liveMbps = SpeedConverter.BytesPerSecondToMbps(Interlocked.Read(ref totalBytesUploaded) / elapsed);
                        double percent = Math.Min(96, 65 + (elapsed / targetDurationSec) * 31);
                        progress?.Report(new SpeedTestProgress(
                            SpeedTestPhase.Upload,
                            liveMbps,
                            percent,
                            $"Testing upload... {liveMbps:0.0} Mbps ({elapsed:0.0}s / {targetDurationSec:0}s)",
                            latencyMs,
                            jitterMs,
                            downloadMbps,
                            liveMbps,
                            ElapsedSeconds: totalSw.Elapsed.TotalSeconds
                        ));
                        lastReport.Restart();
                    }
                }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/__up")
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (phaseSw.Elapsed.TotalSeconds >= targetDurationSec || totalBytesUploaded >= maxBytes)
            {
                break;
            }
        }

        phaseSw.Stop();

        double finalSeconds = Math.Max(0.1, phaseSw.Elapsed.TotalSeconds);
        double finalMbps = Math.Round(SpeedConverter.BytesPerSecondToMbps(totalBytesUploaded / finalSeconds), 2);
        return (finalMbps, totalBytesUploaded);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

public record CloudflareMeta(string? City, string? Country, string? Colo, string? Asn);

internal class ProgressableStreamContent : HttpContent
{
    private readonly byte[] _buffer;
    private readonly Action<long> _onBytesSent;

    public ProgressableStreamContent(byte[] buffer, Action<long> onBytesSent)
    {
        _buffer = buffer;
        _onBytesSent = onBytesSent;
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        Headers.ContentLength = buffer.Length;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        const int chunkSize = 16384;
        int offset = 0;
        while (offset < _buffer.Length)
        {
            int count = Math.Min(chunkSize, _buffer.Length - offset);
            await stream.WriteAsync(_buffer.AsMemory(offset, count));
            offset += count;
            _onBytesSent(offset);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _buffer.Length;
        return true;
    }
}
