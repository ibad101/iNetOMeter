using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace iNetOMeter;

public class HistoryForm : Form
{
    private static HistoryForm? _activeInstance;

    private static readonly Color DarkNavyBg = Color.FromArgb(11, 17, 30);       // #0B111E
    private static readonly Color CardBg = Color.FromArgb(20, 29, 47);           // #141D2F
    private static readonly Color CardBorder = Color.FromArgb(36, 48, 74);       // #24304A
    private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);     // #F8FAFC
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // #94A3B8
    private static readonly Color LatencyColor = Color.FromArgb(56, 189, 248);   // #38BDF8 Cyan
    private static readonly Color DownloadColor = Color.FromArgb(255, 82, 92);   // #FF525C Red
    private static readonly Color UploadColor = Color.FromArgb(76, 168, 255);    // #4CA8FF Blue

    private readonly Button _btnClear;
    private readonly Button _btnClose;
    private readonly Button _btnRunTest;

    public HistoryForm()
    {
        Text = "Speed Test History - iNetOMeter";
        Size = new Size(660, 520);
        MinimumSize = new Size(600, 460);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = DarkNavyBg;
        ForeColor = TextWhite;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        _btnClear = new Button
        {
            Text = "Clear History",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
            Size = new Size(110, 32)
        };
        _btnClear.FlatAppearance.BorderColor = CardBorder;
        _btnClear.Click += OnClearClicked;
        Controls.Add(_btnClear);

        _btnRunTest = new Button
        {
            Text = "Run New Test",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Size = new Size(130, 36)
        };
        _btnRunTest.FlatAppearance.BorderSize = 0;
        _btnRunTest.Click += (_, _) => SpeedTestForm.ShowOrActivate();
        Controls.Add(_btnRunTest);

        _btnClose = new Button
        {
            Text = "Close",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
            Size = new Size(90, 36)
        };
        _btnClose.FlatAppearance.BorderColor = CardBorder;
        _btnClose.Click += (_, _) => Close();
        Controls.Add(_btnClose);

        // Auto refresh when history changes
        HistoryService.Instance.HistoryChanged += OnHistoryChanged;
        FormClosed += (_, _) => HistoryService.Instance.HistoryChanged -= OnHistoryChanged;
        Resize += (_, _) => Invalidate();
    }

    public static void ShowOrActivate()
    {
        if (_activeInstance == null || _activeInstance.IsDisposed)
        {
            _activeInstance = new HistoryForm();
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

    private void OnHistoryChanged()
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(Invalidate));
        }
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Are you sure you want to clear your speed test history?\nThis action cannot be undone.",
            "Confirm Clear History",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirm == DialogResult.Yes)
        {
            HistoryService.Instance.ClearHistory();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width;
        int h = ClientSize.Height;

        // Position Header Controls
        _btnClear.Location = new Point(w - _btnClear.Width - 24, 20);

        // Position Footer Controls
        int footerY = h - 52;
        _btnRunTest.Location = new Point(w - _btnRunTest.Width - _btnClose.Width - 34, footerY);
        _btnClose.Location = new Point(w - _btnClose.Width - 24, footerY);

        // --- 1. HEADER ---
        using var titleFont = new Font("Segoe UI", 13.5f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(TextWhite);
        using var mutedBrush = new SolidBrush(TextMuted);

        g.DrawString("Speed Test History", titleFont, whiteBrush, 24, 16);
        g.DrawString("Last 5 completed tests (newest first, stored locally)", subFont, mutedBrush, 26, 42);

        // Separator line
        using var sepPen = new Pen(CardBorder, 1f);
        g.DrawLine(sepPen, 24, 68, w - 24, 68);

        // --- 2. RECORDS LIST OR EMPTY STATE ---
        var records = HistoryService.Instance.GetHistory();

        if (records.Count == 0)
        {
            DrawEmptyState(g, w, h);
            return;
        }

        int startY = 82;
        int cardH = 62;
        int cardSpacing = 10;
        int cardW = w - 48;

        for (int i = 0; i < records.Count; i++)
        {
            int cardY = startY + i * (cardH + cardSpacing);
            if (cardY + cardH > footerY - 10) break;

            DrawHistoryCard(g, 24, cardY, cardW, cardH, records[i], i + 1);
        }
    }

    private void DrawEmptyState(Graphics g, int w, int h)
    {
        int centerY = h / 2 - 20;

        using var emptyTitleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        using var emptySubFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(TextWhite);
        using var mutedBrush = new SolidBrush(TextMuted);

        string title = "No speed tests recorded yet";
        string sub = "Run an internet speed test from the taskbar menu or dashboard to see results here.";

        var szTitle = g.MeasureString(title, emptyTitleFont);
        var szSub = g.MeasureString(sub, emptySubFont);

        g.DrawString(title, emptyTitleFont, whiteBrush, (w - szTitle.Width) / 2, centerY - 20);
        g.DrawString(sub, emptySubFont, mutedBrush, (w - szSub.Width) / 2, centerY + 10);
    }

    private void DrawHistoryCard(Graphics g, int x, int y, int w, int h, SpeedTestHistoryRecord record, int index)
    {
        var rect = new RectangleF(x, y, w, h);
        using (var path = CreateRoundedRectangle(rect, 6f))
        {
            using var bgBrush = new SolidBrush(CardBg);
            g.FillPath(bgBrush, path);
            using var borderPen = new Pen(CardBorder, 1f);
            g.DrawPath(borderPen, path);
        }

        using var idxFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var dateFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var metricValFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var metricLabelFont = new Font("Segoe UI", 7.0f, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(TextWhite);
        using var mutedBrush = new SolidBrush(TextMuted);
        using var dlBrush = new SolidBrush(DownloadColor);
        using var ulBrush = new SolidBrush(UploadColor);
        using var pingBrush = new SolidBrush(LatencyColor);

        // Index Badge (#1, #2, ...)
        g.DrawString($"#{index}", idxFont, mutedBrush, x + 12, y + 12);

        // Timestamp & Server
        string timeStr = record.Timestamp.ToString("yyyy-MM-dd  HH:mm:ss");
        g.DrawString(timeStr, dateFont, whiteBrush, x + 42, y + 12);

        string metaStr = $"{record.ServerName}  •  {record.DurationSeconds:0.0}s duration";
        g.DrawString(metaStr, subFont, mutedBrush, x + 42, y + 33);

        // Right side: Metric Columns
        // Download (Red)
        int rightMargin = x + w - 16;
        int ulX = rightMargin - 95;
        int dlX = ulX - 110;
        int pingX = dlX - 110;

        // PING
        g.DrawString("PING", metricLabelFont, mutedBrush, pingX, y + 11);
        string pingVal = $"{record.LatencyMs:0} ms";
        g.DrawString(pingVal, metricValFont, pingBrush, pingX, y + 24);
        if (record.JitterMs > 0)
        {
            g.DrawString($"±{record.JitterMs:0.0} ms", subFont, mutedBrush, pingX + 46, y + 28);
        }

        // DOWNLOAD
        g.DrawString("DOWNLOAD", metricLabelFont, mutedBrush, dlX, y + 11);
        string dlVal = $"{record.DownloadMbps:0.0} Mbps";
        g.DrawString(dlVal, metricValFont, dlBrush, dlX, y + 24);

        // UPLOAD
        g.DrawString("UPLOAD", metricLabelFont, mutedBrush, ulX, y + 11);
        string ulVal = $"{record.UploadMbps:0.0} Mbps";
        g.DrawString(ulVal, metricValFont, ulBrush, ulX, y + 24);
    }

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
}
