using System;
using System.Drawing;

namespace iNetOMeter;

public class TaskbarTracker
{
    private IntPtr _hTray = IntPtr.Zero;
    private IntPtr _hNotify = IntPtr.Zero;
    private IntPtr _defaultDesktopHandle = IntPtr.Zero;

    public TaskbarTracker()
    {
        // Try opening the default desktop in case running from another desktop session
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
            // If thread desktop switched, try once more
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

    public Rectangle? GetTargetBounds(int desiredWidth = 84, int desiredHeight = 40)
    {
        if (!TryGetTaskbarWindows(out IntPtr hTray, out IntPtr hNotify))
        {
            return null;
        }

        if (!NativeMethods.GetWindowRect(hTray, out NativeMethods.RECT trayRectRaw))
        {
            return null;
        }

        Rectangle trayRect = trayRectRaw.ToRectangle();
        Rectangle notifyRect = Rectangle.Empty;

        if (hNotify != IntPtr.Zero && NativeMethods.GetWindowRect(hNotify, out NativeMethods.RECT notifyRectRaw))
        {
            notifyRect = notifyRectRaw.ToRectangle();
        }

        return CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);
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
