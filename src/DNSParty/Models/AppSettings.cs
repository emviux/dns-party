using System;
using System.Collections.Generic;

namespace DNSParty.Models
{
    [Serializable]
    public class AppSettings
    {
        // General
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool CloseMinimizesToTray { get; set; }
        public bool StartMinimized { get; set; }

        // DNS
        public string SelectedAdapterId { get; set; }
        public bool AutoDetectAdapter { get; set; }
        public bool ConfirmBeforeDisable { get; set; }

        // Appearance
        public bool DarkMode { get; set; }

        // Sound
        public bool EnableSounds { get; set; }

        // Notifications
        public bool EnableNotifications { get; set; }

        // Overlay
        public string OverlayShortcut { get; set; }

        // State tracking
        public string LastChangeText { get; set; }
        public string LastRequestedProfileId { get; set; }

        // Updates
        public bool AutoCheckUpdates { get; set; }

        // Profiles & Favorites persistence
        public bool InitializedProfiles { get; set; }
        public List<string> FavoriteProfileIds { get; set; }
        public List<DnsProfile> CustomProfiles { get; set; }

        public AppSettings()
        {
            StartWithWindows = false;
            MinimizeToTray = true;
            CloseMinimizesToTray = true;
            StartMinimized = false;

            SelectedAdapterId = string.Empty;
            AutoDetectAdapter = true;
            ConfirmBeforeDisable = false;

            DarkMode = true;
            EnableSounds = true;
            EnableNotifications = true;
            OverlayShortcut = "Shift+Tab";
            AutoCheckUpdates = true;

            LastChangeText = "Ready";
            LastRequestedProfileId = "cloudflare";

            InitializedProfiles = false;
            FavoriteProfileIds = new List<string> { "cloudflare" };
            CustomProfiles = new List<DnsProfile>();
        }
    }
}
