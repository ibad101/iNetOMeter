using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace iNetOMeter;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}

public class TaskbarTracker
{
    private IntPtr _hTray = IntPtr.Zero;
    private IntPtr _hNotify = IntPtr.Zero;
    private IntPtr _defaultDesktopHandle = IntPtr.Zero;

    public TaskbarTracker()
    {
        try
        {
            _defaultDesktopHandle = NativeMethods.OpenDesktop("default", 0, false, 0x01FF);
            if (_defaultDesktopHandle != IntPtr.Zero)
            {
                NativeMethods.SetThreadDesktop(_defaultDesktopHandle);
            }
        }
        catch
        {
            // Ignore if running directly in the default desktop
        }
    }

    public bool TryGetTaskbarWindows(out IntPtr hTray, out IntPtr hNotify)
    {
        hTray = NativeMethods.FindWindow("Shell_TrayWnd", IntPtr.Zero);
        if (hTray == IntPtr.Zero)
        {
            if (_defaultDesktopHandle != IntPtr.Zero)
            {
                NativeMethods.SetThreadDesktop(_defaultDesktopHandle);
                hTray = NativeMethods.FindWindow("Shell_TrayWnd", IntPtr.Zero);
            }
        }

        hNotify = IntPtr.Zero;
        if (hTray != IntPtr.Zero)
        {
            hNotify = NativeMethods.FindWindowEx(hTray, IntPtr.Zero, "TrayNotifyWnd", IntPtr.Zero);
        }

        _hTray = hTray;
        _hNotify = hNotify;
        return hTray != IntPtr.Zero;
    }

    public bool TryGetTaskbarState(out Rectangle trayRect, out Rectangle notifyRect, out Rectangle monitorRect, out TaskbarEdge edge, out bool isVisible)
    {
        trayRect = Rectangle.Empty;
        notifyRect = Rectangle.Empty;
        monitorRect = Rectangle.Empty;
        edge = TaskbarEdge.Bottom;
        isVisible = false;

        if (!TryGetTaskbarWindows(out IntPtr hTray, out IntPtr hNotify))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hTray, out NativeMethods.RECT trayRectRaw))
        {
            return false;
        }

        trayRect = trayRectRaw.ToRectangle();

        if (hNotify != IntPtr.Zero && NativeMethods.GetWindowRect(hNotify, out NativeMethods.RECT notifyRectRaw))
        {
            notifyRect = notifyRectRaw.ToRectangle();
        }

        // Get monitor bounds for the monitor where the taskbar is located
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hTray, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero)
        {
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(mi);
            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                monitorRect = mi.rcMonitor.ToRectangle();
            }
        }

        if (monitorRect.IsEmpty)
        {
            monitorRect = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }

        edge = GetTaskbarEdge(trayRect, monitorRect);
        isVisible = IsTaskbarVisible(trayRect, monitorRect, edge);
        return true;
    }

    public Rectangle? GetTargetBounds(int desiredWidth = 94, int desiredHeight = 40, bool requireVisible = false)
    {
        if (!TryGetTaskbarState(out Rectangle trayRect, out Rectangle notifyRect, out Rectangle monitorRect, out TaskbarEdge edge, out bool isVisible))
        {
            return null;
        }

        if (requireVisible && !isVisible)
        {
            return null;
        }

        return CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);
    }

    public static TaskbarEdge GetTaskbarEdge(Rectangle trayRect, Rectangle monitorRect)
    {
        if (trayRect.Width >= trayRect.Height)
        {
            int midY = monitorRect.Top + monitorRect.Height / 2;
            return trayRect.Top >= midY ? TaskbarEdge.Bottom : TaskbarEdge.Top;
        }
        else
        {
            int midX = monitorRect.Left + monitorRect.Width / 2;
            return trayRect.Left >= midX ? TaskbarEdge.Right : TaskbarEdge.Left;
        }
    }

    public static bool IsTaskbarVisible(Rectangle trayRect, Rectangle monitorRect, TaskbarEdge edge)
    {
        // When Windows auto-hides the taskbar, only a 2px-3px sliver remains on screen.
        // If fewer than 10px of the taskbar are within the monitor boundary, it is hidden.
        const int threshold = 10;
        return edge switch
        {
            TaskbarEdge.Bottom => trayRect.Top < monitorRect.Bottom - threshold,
            TaskbarEdge.Top => trayRect.Bottom > monitorRect.Top + threshold,
            TaskbarEdge.Left => trayRect.Right > monitorRect.Left + threshold,
            TaskbarEdge.Right => trayRect.Left < monitorRect.Right - threshold,
            _ => true
        };
    }

    public static Rectangle CalculateAnchorBounds(Rectangle trayRect, Rectangle notifyRect, int desiredWidth, int desiredHeight)
    {
        bool isHorizontal = trayRect.Width >= trayRect.Height;

        if (isHorizontal)
        {
            int height = trayRect.Height;
            int y = trayRect.Top;

            int x;
            if (!notifyRect.IsEmpty && notifyRect.Left > trayRect.Left && notifyRect.Left < trayRect.Right)
            {
                // Anchor directly to the left of the system notification area
                x = notifyRect.Left - desiredWidth;
            }
            else
            {
                // Fallback: anchor near the right side of the taskbar
                x = trayRect.Right - desiredWidth - 10;
            }

            return new Rectangle(x, y, desiredWidth, height);
        }
        else
        {
            // Vertical taskbar (docked to left or right screen edge)
            int width = trayRect.Width;
            int x = trayRect.Left;

            int y;
            if (!notifyRect.IsEmpty && notifyRect.Top > trayRect.Top && notifyRect.Top < trayRect.Bottom)
            {
                // Anchor directly above the notification area
                y = notifyRect.Top - desiredHeight;
            }
            else
            {
                y = trayRect.Bottom - desiredHeight - 10;
            }

            return new Rectangle(x, y, width, desiredHeight);
        }
    }
}
