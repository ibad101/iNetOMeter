using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace iNetOMeter;

public class TaskbarBandForm : Form
{
    // Design tokens matching reference image
    public static readonly Color DownloadAccent = Color.FromArgb(255, 82, 92);    // #FF525C Red
    public static readonly Color DownloadBadgeBg = Color.FromArgb(69, 37, 43);    // #45252B Dark Muted Red
    public static readonly Color UploadAccent = Color.FromArgb(76, 168, 255);      // #4CA8FF Blue
    public static readonly Color UploadBadgeBg = Color.FromArgb(30, 53, 74);      // #1E354A Dark Muted Blue
    public static readonly Color BackgroundCharcoal = Color.FromArgb(32, 36, 43); // #20242B Dark Charcoal
    public static readonly Color DividerColor = Color.FromArgb(56, 61, 72);        // #383D48 Subtle Divider
    public static readonly Color BorderColor = Color.FromArgb(48, 54, 66);         // #303642 Perimeter Border
    public static readonly Color SpeedTextColor = Color.White;                    // #FFFFFF Bold White

    private readonly NetworkMonitor _networkMonitor;
    private readonly TaskbarTracker _taskbarTracker;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly System.Windows.Forms.Timer _autoHideSyncTimer;
    private readonly ContextMenuStrip _contextMenu;

    private readonly Font _labelFont;
    private readonly Font _valueFont;

    private readonly SolidBrush _dlTextBrush;
    private readonly SolidBrush _ulTextBrush;
    private readonly SolidBrush _whiteBrush;
    private readonly SolidBrush _dlBadgeBrush;
    private readonly SolidBrush _ulBadgeBrush;

    private readonly Pen _dlArrowPen;
    private readonly Pen _ulArrowPen;
    private readonly Pen _divPen;
    private readonly Pen _borderPen;

    private NetworkSpeed _currentSpeed = new(0, 0);
    private Rectangle _lastBounds = Rectangle.Empty;
    private bool _isWidgetShowing = true;

    // WinEventHook for immediate taskbar position and auto-hide tracking
    private IntPtr _hWinEventHook = IntPtr.Zero;
    private readonly NativeMethods.WinEventDelegate _winEventDelegate;

    public TaskbarBandForm()
    {
        _networkMonitor = new NetworkMonitor();
        _taskbarTracker = new TaskbarTracker();

        // Window styling for native taskbar integration
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = BackgroundCharcoal;
        Size = new Size(94, 40);

        // Subtle 96% opacity for reliable hardware-composited translucent blending
        Opacity = 0.96;

        _labelFont = new Font("Segoe UI", 5.0f, FontStyle.Bold, GraphicsUnit.Point);
        _valueFont = new Font("Segoe UI", 7.2f, FontStyle.Bold, GraphicsUnit.Point);

        _dlTextBrush = new SolidBrush(DownloadAccent);
        _ulTextBrush = new SolidBrush(UploadAccent);
        _whiteBrush = new SolidBrush(SpeedTextColor);
        _dlBadgeBrush = new SolidBrush(DownloadBadgeBg);
        _ulBadgeBrush = new SolidBrush(UploadBadgeBg);

        _dlArrowPen = new Pen(DownloadAccent, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        _ulArrowPen = new Pen(UploadAccent, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        _divPen = new Pen(DividerColor, 1f);
        _borderPen = new Pen(BorderColor, 1f);

        // Context menu
        _contextMenu = new ContextMenuStrip();
        var titleItem = new ToolStripMenuItem("iNetOMeter") { Enabled = false };
        var speedTestItem = new ToolStripMenuItem("Internet Speed Test...", null, (_, _) => SpeedTestForm.ShowOrActivate())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Application.Exit());

        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(speedTestItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);
        ContextMenuStrip = _contextMenu;

        // Double click launches the Internet Speed Test
        DoubleClick += (_, _) => SpeedTestForm.ShowOrActivate();

        // Bandwidth calculation timer (1 second interval)
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // Auto-hide synchronization timer (150ms responsive interval)
        _autoHideSyncTimer = new System.Windows.Forms.Timer
        {
            Interval = 150
        };
        _autoHideSyncTimer.Tick += (_, _) => SyncTaskbarPositionAndVisibility();

        // Pin delegate to prevent GC collection while WinEventHook is active
        _winEventDelegate = OnWinEvent;
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

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SyncTaskbarPositionAndVisibility();
        _updateTimer.Start();
        _autoHideSyncTimer.Start();
        InstallWinEventHook();
    }

    private void InstallWinEventHook()
    {
        try
        {
            if (_taskbarTracker.TryGetTaskbarWindows(out IntPtr hTray, out _))
            {
                NativeMethods.GetWindowThreadProcessId(hTray, out uint explorerPid);
                _hWinEventHook = NativeMethods.SetWinEventHook(
                    NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                    NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                    IntPtr.Zero,
                    _winEventDelegate,
                    explorerPid,
                    0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS
                );
            }
        }
        catch
        {
            // If hook installation fails, the 150ms timer remains as reliable fallback
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(SyncTaskbarPositionAndVisibility));
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        _currentSpeed = _networkMonitor.GetCurrentSpeed();
        if (_isWidgetShowing)
        {
            Invalidate();
        }
    }

    private void SyncTaskbarPositionAndVisibility()
    {
        if (!IsHandleCreated || IsDisposed) return;

        if (!_taskbarTracker.TryGetTaskbarState(out Rectangle trayRect, out Rectangle notifyRect, out Rectangle monitorRect, out TaskbarEdge edge, out bool isTaskbarVisible))
        {
            return;
        }

        if (!isTaskbarVisible)
        {
            // Taskbar is currently auto-hidden offscreen: hide meter immediately
            if (_isWidgetShowing)
            {
                _isWidgetShowing = false;
                NativeMethods.SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_HIDEWINDOW
                );
            }
            return;
        }

        // Taskbar is visible: calculate anchor bounds and reposition
        Rectangle targetBounds = TaskbarTracker.CalculateAnchorBounds(trayRect, notifyRect, Width, Height);

        if (!_isWidgetShowing || targetBounds != _lastBounds)
        {
            _lastBounds = targetBounds;
            _isWidgetShowing = true;

            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                targetBounds.X,
                targetBounds.Y,
                targetBounds.Width,
                targetBounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
            );
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackgroundCharcoal);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Subtle perimeter border
        g.DrawRectangle(_borderPen, 0, 0, Width - 1, Height - 1);

