using System;
using System.Drawing;
using System.Runtime.InteropServices;
using iNetOMeter;

namespace iNetOMeter.Tests;

internal class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("  iNetOMeter Automated Test Suite (Stage 1.1)");
        Console.WriteLine("==================================================");

        // Network Monitor FormatSpeed Tests
        RunTest("FormatSpeed_ZeroBytes_ReturnsDefault", () =>
        {
            AssertEqual("0.0 KB/s", NetworkMonitor.FormatSpeed(0));
            AssertEqual("0.0 KB/s", NetworkMonitor.FormatSpeed(-50));
        });

        RunTest("FormatSpeed_BytesTier_FormatsCorrectly", () =>
        {
            AssertEqual("500 B/s", NetworkMonitor.FormatSpeed(500));
            AssertEqual("1023 B/s", NetworkMonitor.FormatSpeed(1023));
        });

        RunTest("FormatSpeed_KilobytesTier_FormatsCorrectly", () =>
        {
            AssertEqual("1.0 KB/s", NetworkMonitor.FormatSpeed(1024));
            AssertEqual("12.5 KB/s", NetworkMonitor.FormatSpeed(12.5 * 1024));
            AssertEqual("150 KB/s", NetworkMonitor.FormatSpeed(150 * 1024));
        });

        RunTest("FormatSpeed_MegabytesTier_FormatsCorrectly", () =>
        {
            AssertEqual("1.0 MB/s", NetworkMonitor.FormatSpeed(1024 * 1024));
            AssertEqual("25.4 MB/s", NetworkMonitor.FormatSpeed(25.4 * 1024 * 1024));
            AssertEqual("120 MB/s", NetworkMonitor.FormatSpeed(120 * 1024 * 1024));
        });

        RunTest("FormatSpeed_GigabytesTier_FormatsCorrectly", () =>
        {
            AssertEqual("1.50 GB/s", NetworkMonitor.FormatSpeed(1.5 * 1024 * 1024 * 1024));
        });

        RunTest("NetworkSpeedRecord_FormatsCorrectly", () =>
        {
            var speed = new NetworkSpeed(1024 * 1024, 512 * 1024);
            AssertEqual("1.0 MB/s", speed.DownloadFormatted);
            AssertEqual("512 KB/s", speed.UploadFormatted);
        });

        // Taskbar Layout Calculation Tests
        RunTest("CalculateAnchorBounds_BottomTaskbar_AnchorsLeftOfNotifyWnd", () =>
        {
            var trayRect = new Rectangle(0, 1040, 1920, 40);
            var notifyRect = new Rectangle(1600, 1040, 320, 40);
            int desiredWidth = 84;
            int desiredHeight = 40;

            var bounds = TaskbarTracker.CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);

            AssertEqual(1600 - desiredWidth, bounds.X);
            AssertEqual(1040, bounds.Y);
            AssertEqual(desiredWidth, bounds.Width);
            AssertEqual(40, bounds.Height);
        });

        RunTest("CalculateAnchorBounds_TopTaskbar_AnchorsLeftOfNotifyWnd", () =>
        {
            var trayRect = new Rectangle(0, 0, 1920, 40);
            var notifyRect = new Rectangle(1550, 0, 370, 40);
            int desiredWidth = 84;
            int desiredHeight = 40;

            var bounds = TaskbarTracker.CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);

            AssertEqual(1550 - desiredWidth, bounds.X);
            AssertEqual(0, bounds.Y);
            AssertEqual(desiredWidth, bounds.Width);
            AssertEqual(40, bounds.Height);
        });

        RunTest("CalculateAnchorBounds_EmptyNotifyWnd_FallsBackToTrayRightEdge", () =>
        {
            var trayRect = new Rectangle(0, 1040, 1920, 40);
            var notifyRect = Rectangle.Empty;
            int desiredWidth = 84;
            int desiredHeight = 40;

            var bounds = TaskbarTracker.CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);

            AssertEqual(1920 - desiredWidth - 10, bounds.X);
            AssertEqual(1040, bounds.Y);
            AssertEqual(desiredWidth, bounds.Width);
            AssertEqual(40, bounds.Height);
        });

        RunTest("CalculateAnchorBounds_VerticalTaskbar_AnchorsAboveNotifyWnd", () =>
        {
            var trayRect = new Rectangle(0, 0, 60, 1080);
            var notifyRect = new Rectangle(0, 900, 60, 180);
            int desiredWidth = 60;
            int desiredHeight = 36;

            var bounds = TaskbarTracker.CalculateAnchorBounds(trayRect, notifyRect, desiredWidth, desiredHeight);

            AssertEqual(0, bounds.X);
            AssertEqual(900 - desiredHeight, bounds.Y);
            AssertEqual(60, bounds.Width);
            AssertEqual(desiredHeight, bounds.Height);
        });

        // Visual Improvements & Accent Color Tests (Stage 1.1)
        RunTest("Visuals_DownloadColor_MatchesRequirement_FF525C", () =>
        {
            AssertEqual(255, (int)TaskbarBandForm.DownloadColor.R);
            AssertEqual(82, (int)TaskbarBandForm.DownloadColor.G);
            AssertEqual(92, (int)TaskbarBandForm.DownloadColor.B);
        });

        RunTest("Visuals_UploadColor_MatchesRequirement_4CA8FF", () =>
        {
            AssertEqual(76, (int)TaskbarBandForm.UploadColor.R);
            AssertEqual(168, (int)TaskbarBandForm.UploadColor.G);
            AssertEqual(255, (int)TaskbarBandForm.UploadColor.B);
        });

        RunTest("NativeMethods_DwmStructSizes_Valid", () =>
        {
            AssertEqual(16, Marshal.SizeOf<NativeMethods.AccentPolicy>());
            AssertEqual(IntPtr.Size == 8 ? 24 : 12, Marshal.SizeOf<NativeMethods.WindowCompositionAttributeData>());
        });

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Total Tests: {_passedCount + _failedCount} | Passed: {_passedCount} | Failed: {_failedCount}");
        Console.WriteLine("==================================================");

        return _failedCount == 0 ? 0 : 1;
    }

    private static void RunTest(string testName, Action testAction)
    {
        try
        {
            testAction();
            Console.WriteLine($"[PASS] {testName}");
            _passedCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {testName} - {ex.Message}");
            _failedCount++;
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            throw new Exception($"Expected: '{expected}', but was: '{actual}'");
        }
    }
}
