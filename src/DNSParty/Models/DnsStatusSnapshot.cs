using System;

namespace DNSParty.Models
{
    public class DnsStatusSnapshot
    {
        public string AdapterName { get; set; }
        public string AdapterId { get; set; }
        public string AdapterDescription { get; set; }
        public bool IsConnected { get; set; }
        public bool IsDhcpDns { get; set; }
        public string ActualPrimaryDns { get; set; }
        public string ActualSecondaryDns { get; set; }
        public string ActiveProfileId { get; set; }
        public string ActiveProfileName { get; set; }
        public string RequestedProfileId { get; set; }
        public string StatusMessage { get; set; }
        public DateTime LastUpdated { get; set; }

        public bool IsCustomDnsActive
        {
            get
            {
                return IsConnected && !IsDhcpDns && !string.IsNullOrWhiteSpace(ActualPrimaryDns);
            }
        }

        public DnsStatusSnapshot()
        {
            AdapterName = "No Active Adapter";
            AdapterId = string.Empty;
            AdapterDescription = string.Empty;
            IsConnected = false;
            IsDhcpDns = true;
            ActualPrimaryDns = string.Empty;
            ActualSecondaryDns = string.Empty;
            ActiveProfileId = string.Empty;
            ActiveProfileName = "Automatic (DHCP)";
            RequestedProfileId = string.Empty;
            StatusMessage = "Ready";
            LastUpdated = DateTime.Now;
        }
    }
}
