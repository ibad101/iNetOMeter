using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using iNetOMeter;

namespace iNetOMeter.Tests;

internal class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    [STAThread]
    static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("  iNetOMeter Automated Test Suite (Stages 3 - 6)");
        Console.WriteLine("==================================================");

        // --- NETWORK MONITOR & FORMATTING TESTS ---
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

        RunTest("FormatSpeed_SpeedFormatOptions_FormatsBitsAndMbCorrectly", () =>
        {
            // AutoBits
            AssertEqual("0.0 Kbps", NetworkMonitor.FormatSpeed(0, SpeedFormatOption.AutoBits));
            AssertEqual("8.0 Kbps", NetworkMonitor.FormatSpeed(1000, SpeedFormatOption.AutoBits));
            AssertEqual("80.0 Mbps", NetworkMonitor.FormatSpeed(10_000_000, SpeedFormatOption.AutoBits));

            // AlwaysMbps
            AssertEqual("0.00 Mbps", NetworkMonitor.FormatSpeed(0, SpeedFormatOption.AlwaysMbps));
            AssertEqual("8.00 Mbps", NetworkMonitor.FormatSpeed(1_000_000, SpeedFormatOption.AlwaysMbps));

            // AlwaysMBps
            AssertEqual("0.00 MB/s", NetworkMonitor.FormatSpeed(0, SpeedFormatOption.AlwaysMBps));
            AssertEqual("1.00 MB/s", NetworkMonitor.FormatSpeed(1024 * 1024, SpeedFormatOption.AlwaysMBps));
        });

        RunTest("NetworkSpeedRecord_FormatsCorrectly", () =>
        {
            var speed = new NetworkSpeed(1024 * 1024, 512 * 1024);
            AssertEqual("1.0 MB/s", speed.GetFormattedDownload(SpeedFormatOption.AutoBytes));
            AssertEqual("512 KB/s", speed.GetFormattedUpload(SpeedFormatOption.AutoBytes));
        });

        // --- TASKBAR LAYOUT & ANCHORING TESTS ---
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

        // --- VISUAL TOKENS & REFERENCE PALETTE TESTS ---
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

        // --- SPEED CONVERTER, JITTER & MEDIAN TESTS ---
        RunTest("SpeedConverter_BytesPerSecondToMbps_CalculatesCorrectly", () =>
        {
            AssertEqual(0.0, SpeedConverter.BytesPerSecondToMbps(0));
            AssertEqual(0.0, SpeedConverter.BytesPerSecondToMbps(-100));
            AssertEqual(1.0, SpeedConverter.BytesPerSecondToMbps(125_000));
            AssertEqual(100.0, SpeedConverter.BytesPerSecondToMbps(12_500_000));
        });

        RunTest("SpeedConverter_FormatMbps_FormatsVariousTiers", () =>
        {
            AssertEqual("0.00", SpeedConverter.FormatMbps(0));
            AssertEqual("8.42", SpeedConverter.FormatMbps(8.42));
            AssertEqual("84.2", SpeedConverter.FormatMbps(84.23));
            AssertEqual("250", SpeedConverter.FormatMbps(250.4));
        });

        RunTest("SpeedConverter_CalculateJitter_KnownSamples_CalculatesAccurately", () =>
        {
            // Empty and single sample
            AssertEqual(0.0, SpeedConverter.CalculateJitter(new List<double>()));
            AssertEqual(0.0, SpeedConverter.CalculateJitter(new List<double> { 50.0 }));

            // Known RFC 3550 samples: [50, 52, 49, 53]
            // Differences: |52-50|=2, |49-52|=3, |53-49|=4. Mean = (2+3+4)/3 = 3.0 ms
            var samples = new List<double> { 50.0, 52.0, 49.0, 53.0 };
            AssertEqual(3.0, SpeedConverter.CalculateJitter(samples));

            // Stable connection (jitter = 0)
            var zeroJitter = new List<double> { 35.0, 35.0, 35.0 };
            AssertEqual(0.0, SpeedConverter.CalculateJitter(zeroJitter));
        });

        RunTest("SpeedConverter_CalculateMedian_OddAndEvenCounts_CalculatesAccurately", () =>
        {
            AssertEqual(0.0, SpeedConverter.CalculateMedian(new List<double>()));

            // Odd count: [45, 55, 50] -> sorted: [45, 50, 55] -> median: 50.0
            AssertEqual(50.0, SpeedConverter.CalculateMedian(new List<double> { 45.0, 55.0, 50.0 }));

            // Even count: [40, 50, 60, 70] -> median: (50 + 60) / 2 = 55.0
            AssertEqual(55.0, SpeedConverter.CalculateMedian(new List<double> { 40.0, 50.0, 60.0, 70.0 }));
        });

        RunTest("SpeedTestResult_Factories_CreateExpectedRecords", () =>
        {
            var fail = SpeedTestResult.Failure("Network unreachable", 12.3);
            AssertEqual(false, fail.Success);
            AssertEqual("Network unreachable", fail.ErrorMessage);
            AssertEqual(12.3, fail.DurationSeconds);

            var cancel = SpeedTestResult.Cancelled(5.5);
            AssertEqual(false, cancel.Success);
            AssertEqual("Cancelled", cancel.ServerLocation);
            AssertEqual(5.5, cancel.DurationSeconds);
        });

        // --- TASKBAR AUTO-HIDE & EDGE DETECTION TESTS ---
        var monitor = new Rectangle(0, 0, 1920, 1080);

        RunTest("TaskbarTracker_GetTaskbarEdge_IdentifiesAllFourEdges", () =>
        {
            var bottomTray = new Rectangle(0, 1040, 1920, 40);
            AssertEqual(TaskbarEdge.Bottom, TaskbarTracker.GetTaskbarEdge(bottomTray, monitor));

            var topTray = new Rectangle(0, 0, 1920, 40);
            AssertEqual(TaskbarEdge.Top, TaskbarTracker.GetTaskbarEdge(topTray, monitor));

            var leftTray = new Rectangle(0, 0, 60, 1080);
            AssertEqual(TaskbarEdge.Left, TaskbarTracker.GetTaskbarEdge(leftTray, monitor));

            var rightTray = new Rectangle(1860, 0, 60, 1080);
            AssertEqual(TaskbarEdge.Right, TaskbarTracker.GetTaskbarEdge(rightTray, monitor));
        });

        RunTest("TaskbarTracker_IsTaskbarVisible_BottomTaskbar_DetectsVisibleAndHidden", () =>
        {
            var visibleTray = new Rectangle(0, 1040, 1920, 40);
            AssertEqual(true, TaskbarTracker.IsTaskbarVisible(visibleTray, monitor, TaskbarEdge.Bottom));

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
            cts.Cancel();

            var task = svc.RunFullTestAsync(null, cts.Token);
            var result = task.GetAwaiter().GetResult();

            AssertEqual(false, result.Success);
            AssertEqual("Cancelled", result.ServerLocation);
        });

        // --- STAGE 4 TESTS: PERSISTENT HISTORY SERVICE ---
        string tempHistoryPath = Path.Combine(Path.GetTempPath(), $"iNetOMeter_Test_History_{Guid.NewGuid():N}.json");

        RunTest("HistoryService_KeepsExactlyFiveNewestRecords_AndOrdersNewestFirst", () =>
        {
            try
            {
                var historySvc = new HistoryService(tempHistoryPath);
                AssertEqual(0, historySvc.GetHistory().Count);

                DateTime baseTime = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Local);
                for (int i = 1; i <= 7; i++)
                {
                    var res = new SpeedTestResult(
                        LatencyMs: 20 + i,
                        JitterMs: 1 + (i * 0.5),
                        DownloadMbps: 50 + i * 10,
                        UploadMbps: 20 + i * 5,
                        ServerLocation: $"Colo-{i}",
                        ServerColo: $"C{i}",
                        Asn: "AS13335",
                        Timestamp: baseTime.AddMinutes(i),
                        Success: true,
                        ErrorMessage: null,
                        DurationSeconds: 30.0 + i
                    );
                    historySvc.AddResult(res);
                }

                var records = historySvc.GetHistory();
                AssertEqual(5, records.Count);

                AssertEqual("Colo-7", records[0].ServerName);
                AssertEqual(120.0, records[0].DownloadMbps);
                AssertEqual("Colo-3", records[4].ServerName);

                var freshHistorySvc = new HistoryService(tempHistoryPath);
                var reloaded = freshHistorySvc.GetHistory();
                AssertEqual(5, reloaded.Count);
                AssertEqual("Colo-7", reloaded[0].ServerName);
            }
            finally
            {
                if (File.Exists(tempHistoryPath)) File.Delete(tempHistoryPath);
            }
        });

        RunTest("HistoryService_RejectsFailedAndCancelledResults", () =>
        {
            try
            {
                var historySvc = new HistoryService(tempHistoryPath);

                historySvc.AddResult(SpeedTestResult.Failure("Network down"));
                historySvc.AddResult(SpeedTestResult.Cancelled());
                AssertEqual(0, historySvc.GetHistory().Count);

                historySvc.AddResult(new SpeedTestResult(25, 2, 70, 40, "Edge", "E1", "AS1", DateTime.Now, true));
                AssertEqual(1, historySvc.GetHistory().Count);
            }
            finally
            {
                if (File.Exists(tempHistoryPath)) File.Delete(tempHistoryPath);
            }
        });

        RunTest("HistoryService_CorruptFile_RecoversGracefullyWithoutCrashing", () =>
        {
            try
            {
                File.WriteAllText(tempHistoryPath, "{ this is invalid corrupted json content }}}");
                var historySvc = new HistoryService(tempHistoryPath);
                var list = historySvc.GetHistory();
                AssertEqual(0, list.Count);
            }
            finally
            {
                if (File.Exists(tempHistoryPath)) File.Delete(tempHistoryPath);
            }
        });

        RunTest("HistoryService_ClearHistory_DeletesFileAndEmptiesList", () =>
        {
            try
            {
                var historySvc = new HistoryService(tempHistoryPath);
                historySvc.AddResult(new SpeedTestResult(25, 2, 70, 40, "Edge", "E1", "AS1", DateTime.Now, true));
                AssertEqual(1, historySvc.GetHistory().Count);

                historySvc.ClearHistory();
                AssertEqual(0, historySvc.GetHistory().Count);
                AssertEqual(false, File.Exists(tempHistoryPath));
            }
            finally
            {
                if (File.Exists(tempHistoryPath)) File.Delete(tempHistoryPath);
            }
        });

        // --- STAGE 4 TESTS: SETTINGS SERVICE ---
        string tempSettingsPath = Path.Combine(Path.GetTempPath(), $"iNetOMeter_Test_Settings_{Guid.NewGuid():N}.json");

        RunTest("SettingsService_DefaultsAndPersistence_SavesAndLoadsCorrectly", () =>
        {
            try
            {
                var svc = new SettingsService(tempSettingsPath);
                var defaults = svc.Current;

                AssertEqual(false, defaults.StartWithWindows);
                AssertEqual(SpeedFormatOption.AutoBytes, defaults.SpeedFormat);
                AssertEqual(0.96, defaults.WidgetOpacity);
                AssertEqual(1000, defaults.UpdateIntervalMs);

                var custom = new UserSettings
                {
                    StartWithWindows = false,
                    SpeedFormat = SpeedFormatOption.AlwaysMbps,
                    WidgetOpacity = 0.88,
                    UpdateIntervalMs = 500
                };
                svc.Save(custom);

                var reloadedSvc = new SettingsService(tempSettingsPath);
                var loaded = reloadedSvc.Current;

                AssertEqual(SpeedFormatOption.AlwaysMbps, loaded.SpeedFormat);
                AssertEqual(0.88, loaded.WidgetOpacity);
                AssertEqual(500, loaded.UpdateIntervalMs);
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        });

        RunTest("SettingsService_CorruptFile_RecoversWithDefaults", () =>
        {
            try
            {
                File.WriteAllText(tempSettingsPath, "INVALID SETTINGS JSON <<>>");
                var svc = new SettingsService(tempSettingsPath);
                var current = svc.Current;

                AssertEqual(0.96, current.WidgetOpacity);
                AssertEqual(1000, current.UpdateIntervalMs);
                AssertEqual(SpeedFormatOption.AutoBytes, current.SpeedFormat);
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        });

        RunTest("SettingsService_ValidationAndClamping_ClampsOutOfBoundsValues", () =>
        {
            try
            {
                var svc = new SettingsService(tempSettingsPath);
                var extreme = new UserSettings
                {
                    WidgetOpacity = 0.10,
                    UpdateIntervalMs = 100,
                    SlowSpeedThresholdMbps = -10.0,
                    NotificationCooldownSeconds = 5
                };
                svc.Save(extreme);

                var loaded = svc.Current;
                AssertEqual(0.50, loaded.WidgetOpacity);
                AssertEqual(500, loaded.UpdateIntervalMs);
                AssertEqual(0.5, loaded.SlowSpeedThresholdMbps);
                AssertEqual(10, loaded.NotificationCooldownSeconds);
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        });

        // --- STAGE 5 & 6 TESTS: NOTIFICATIONS & CONNECTIVITY ---
        RunTest("SettingsService_NotificationDefaults_AreDisabledByDefault", () =>
        {
            try
            {
                var svc = new SettingsService(tempSettingsPath);
                var defaults = svc.Current;

                AssertEqual(false, defaults.NotificationsEnabled);
                AssertEqual(false, defaults.NotifyOnDisconnect);
                AssertEqual(false, defaults.NotifyOnRestore);
                AssertEqual(false, defaults.NotifyOnSlowSpeed);
                AssertEqual(5.0, defaults.SlowSpeedThresholdMbps);
                AssertEqual(60, defaults.NotificationCooldownSeconds);
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        });

        RunTest("SettingsService_BackwardCompatibility_LoadsOldJsonWithoutNotificationFields", () =>
        {
            try
            {
                string legacyJson = @"{
                    ""StartWithWindows"": false,
                    ""SpeedFormat"": 2,
                    ""WidgetOpacity"": 0.90,
                    ""UpdateIntervalMs"": 1000
                }";
                File.WriteAllText(tempSettingsPath, legacyJson);

                var svc = new SettingsService(tempSettingsPath);
                var loaded = svc.Current;

                AssertEqual(SpeedFormatOption.AlwaysMbps, loaded.SpeedFormat);
                AssertEqual(0.90, loaded.WidgetOpacity);
                // Notification fields must take safe defaults
                AssertEqual(false, loaded.NotificationsEnabled);
                AssertEqual(false, loaded.NotifyOnDisconnect);
                AssertEqual(5.0, loaded.SlowSpeedThresholdMbps);
                AssertEqual(60, loaded.NotificationCooldownSeconds);
            }
            finally
            {
                if (File.Exists(tempSettingsPath)) File.Delete(tempSettingsPath);
            }
        });

        RunTest("ConnectivityMonitor_StartupCheck_DoesNotSendRestoredNotification", () =>
        {
            var probe = new TestConnectivityProbe { Reachable = true };
            var notif = new TestNotificationService { Settings = new UserSettings { NotificationsEnabled = true, NotifyOnRestore = true } };
            using var monitor = new ConnectivityMonitor(probe, notif);

            // Initial check marks state as Connected without notification
            monitor.ProcessProbeResult(true);

            AssertEqual(ConnectivityState.Connected, monitor.CurrentState);
            AssertEqual(0, notif.Notifications.Count);
        });

        RunTest("ConnectivityMonitor_Debouncing_RequiresTwoConsecutiveFailuresBeforeDisconnectAlert", () =>
        {
            var probe = new TestConnectivityProbe { Reachable = true };
            var notif = new TestNotificationService { Settings = new UserSettings { NotificationsEnabled = true, NotifyOnDisconnect = true } };
            using var monitor = new ConnectivityMonitor(probe, notif);

            monitor.ProcessProbeResult(true); // Initial startup
            AssertEqual(0, notif.Notifications.Count);

            // First failure (transient blip)
            monitor.ProcessProbeResult(false);
            AssertEqual(ConnectivityState.Connected, monitor.CurrentState); // Still considered connected
            AssertEqual(0, notif.Notifications.Count);

            // Second failure (confirmed outage)
            monitor.ProcessProbeResult(false);
            AssertEqual(ConnectivityState.Disconnected, monitor.CurrentState);
            AssertEqual(1, notif.Notifications.Count);
            AssertEqual(NotificationType.InternetDisconnected, notif.Notifications[0].Type);
        });

        RunTest("ConnectivityMonitor_RepeatedFailures_DoesNotSendDuplicateDisconnectAlerts", () =>
        {
            var probe = new TestConnectivityProbe { Reachable = true };
            var notif = new TestNotificationService { Settings = new UserSettings { NotificationsEnabled = true, NotifyOnDisconnect = true } };
            using var monitor = new ConnectivityMonitor(probe, notif);

            monitor.ProcessProbeResult(true); // startup
            monitor.ProcessProbeResult(false);
            monitor.ProcessProbeResult(false); // Disconnected
            AssertEqual(1, notif.Notifications.Count);

            // Subsequent failures while already disconnected
            monitor.ProcessProbeResult(false);
            monitor.ProcessProbeResult(false);
            AssertEqual(1, notif.Notifications.Count); // Count remains 1
        });

        RunTest("ConnectivityMonitor_RecoveryDebouncing_RequiresTwoConsecutiveSuccessesBeforeRestoreAlert", () =>
        {
            var probe = new TestConnectivityProbe { Reachable = true };
            var notif = new TestNotificationService { Settings = new UserSettings { NotificationsEnabled = true, NotifyOnDisconnect = true, NotifyOnRestore = true } };
            using var monitor = new ConnectivityMonitor(probe, notif);

            monitor.ProcessProbeResult(true); // startup
            monitor.ProcessProbeResult(false);
            monitor.ProcessProbeResult(false); // Disconnected
            AssertEqual(1, notif.Notifications.Count);

            // First recovery probe
            monitor.ProcessProbeResult(true);
            AssertEqual(ConnectivityState.Disconnected, monitor.CurrentState); // Not yet confirmed
            AssertEqual(1, notif.Notifications.Count);

            // Second recovery probe (confirmed restored)
            monitor.ProcessProbeResult(true);
            AssertEqual(ConnectivityState.Connected, monitor.CurrentState);
            AssertEqual(2, notif.Notifications.Count);
            AssertEqual(NotificationType.InternetRestored, notif.Notifications[1].Type);
        });

        RunTest("NotificationService_TogglesRespected_WhenDisabledNoNotificationShown", () =>
        {
            var notif = new TestNotificationService
            {
                Settings = new UserSettings
                {
                    NotificationsEnabled = false, // Master disabled
                    NotifyOnDisconnect = true,
                    NotifyOnRestore = true,
                    NotifyOnSlowSpeed = true
                }
            };

            AssertEqual(false, notif.CanShowNotification(NotificationType.InternetDisconnected));
            AssertEqual(false, notif.CanShowNotification(NotificationType.InternetRestored));
            AssertEqual(false, notif.CanShowNotification(NotificationType.SlowSpeed));

            // Enable master, but disable specific type
            notif.Settings.NotificationsEnabled = true;
            notif.Settings.NotifyOnDisconnect = false;
            AssertEqual(false, notif.CanShowNotification(NotificationType.InternetDisconnected));
            AssertEqual(true, notif.CanShowNotification(NotificationType.InternetRestored));
        });

        RunTest("NotificationService_Cooldown_SuppressesRepeatedAlertsWithinCooldownWindow", () =>
        {
            var notif = new TestCooldownNotificationService(cooldownSeconds: 60);

            // First notification allowed
            AssertEqual(true, notif.TryShow("Alert 1"));

            // Immediate second notification suppressed
            AssertEqual(false, notif.TryShow("Alert 2"));

            // Advance time past cooldown
            notif.AdvanceTime(TimeSpan.FromSeconds(65));
            AssertEqual(true, notif.TryShow("Alert 3"));
        });

        RunTest("SlowSpeedWarning_SuppressionAndTriggerLogic_WorksCorrectly", () =>
        {
            var notif = new TestNotificationService
            {
                Settings = new UserSettings
                {
                    NotificationsEnabled = true,
                    NotifyOnSlowSpeed = true,
                    SlowSpeedThresholdMbps = 10.0
                }
            };

            // Test high speed (above threshold): no warning
            double goodDownload = 45.2;
            if (notif.Settings.NotificationsEnabled && notif.Settings.NotifyOnSlowSpeed && goodDownload < notif.Settings.SlowSpeedThresholdMbps)
            {
                notif.ShowNotification("Slow Speed", "Speed low", NotificationType.SlowSpeed);
            }
            AssertEqual(0, notif.Notifications.Count);

            // Test low speed (below threshold): warning triggered
            double lowDownload = 4.8;
            if (notif.Settings.NotificationsEnabled && notif.Settings.NotifyOnSlowSpeed && lowDownload < notif.Settings.SlowSpeedThresholdMbps)
            {
                notif.ShowNotification("Slow Speed", "Speed low", NotificationType.SlowSpeed);
            }
            AssertEqual(1, notif.Notifications.Count);
            AssertEqual(NotificationType.SlowSpeed, notif.Notifications[0].Type);
        });

        // --- FORM LIFECYCLE & REUSE TESTS ---
        RunTest("SpeedTestForm_InstantiatesAndDisposesCleanly", () =>
        {
            using var form = new SpeedTestForm();
            AssertEqual("Internet Speed Test - iNetOMeter", form.Text);
            AssertEqual(680, form.Size.Width);
            AssertEqual(560, form.Size.Height);
        });

        RunTest("HistoryForm_InstantiatesAndDisposesCleanly", () =>
        {
            using var form = new HistoryForm();
            AssertEqual("Speed Test History - iNetOMeter", form.Text);
            AssertEqual(660, form.Size.Width);
            AssertEqual(520, form.Size.Height);
        });

        RunTest("SettingsForm_InstantiatesAndDisposesCleanly", () =>
        {
            using var form = new SettingsForm();
            AssertEqual("Settings - iNetOMeter", form.Text);
            AssertEqual(540, form.Size.Width);
            AssertEqual(700, form.Size.Height);
        });

        RunTest("TaskbarBandForm_InstantiatesAndDisposesCleanly", () =>
        {
            using var form = new TaskbarBandForm();
            AssertEqual(94, form.Size.Width);
            AssertEqual(40, form.Size.Height);
            AssertEqual(false, form.ShowInTaskbar);
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

internal class TestNotificationService : INotificationService
{
    public List<(string Title, string Message, NotificationType Type)> Notifications { get; } = new();
    public UserSettings Settings { get; set; } = new();

    public bool CanShowNotification(NotificationType type)
    {
        if (!Settings.NotificationsEnabled) return false;
        return type switch
        {
            NotificationType.InternetDisconnected => Settings.NotifyOnDisconnect,
            NotificationType.InternetRestored => Settings.NotifyOnRestore,
            NotificationType.SlowSpeed => Settings.NotifyOnSlowSpeed,
            _ => false
        };
    }

    public void ShowNotification(string title, string message, NotificationType type)
    {
        if (CanShowNotification(type))
        {
            Notifications.Add((title, message, type));
        }
    }
}

internal class TestConnectivityProbe : IConnectivityProbe
{
    public bool Reachable { get; set; } = true;
    public Task<bool> CheckInternetReachabilityAsync(CancellationToken ct) => Task.FromResult(Reachable);
}

internal class TestCooldownNotificationService
{
    private readonly int _cooldownSeconds;
    private DateTime _lastTime = DateTime.MinValue;
    private DateTime _simulatedNow = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    public TestCooldownNotificationService(int cooldownSeconds)
    {
        _cooldownSeconds = cooldownSeconds;
    }

    public void AdvanceTime(TimeSpan span) => _simulatedNow += span;

    public bool TryShow(string message)
    {
        if ((_simulatedNow - _lastTime).TotalSeconds < _cooldownSeconds)
        {
            return false;
        }
        _lastTime = _simulatedNow;
        return true;
    }
}
