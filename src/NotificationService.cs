using System;
using System.Drawing;
using System.Windows.Forms;

namespace iNetOMeter;

public class WindowsNotificationService : INotificationService, IDisposable
{
    private static WindowsNotificationService? _instance;
    private static readonly object _syncLock = new();

    public static WindowsNotificationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new WindowsNotificationService();
                }
            }
            return _instance;
        }
    }

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private bool _disposed;

    public WindowsNotificationService()
    {
        // Generate crisp 16x16 dark navy icon for notification tray integration
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(11, 17, 30));
            using var cyanPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f);
            g.DrawArc(cyanPen, 2, 2, 11, 11, 135, 270);
            using var needlePen = new Pen(Color.FromArgb(255, 82, 92), 1.5f);
            g.DrawLine(needlePen, 8, 8, 12, 5);
        }

        IntPtr hIcon = bmp.GetHicon();
        _icon = Icon.FromHandle(hIcon);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "iNetOMeter",
            Visible = true
        };

        _notifyIcon.BalloonTipClicked += (_, _) => SpeedTestForm.ShowOrActivate();
    }

    public bool CanShowNotification(NotificationType type)
    {
        var settings = SettingsService.Instance.Current;
        if (!settings.NotificationsEnabled) return false;

        bool isTypeEnabled = type switch
        {
            NotificationType.InternetDisconnected => settings.NotifyOnDisconnect,
            NotificationType.InternetRestored => settings.NotifyOnRestore,
            NotificationType.SlowSpeed => settings.NotifyOnSlowSpeed,
            _ => false
        };

        if (!isTypeEnabled) return false;

        // Check rate limiting / cooldown
        double elapsed = (DateTime.UtcNow - _lastNotificationTime).TotalSeconds;
        if (elapsed < settings.NotificationCooldownSeconds)
        {
            return false;
        }

        return true;
    }

    public void ShowNotification(string title, string message, NotificationType type)
    {
        if (!CanShowNotification(type)) return;

        _lastNotificationTime = DateTime.UtcNow;

        ToolTipIcon iconType = type switch
        {
            NotificationType.InternetDisconnected => ToolTipIcon.Error,
            NotificationType.SlowSpeed => ToolTipIcon.Warning,
            _ => ToolTipIcon.Info
        };

        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, iconType);
        }
        catch
        {
            // Fail safely if shell notifications are unavailable
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _icon.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
