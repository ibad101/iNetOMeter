using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace iNetOMeter;

public class TaskbarBandForm : Form
{
    private readonly NetworkMonitor _networkMonitor;
    private readonly TaskbarTracker _taskbarTracker;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly Font _textFont;
    private readonly Font _arrowFont;
    private readonly ContextMenuStrip _contextMenu;

    private NetworkSpeed _currentSpeed = new(0, 0);
    private Rectangle _lastBounds = Rectangle.Empty;

    public TaskbarBandForm()
    {
        _networkMonitor = new NetworkMonitor();
        _taskbarTracker = new TaskbarTracker();

        // Window styling for native taskbar integration
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(16, 16, 16);
        Size = new Size(84, 40);

        _textFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        _arrowFont = new Font("Segoe UI", 7.0f, FontStyle.Bold, GraphicsUnit.Point);

        // Context menu
        _contextMenu = new ContextMenuStrip();
        var titleItem = new ToolStripMenuItem("iNetOMeter Prototype (Stage 1)") { Enabled = false };
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Application.Exit());
        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);
        ContextMenuStrip = _contextMenu;

        // Periodic update timer (1 second interval)
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _updateTimer.Tick += OnUpdateTimerTick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // Prevent activation on click and keep as tool window
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
            cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdatePositionAndSpeed();
        _updateTimer.Start();
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        UpdatePositionAndSpeed();
    }

    private void UpdatePositionAndSpeed()
    {
        _currentSpeed = _networkMonitor.GetCurrentSpeed();

        var targetBounds = _taskbarTracker.GetTargetBounds(Width, Height);
        if (targetBounds.HasValue && targetBounds.Value != _lastBounds && !targetBounds.Value.IsEmpty)
        {
            _lastBounds = targetBounds.Value;
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                _lastBounds.X,
                _lastBounds.Y,
                _lastBounds.Width,
                _lastBounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Colors
        using var upBrush = new SolidBrush(Color.FromArgb(100, 220, 255));   // Soft Cyan
        using var downBrush = new SolidBrush(Color.FromArgb(120, 240, 140)); // Soft Green
        using var textBrush = new SolidBrush(Color.FromArgb(230, 230, 230));

        // Line 1: Upload (▲)
        string upText = _currentSpeed.UploadFormatted;
        g.DrawString("▲", _arrowFont, upBrush, 2, 3);
        g.DrawString(upText, _textFont, textBrush, 14, 3);

        // Line 2: Download (▼)
        string downText = _currentSpeed.DownloadFormatted;
        g.DrawString("▼", _arrowFont, downBrush, 2, 20);
        g.DrawString(downText, _textFont, textBrush, 14, 20);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _textFont.Dispose();
            _arrowFont.Dispose();
            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
