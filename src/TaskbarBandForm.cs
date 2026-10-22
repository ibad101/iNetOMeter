using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

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
    private readonly uint _taskbarCreatedMsg;
    private readonly ConnectivityMonitor _connectivityMonitor;
    private readonly WiFiAssistant _wifiAssistant;
    private uint _explorerPid;

    public TaskbarBandForm()
    {
        _networkMonitor = new NetworkMonitor();
        _taskbarTracker = new TaskbarTracker();
        _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        _connectivityMonitor = new ConnectivityMonitor();
        _wifiAssistant = new WiFiAssistant(WiFiService.Instance, _connectivityMonitor);

        // Window styling for native taskbar integration
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = BackgroundCharcoal;
        Size = new Size(94, 40);

        // Apply saved opacity preference (default: 0.96)
        Opacity = SettingsService.Instance.Current.WidgetOpacity;

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

        _contextMenu = new ContextMenuStrip
        {
            AutoClose = true
        };
        var titleItem = new ToolStripMenuItem("iNetOMeter") { Enabled = false };

        var speedTestItem = new ToolStripMenuItem("Run Speed Test", null, (_, _) => SpeedTestForm.ShowOrActivate())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var historyItem = new ToolStripMenuItem("View Last 5 Tests", null, (_, _) => HistoryForm.ShowOrActivate());
        var settingsItem = new ToolStripMenuItem("Settings", null, (_, _) => SettingsForm.ShowOrActivate());
        var uninstallItem = new ToolStripMenuItem("Uninstall iNetOMeter", null, (_, _) => OnUninstallClicked());
        var exitItem = new ToolStripMenuItem("Exit iNetOMeter", null, (_, _) => Application.Exit());

        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(speedTestItem);
        _contextMenu.Items.Add(historyItem);
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(uninstallItem);
        _contextMenu.Items.Add(exitItem);

        // Microsoft KB135788 tray menu pattern:
        // Set foreground window on opening and post WM_NULL on closed so outside clicks
        // and Escape cleanly dismiss the menu, without aggressive repeated activation tricks.
        _contextMenu.Opening += (_, _) =>
        {
            if (IsHandleCreated)
            {
                NativeMethods.SetForegroundWindow(Handle);
            }
        };

        _contextMenu.Closed += (_, _) =>
        {
            if (IsHandleCreated)
            {
                NativeMethods.PostMessage(Handle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
        };

        ContextMenuStrip = _contextMenu;

        // Double click launches the Internet Speed Test
        DoubleClick += (_, _) => SpeedTestForm.ShowOrActivate();

        // Bandwidth calculation timer
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = SettingsService.Instance.Current.UpdateIntervalMs
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

        // Listen for user preference updates
        SettingsService.Instance.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(UserSettings settings)
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(() =>
            {
                Opacity = settings.WidgetOpacity;
                _updateTimer.Interval = settings.UpdateIntervalMs;
                Invalidate();
            }));
        }
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
                _explorerPid = explorerPid;
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
            EnsureMeterVisibleAboveTaskbar();
        }
    }

    private void EnsureMeterVisibleAboveTaskbar()
    {
        if (_contextMenu.Visible || !IsHandleCreated || IsDisposed || !_isWidgetShowing)
        {
            return;
        }

        // 1. Recover from native hidden state (Win+D, lock screen, display sleep)
        if (!NativeMethods.IsWindowVisible(Handle))
        {
            _lastBounds = Rectangle.Empty;
            _isWidgetShowing = false;
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
            );
            _isWidgetShowing = true;
            return;
        }

        // 2. Least intrusive check: only restore if Explorer's taskbar is actively covering our window.
        // Never cover third-party windows, popups, or fullscreen applications.
        if (_lastBounds.Width > 0 && _lastBounds.Height > 0)
        {
            if (_explorerPid == 0 && _taskbarTracker.TryGetTaskbarWindows(out IntPtr hTray, out _))
            {
                NativeMethods.GetWindowThreadProcessId(hTray, out _explorerPid);
            }

            Point centerPt = new Point(_lastBounds.Left + _lastBounds.Width / 2, _lastBounds.Top + _lastBounds.Height / 2);
            IntPtr topWnd = NativeMethods.WindowFromPoint(centerPt);
            if (topWnd != IntPtr.Zero && topWnd != Handle)
            {
                NativeMethods.GetWindowThreadProcessId(topWnd, out uint topPid);
                if (topPid != 0 && topPid == _explorerPid)
                {
                    NativeMethods.SetWindowPos(
                        Handle,
                        NativeMethods.HWND_TOPMOST,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
                    );
                }
            }
        }
    }

    private void SyncTaskbarPositionAndVisibility()
    {
        if (!IsHandleCreated || IsDisposed) return;

        // If the context menu is open, never disturb or reposition or hide anything while user interacts with menu
        if (_contextMenu.Visible)
        {
            return;
        }

        if (!_taskbarTracker.TryGetTaskbarState(out Rectangle trayRect, out Rectangle notifyRect, out Rectangle monitorRect, out TaskbarEdge edge, out bool isTaskbarVisible))
        {
            return;
        }

        if (!isTaskbarVisible)
        {
            // Taskbar is legitimately auto-hidden offscreen: hide meter immediately
            if (_isWidgetShowing)
            {
                _isWidgetShowing = false;
                _lastBounds = Rectangle.Empty; // Reset so when taskbar returns, it will always reposition and show!
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
        else
        {
            // If already showing and at target bounds, ensure the native window hasn't been hidden
            // by a temporary event (such as Win+D / Show Desktop or monitor sleep)
            EnsureMeterVisibleAboveTaskbar();
        }
    }

    internal bool IsContextMenuVisible => _contextMenu.Visible;
    internal ContextMenuStrip MenuStrip => _contextMenu;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == _taskbarCreatedMsg && _taskbarCreatedMsg != 0)
        {
            // Windows Explorer restarted: re-establish hooks and reposition
            if (_hWinEventHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hWinEventHook);
                _hWinEventHook = IntPtr.Zero;
            }
            _lastBounds = Rectangle.Empty;
            _isWidgetShowing = false;
            _explorerPid = 0;
            InstallWinEventHook();
            SyncTaskbarPositionAndVisibility();
        }
        else if (m.Msg == NativeMethods.WM_DISPLAYCHANGE || m.Msg == NativeMethods.WM_DPICHANGED)
        {
            _lastBounds = Rectangle.Empty;
            SyncTaskbarPositionAndVisibility();
        }

        base.WndProc(ref m);
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
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;

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

            _wifiAssistant.Dispose();
            _connectivityMonitor.Dispose();
            WindowsNotificationService.Instance.Dispose();
        }
        base.Dispose(disposing);
    }

    public static bool IsInstalledEnvironment(string appDirectory, string? registryInstallLocation = null)
    {
        string localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "iNetOMeter"
        ).TrimEnd('\\', '/');

        string normalizedAppDir = appDirectory.TrimEnd('\\', '/');

        if (normalizedAppDir.Equals(localPrograms, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(registryInstallLocation))
        {
            string normalizedRegDir = registryInstallLocation.TrimEnd('\\', '/');
            if (normalizedAppDir.Equals(normalizedRegDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (File.Exists(Path.Combine(appDirectory, "unins000.exe")))
        {
            return true;
        }

        return false;
    }

    private void OnUninstallClicked()
    {
        string appDir = AppContext.BaseDirectory;
        string? regInstallLocation = null;
        string? regUninstallString = null;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\iNetOMeter");
            regInstallLocation = key?.GetValue("InstallLocation") as string;
            regUninstallString = key?.GetValue("UninstallString") as string;
        }
        catch { }

        bool isInstalled = IsInstalledEnvironment(appDir, regInstallLocation);

        if (!isInstalled)
        {
            var portResult = MessageBox.Show(
                "iNetOMeter is currently running in portable mode (not installed through Windows setup).\n\n" +
                "Because it was extracted rather than installed, there are no system uninstaller files.\n\n" +
                $"To remove iNetOMeter:\n1. Exit the application.\n2. Delete this folder:\n   {appDir}\n\n" +
                "Your settings and test history in %LOCALAPPDATA%\\iNetOMeter will be preserved unless manually deleted.\n\n" +
                "Would you like to exit iNetOMeter now?",
                "Uninstall iNetOMeter (Portable Mode)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (portResult == DialogResult.Yes)
            {
                Application.Exit();
            }
            return;
        }

        // Installed mode confirmation dialog
        var confirmResult = MessageBox.Show(
            "Are you sure you want to uninstall iNetOMeter from your computer?\n\n" +
            "This will remove the application, Start Menu shortcut, and system registration.\n\n" +
            "Your settings and test history in %LOCALAPPDATA%\\iNetOMeter will be preserved by default.\n\n" +
            "Click Yes to proceed with uninstallation, or No to cancel.",
            "Uninstall iNetOMeter",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmResult != DialogResult.Yes)
        {
            return; // Cancel makes no changes
        }

        // Locate uninstaller
        string innoUninstaller = Path.Combine(appDir, "unins000.exe");
        string psUninstaller = Path.Combine(appDir, "Uninstall-iNetOMeter.ps1");

        try
        {
            if (File.Exists(innoUninstaller))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = innoUninstaller,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Application.Exit();
                return;
            }

            if (File.Exists(psUninstaller))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -WindowStyle Normal -Command \"Start-Sleep -Milliseconds 800; & '{psUninstaller}'\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
                Application.Exit();
                return;
            }

            // Fallback to registry UninstallString if defined
            if (!string.IsNullOrEmpty(regUninstallString))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -WindowStyle Normal -Command \"Start-Sleep -Milliseconds 800; {regUninstallString}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
                Application.Exit();
                return;
            }

            MessageBox.Show(
                "Could not locate the uninstaller script or executable in the application directory.\n\n" +
                "You can uninstall iNetOMeter through Windows Settings > Apps & Features.",
                "Uninstaller Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start the uninstaller: {ex.Message}\n\n" +
                "You can uninstall iNetOMeter through Windows Settings > Apps & Features.",
                "Uninstall Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
