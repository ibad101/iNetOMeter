using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iNetOMeter;

public class SpeedTestForm : Form
{
    private static SpeedTestForm? _activeInstance;

    // Design Palette
    private static readonly Color DarkNavyBg = Color.FromArgb(11, 17, 30);       // #0B111E
    private static readonly Color CardBg = Color.FromArgb(20, 29, 47);           // #141D2F
    private static readonly Color CardBorder = Color.FromArgb(36, 48, 74);       // #24304A
    private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);     // #F8FAFC
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // #94A3B8
    private static readonly Color LatencyColor = Color.FromArgb(56, 189, 248);   // #38BDF8 Cyan
    private static readonly Color DownloadColor = Color.FromArgb(255, 82, 92);   // #FF525C Red
    private static readonly Color UploadColor = Color.FromArgb(76, 168, 255);    // #4CA8FF Blue
    private static readonly Color CompletedColor = Color.FromArgb(16, 185, 129); // #10B981 Green

    private readonly SpeedTestService _speedTestService;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // UI state
    private SpeedTestPhase _currentPhase = SpeedTestPhase.Idle;
    private double _gaugeSpeed = 0.0;
    private double _gaugeTargetSpeed = 0.0;
    private double _percentComplete = 0.0;
    private string _statusText = "Ready to test. Click 'START SPEED TEST'.";
    private string _serverInfo = "Server: Cloudflare Edge (Anycast)";

    private double? _finalLatencyMs;
    private double? _finalJitterMs;
    private double? _finalDownloadMbps;
    private double? _finalUploadMbps;

    // Smooth animation timer for gauge needle
    private readonly System.Windows.Forms.Timer _animTimer;

    // Controls
    private readonly Button _btnStart;
    private readonly Button _btnReset;
    private readonly Button _btnHistory;
    private readonly LinkLabel _linkTerms;

    public SpeedTestForm()
    {
        _speedTestService = new SpeedTestService();

        Text = "Internet Speed Test - iNetOMeter";
        Size = new Size(680, 560);
        MinimumSize = new Size(620, 500);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = DarkNavyBg;
        ForeColor = TextWhite;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        // Buttons
        _btnStart = new Button
        {
            Text = "START SPEED TEST",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235), // Royal Blue
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Size = new Size(180, 42)
        };
        _btnStart.FlatAppearance.BorderSize = 0;
        _btnStart.Click += OnStartClicked;
        Controls.Add(_btnStart);

        _btnReset = new Button
        {
            Text = "Reset",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
            Size = new Size(70, 42)
        };
        _btnReset.FlatAppearance.BorderColor = CardBorder;
        _btnReset.Click += (_, _) => ResetTestState();
        Controls.Add(_btnReset);

        _btnHistory = new Button
        {
            Text = "History",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
            Size = new Size(70, 42)
        };
        _btnHistory.FlatAppearance.BorderColor = CardBorder;
        _btnHistory.Click += (_, _) => HistoryForm.ShowOrActivate();
        Controls.Add(_btnHistory);

        _linkTerms = new LinkLabel
        {
            Text = "Cloudflare Anycast • Latency reflects HTTP round-trip • Terms & Privacy",
            LinkColor = LatencyColor,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = LatencyColor,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _linkTerms.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://speed.cloudflare.com/terms") { UseShellExecute = true });
            }
            catch { /* Ignore if browser cannot be launched */ }
        };
        Controls.Add(_linkTerms);

        _animTimer = new System.Windows.Forms.Timer { Interval = 25 };
        _animTimer.Tick += OnAnimTimerTick;
        _animTimer.Start();

        Resize += (_, _) => Invalidate();
    }

    public static void ShowOrActivate()
    {
        if (_activeInstance == null || _activeInstance.IsDisposed)
        {
            _activeInstance = new SpeedTestForm();
            _activeInstance.FormClosed += (_, _) => _activeInstance = null;
            _activeInstance.Show();
        }
        else
        {
            if (_activeInstance.WindowState == FormWindowState.Minimized)
            {
                _activeInstance.WindowState = FormWindowState.Normal;
            }
            _activeInstance.BringToFront();
            _activeInstance.Activate();
        }
    }

    private void OnAnimTimerTick(object? sender, EventArgs e)
    {
        // Smoothly interpolate gauge towards target speed
        double delta = _gaugeTargetSpeed - _gaugeSpeed;
        if (Math.Abs(delta) > 0.05)
        {
            _gaugeSpeed += delta * 0.25;
            Invalidate();
        }
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            // Cancel current test
            _cts?.Cancel();
            _btnStart.Enabled = false;
            _statusText = "Cancelling test...";
            Invalidate();
            return;
        }

        // Start test
        _isRunning = true;
        _btnStart.Text = "CANCEL";
        _btnStart.BackColor = Color.FromArgb(239, 68, 68); // Red
        _btnReset.Enabled = false;
        _btnHistory.Enabled = false;
        _cts = new CancellationTokenSource();

        ResetTestState(clearServerInfo: false);

        var progress = new Progress<SpeedTestProgress>(p =>
        {
            _currentPhase = p.Phase;
            _gaugeTargetSpeed = p.CurrentSpeedMbps;
            _percentComplete = p.PercentComplete;
            _statusText = p.StatusMessage;

            if (p.LatencyMs.HasValue) _finalLatencyMs = p.LatencyMs.Value;
            if (p.JitterMs.HasValue) _finalJitterMs = p.JitterMs.Value;
            if (p.DownloadMbps.HasValue) _finalDownloadMbps = p.DownloadMbps.Value;
            if (p.UploadMbps.HasValue) _finalUploadMbps = p.UploadMbps.Value;

            Invalidate();
        });

        try
        {
            var result = await _speedTestService.RunFullTestAsync(progress, _cts.Token);
            if (result.Success)
            {
                _serverInfo = $"Server: Cloudflare Edge ({result.ServerLocation})";
                _statusText = $"Test complete in {result.DurationSeconds:0.0}s. Results recorded to history.";
                _currentPhase = SpeedTestPhase.Completed;
                _gaugeTargetSpeed = 0.0;

                // Stage 4 requirement: Record to persistent history
                HistoryService.Instance.AddResult(result);

                // Stage 6 requirement: Slow speed notification check
                var settings = SettingsService.Instance.Current;
                if (settings.NotificationsEnabled && settings.NotifyOnSlowSpeed && result.DownloadMbps < settings.SlowSpeedThresholdMbps)
                {
                    WindowsNotificationService.Instance.ShowNotification(
                        "Slow Download Speed",
                        $"Download speed is below your configured threshold: {result.DownloadMbps:0.0} Mbps (threshold: {settings.SlowSpeedThresholdMbps:0.0} Mbps).",
                        NotificationType.SlowSpeed
                    );
                }
            }
            else
            {
                _statusText = result.ErrorMessage ?? "Test failed.";
                _currentPhase = SpeedTestPhase.Failed;
            }
        }
        catch (Exception ex)
        {
            _statusText = $"Error: {ex.Message}";
            _currentPhase = SpeedTestPhase.Failed;
        }
        finally
        {
            _isRunning = false;
            _btnStart.Text = "START SPEED TEST";
            _btnStart.BackColor = Color.FromArgb(37, 99, 235);
            _btnStart.Enabled = true;
            _btnReset.Enabled = true;
            _btnHistory.Enabled = true;
            _cts?.Dispose();
            _cts = null;
            Invalidate();
        }
    }

    private void ResetTestState(bool clearServerInfo = true)
    {
        _currentPhase = SpeedTestPhase.Idle;
        _gaugeSpeed = 0.0;
        _gaugeTargetSpeed = 0.0;
        _percentComplete = 0.0;
        _finalLatencyMs = null;
        _finalJitterMs = null;
        _finalDownloadMbps = null;
        _finalUploadMbps = null;
        if (clearServerInfo) _serverInfo = "Server: Cloudflare Edge (Anycast)";
        _statusText = "Ready to test. Click 'START SPEED TEST'.";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width;
        int h = ClientSize.Height;

        // Position bottom controls
        int btnY = h - 68;
        int totalBtnWidth = _btnStart.Width + _btnReset.Width + _btnHistory.Width + 20;
        int btnStartX = (w - totalBtnWidth) / 2;

        _btnStart.Location = new Point(btnStartX, btnY);
        _btnReset.Location = new Point(_btnStart.Right + 10, btnY);
        _btnHistory.Location = new Point(_btnReset.Right + 10, btnY);
        _linkTerms.Location = new Point((w - _linkTerms.Width) / 2, h - 22);

        // --- 1. HEADER ---
        using var titleFont = new Font("Segoe UI", 13.5f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(TextWhite);
        using var mutedBrush = new SolidBrush(TextMuted);

        g.DrawString("Internet Speed Test", titleFont, whiteBrush, 24, 16);
        g.DrawString(_serverInfo, subFont, mutedBrush, 26, 42);

        // --- 2. THREE RESULT CARDS ---
        int cardY = 68;
        int cardH = 68;
        int cardSpacing = 12;
        int cardW = (w - 48 - (cardSpacing * 2)) / 3;

        // Card 1: PING & JITTER
        DrawMetricCard(
            g,
            24, cardY, cardW, cardH,
            "PING / LATENCY",
            _finalLatencyMs.HasValue ? $"{_finalLatencyMs.Value:0}" : "--",
            "ms",
            _finalJitterMs.HasValue ? $"± {_finalJitterMs.Value:0.0} ms jitter" : "HTTP Round Trip",
            LatencyColor
        );

        // Card 2: DOWNLOAD
        DrawMetricCard(
            g,
            24 + cardW + cardSpacing, cardY, cardW, cardH,
            "DOWNLOAD",
            _finalDownloadMbps.HasValue ? $"{_finalDownloadMbps.Value:0.0}" : "--",
            "Mbps",
            _currentPhase == SpeedTestPhase.Download ? "Streaming (15s)..." : "Throughput",
            DownloadColor
        );

        // Card 3: UPLOAD
        DrawMetricCard(
            g,
            24 + (cardW + cardSpacing) * 2, cardY, cardW, cardH,
            "UPLOAD",
            _finalUploadMbps.HasValue ? $"{_finalUploadMbps.Value:0.0}" : "--",
            "Mbps",
            _currentPhase == SpeedTestPhase.Upload ? "Streaming (15s)..." : "Throughput",
            UploadColor
        );

        // --- 3. CENTRAL SPEEDOMETER / GAUGE ---
        int gaugeTop = 150;
        int gaugeBottom = btnY - 35;
        int gaugeCenterY = (gaugeTop + gaugeBottom) / 2 + 10;
        int gaugeCenterX = w / 2;
        int gaugeRadius = Math.Min(115, (gaugeBottom - gaugeTop) / 2);

        DrawSpeedometer(g, gaugeCenterX, gaugeCenterY, gaugeRadius);

        // --- 4. PROGRESS BAR ---
        int barY = btnY - 26;
        int barW = Math.Min(360, w - 80);
        int barX = (w - barW) / 2;
        using var barBgBrush = new SolidBrush(Color.FromArgb(24, 34, 54));
        using (var bgPath = CreateRoundedRectangle(new RectangleF(barX, barY, barW, 5), 2.5f))
        {
            g.FillPath(barBgBrush, bgPath);
        }

        if (_percentComplete > 0)
        {
            float fillW = (float)(barW * (_percentComplete / 100.0));
            Color activeColor = GetPhaseColor(_currentPhase);
            using var barFillBrush = new SolidBrush(activeColor);
            using (var fillPath = CreateRoundedRectangle(new RectangleF(barX, barY, fillW, 5), 2.5f))
            {
                g.FillPath(barFillBrush, fillPath);
            }
        }

        // --- 5. STATUS TEXT ---
        using var statusFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        var statusSize = g.MeasureString(_statusText, statusFont);
        g.DrawString(_statusText, statusFont, mutedBrush, (w - statusSize.Width) / 2, btnY - 20);
    }

    private void DrawMetricCard(Graphics g, int x, int y, int w, int h, string title, string value, string unit, string subtext, Color accent)
    {
        var rect = new RectangleF(x, y, w, h);
        using (var path = CreateRoundedRectangle(rect, 6f))
        {
            using var bgBrush = new SolidBrush(CardBg);
            g.FillPath(bgBrush, path);
            using var borderPen = new Pen(CardBorder, 1f);
            g.DrawPath(borderPen, path);
        }

        // Title with small color dot
        using var dotBrush = new SolidBrush(accent);
        g.FillEllipse(dotBrush, x + 12, y + 11, 6, 6);

        using var titleFont = new Font("Segoe UI", 7.0f, FontStyle.Bold);
        using var mutedBrush = new SolidBrush(TextMuted);
        g.DrawString(title, titleFont, mutedBrush, x + 22, y + 8);

        // Value & unit
        using var valFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(TextWhite);
        g.DrawString(value, valFont, whiteBrush, x + 10, y + 22);

        var valSize = g.MeasureString(value, valFont);
        using var unitFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var accentBrush = new SolidBrush(accent);
        g.DrawString(unit, unitFont, accentBrush, x + 12 + valSize.Width, y + 30);

        // Subtext
        using var subFont = new Font("Segoe UI", 6.8f, FontStyle.Regular);
        g.DrawString(subtext, subFont, mutedBrush, x + 12, y + 49);
    }

    private void DrawSpeedometer(Graphics g, int cx, int cy, int radius)
    {
        RectangleF arcRect = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);

        // 270-degree sweep (from 135 to 405)
        const float startAngle = 135f;
        const float sweepAngle = 270f;
        const float trackThickness = 10f;

        // Background track arc
        using (var trackPen = new Pen(Color.FromArgb(24, 34, 54), trackThickness))
        {
            trackPen.StartCap = LineCap.Round;
            trackPen.EndCap = LineCap.Round;
            g.DrawArc(trackPen, arcRect, startAngle, sweepAngle);
        }

        // Calculate progress percentage on logarithmic scale (0 to 1000 Mbps)
        double currentSpeed = Math.Max(0, _gaugeSpeed);
        float progressFraction = (float)CalculateSpeedFraction(currentSpeed);
        float activeSweep = Math.Max(1f, sweepAngle * progressFraction);

        Color activeColor = GetPhaseColor(_currentPhase);

        // Active progress arc
        using (var activePen = new Pen(activeColor, trackThickness))
        {
            activePen.StartCap = LineCap.Round;
            activePen.EndCap = LineCap.Round;
            g.DrawArc(activePen, arcRect, startAngle, activeSweep);
        }

        // Gauge scale labels
        using var tickFont = new Font("Segoe UI", 6.5f, FontStyle.Regular);
        using var tickBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        int[] scaleValues = { 0, 10, 50, 100, 250, 500, 1000 };

        for (int i = 0; i < scaleValues.Length; i++)
        {
            int val = scaleValues[i];
            float frac = (float)CalculateSpeedFraction(val);
            float angleDeg = startAngle + (sweepAngle * frac);
            double angleRad = angleDeg * Math.PI / 180.0;

            int tickR = radius - 16;
            int tx = cx + (int)(tickR * Math.Cos(angleRad));
            int ty = cy + (int)(tickR * Math.Sin(angleRad));

            string text = val.ToString();
            var sz = g.MeasureString(text, tickFont);
            g.DrawString(text, tickFont, tickBrush, tx - sz.Width / 2, ty - sz.Height / 2);
        }

        // Central Speed Readout
        string speedDisplay = _currentPhase == SpeedTestPhase.Latency
            ? (_finalLatencyMs.HasValue ? $"{_finalLatencyMs.Value:0}" : $"{currentSpeed:0}")
            : $"{currentSpeed:0.0}";

        string unitDisplay = _currentPhase == SpeedTestPhase.Latency ? "ms ping" : "Mbps";

        using var readoutFont = new Font("Segoe UI", 26f, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(TextWhite);
        var readoutSize = g.MeasureString(speedDisplay, readoutFont);
        g.DrawString(speedDisplay, readoutFont, whiteBrush, cx - readoutSize.Width / 2, cy - 24);

        using var unitFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var activeBrush = new SolidBrush(activeColor);
        var unitSize = g.MeasureString(unitDisplay, unitFont);
        g.DrawString(unitDisplay, unitFont, activeBrush, cx - unitSize.Width / 2, cy + 18);

        // Phase Badge Pill
        string badgeText = _currentPhase switch
        {
            SpeedTestPhase.Connecting => "CONNECTING",
            SpeedTestPhase.Latency => "TESTING LATENCY",
            SpeedTestPhase.Download => "TESTING DOWNLOAD",
            SpeedTestPhase.Upload => "TESTING UPLOAD",
            SpeedTestPhase.Completed => "TEST COMPLETE",
            SpeedTestPhase.Failed => "TEST FAILED",
            SpeedTestPhase.Cancelled => "CANCELLED",
            _ => "READY"
        };

        using var badgeFont = new Font("Segoe UI", 6.8f, FontStyle.Bold);
        var badgeSize = g.MeasureString(badgeText, badgeFont);
        int badgeW = (int)badgeSize.Width + 16;
        int badgeH = 18;
        int badgeX = cx - badgeW / 2;
        int badgeY = cy + 40;

        using (var pillPath = CreateRoundedRectangle(new RectangleF(badgeX, badgeY, badgeW, badgeH), 4f))
        {
            using var pillBg = new SolidBrush(Color.FromArgb(30, 41, 59));
            g.FillPath(pillBg, pillPath);
            using var pillBorder = new Pen(activeColor, 1f);
            g.DrawPath(pillBorder, pillPath);
        }
        g.DrawString(badgeText, badgeFont, activeBrush, badgeX + 8, badgeY + 2);
    }

    private static double CalculateSpeedFraction(double speedMbps)
    {
        if (speedMbps <= 0) return 0.0;
        if (speedMbps >= 1000) return 1.0;
        return Math.Log10(1 + (speedMbps * 9.0 / 1000.0)) / Math.Log10(10);
    }

    private static Color GetPhaseColor(SpeedTestPhase phase) => phase switch
    {
        SpeedTestPhase.Latency => LatencyColor,
        SpeedTestPhase.Download => DownloadColor,
        SpeedTestPhase.Upload => UploadColor,
        SpeedTestPhase.Completed => CompletedColor,
        SpeedTestPhase.Failed => Color.FromArgb(239, 68, 68),
        _ => Color.FromArgb(59, 130, 246)
    };

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_isRunning)
        {
            _cts?.Cancel();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _animTimer.Stop();
            _animTimer.Dispose();
            _speedTestService.Dispose();
        }
        base.Dispose(disposing);
    }
}
