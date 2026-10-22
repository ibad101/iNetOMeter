using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iNetOMeter;

public class WiFiSuggestionPopup : Form
{
    private static WiFiSuggestionPopup? _currentPopup;
    private static readonly object _popupLock = new();

    private static readonly Color DarkNavyBg = Color.FromArgb(11, 17, 30);   // #0B111E
    private static readonly Color CardBg = Color.FromArgb(20, 29, 47);       // #141D2F
    private static readonly Color CardBorder = Color.FromArgb(36, 48, 74);   // #24304A
    private static readonly Color TextWhite = Color.FromArgb(248, 250, 252); // #F8FAFC
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184); // #94A3B8
    private static readonly Color AccentCyan = Color.FromArgb(56, 189, 248); // #38BDF8
    private static readonly Color ActionBlue = Color.FromArgb(37, 99, 235);  // #2563EB
    private static readonly Color SuccessGreen = Color.FromArgb(16, 185, 129); // #10B981
    private static readonly Color FailureRed = Color.FromArgb(255, 82, 92);  // #FF525C

    private readonly WiFiCandidate _candidate;
    private readonly Func<WiFiCandidate, Task<WiFiSwitchResult>> _switchAction;
    private readonly System.Windows.Forms.Timer _autoDismissTimer;
    private int _secondsRemaining = 15;

    private readonly Label _lblTitle;
    private readonly Label _lblMessage;
    private readonly Label _lblSubtext;
    private readonly Label _lblStatus;
    private readonly Button _btnSwitch;
    private readonly Button _btnDismiss;
    private readonly Button _btnClose;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;

            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public WiFiSuggestionPopup(
        string currentSsid,
        int currentSignal,
        bool isInternetLost,
        WiFiCandidate candidate,
        Func<WiFiCandidate, Task<WiFiSwitchResult>> switchAction)
    {
        _candidate = candidate;
        _switchAction = switchAction;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = DarkNavyBg;
        Size = new Size(360, 210);
        DoubleBuffered = true;

        // Position at bottom-right corner of desktop working area
        var wa = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);

        // Header Title
        _lblTitle = new Label
        {
            Text = "Smart Wi-Fi Assistant",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = AccentCyan,
            Location = new Point(16, 14),
            AutoSize = true
        };
        Controls.Add(_lblTitle);

        // Close button (×)
        _btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(26, 24),
            Location = new Point(Width - 36, 12),
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.FlatAppearance.MouseOverBackColor = CardBg;
        _btnClose.Click += (_, _) => Close();
        Controls.Add(_btnClose);

        // Message
        string messageText = isInternetLost
            ? $"Internet is unreachable on \"{currentSsid}\".\nSaved network \"{candidate.Ssid}\" is in range with {candidate.SignalPercent}% signal."
            : $"Current Wi-Fi \"{currentSsid}\" is weak ({currentSignal}%).\nSaved network \"{candidate.Ssid}\" is in range with {candidate.SignalPercent}% signal (+{candidate.SignalAdvantage}% advantage).";

        _lblMessage = new Label
        {
            Text = messageText,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = TextWhite,
            Location = new Point(16, 44),
            Size = new Size(328, 48)
        };
        Controls.Add(_lblMessage);

        // Subtext / Disclaimer
        _lblSubtext = new Label
        {
            Text = "Windows-managed saved profile. Switching requires approval.",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            ForeColor = TextMuted,
            Location = new Point(16, 96),
            Size = new Size(328, 18)
        };
        Controls.Add(_lblSubtext);

        // Live Action Status Label
        _lblStatus = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(16, 122),
            Size = new Size(328, 22),
            Visible = false
        };
        Controls.Add(_lblStatus);

        // Switch Button
        _btnSwitch = new Button
        {
            Text = $"Switch to {candidate.Ssid}",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ActionBlue,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(200, 34),
            Location = new Point(16, 155),
            Cursor = Cursors.Hand
        };
        _btnSwitch.FlatAppearance.BorderSize = 0;
        _btnSwitch.Click += OnSwitchClicked;
        Controls.Add(_btnSwitch);

        // Dismiss Button
        _btnDismiss = new Button
        {
            Text = $"Dismiss ({_secondsRemaining}s)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = TextMuted,
            BackColor = CardBg,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(118, 34),
            Location = new Point(226, 155),
            Cursor = Cursors.Hand
        };
        _btnDismiss.FlatAppearance.BorderColor = CardBorder;
        _btnDismiss.Click += (_, _) => Close();
        Controls.Add(_btnDismiss);

        // Auto-dismiss timer
        _autoDismissTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _autoDismissTimer.Tick += OnAutoDismissTick;
        _autoDismissTimer.Start();

        Paint += OnPaintBorder;
    }

    public static void ShowOrUpdate(
        string currentSsid,
        int currentSignal,
        bool isInternetLost,
        WiFiCandidate candidate,
        Func<WiFiCandidate, Task<WiFiSwitchResult>> switchAction)
    {
        lock (_popupLock)
        {
            if (_currentPopup != null && !_currentPopup.IsDisposed)
            {
                _currentPopup.Close();
                _currentPopup.Dispose();
                _currentPopup = null;
            }

            _currentPopup = new WiFiSuggestionPopup(
                currentSsid,
                currentSignal,
                isInternetLost,
                candidate,
                switchAction
            );
            _currentPopup.FormClosed += (_, _) =>
            {
                lock (_popupLock)
                {
                    _currentPopup = null;
                }
            };
            _currentPopup.Show();
        }
    }

    private void OnPaintBorder(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(CardBorder, 1.5f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        // Accent top strip
        using var accentBrush = new SolidBrush(AccentCyan);
        e.Graphics.FillRectangle(accentBrush, 0, 0, Width, 3);
    }

    private void OnAutoDismissTick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            _autoDismissTimer.Stop();
            Close();
        }
        else
        {
            if (_btnDismiss.Enabled)
            {
                _btnDismiss.Text = $"Dismiss ({_secondsRemaining}s)";
            }
        }
    }

    private async void OnSwitchClicked(object? sender, EventArgs e)
    {
        _autoDismissTimer.Stop();
        _btnSwitch.Enabled = false;
        _btnDismiss.Enabled = false;
        _btnClose.Enabled = false;
        _btnSwitch.Text = "Switching...";

        _lblStatus.ForeColor = AccentCyan;
        _lblStatus.Text = $"Connecting to {_candidate.Ssid}...";
        _lblStatus.Visible = true;

        try
        {
            var result = await _switchAction(_candidate);

            if (result.Success && result.InternetReachable)
            {
                _lblStatus.ForeColor = SuccessGreen;
                _lblStatus.Text = $"✓ Connected to {result.TargetSsid}. Internet verified!";
                await Task.Delay(2500);
                Close();
            }
            else
            {
                _lblStatus.ForeColor = FailureRed;
                _lblStatus.Text = string.IsNullOrEmpty(result.ErrorMessage)
                    ? "Could not verify connection. Kept original network."
                    : result.ErrorMessage;
                _btnDismiss.Enabled = true;
                _btnDismiss.Text = "Close";
                await Task.Delay(4000);
                Close();
            }
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = FailureRed;
            _lblStatus.Text = $"Error: {ex.Message}";
            _btnDismiss.Enabled = true;
            _btnDismiss.Text = "Close";
            await Task.Delay(4000);
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