        // --- TOP ROW: DOWNLOAD ---
        using (var dlBadgePath = CreateRoundedRectangle(new RectangleF(4, 2, 16, 16), 3.5f))
        {
            g.FillPath(_dlBadgeBrush, dlBadgePath);
        }

        // Downward Arrow (↓)
        g.DrawLine(_dlArrowPen, 12.0f, 5.0f, 12.0f, 13.0f);
        g.DrawLine(_dlArrowPen, 8.5f, 9.5f, 12.0f, 13.0f);
        g.DrawLine(_dlArrowPen, 15.5f, 9.5f, 12.0f, 13.0f);

        // DOWNLOAD label (uppercase red)
        g.DrawString("DOWNLOAD", _labelFont, _dlTextBrush, 24f, 1.0f);

        // Download speed value (bold white)
        string downSpeed = _currentSpeed.DownloadFormatted;
        g.DrawString(downSpeed, _valueFont, _whiteBrush, 24f, 8.5f);

        // --- SUBTLE HORIZONTAL DIVIDER ---
        g.DrawLine(_divPen, 3f, 19.5f, Width - 3f, 19.5f);

        // --- BOTTOM ROW: UPLOAD ---
        using (var ulBadgePath = CreateRoundedRectangle(new RectangleF(4, 22, 16, 16), 3.5f))
        {
            g.FillPath(_ulBadgeBrush, ulBadgePath);
        }

        // Upward Arrow (↑)
        g.DrawLine(_ulArrowPen, 12.0f, 34.5f, 12.0f, 26.5f);
        g.DrawLine(_ulArrowPen, 8.5f, 30.0f, 12.0f, 26.5f);
        g.DrawLine(_ulArrowPen, 15.5f, 30.0f, 12.0f, 26.5f);

        // UPLOAD label (uppercase blue)
        g.DrawString("UPLOAD", _labelFont, _ulTextBrush, 24f, 21.0f);

        // Upload speed value (bold white)
        string upSpeed = _currentSpeed.UploadFormatted;
        g.DrawString(upSpeed, _valueFont, _whiteBrush, 24f, 28.5f);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_hWinEventHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hWinEventHook);
                _hWinEventHook = IntPtr.Zero;
            }

            _updateTimer.Stop();
            _updateTimer.Dispose();

            _autoHideSyncTimer.Stop();
            _autoHideSyncTimer.Dispose();

            _labelFont.Dispose();
            _valueFont.Dispose();

            _dlTextBrush.Dispose();
            _ulTextBrush.Dispose();
            _whiteBrush.Dispose();
            _dlBadgeBrush.Dispose();
            _ulBadgeBrush.Dispose();

            _dlArrowPen.Dispose();
            _ulArrowPen.Dispose();
            _divPen.Dispose();
            _borderPen.Dispose();

            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
