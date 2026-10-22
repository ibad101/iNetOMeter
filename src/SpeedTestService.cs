using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
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
                    // Prioritize IPv4 to avoid broken IPv6 route hangs
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
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "iNetOMeter/1.0");
        }
    }

    public async Task<SpeedTestResult> RunFullTestAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        string serverLocation = "Cloudflare Edge";
        string serverColo = "Anycast";
        string serverAsn = "Global";

        try
        {
            // Phase 1: Connect & Query Server Meta
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Connecting, 0, 5, "Connecting to Cloudflare Anycast edge..."));

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

            // Phase 2: Latency & Jitter
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Latency, 0, 15, "Measuring HTTP latency & jitter...", null, null, null, null));
            var (latencyMs, jitterMs) = await MeasureLatencyAsync(progress, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Phase 3: Download Throughput
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Download, 0, 35, "Testing download throughput...", latencyMs, jitterMs, 0, null));
            double downloadMbps = await MeasureDownloadAsync(progress, latencyMs, jitterMs, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Phase 4: Upload Throughput
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Upload, 0, 70, "Testing upload throughput...", latencyMs, jitterMs, downloadMbps, 0));
            double uploadMbps = await MeasureUploadAsync(progress, latencyMs, jitterMs, downloadMbps, ct).ConfigureAwait(false);

            // Phase 5: Completed
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Completed,
                0,
                100,
                "Test complete.",
                latencyMs,
                jitterMs,
                downloadMbps,
                uploadMbps
            ));

            return new SpeedTestResult(
                latencyMs,
                jitterMs,
                downloadMbps,
                uploadMbps,
                serverLocation,
                serverColo,
                serverAsn,
                DateTime.UtcNow,
                true
            );
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Cancelled, 0, 0, "Test was cancelled."));
            return SpeedTestResult.Cancelled();
        }
        catch (Exception ex)
        {
            string errorMessage = ex switch
            {
                HttpRequestException httpEx => $"Network error: {httpEx.Message}",
                TimeoutException => "Connection timed out. Check your internet connection.",
                _ => $"Test failed: {ex.Message}"
            };

            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Failed, 0, 0, errorMessage));
            return SpeedTestResult.Failure(errorMessage);
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

    public async Task<(double LatencyMs, double JitterMs)> MeasureLatencyAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        const int sampleCount = 5;
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

            double currentAvg = samples.Average();
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Latency,
                0,
                15 + (int)((i + 1.0) / sampleCount * 15),
                $"Measuring latency ({i + 1}/{sampleCount})...",
                currentAvg,
                null,
                null,
                null
            ));

            await Task.Delay(40, ct).ConfigureAwait(false);
        }

        samples.Sort();
        double medianLatency = samples[samples.Count / 2];

        double jitterSum = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            jitterSum += Math.Abs(samples[i] - samples[i - 1]);
        }
        double jitter = samples.Count > 1 ? jitterSum / (samples.Count - 1) : 0;

        return (Math.Round(medianLatency, 1), Math.Round(jitter, 1));
    }

    public async Task<double> MeasureDownloadAsync(IProgress<SpeedTestProgress>? progress, double latencyMs, double jitterMs, CancellationToken ct)
    {
        const int streamCount = 2;
        const int chunkSize = 8_000_000; // 8 MB per stream
        long totalBytes = 0;
        var sw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        var lastReport = Stopwatch.StartNew();
        var streamLock = new object();

        for (int s = 0; s < streamCount; s++)
        {
            int streamId = s;
            tasks.Add(Task.Run(async () =>
            {
                byte[] buffer = new byte[32768];
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
                        double elapsed = sw.Elapsed.TotalSeconds;
                        if (elapsed > 0.05)
                        {
                            double liveMbps = SpeedConverter.BytesPerSecondToMbps(totalBytes / elapsed);
                            double percent = Math.Min(65, 30 + (elapsed / 5.0) * 35);
                            progress?.Report(new SpeedTestProgress(
                                SpeedTestPhase.Download,
                                liveMbps,
                                percent,
                                $"Testing download... {liveMbps:0.0} Mbps",
                                latencyMs,
                                jitterMs,
                                liveMbps,
                                null
                            ));
                            lastReport.Restart();
                        }
                    }

                    if (sw.Elapsed.TotalSeconds >= 5.5)
                    {
                        break;
                    }
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        double finalSeconds = Math.Max(0.1, sw.Elapsed.TotalSeconds);
        double finalMbps = Math.Round(SpeedConverter.BytesPerSecondToMbps(totalBytes / finalSeconds), 2);
        return finalMbps;
    }

    public async Task<double> MeasureUploadAsync(IProgress<SpeedTestProgress>? progress, double latencyMs, double jitterMs, double downloadMbps, CancellationToken ct)
    {
        const int payloadSize = 2_500_000; // 2.5 MB payload
        byte[] payload = new byte[payloadSize];
        new Random(42).NextBytes(payload);

        long bytesUploaded = 0;
        var sw = Stopwatch.StartNew();
        var lastReport = Stopwatch.StartNew();

        var content = new ProgressableStreamContent(payload, sent =>
        {
            bytesUploaded = sent;
            if (lastReport.ElapsedMilliseconds >= 75)
            {
                double elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed > 0.05)
                {
                    double liveMbps = SpeedConverter.BytesPerSecondToMbps(bytesUploaded / elapsed);
                    double percent = Math.Min(95, 65 + (elapsed / 4.0) * 30);
                    progress?.Report(new SpeedTestProgress(
                        SpeedTestPhase.Upload,
                        liveMbps,
                        percent,
                        $"Testing upload... {liveMbps:0.0} Mbps",
                        latencyMs,
                        jitterMs,
                        downloadMbps,
                        liveMbps
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
        sw.Stop();

        double finalSeconds = Math.Max(0.1, sw.Elapsed.TotalSeconds);
        double finalMbps = Math.Round(SpeedConverter.BytesPerSecondToMbps(bytesUploaded / finalSeconds), 2);
        return finalMbps;
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
