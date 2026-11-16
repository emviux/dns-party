using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DNSParty.Models
{
    [Serializable]
    public class DnsProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string PrimaryDns { get; set; }
        public string SecondaryDns { get; set; }
        public bool IsIPv6 { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsDefaultProvider { get; set; }
        public string IconKey { get; set; }
        public string Description { get; set; }
        public string Warning { get; set; }

        public DnsProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            Tag = string.Empty;
            PrimaryDns = string.Empty;
            SecondaryDns = string.Empty;
            IsIPv6 = false;
            IsFavorite = false;
            IsDefaultProvider = false;
            IconKey = "custom";
            Description = string.Empty;
            Warning = string.Empty;
        }

        public DnsProfile Clone()
        {
            return new DnsProfile
            {
                Id = this.Id,
                Name = this.Name,
                Tag = this.Tag,
                PrimaryDns = this.PrimaryDns,
                SecondaryDns = this.SecondaryDns,
                IsIPv6 = this.IsIPv6,
                IsFavorite = this.IsFavorite,
                IsDefaultProvider = this.IsDefaultProvider,
                IconKey = this.IconKey,
                Description = this.Description,
                Warning = this.Warning
            };
        }

        public string GetDisplayAddresses()
        {
            if (string.IsNullOrWhiteSpace(SecondaryDns))
            {
                return PrimaryDns ?? string.Empty;
            }
            return string.Format("{0}   {1}", PrimaryDns, SecondaryDns);
        }

        /// <summary>
        /// Validates an IPv4 or IPv6 DNS server IP address.
        /// Rejects empty, malformed, wrong address family, unspecified (0.0.0.0 / ::),
        /// loopback, broadcast (255.255.255.255), and multicast addresses.
        /// </summary>
        public static bool ValidateDnsAddress(string ip, bool isIPv6, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(ip))
            {
                error = "DNS IP address cannot be empty.";
                return false;
            }

            string trimmed = ip.Trim();

            // For IPv4, enforce strict 4-octet dotted-quad format so partial numbers like "1.1" are rejected.
            if (!isIPv6)
            {
                string[] parts = trimmed.Split('.');
                if (parts.Length != 4)
                {
                    error = "Enter a valid IPv4 address (e.g., 1.1.1.1).";
                    return false;
                }

                foreach (string part in parts)
                {
                    int octet;
                    if (string.IsNullOrEmpty(part) || !int.TryParse(part, out octet) || octet < 0 || octet > 255)
                    {
                        error = "Each IPv4 octet must be a number between 0 and 255.";
                        return false;
                    }
                }
            }

            IPAddress parsed;
            if (!IPAddress.TryParse(trimmed, out parsed))
            {
                error = isIPv6
                    ? "Enter a valid IPv6 address (e.g., 2606:4700:4700::1111)."
                    : "Enter a valid IPv4 address (e.g., 1.1.1.1).";
                return false;
            }

            AddressFamily expectedFamily = isIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
            if (parsed.AddressFamily != expectedFamily)
            {
                error = isIPv6
                    ? "Address must be a valid IPv6 DNS address."
                    : "Address must be a valid IPv4 DNS address.";
                return false;
            }

            if (IPAddress.IsLoopback(parsed))
            {
                error = "Loopback addresses cannot be used as an external DNS server.";
                return false;
            }

            if (!isIPv6)
            {
                byte[] bytes = parsed.GetAddressBytes();
                if (parsed.Equals(IPAddress.Any) || bytes[0] == 0)
                {
                    error = "Unspecified (0.x.x.x) address is not a valid DNS server.";
                    return false;
                }

                if (parsed.Equals(IPAddress.Broadcast) || (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255))
                {
                    error = "Broadcast address (255.255.255.255) is not a valid DNS server.";
                    return false;
                }

                if (bytes[0] >= 224 && bytes[0] <= 239)
                {
                    error = "Multicast IPv4 addresses (224.0.0.0 - 239.255.255.255) are not valid DNS servers.";
                    return false;
                }
            }
            else
            {
                if (parsed.Equals(IPAddress.IPv6Any) || parsed.Equals(IPAddress.IPv6None))
                {
                    error = "Unspecified IPv6 address (::) is not a valid DNS server.";
                    return false;
                }

                if (parsed.IsIPv6Multicast)
                {
                    error = "Multicast IPv6 addresses are not valid DNS servers.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the 5 required built-in DNS providers in display order.
        /// </summary>
        public static List<DnsProfile> GetDefaultProfiles()
        {
            return new List<DnsProfile>
            {
                new DnsProfile
                {
                    Id = "cloudflare",
                    Name = "Cloudflare",
                    Tag = "Recommended",
                    PrimaryDns = "1.1.1.1",
                    SecondaryDns = "1.0.0.1",
                    IsIPv6 = false,
                    IsFavorite = true,
                    IsDefaultProvider = true,
                    IconKey = "cloudflare"
                },
                new DnsProfile
                {
                    Id = "google",
                    Name = "Google",
                    Tag = "",
                    PrimaryDns = "8.8.8.8",
                    SecondaryDns = "8.8.4.4",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "google"
                },
                new DnsProfile
                {
                    Id = "quad9",
                    Name = "Quad9",
                    Tag = "",
                    PrimaryDns = "9.9.9.9",
                    SecondaryDns = "149.112.112.112",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "quad9"
                },
                new DnsProfile
                {
                    Id = "adguard",
                    Name = "AdGuard DNS",
                    Tag = "Filtered",
                    PrimaryDns = "94.140.14.14",
                    SecondaryDns = "94.140.15.15",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "adguard"
                },
                new DnsProfile
                {
                    Id = "opendns",
                    Name = "OpenDNS",
                    Tag = "",
                    PrimaryDns = "208.67.222.222",
                    SecondaryDns = "208.67.220.220",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "opendns"
                },
                new DnsProfile
                {
                    Id = "electro",
                    Name = "Electro",
                    Tag = "Gaming",
                    PrimaryDns = "78.157.42.100",
                    SecondaryDns = "78.157.42.101",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "electro",
                    Description = "Gaming-focused DNS for PC, PlayStation and Xbox."
                },
                new DnsProfile
                {
                    Id = "403online",
                    Name = "403 Online",
                    Tag = "Anti-Sanction",
                    PrimaryDns = "10.202.10.202",
                    SecondaryDns = "10.202.10.102",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "403online",
                    Description = "DNS service supporting DoH and DoT.",
                    Warning = "May require an Iranian ISP connection."
                },
                new DnsProfile
                {
                    Id = "radargame",
                    Name = "Radar Game",
                    Tag = "Gaming",
                    PrimaryDns = "10.202.10.10",
                    SecondaryDns = "10.202.10.11",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "radargame",
                    Description = "Gaming-oriented DNS service.",
                    Warning = "May require an Iranian ISP connection."
                },
                new DnsProfile
                {
                    Id = "shecan",
                    Name = "Shecan",
                    Tag = "Anti-Sanction",
                    PrimaryDns = "178.22.122.100",
                    SecondaryDns = "185.51.200.2",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "shecan",
                    Description = "Free DNS service with DoH and DoT support."
                },
                new DnsProfile
                {
                    Id = "begzar",
                    Name = "Begzar",
                    Tag = "Gaming",
                    PrimaryDns = "185.55.226.26",
                    SecondaryDns = "185.55.225.25",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "begzar",
                    Description = "Alternative DNS for game and launcher connectivity."
                },
                new DnsProfile
                {
                    Id = "shatel",
                    Name = "Shatel",
                    Tag = "ISP-Specific",
                    PrimaryDns = "85.15.1.14",
                    SecondaryDns = "85.15.1.15",
                    IsIPv6 = false,
                    IsFavorite = false,
                    IsDefaultProvider = true,
                    IconKey = "shatel",
                    Description = "ISP-specific DNS, recommended for Shatel subscribers.",
                    Warning = "Recommended for Shatel subscribers."
                }
            };
        }
    }
}
