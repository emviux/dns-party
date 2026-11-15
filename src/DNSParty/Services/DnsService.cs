using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DNSParty.Models;
using Microsoft.Win32;

namespace DNSParty.Services
{
    public class DnsOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool CancelledByUac { get; set; }
        public DnsStatusSnapshot Snapshot { get; set; }

        public DnsOperationResult()
        {
            Success = false;
            Message = string.Empty;
            CancelledByUac = false;
            Snapshot = new DnsStatusSnapshot();
        }
    }

    public class DnsTestResult
    {
        public bool IsReachable { get; set; }
        public long LatencyMs { get; set; }
        public string ServerIp { get; set; }
        public string Details { get; set; }

        public DnsTestResult()
        {
            IsReachable = false;
            LatencyMs = -1;
            ServerIp = string.Empty;
            Details = string.Empty;
        }
    }

    public class AdapterInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public NetworkInterfaceType Type { get; set; }
        public OperationalStatus Status { get; set; }
        public bool IsActiveCandidate { get; set; }

        public override string ToString()
        {
            string statusTag = Status == OperationalStatus.Up ? "Connected" : Status.ToString();
            return string.Format("{0} — {1} ({2})", Name, Description, statusTag);
        }
    }

    public class DnsService : IDisposable
    {
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private System.Threading.Timer _debounceTimer;
        private bool _disposed;

        public bool IsOperationInProgress
        {
            get { return _operationLock.CurrentCount == 0; }
        }

        public event EventHandler NetworkConfigurationChanged;

        public DnsService()
        {
            _debounceTimer = new System.Threading.Timer(OnDebounceTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            NetworkChange.NetworkAddressChanged += OnSystemNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnSystemNetworkAvailabilityChanged;
            SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
        }

        private void OnSystemNetworkAddressChanged(object sender, EventArgs e)
        {
            ScheduleNetworkChangedNotification();
        }

        private void OnSystemNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            ScheduleNetworkChangedNotification();
        }

        private void OnSystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume || e.Mode == PowerModes.StatusChange)
            {
                ScheduleNetworkChangedNotification();
            }
        }

        private void ScheduleNetworkChangedNotification()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _debounceTimer.Change(450, Timeout.Infinite);
            }
            catch
            {
                // Ignore timer disposal race
            }
        }

        private void OnDebounceTimerElapsed(object state)
        {
            if (_disposed)
            {
                return;
            }

            var handler = NetworkConfigurationChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch
                {
                    // Prevent unhandled listener exceptions from crashing thread pool
                }
            }
        }

        public static bool IsCurrentProcessElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public List<AdapterInfo> GetCandidateAdapters()
        {
            var result = new List<AdapterInfo>();
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    bool hasGateway = false;
                    try
                    {
                        IPInterfaceProperties ipProps = ni.GetIPProperties();
                        if (ipProps != null && ipProps.GatewayAddresses != null)
                        {
                            foreach (GatewayIPAddressInformation gw in ipProps.GatewayAddresses)
                            {
                                if (gw.Address != null &&
                                    !gw.Address.Equals(IPAddress.Any) &&
                                    !gw.Address.Equals(IPAddress.IPv6Any) &&
                                    !gw.Address.Equals(IPAddress.IPv6None))
                                {
                                    hasGateway = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore adapter property read exceptions
                    }

                    bool isUp = ni.OperationalStatus == OperationalStatus.Up;
                    result.Add(new AdapterInfo
                    {
                        Id = ni.Id,
                        Name = ni.Name,
                        Description = ni.Description,
                        Type = ni.NetworkInterfaceType,
                        Status = ni.OperationalStatus,
                        IsActiveCandidate = isUp && hasGateway
                    });
                }
            }
            catch
            {
                // Return whatever was collected
            }

            return result
                .OrderByDescending(a => a.IsActiveCandidate)
                .ThenByDescending(a => a.Status == OperationalStatus.Up)
                .ThenBy(a => a.Name)
                .ToList();
        }

        public NetworkInterface ResolveTargetAdapter(string preferredAdapterId, bool autoDetect)
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .ToArray();

                if (!autoDetect && !string.IsNullOrWhiteSpace(preferredAdapterId))
                {
                    NetworkInterface explicitMatch = interfaces.FirstOrDefault(
                        ni => string.Equals(ni.Id, preferredAdapterId, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(ni.Name, preferredAdapterId, StringComparison.OrdinalIgnoreCase));
                    if (explicitMatch != null)
                    {
                        return explicitMatch;
                    }
                }

                // 1. Prefer Up Ethernet/Wi-Fi adapters with a valid non-zero IPv4 or IPv6 gateway
                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    bool isStandardType = ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                          ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                          ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                                          ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT ||
                                          ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx;

                    if (isStandardType && HasValidGateway(ni))
                    {
                        return ni;
                    }
                }

                // 2. Any Up adapter with a valid gateway (e.g., PPP / VPN / virtual Ethernet)
                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && HasValidGateway(ni))
                    {
                        return ni;
                    }
                }

                // 3. Fallback to any Up Ethernet or Wi-Fi adapter
                NetworkInterface upFallback = interfaces.FirstOrDefault(
                    ni => ni.OperationalStatus == OperationalStatus.Up &&
                          (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                           ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                           ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet));
                if (upFallback != null)
                {
                    return upFallback;
                }

                // 4. Any Up adapter
                NetworkInterface anyUp = interfaces.FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up);
                if (anyUp != null)
                {
                    return anyUp;
                }

                return interfaces.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool HasValidGateway(NetworkInterface ni)
        {
            try
            {
                IPInterfaceProperties props = ni.GetIPProperties();
                if (props == null || props.GatewayAddresses == null)
                {
                    return false;
                }

                foreach (GatewayIPAddressInformation gw in props.GatewayAddresses)
                {
                    if (gw.Address != null &&
                        !gw.Address.Equals(IPAddress.Any) &&
                        !gw.Address.Equals(IPAddress.IPv6Any) &&
                        !gw.Address.Equals(IPAddress.IPv6None))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return false;
        }

        public DnsStatusSnapshot GetActualDnsSnapshot(
            List<DnsProfile> profiles,
            string preferredAdapterId,
            bool autoDetect,
            string requestedProfileId)
        {
            var snapshot = new DnsStatusSnapshot
            {
                RequestedProfileId = requestedProfileId ?? string.Empty,
                LastUpdated = DateTime.Now
            };

            NetworkInterface adapter = ResolveTargetAdapter(preferredAdapterId, autoDetect);
            if (adapter == null)
            {
                snapshot.AdapterName = "No Active Adapter";
                snapshot.IsConnected = false;
                snapshot.IsDhcpDns = true;
                snapshot.ActiveProfileId = string.Empty;
                snapshot.ActiveProfileName = "Disconnected";
                snapshot.StatusMessage = "No active network adapter found.";
                return snapshot;
            }

            snapshot.AdapterId = adapter.Id;
            snapshot.AdapterName = adapter.Name;
            snapshot.AdapterDescription = adapter.Description;
            snapshot.IsConnected = adapter.OperationalStatus == OperationalStatus.Up;

            string staticV4NameServer = ReadRegistryNameServer(adapter.Id, false);
            string staticV6NameServer = ReadRegistryNameServer(adapter.Id, true);

            bool hasStaticV4 = !string.IsNullOrWhiteSpace(staticV4NameServer);
            bool hasStaticV6 = !string.IsNullOrWhiteSpace(staticV6NameServer);
            snapshot.IsDhcpDns = !hasStaticV4 && !hasStaticV6;

            List<string> configuredDnsList = new List<string>();

            if (hasStaticV4)
            {
                configuredDnsList.AddRange(ParseRegistryDnsServers(staticV4NameServer));
            }

            if (hasStaticV6)
            {
                configuredDnsList.AddRange(ParseRegistryDnsServers(staticV6NameServer));
            }

            if (configuredDnsList.Count == 0)
            {
                try
                {
                    IPInterfaceProperties ipProps = adapter.GetIPProperties();
                    if (ipProps != null && ipProps.DnsAddresses != null)
                    {
                        foreach (IPAddress addr in ipProps.DnsAddresses)
                        {
                            if (addr.IsIPv6SiteLocal)
                            {
                                continue;
                            }
                            configuredDnsList.Add(addr.ToString());
                        }
                    }
                }
                catch
                {
                    // Ignore IPInterfaceProperties exceptions
                }
            }

            if (configuredDnsList.Count > 0)
            {
                snapshot.ActualPrimaryDns = configuredDnsList[0];
            }

            if (configuredDnsList.Count > 1)
            {
                snapshot.ActualSecondaryDns = configuredDnsList[1];
            }

            if (snapshot.IsDhcpDns)
            {
                snapshot.ActiveProfileId = string.Empty;
                snapshot.ActiveProfileName = "Automatic (DHCP)";
                snapshot.StatusMessage = snapshot.IsConnected ? "Automatic (DHCP) DNS active" : "Adapter disconnected";
            }
            else
            {
                DnsProfile matchedProfile = null;
                if (profiles != null)
                {
                    matchedProfile = profiles.FirstOrDefault(p =>
                        p != null &&
                        IpAddressesEqual(p.PrimaryDns, snapshot.ActualPrimaryDns) &&
                        (string.IsNullOrWhiteSpace(p.SecondaryDns) ||
                         string.IsNullOrWhiteSpace(snapshot.ActualSecondaryDns) ||
                         IpAddressesEqual(p.SecondaryDns, snapshot.ActualSecondaryDns)));

                    if (matchedProfile == null)
                    {
                        matchedProfile = profiles.FirstOrDefault(p =>
                            p != null && IpAddressesEqual(p.PrimaryDns, snapshot.ActualPrimaryDns));
                    }
                }

                if (matchedProfile != null)
                {
                    snapshot.ActiveProfileId = matchedProfile.Id;
                    snapshot.ActiveProfileName = matchedProfile.Name;
                    snapshot.StatusMessage = string.Format("Connected to {0} ({1})", matchedProfile.Name, snapshot.ActualPrimaryDns);
                }
                else
                {
                    snapshot.ActiveProfileId = "custom_external";
                    snapshot.ActiveProfileName = string.Format("Custom ({0})", snapshot.ActualPrimaryDns);
                    snapshot.StatusMessage = string.Format("Custom DNS active ({0})", snapshot.ActualPrimaryDns);
                }
            }

            return snapshot;
        }

        private static bool IpAddressesEqual(string ip1, string ip2)
        {
            if (string.IsNullOrWhiteSpace(ip1) || string.IsNullOrWhiteSpace(ip2))
            {
                return false;
            }

            IPAddress a1;
            IPAddress a2;
            if (IPAddress.TryParse(ip1.Trim(), out a1) && IPAddress.TryParse(ip2.Trim(), out a2))
            {
                return a1.Equals(a2);
            }

            return string.Equals(ip1.Trim(), ip2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadRegistryNameServer(string adapterId, bool ipv6)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
            {
                return string.Empty;
            }

            try
            {
                string subKeyPath = ipv6
                    ? @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\" + adapterId
                    : @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + adapterId;

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKeyPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("NameServer");
                        if (val != null)
                        {
                            return val.ToString().Trim();
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry read error
            }

            return string.Empty;
        }

        private static List<string> ParseRegistryDnsServers(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return list;
            }

            char[] separators = new char[] { ',', ' ', ';', '\t', '\r', '\n' };
            string[] tokens = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                string trimmed = token.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(trimmed);
                }
            }

            return list;
        }

        public async Task<DnsOperationResult> ApplyDnsProfileAsync(
            DnsProfile profile,
            string preferredAdapterId,
            bool autoDetect,
            List<DnsProfile> allProfiles = null)
        {
            if (profile == null)
            {
                return new DnsOperationResult
                {
                    Success = false,
                    Message = "No DNS profile selected."
                };
            }

            string validationError;
            if (!DnsProfile.ValidateDnsAddress(profile.PrimaryDns, profile.IsIPv6, out validationError))
            {
                return new DnsOperationResult
                {
                    Success = false,
                    Message = "Invalid Primary DNS: " + validationError
                };
            }

            if (!string.IsNullOrWhiteSpace(profile.SecondaryDns))
            {
                if (!DnsProfile.ValidateDnsAddress(profile.SecondaryDns, profile.IsIPv6, out validationError))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "Invalid Secondary DNS: " + validationError
                    };
                }
            }

            if (!await _operationLock.WaitAsync(0))
            {
                return new DnsOperationResult
                {
                    Success = false,
                    Message = "Another DNS operation is already in progress."
                };
            }

            try
            {
                NetworkInterface adapter = ResolveTargetAdapter(preferredAdapterId, autoDetect);
                if (adapter == null)
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "No active network adapter found."
                    };
                }

                string adapterName = adapter.Name;
                string ipVersion = profile.IsIPv6 ? "ipv6" : "ipv4";
                string primaryDns = profile.PrimaryDns.Trim();
                string secondaryDns = string.IsNullOrWhiteSpace(profile.SecondaryDns) ? string.Empty : profile.SecondaryDns.Trim();

                var profilesForSnapshot = allProfiles ?? new List<DnsProfile> { profile };

                bool cancelledByUac = false;
                string execError = string.Empty;

                bool cmdSucceeded = await Task.Run(() =>
                {
                    if (IsCurrentProcessElevated())
                    {
                        return ExecuteNetshApplyDirect(adapterName, ipVersion, primaryDns, secondaryDns, out execError);
                    }
                    else
                    {
                        return ExecuteElevatedHelperApply(adapterName, ipVersion, primaryDns, secondaryDns, out cancelledByUac, out execError);
                    }
                });

                if (cancelledByUac)
                {
                    var currentSnap = GetActualDnsSnapshot(profilesForSnapshot, preferredAdapterId, autoDetect, profile.Id);
                    return new DnsOperationResult
                    {
                        Success = false,
                        CancelledByUac = true,
                        Message = "Administrator permission was cancelled. DNS remained unchanged.",
                        Snapshot = currentSnap
                    };
                }

                // Wait and verify actual Windows DNS configuration
                DnsStatusSnapshot verifiedSnapshot = null;
                bool verified = false;

                for (int attempt = 0; attempt < 7; attempt++)
                {
                    verifiedSnapshot = GetActualDnsSnapshot(profilesForSnapshot, preferredAdapterId, autoDetect, profile.Id);
                    if (!verifiedSnapshot.IsDhcpDns &&
                        IpAddressesEqual(verifiedSnapshot.ActualPrimaryDns, primaryDns))
                    {
                        verified = true;
                        break;
                    }

                    await Task.Delay(150);
                }

                if (verified)
                {
                    ScheduleNetworkChangedNotification();
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = string.Format("Connected to {0} ({1})", profile.Name, primaryDns),
                        Snapshot = verifiedSnapshot
                    };
                }

                string failReason = !string.IsNullOrWhiteSpace(execError)
                    ? execError
                    : "Windows network configuration could not be verified after applying DNS.";

                if (!IsCurrentProcessElevated() && !cmdSucceeded && string.IsNullOrWhiteSpace(execError))
                {
                    failReason = "Administrator permissions are required to change Windows DNS settings.";
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = failReason,
                    Snapshot = verifiedSnapshot ?? GetActualDnsSnapshot(profilesForSnapshot, preferredAdapterId, autoDetect, profile.Id)
                };
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<DnsOperationResult> DisableDnsAsync(
            string preferredAdapterId,
            bool autoDetect,
            List<DnsProfile> allProfiles = null)
        {
            if (!await _operationLock.WaitAsync(0))
            {
                return new DnsOperationResult
                {
                    Success = false,
                    Message = "Another DNS operation is already in progress."
                };
            }

            try
            {
                NetworkInterface adapter = ResolveTargetAdapter(preferredAdapterId, autoDetect);
                if (adapter == null)
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "No active network adapter found."
                    };
                }

                string adapterName = adapter.Name;
                bool cancelledByUac = false;
                string execError = string.Empty;

                bool cmdSucceeded = await Task.Run(() =>
                {
                    if (IsCurrentProcessElevated())
                    {
                        return ExecuteNetshDhcpDirect(adapterName, out execError);
                    }
                    else
                    {
                        return ExecuteElevatedHelperDhcp(adapterName, out cancelledByUac, out execError);
                    }
                });

                if (cancelledByUac)
                {
                    var currentSnap = GetActualDnsSnapshot(allProfiles, preferredAdapterId, autoDetect, string.Empty);
                    return new DnsOperationResult
                    {
                        Success = false,
                        CancelledByUac = true,
                        Message = "Administrator permission was cancelled. DNS remained unchanged.",
                        Snapshot = currentSnap
                    };
                }

                DnsStatusSnapshot verifiedSnapshot = null;
                bool verified = false;

                for (int attempt = 0; attempt < 7; attempt++)
                {
                    verifiedSnapshot = GetActualDnsSnapshot(allProfiles, preferredAdapterId, autoDetect, string.Empty);
                    if (verifiedSnapshot.IsDhcpDns)
                    {
                        verified = true;
                        break;
                    }

                    await Task.Delay(150);
                }

                if (verified)
                {
                    ScheduleNetworkChangedNotification();
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "Restored Automatic (DHCP) DNS",
                        Snapshot = verifiedSnapshot
                    };
                }

                string failReason = !string.IsNullOrWhiteSpace(execError)
                    ? execError
                    : "Failed to verify Automatic (DHCP) DNS state on Windows.";

                if (!cmdSucceeded && string.IsNullOrWhiteSpace(execError))
                {
                    failReason = "Could not restore Automatic (DHCP) DNS. Administrator privileges are required.";
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = failReason,
                    Snapshot = verifiedSnapshot ?? GetActualDnsSnapshot(allProfiles, preferredAdapterId, autoDetect, string.Empty)
                };
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public static int ExecuteElevatedCliCommand(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return 1;
            }

            try
            {
                string mode = args[0];
                string error;

                if (string.Equals(mode, "--elevated-apply", StringComparison.OrdinalIgnoreCase) && args.Length >= 4)
                {
                    string adapterName = args[1];
                    string ipVersion = args[2];
                    string primaryDns = args[3];
                    string secondaryDns = args.Length >= 5 ? args[4] : string.Empty;

                    bool ok = ExecuteNetshApplyDirect(adapterName, ipVersion, primaryDns, secondaryDns, out error);
                    return ok ? 0 : 2;
                }

                if (string.Equals(mode, "--elevated-dhcp", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
                {
                    string adapterName = args[1];
                    bool ok = ExecuteNetshDhcpDirect(adapterName, out error);
                    return ok ? 0 : 3;
                }

                return 1;
            }
            catch
            {
                return 99;
            }
        }

        private static bool ExecuteNetshApplyDirect(
            string adapterName,
            string ipVersion,
            string primaryDns,
            string secondaryDns,
            out string error)
        {
            error = string.Empty;
            string escapedName = adapterName.Replace("\"", "\\\"");
            string proto = string.Equals(ipVersion, "ipv6", StringComparison.OrdinalIgnoreCase) ? "ipv6" : "ipv4";

            // Reset the other stack to DHCP if needed or keep clean; apply primary
            string setPrimaryArgs = string.Format(
                "interface {0} set dnsservers name=\"{1}\" source=static address={2} register=primary validate=no",
                proto, escapedName, primaryDns);

            int exit1;
            string out1;
            if (!RunHiddenCommand("netsh", setPrimaryArgs, out exit1, out out1))
            {
                error = string.IsNullOrWhiteSpace(out1) ? "Failed to set primary DNS via netsh." : out1.Trim();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(secondaryDns))
            {
                string addSecondaryArgs = string.Format(
                    "interface {0} add dnsservers name=\"{1}\" address={2} index=2 validate=no",
                    proto, escapedName, secondaryDns);

                int exit2;
                string out2;
                RunHiddenCommand("netsh", addSecondaryArgs, out exit2, out out2);
            }

            // Flush DNS cache automatically after switch
            int flushExit;
            string flushOut;
            RunHiddenCommand("ipconfig", "/flushdns", out flushExit, out flushOut);

            return true;
        }

        private static bool ExecuteNetshDhcpDirect(string adapterName, out string error)
        {
            error = string.Empty;
            string escapedName = adapterName.Replace("\"", "\\\"");

            string dhcpV4Args = string.Format("interface ipv4 set dnsservers name=\"{0}\" source=dhcp", escapedName);
            int exitV4;
            string outV4;
            bool okV4 = RunHiddenCommand("netsh", dhcpV4Args, out exitV4, out outV4);

            string dhcpV6Args = string.Format("interface ipv6 set dnsservers name=\"{0}\" source=dhcp", escapedName);
            int exitV6;
            string outV6;
            RunHiddenCommand("netsh", dhcpV6Args, out exitV6, out outV6);

            int flushExit;
            string flushOut;
            RunHiddenCommand("ipconfig", "/flushdns", out flushExit, out flushOut);

            if (!okV4 && !string.IsNullOrWhiteSpace(outV4))
            {
                error = outV4.Trim();
            }

            return okV4;
        }

        private static bool ExecuteElevatedHelperApply(
            string adapterName,
            string ipVersion,
            string primaryDns,
            string secondaryDns,
            out bool cancelledByUac,
            out string error)
        {
            cancelledByUac = false;
            error = string.Empty;

            string args = string.Format(
                "--elevated-apply \"{0}\" \"{1}\" \"{2}\" \"{3}\"",
                adapterName.Replace("\"", ""),
                ipVersion.Replace("\"", ""),
                primaryDns.Replace("\"", ""),
                (secondaryDns ?? string.Empty).Replace("\"", ""));

            return RunElevatedSelf(args, out cancelledByUac, out error);
        }

        private static bool ExecuteElevatedHelperDhcp(
            string adapterName,
            out bool cancelledByUac,
            out string error)
        {
            cancelledByUac = false;
            error = string.Empty;

            string args = string.Format("--elevated-dhcp \"{0}\"", adapterName.Replace("\"", ""));
            return RunElevatedSelf(args, out cancelledByUac, out error);
        }

        private static bool RunElevatedSelf(string arguments, out bool cancelledByUac, out string error)
        {
            cancelledByUac = false;
            error = string.Empty;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = arguments,
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        error = "Could not start elevated helper process.";
                        return false;
                    }

                    proc.WaitForExit(15000);
                    return proc.ExitCode == 0;
                }
            }
            catch (Win32Exception ex)
            {
                // Error code 1223 = ERROR_CANCELLED (The operation was canceled by the user)
                if (ex.NativeErrorCode == 1223)
                {
                    cancelledByUac = true;
                    error = "Administrator permission prompt was cancelled.";
                    return false;
                }

                error = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool RunHiddenCommand(string fileName, string arguments, out int exitCode, out string output)
        {
            exitCode = -1;
            output = string.Empty;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        return false;
                    }

                    string stdOut = proc.StandardOutput.ReadToEnd();
                    string stdErr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);

                    exitCode = proc.ExitCode;
                    output = string.IsNullOrWhiteSpace(stdErr) ? stdOut : (stdOut + "\n" + stdErr);
                    return exitCode == 0;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }

        public async Task<DnsOperationResult> FlushDnsCacheAsync()
        {
            return await Task.Run(() =>
            {
                int exitCode;
                string output;
                bool ok = RunHiddenCommand("ipconfig", "/flushdns", out exitCode, out output);

                if (ok)
                {
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "Windows DNS Resolver Cache successfully flushed."
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(output)
                        ? "Failed to flush Windows DNS cache."
                        : output.Trim()
                };
            });
        }

        public async Task<DnsTestResult> TestDnsServerAsync(string dnsServerIp, int timeoutMs = 2000)
        {
            var result = new DnsTestResult
            {
                ServerIp = dnsServerIp ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(dnsServerIp))
            {
                result.Details = "DNS server address is empty.";
                return result;
            }

            IPAddress serverAddress;
            if (!IPAddress.TryParse(dnsServerIp.Trim(), out serverAddress))
            {
                result.Details = "Invalid IP address format.";
                return result;
            }

            return await Task.Run(() =>
            {
                try
                {
                    ushort txId = (ushort)new Random(Guid.NewGuid().GetHashCode()).Next(1, 65534);
                    byte[] queryPacket = BuildDnsQueryPacket(txId, "cloudflare.com");

                    using (var socket = new Socket(serverAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.ReceiveTimeout = timeoutMs;
                        socket.SendTimeout = timeoutMs;

                        EndPoint remoteEp = new IPEndPoint(serverAddress, 53);
                        var sw = Stopwatch.StartNew();

                        socket.SendTo(queryPacket, remoteEp);

                        byte[] receiveBuffer = new byte[512];
                        int received = socket.ReceiveFrom(receiveBuffer, ref remoteEp);
                        sw.Stop();

                        if (received >= 12)
                        {
                            ushort respTxId = (ushort)((receiveBuffer[0] << 8) | receiveBuffer[1]);
                            bool isResponse = (receiveBuffer[2] & 0x80) != 0;

                            if (respTxId == txId && isResponse)
                            {
                                long ms = Math.Max(1L, sw.ElapsedMilliseconds);
                                result.IsReachable = true;
                                result.LatencyMs = ms;
                                result.Details = string.Format("{0} ms", ms);
                                return result;
                            }
                        }

                        result.Details = "Invalid DNS response header received.";
                        return result;
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Details = "Timed out (Unreachable)";
                    }
                    else
                    {
                        result.Details = "Unreachable (" + ex.SocketErrorCode + ")";
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    result.Details = "Error: " + ex.Message;
                    return result;
                }
            });
        }

        private static byte[] BuildDnsQueryPacket(ushort transactionId, string domainName)
        {
            var bytes = new List<byte>(64);

            // Header (12 bytes)
            // ID
            bytes.Add((byte)(transactionId >> 8));
            bytes.Add((byte)(transactionId & 0xFF));
            // Flags: Standard query with Recursion Desired (0x0100)
            bytes.Add(0x01);
            bytes.Add(0x00);
            // QDCOUNT = 1
            bytes.Add(0x00);
            bytes.Add(0x01);
            // ANCOUNT = 0
            bytes.Add(0x00);
            bytes.Add(0x00);
            // NSCOUNT = 0
            bytes.Add(0x00);
            bytes.Add(0x00);
            // ARCOUNT = 0
            bytes.Add(0x00);
            bytes.Add(0x00);

            // Question section
            string[] labels = domainName.Split('.');
            foreach (string label in labels)
            {
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                bytes.Add((byte)labelBytes.Length);
                bytes.AddRange(labelBytes);
            }
            bytes.Add(0x00); // Root terminator

            // QTYPE = A (1)
            bytes.Add(0x00);
            bytes.Add(0x01);
            // QCLASS = IN (1)
            bytes.Add(0x00);
            bytes.Add(0x01);

            return bytes.ToArray();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                NetworkChange.NetworkAddressChanged -= OnSystemNetworkAddressChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnSystemNetworkAvailabilityChanged;
                SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
            }
            catch
            {
                // Ignore event unhook errors
            }

            try
            {
                if (_debounceTimer != null)
                {
                    _debounceTimer.Dispose();
                    _debounceTimer = null;
                }
            }
            catch
            {
                // Ignore timer dispose errors
            }
        }
    }
}
