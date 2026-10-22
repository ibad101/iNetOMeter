using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace iNetOMeter;

public class TaskbarBandForm : Form
{
    // Design accents per Stage 1.1 requirements
    public static readonly Color DownloadColor = Color.FromArgb(255, 82, 92);  // #FF525C Red accent
    public static readonly Color UploadColor = Color.FromArgb(76, 168, 255);    // #4CA8FF Blue accent

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
        BackColor = Color.Black;
        Size = new Size(84, 40);

        _textFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        _arrowFont = new Font("Segoe UI", 7.0f, FontStyle.Bold, GraphicsUnit.Point);

        // Context menu
        _contextMenu = new ContextMenuStrip();
        var titleItem = new ToolStripMenuItem("iNetOMeter (Stage 1.1)") { Enabled = false };
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
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
            cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Apply acrylic blur behind window with subtle 78% opacity dark tint (0xC8101010)
        NativeMethods.EnableTaskbarBlur(Handle, 0xC8101010);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        NativeMethods.EnableTaskbarBlur(Handle, 0xC8101010);
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

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Clear with Color.Black so DWM replaces black with the acrylic blur and tint
        e.Graphics.Clear(Color.Black);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Subtle frosted-glass boundary highlight for clean edge definition
        using var borderPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // Upload brush (#4CA8FF Blue accent)
        using var upBrush = new SolidBrush(UploadColor);
        // Download brush (#FF525C Red accent)
        using var downBrush = new SolidBrush(DownloadColor);

        // Line 1: Upload (▲)
        string upText = _currentSpeed.UploadFormatted;
        g.DrawString("▲", _arrowFont, upBrush, 3, 3);
        g.DrawString(upText, _textFont, upBrush, 15, 3);

        // Line 2: Download (▼)
        string downText = _currentSpeed.DownloadFormatted;
        g.DrawString("▼", _arrowFont, downBrush, 3, 20);
        g.DrawString(downText, _textFont, downBrush, 15, 20);
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
