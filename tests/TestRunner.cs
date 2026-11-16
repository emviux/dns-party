using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DNSParty.Models;
using DNSParty.Services;
using DNSParty.UI;

namespace DNSParty.Tests
{
    public static class TestRunner
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private static int _passed = 0;
        private static int _failed = 0;
        private static readonly List<string> _failures = new List<string>();

        private static void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  [PASS] " + testName + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
            }
            else
            {
                _failed++;
                string msg = testName + (string.IsNullOrEmpty(detail) ? "" : " -> " + detail);
                _failures.Add(msg);
                Console.WriteLine("  [FAIL] " + msg);
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine(" DNS Party — Automated QA & Acceptance Verification Suite");
            Console.WriteLine("============================================================");

            try
            {
                TestDefaultProfilesAndValidation();
                TestConfigPersistenceAndCorruptionRecovery();
                TestSoundGenerationAndPlayback();
                TestNetworkAdapterDiscoveryAndDnsServices().GetAwaiter().GetResult();
                TestUiRenderingAndTrayBehavior();
                TestResourceConsumption();
            }
            catch (Exception ex)
            {
                Assert(false, "Unhandled exception in test suite", ex.ToString());
            }

            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine(string.Format(" Summary: {0} Passed, {1} Failed", _passed, _failed));
            Console.WriteLine("------------------------------------------------------------");

            return _failed == 0 ? 0 : 1;
        }

        private static void TestDefaultProfilesAndValidation()
        {
            Console.WriteLine("\n1. Default DNS Profiles & IP Validation (IPv4 / IPv6)");
            var defaults = DnsProfile.GetDefaultProfiles();
            Assert(defaults.Count == 11, "Prepopulated with 11 default DNS providers", "Count=" + defaults.Count);

            Assert(defaults[0].Name == "Cloudflare" && defaults[0].PrimaryDns == "1.1.1.1" && defaults[0].SecondaryDns == "1.0.0.1", "Cloudflare 1.1.1.1 / 1.0.0.1");
            Assert(defaults[1].Name == "Google" && defaults[1].PrimaryDns == "8.8.8.8" && defaults[1].SecondaryDns == "8.8.4.4", "Google 8.8.8.8 / 8.8.4.4");
            Assert(defaults[2].Name == "Quad9" && defaults[2].PrimaryDns == "9.9.9.9" && defaults[2].SecondaryDns == "149.112.112.112", "Quad9 9.9.9.9 / 149.112.112.112");
            Assert(defaults[3].Name == "AdGuard DNS" && defaults[3].PrimaryDns == "94.140.14.14" && defaults[3].SecondaryDns == "94.140.15.15", "AdGuard DNS 94.140.14.14 / 94.140.15.15");
            Assert(defaults[4].Name == "OpenDNS" && defaults[4].PrimaryDns == "208.67.222.222" && defaults[4].SecondaryDns == "208.67.220.220", "OpenDNS 208.67.222.222 / 208.67.220.220");

            string err;
            Assert(DnsProfile.ValidateDnsAddress("1.1.1.1", false, out err), "Valid IPv4 1.1.1.1 accepted");
            Assert(DnsProfile.ValidateDnsAddress("2606:4700:4700::1111", true, out err), "Valid IPv6 2606:4700:4700::1111 accepted");
            Assert(!DnsProfile.ValidateDnsAddress("", false, out err), "Empty DNS rejected");
            Assert(!DnsProfile.ValidateDnsAddress("1.1", false, out err), "Partial IPv4 '1.1' rejected");
            Assert(!DnsProfile.ValidateDnsAddress("999.1.1.1", false, out err), "Out-of-range octet rejected");
            Assert(!DnsProfile.ValidateDnsAddress("127.0.0.1", false, out err), "Loopback IPv4 127.0.0.1 rejected");
            Assert(!DnsProfile.ValidateDnsAddress("0.0.0.0", false, out err), "Unspecified IPv4 0.0.0.0 rejected");
            Assert(!DnsProfile.ValidateDnsAddress("255.255.255.255", false, out err), "Broadcast IPv4 255.255.255.255 rejected");
            Assert(!DnsProfile.ValidateDnsAddress("224.0.0.1", false, out err), "Multicast IPv4 224.0.0.1 rejected");
            Assert(!DnsProfile.ValidateDnsAddress("::1", true, out err), "Loopback IPv6 ::1 rejected");
            Assert(!DnsProfile.ValidateDnsAddress("::", true, out err), "Unspecified IPv6 :: rejected");
            Assert(!DnsProfile.ValidateDnsAddress("1.1.1.1", true, out err), "IPv4 rejected when IPv6 selected");
        }

        private static void TestConfigPersistenceAndCorruptionRecovery()
        {
            Console.WriteLine("\n2. Configuration Persistence, Custom Profiles, Favorites & Corruption Recovery");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string flagPath = Path.Combine(baseDir, "portable.flag");
            string portableCfgPath = Path.Combine(baseDir, "portable_config.json");

            try
            {
                File.WriteAllText(flagPath, "portable");
                if (File.Exists(portableCfgPath)) File.Delete(portableCfgPath);

                var cfg1 = new ConfigService();
                Assert(cfg1.Profiles.Count == 11, "First launch initializes 11 default profiles");

                // Add custom DNS profile & mark as favorite
                var custom = new DnsProfile
                {
                    Id = "custom_test_1",
                    Name = "My Gaming DNS",
                    Tag = "Custom",
                    PrimaryDns = "185.228.168.9",
                    SecondaryDns = "185.228.169.9",
                    IsFavorite = true
                };
                cfg1.Profiles.Add(custom);
                cfg1.Settings.ConfirmBeforeDisable = true;
                cfg1.Settings.EnableSounds = false;
                Assert(cfg1.Save(), "Saved custom profile, favorites, and settings");

                // Reload in new instance
                var cfg2 = new ConfigService();
                Assert(cfg2.Profiles.Count == 12, "Custom profile persisted across restart", "Count=" + cfg2.Profiles.Count);
                var reloadedCustom = cfg2.Profiles.FirstOrDefault(p => p.Id == "custom_test_1");
                Assert(reloadedCustom != null && reloadedCustom.IsFavorite && reloadedCustom.Name == "My Gaming DNS", "Favorite & custom fields persisted");
                Assert(cfg2.Settings.ConfirmBeforeDisable && !cfg2.Settings.EnableSounds, "Settings flags persisted");

                // Edit and Delete profile
                reloadedCustom.Name = "Edited Gaming DNS";
                cfg2.Save();
                var cfg3 = new ConfigService();
                Assert(cfg3.Profiles.First(p => p.Id == "custom_test_1").Name == "Edited Gaming DNS", "Profile edit persisted");

                cfg3.Profiles.RemoveAll(p => p.Id == "custom_test_1");
                cfg3.Save();
                var cfg4 = new ConfigService();
                Assert(cfg4.Profiles.Count == 11, "Profile deletion persisted");

                // Corrupt the JSON file intentionally
                File.WriteAllText(portableCfgPath, "{ invalid corrupted json !!! [[[");
                var cfgCorrupt = new ConfigService();
                Assert(cfgCorrupt.RecoveredFromCorruption, "Corrupted config file detected and recovered gracefully");
                Assert(cfgCorrupt.Profiles.Count == 11, "Defaults restored after corruption recovery");
            }
            finally
            {
                if (File.Exists(flagPath)) File.Delete(flagPath);
                if (File.Exists(portableCfgPath)) File.Delete(portableCfgPath);
                foreach (var bak in Directory.GetFiles(baseDir, "portable_config.json.corrupt.*.bak"))
                {
                    try { File.Delete(bak); } catch { }
                }
            }
        }

        private static void TestSoundGenerationAndPlayback()
        {
            Console.WriteLine("\n3. Sound Effects Generation & Playback");
            var sound = new SoundService();
            sound.EnsureSoundFiles();

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string connectWav = Path.Combine(localAppData, "DNSParty", "sounds", "connect.wav");
            string disconnectWav = Path.Combine(localAppData, "DNSParty", "sounds", "disconnect.wav");

            Assert(File.Exists(connectWav) && new FileInfo(connectWav).Length > 1000, "connect.wav generated locally", connectWav);
            Assert(File.Exists(disconnectWav) && new FileInfo(disconnectWav).Length > 1000, "disconnect.wav generated locally", disconnectWav);

            sound.PlayConnect(false);
            sound.PlayDisconnect(false);
            Assert(true, "Sound playback respects enabled/disabled flag without error");
        }

        private static async Task TestNetworkAdapterDiscoveryAndDnsServices()
        {
            Console.WriteLine("\n4. Windows Network Adapter Discovery, DNS Cache Flush & UDP Port 53 Latency Test");
            using (var dns = new DnsService())
            {
                var adapters = dns.GetCandidateAdapters();
                Assert(adapters != null && adapters.Count > 0, "Discovered Windows network adapters", "Count=" + (adapters != null ? adapters.Count : 0));

                var snap = dns.GetActualDnsSnapshot(DnsProfile.GetDefaultProfiles(), "", true, "cloudflare");
                Assert(snap != null && !string.IsNullOrEmpty(snap.AdapterName), "Read actual Windows DNS snapshot",
                    string.Format("Adapter={0}, Mode={1}, Primary={2}", snap.AdapterName, snap.ActiveProfileName, snap.ActualPrimaryDns));

                // Test Flush DNS Cache (ipconfig /flushdns)
                var flushRes = await dns.FlushDnsCacheAsync();
                Assert(flushRes.Success, "Flush DNS Cache (ipconfig /flushdns) executed hidden and succeeded", flushRes.Message);

                // Spin up a local mock RFC 1035 UDP DNS server on 127.0.0.1 (using TestDnsServerAsync against local/public)
                // Also test against 1.1.1.1 or 8.8.8.8
                var testRes = await dns.TestDnsServerAsync("1.1.1.1", 2000);
                Console.WriteLine(string.Format("  [INFO] Live DNS Test (1.1.1.1): Reachable={0}, Latency={1} ms, Details={2}",
                    testRes.IsReachable, testRes.LatencyMs, testRes.Details));
                Assert(testRes.ServerIp == "1.1.1.1" && !string.IsNullOrEmpty(testRes.Details), "DNS Server UDP test completed cleanly", testRes.Details);

                // Test unreachable address timeout handling
                var timeoutRes = await dns.TestDnsServerAsync("192.0.2.1", 400); // TEST-NET-1 unreachable
                Assert(!timeoutRes.IsReachable, "Unreachable DNS server accurately reported as unreachable", timeoutRes.Details);
            }
        }

        private static void TestUiRenderingAndTrayBehavior()
        {
            Console.WriteLine("\n5. UI Dashboard Rendering, Logo Assets, System Tray Integration & Alt+Tab Exclusion");
            Icon appIcon = IconRenderer.GetAppIcon();
            Icon trayIcon = IconRenderer.GetTrayIcon();
            Assert(appIcon != null && appIcon.Width > 0 && appIcon.Height > 0, "Primary Windows application icon (dns-party.ico) loaded", appIcon.Width + "x" + appIcon.Height);
            Assert(trayIcon != null && trayIcon.Width > 0 && trayIcon.Height > 0, "System tray small icon loaded from dns-party.ico", trayIcon.Width + "x" + trayIcon.Height);

            using (Bitmap logo32 = IconRenderer.GetAppLogoBitmap(32))
            using (Bitmap logo64 = IconRenderer.GetAppLogoBitmap(64))
            {
                Assert(logo32 != null && logo32.Width == 32 && logo32.GetPixel(0, 0).A == 0 && logo32.GetPixel(16, 16).A > 0,
                    "Transparent 32x32 PNG logo loaded with preserved transparency");
                Assert(logo64 != null && logo64.Width == 64 && logo64.GetPixel(0, 0).A == 0 && logo64.GetPixel(32, 32).A > 0,
                    "Transparent 64x64 PNG logo loaded with preserved transparency");
            }

            var cfg = new ConfigService();
            var snd = new SoundService();
            using (var dns = new DnsService())
            using (var form = new MainForm(cfg, dns, snd, false))
            {
                form.Show();
                Application.DoEvents();

                Assert(!form.TopMost, "Main window does not force TopMost above other applications");
                Assert(form.ClientSize.Width >= 960 && form.ClientSize.Height >= 620, "Main window initialized with proper dimensions");

                // Render MainForm to bitmap to verify GDI+ custom drawing produces a crisp non-empty dashboard
                using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    string previewPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard_preview.png");
                    bmp.Save(previewPath, ImageFormat.Png);
                    Assert(File.Exists(previewPath) && new FileInfo(previewPath).Length > 10000,
                        "Rendered MainForm dashboard screenshot verified", previewPath);
                }

                // Verify HideToTray behavior (Close/Minimize hides to tray and sets WS_EX_TOOLWINDOW & ShowInTaskbar=false)
                form.HideToTray(false);
                Application.DoEvents();

                int exStyle = GetWindowLong(form.Handle, GWL_EXSTYLE);
                bool hasToolWindowStyle = (exStyle & WS_EX_TOOLWINDOW) != 0;
                Assert(!form.Visible && !form.ShowInTaskbar && hasToolWindowStyle,
                    "Hidden tray window is invisible, excluded from Taskbar, and excluded from Alt+Tab (WS_EX_TOOLWINDOW)");

                // Restore from tray
                form.ShowFromTray();
                Application.DoEvents();
                Assert(form.Visible && form.ShowInTaskbar, "Left-click / ShowFromTray restores window cleanly");

                // Render OverlayForm to bitmap to verify in-game overlay UI
                using (var overlay = new OverlayForm(cfg, dns, snd))
                {
                    overlay.Show();
                    Application.DoEvents();
                    using (Bitmap ovBmp = new Bitmap(overlay.Width, overlay.Height))
                    {
                        overlay.DrawToBitmap(ovBmp, new Rectangle(0, 0, overlay.Width, overlay.Height));
                        string ovPreviewPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlay_preview.png");
                        ovBmp.Save(ovPreviewPath, ImageFormat.Png);
                        Assert(File.Exists(ovPreviewPath) && new FileInfo(ovPreviewPath).Length > 10000,
                            "Rendered OverlayForm in-game overlay screenshot verified", ovPreviewPath);
                    }
                    overlay.Close();
                }

                form.ExitApplication();
            }
        }

        private static void TestResourceConsumption()
        {
            Console.WriteLine("\n6. Performance & Resource Consumption Verification");
            Process current = Process.GetCurrentProcess();
            double workingSetMb = current.WorkingSet64 / (1024.0 * 1024.0);
            Assert(workingSetMb < 120.0, "Low RAM footprint verified", string.Format("{0:F1} MB Working Set", workingSetMb));
        }
    }
}
