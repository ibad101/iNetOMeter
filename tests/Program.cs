using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using iNetOMeter;

namespace iNetOMeter.Tests;

internal class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("  iNetOMeter Automated Test Suite (Stage 3)");
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

        // Visual Tokens & Reference Design Tests
        RunTest("Visuals_DownloadAccent_Matches_FF525C", () =>
        {
            AssertEqual(255, (int)TaskbarBandForm.DownloadAccent.R);
            AssertEqual(82, (int)TaskbarBandForm.DownloadAccent.G);
            AssertEqual(92, (int)TaskbarBandForm.DownloadAccent.B);
        });

        RunTest("Visuals_UploadAccent_Matches_4CA8FF", () =>
        {
            AssertEqual(76, (int)TaskbarBandForm.UploadAccent.R);
            AssertEqual(168, (int)TaskbarBandForm.UploadAccent.G);
            AssertEqual(255, (int)TaskbarBandForm.UploadAccent.B);
        });

        RunTest("Visuals_ReferenceBadgesAndBackground_MatchPalette", () =>
        {
            AssertEqual(Color.FromArgb(69, 37, 43), TaskbarBandForm.DownloadBadgeBg);
            AssertEqual(Color.FromArgb(30, 53, 74), TaskbarBandForm.UploadBadgeBg);
            AssertEqual(Color.FromArgb(32, 36, 43), TaskbarBandForm.BackgroundCharcoal);
            AssertEqual(Color.White, TaskbarBandForm.SpeedTextColor);
        });

        RunTest("NativeMethods_DwmStructSizes_Valid", () =>
        {
            AssertEqual(16, Marshal.SizeOf<NativeMethods.AccentPolicy>());
            AssertEqual(IntPtr.Size == 8 ? 24 : 12, Marshal.SizeOf<NativeMethods.WindowCompositionAttributeData>());
        });

        // --- STAGE 3 TESTS: SPEED CONVERTER & MODELS ---
        RunTest("SpeedConverter_BytesPerSecondToMbps_CalculatesCorrectly", () =>
        {
            AssertEqual(0.0, SpeedConverter.BytesPerSecondToMbps(0));
            AssertEqual(0.0, SpeedConverter.BytesPerSecondToMbps(-100));
            // 125,000 bytes/sec * 8 = 1,000,000 bits/sec = 1.0 Mbps
            AssertEqual(1.0, SpeedConverter.BytesPerSecondToMbps(125_000));
            // 12,500,000 bytes/sec * 8 = 100.0 Mbps
            AssertEqual(100.0, SpeedConverter.BytesPerSecondToMbps(12_500_000));
        });

        RunTest("SpeedConverter_FormatMbps_FormatsVariousTiers", () =>
        {
            AssertEqual("0.00", SpeedConverter.FormatMbps(0));
            AssertEqual("8.42", SpeedConverter.FormatMbps(8.42));
            AssertEqual("84.2", SpeedConverter.FormatMbps(84.23));
            AssertEqual("250", SpeedConverter.FormatMbps(250.4));
        });

        RunTest("SpeedTestResult_Factories_CreateExpectedRecords", () =>
        {
            var fail = SpeedTestResult.Failure("Network unreachable");
            AssertEqual(false, fail.Success);
            AssertEqual("Network unreachable", fail.ErrorMessage);

            var cancel = SpeedTestResult.Cancelled();
            AssertEqual(false, cancel.Success);
            AssertEqual("Cancelled", cancel.ServerLocation);
        });

        // --- STAGE 3 TESTS: TASKBAR AUTO-HIDE & EDGE DETECTION ---
        var monitor = new Rectangle(0, 0, 1920, 1080);

        RunTest("TaskbarTracker_GetTaskbarEdge_IdentifiesAllFourEdges", () =>
        {
            // Bottom horizontal
            var bottomTray = new Rectangle(0, 1040, 1920, 40);
            AssertEqual(TaskbarEdge.Bottom, TaskbarTracker.GetTaskbarEdge(bottomTray, monitor));

            // Top horizontal
            var topTray = new Rectangle(0, 0, 1920, 40);
            AssertEqual(TaskbarEdge.Top, TaskbarTracker.GetTaskbarEdge(topTray, monitor));

            // Left vertical
            var leftTray = new Rectangle(0, 0, 60, 1080);
            AssertEqual(TaskbarEdge.Left, TaskbarTracker.GetTaskbarEdge(leftTray, monitor));

            // Right vertical
            var rightTray = new Rectangle(1860, 0, 60, 1080);
            AssertEqual(TaskbarEdge.Right, TaskbarTracker.GetTaskbarEdge(rightTray, monitor));
        });

        RunTest("TaskbarTracker_IsTaskbarVisible_BottomTaskbar_DetectsVisibleAndHidden", () =>
        {
            var visibleTray = new Rectangle(0, 1040, 1920, 40);
            AssertEqual(true, TaskbarTracker.IsTaskbarVisible(visibleTray, monitor, TaskbarEdge.Bottom));

            // Auto-hidden: only 2px on screen
            var hiddenTray = new Rectangle(0, 1078, 1920, 40);
            AssertEqual(false, TaskbarTracker.IsTaskbarVisible(hiddenTray, monitor, TaskbarEdge.Bottom));
        });

        RunTest("TaskbarTracker_IsTaskbarVisible_TopTaskbar_DetectsVisibleAndHidden", () =>
        {
            var visibleTray = new Rectangle(0, 0, 1920, 40);
            AssertEqual(true, TaskbarTracker.IsTaskbarVisible(visibleTray, monitor, TaskbarEdge.Top));

            var hiddenTray = new Rectangle(0, -38, 1920, 40);
            AssertEqual(false, TaskbarTracker.IsTaskbarVisible(hiddenTray, monitor, TaskbarEdge.Top));
        });

        RunTest("TaskbarTracker_IsTaskbarVisible_LeftTaskbar_DetectsVisibleAndHidden", () =>
        {
            var visibleTray = new Rectangle(0, 0, 60, 1080);
            AssertEqual(true, TaskbarTracker.IsTaskbarVisible(visibleTray, monitor, TaskbarEdge.Left));

            var hiddenTray = new Rectangle(-58, 0, 60, 1080);
            AssertEqual(false, TaskbarTracker.IsTaskbarVisible(hiddenTray, monitor, TaskbarEdge.Left));
        });

        RunTest("TaskbarTracker_IsTaskbarVisible_RightTaskbar_DetectsVisibleAndHidden", () =>
        {
            var visibleTray = new Rectangle(1860, 0, 60, 1080);
            AssertEqual(true, TaskbarTracker.IsTaskbarVisible(visibleTray, monitor, TaskbarEdge.Right));

            var hiddenTray = new Rectangle(1918, 0, 60, 1080);
            AssertEqual(false, TaskbarTracker.IsTaskbarVisible(hiddenTray, monitor, TaskbarEdge.Right));
        });

        RunTest("SpeedTestService_Cancellation_ReturnsCancelledResult", () =>
        {
            using var svc = new SpeedTestService();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var task = svc.RunFullTestAsync(null, cts.Token);
            var result = task.GetAwaiter().GetResult();

            AssertEqual(false, result.Success);
            AssertEqual("Cancelled", result.ServerLocation);
        });

        RunTest("SpeedTestForm_InstantiatesAndDisposesCleanly", () =>
        {
            using var form = new SpeedTestForm();
            AssertEqual("Internet Speed Test - iNetOMeter", form.Text);
            AssertEqual(680, form.Size.Width);
            AssertEqual(540, form.Size.Height);
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
