using System;
using System.Drawing;
using System.Windows.Forms;

namespace iNetOMeter;

public class SettingsForm : Form
{
    private static SettingsForm? _activeInstance;

    private static readonly Color DarkNavyBg = Color.FromArgb(11, 17, 30);   // #0B111E
    private static readonly Color CardBg = Color.FromArgb(20, 29, 47);       // #141D2F
    private static readonly Color CardBorder = Color.FromArgb(36, 48, 74);   // #24304A
    private static readonly Color TextWhite = Color.FromArgb(248, 250, 252); // #F8FAFC
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184); // #94A3B8

    private readonly CheckBox _chkStartup;
    private readonly ComboBox _cmbSpeedFormat;
    private readonly ComboBox _cmbUpdateInterval;
    private readonly TrackBar _tbOpacity;
    private readonly Label _lblOpacityValue;

    // Stage 6 Notifications Controls
    private readonly CheckBox _chkNotificationsEnabled;
    private readonly CheckBox _chkNotifyDisconnect;
    private readonly CheckBox _chkNotifyRestore;
    private readonly CheckBox _chkNotifySlowSpeed;
    private readonly NumericUpDown _numSlowThreshold;
    private readonly NumericUpDown _numCooldown;

    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    public SettingsForm()
    {
        Text = "Settings - iNetOMeter";
        Size = new Size(540, 700);
        MinimumSize = new Size(500, 660);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = DarkNavyBg;
        ForeColor = TextWhite;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

        var current = SettingsService.Instance.Current;

        // --- 1. HEADER ---
        var lblTitle = new Label
        {
            Text = "Preferences",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(24, 16),
            AutoSize = true
        };
        Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = "Customize taskbar widget, display units, and smart notifications",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = TextMuted,
            Location = new Point(26, 42),
            AutoSize = true
        };
        Controls.Add(lblSub);

        // --- 2. TASKBAR SETTINGS ---
        int currentY = 76;

        // Setting 1: Start with Windows
        _chkStartup = new CheckBox
        {
            Text = "Start iNetOMeter with Windows",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(28, currentY),
            Size = new Size(460, 24),
            Checked = current.StartWithWindows,
            Cursor = Cursors.Hand
        };
        Controls.Add(_chkStartup);

        var lblStartupHint = new Label
        {
            Text = "Opt-in: Places a per-user shortcut in your Windows Startup folder (shell:startup).",
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            ForeColor = TextMuted,
            Location = new Point(48, currentY + 24),
            Size = new Size(450, 18)
        };
        Controls.Add(lblStartupHint);

        currentY += 52;

        // Setting 2: Speed Unit Format
        var lblFormat = new Label
        {
            Text = "Taskbar Speed Unit Formatting",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(28, currentY),
            AutoSize = true
        };
        Controls.Add(lblFormat);

        _cmbSpeedFormat = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(28, currentY + 22),
            Size = new Size(460, 28),
            BackColor = CardBg,
            ForeColor = TextWhite,
            FlatStyle = FlatStyle.Flat
        };
        _cmbSpeedFormat.Items.Add("Auto Bytes: B/s, KB/s, MB/s, GB/s (Default)");
        _cmbSpeedFormat.Items.Add("Auto Bits: bps, Kbps, Mbps, Gbps");
        _cmbSpeedFormat.Items.Add("Always Mbps: e.g. 12.45 Mbps");
        _cmbSpeedFormat.Items.Add("Always MB/s: e.g. 1.56 MB/s");
        _cmbSpeedFormat.SelectedIndex = (int)current.SpeedFormat;
        Controls.Add(_cmbSpeedFormat);

        currentY += 60;

        // Setting 3: Update Interval
        var lblInterval = new Label
        {
            Text = "Taskbar Refresh Frequency",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(28, currentY),
            AutoSize = true
        };
        Controls.Add(lblInterval);

        _cmbUpdateInterval = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(28, currentY + 22),
            Size = new Size(460, 28),
            BackColor = CardBg,
            ForeColor = TextWhite,
            FlatStyle = FlatStyle.Flat
        };
        _cmbUpdateInterval.Items.Add("500 ms (Fast)");
        _cmbUpdateInterval.Items.Add("1000 ms (Normal / Recommended)");
        _cmbUpdateInterval.Items.Add("2000 ms (Low Background Activity)");

        _cmbUpdateInterval.SelectedIndex = current.UpdateIntervalMs switch
        {
            500 => 0,
            2000 => 2,
            _ => 1
        };
        Controls.Add(_cmbUpdateInterval);

        currentY += 60;

        // Setting 4: Widget Opacity
        var lblOpacity = new Label
        {
            Text = "Taskbar Meter Opacity",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(28, currentY),
            AutoSize = true
        };
        Controls.Add(lblOpacity);

        _lblOpacityValue = new Label
        {
            Text = $"{(int)(current.WidgetOpacity * 100)}%",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(76, 168, 255),
            Location = new Point(430, currentY),
            Size = new Size(55, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(_lblOpacityValue);

        _tbOpacity = new TrackBar
        {
            Minimum = 70,
            Maximum = 100,
            Value = Math.Clamp((int)(current.WidgetOpacity * 100), 70, 100),
            TickFrequency = 5,
            SmallChange = 1,
            LargeChange = 5,
            Location = new Point(24, currentY + 22),
            Size = new Size(470, 45)
        };
        _tbOpacity.ValueChanged += (_, _) => _lblOpacityValue.Text = $"{_tbOpacity.Value}%";
        Controls.Add(_tbOpacity);

        currentY += 68;

        // --- 3. STAGE 6: NOTIFICATIONS SECTION ---
        var pnlDivider = new Panel
        {
            BackColor = CardBorder,
            Location = new Point(24, currentY),
            Size = new Size(470, 1)
        };
        Controls.Add(pnlDivider);
        currentY += 10;

        var lblNotifSection = new Label
        {
            Text = "Smart Desktop Notifications",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(26, currentY),
            AutoSize = true
        };
        Controls.Add(lblNotifSection);
        currentY += 28;

        // Master toggle
        _chkNotificationsEnabled = new CheckBox
        {
            Text = "Enable Windows Desktop Notifications (Opt-in)",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = TextWhite,
            Location = new Point(28, currentY),
            Size = new Size(460, 24),
            Checked = current.NotificationsEnabled,
            Cursor = Cursors.Hand
        };
        _chkNotificationsEnabled.CheckedChanged += OnNotificationsMasterToggled;
        Controls.Add(_chkNotificationsEnabled);
        currentY += 30;

        // Sub-toggles
        _chkNotifyDisconnect = new CheckBox
        {
            Text = "Alert when internet connection is lost",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = TextWhite,
            Location = new Point(48, currentY),
            Size = new Size(430, 22),
            Checked = current.NotifyOnDisconnect,
            Cursor = Cursors.Hand
        };
        Controls.Add(_chkNotifyDisconnect);
        currentY += 26;

        _chkNotifyRestore = new CheckBox
        {
            Text = "Alert when internet connection is restored",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = TextWhite,
            Location = new Point(48, currentY),
            Size = new Size(430, 22),
            Checked = current.NotifyOnRestore,
            Cursor = Cursors.Hand
        };
        Controls.Add(_chkNotifyRestore);
        currentY += 26;

        _chkNotifySlowSpeed = new CheckBox
        {
            Text = "Alert if speed test download speed is below threshold",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = TextWhite,
            Location = new Point(48, currentY),
            Size = new Size(430, 22),
            Checked = current.NotifyOnSlowSpeed,
            Cursor = Cursors.Hand
        };
        Controls.Add(_chkNotifySlowSpeed);
        currentY += 32;

        // Slow speed threshold input
        var lblSlowThreshold = new Label
        {
            Text = "Slow speed threshold (Mbps):",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = TextMuted,
            Location = new Point(48, currentY + 3),
            AutoSize = true
        };
        Controls.Add(lblSlowThreshold);

        _numSlowThreshold = new NumericUpDown
        {
            Location = new Point(240, currentY),
            Size = new Size(90, 24),
            Minimum = 0.5m,
            Maximum = 1000.0m,
            DecimalPlaces = 1,
            Increment = 1.0m,
            Value = (decimal)Math.Clamp(current.SlowSpeedThresholdMbps, 0.5, 1000.0),
            BackColor = CardBg,
            ForeColor = TextWhite
        };
        Controls.Add(_numSlowThreshold);
        currentY += 32;

        // Cooldown input
        var lblCooldown = new Label
        {
            Text = "Notification cooldown (seconds):",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = TextMuted,
            Location = new Point(48, currentY + 3),
            AutoSize = true
        };
        Controls.Add(lblCooldown);

        _numCooldown = new NumericUpDown
        {
            Location = new Point(240, currentY),
            Size = new Size(90, 24),
            Minimum = 10,
            Maximum = 3600,
            DecimalPlaces = 0,
            Increment = 10,
            Value = Math.Clamp(current.NotificationCooldownSeconds, 10, 3600),
            BackColor = CardBg,
            ForeColor = TextWhite
        };
        Controls.Add(_numCooldown);
        currentY += 30;

        var lblTrafficNote = new Label
        {
            Text = "Note: Live idle traffic does not indicate line speed. Slow speed alerts trigger after speed test measurements.",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            ForeColor = TextMuted,
            Location = new Point(48, currentY),
            Size = new Size(440, 30)
        };
        Controls.Add(lblTrafficNote);

        UpdateNotificationControlsState(current.NotificationsEnabled);

        // --- 4. BOTTOM BUTTONS ---
        int btnY = ClientSize.Height - 55;

        _btnSave = new Button
        {
            Text = "Save Preferences",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235), // Royal Blue
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Size = new Size(140, 36),
            Location = new Point(ClientSize.Width - 250, btnY)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += OnSaveClicked;
        Controls.Add(_btnSave);

        _btnCancel = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
            Size = new Size(80, 36),
            Location = new Point(ClientSize.Width - 100, btnY)
        };
        _btnCancel.FlatAppearance.BorderColor = CardBorder;
        _btnCancel.Click += (_, _) => Close();
        Controls.Add(_btnCancel);
    }

    public static void ShowOrActivate()
    {
        if (_activeInstance == null || _activeInstance.IsDisposed)
        {
            _activeInstance = new SettingsForm();
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

    private void OnNotificationsMasterToggled(object? sender, EventArgs e)
    {
        UpdateNotificationControlsState(_chkNotificationsEnabled.Checked);
    }

    private void UpdateNotificationControlsState(bool enabled)
    {
        _chkNotifyDisconnect.Enabled = enabled;
        _chkNotifyRestore.Enabled = enabled;
        _chkNotifySlowSpeed.Enabled = enabled;
        _numSlowThreshold.Enabled = enabled;
        _numCooldown.Enabled = enabled;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var settings = new UserSettings
        {
            StartWithWindows = _chkStartup.Checked,
            SpeedFormat = (SpeedFormatOption)_cmbSpeedFormat.SelectedIndex,
            UpdateIntervalMs = _cmbUpdateInterval.SelectedIndex switch
            {
                0 => 500,
                2 => 2000,
                _ => 1000
            },
            WidgetOpacity = _tbOpacity.Value / 100.0,
            NotificationsEnabled = _chkNotificationsEnabled.Checked,
            NotifyOnDisconnect = _chkNotifyDisconnect.Checked,
            NotifyOnRestore = _chkNotifyRestore.Checked,
            NotifyOnSlowSpeed = _chkNotifySlowSpeed.Checked,
            SlowSpeedThresholdMbps = (double)_numSlowThreshold.Value,
            NotificationCooldownSeconds = (int)_numCooldown.Value
        };

        SettingsService.Instance.Save(settings);
        Close();
    }
}
