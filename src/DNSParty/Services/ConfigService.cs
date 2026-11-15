using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using DNSParty.Models;
using Microsoft.Win32;

namespace DNSParty.Services
{
    public class ConfigService
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "DNSParty";

        private readonly string _configFilePath;
        private readonly object _saveLock = new object();

        public AppSettings Settings { get; private set; }
        public List<DnsProfile> Profiles { get; private set; }
        public bool RecoveredFromCorruption { get; private set; }
        public string ConfigFilePath { get { return _configFilePath; } }

        public ConfigService()
        {
            _configFilePath = ResolveConfigFilePath();
            Settings = new AppSettings();
            Profiles = new List<DnsProfile>();
            Load();
        }

        private static string ResolveConfigFilePath()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string portableFlag = Path.Combine(baseDir, "portable.flag");
                string portableConfig = Path.Combine(baseDir, "portable_config.json");

                if (File.Exists(portableFlag) || File.Exists(portableConfig))
                {
                    return portableConfig;
                }
            }
            catch
            {
                // Ignore and fall back to LocalAppData
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDir = Path.Combine(localAppData, "DNSParty");
            return Path.Combine(appDir, "config.json");
        }

        public void Load()
        {
            RecoveredFromCorruption = false;

            if (!File.Exists(_configFilePath))
            {
                InitializeDefaults();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("Configuration file is empty.");
                }

                var serializer = new JavaScriptSerializer();
                var loaded = serializer.Deserialize<AppSettings>(json);
                if (loaded == null)
                {
                    throw new InvalidDataException("Deserialized configuration was null.");
                }

                Settings = loaded;
                NormalizeLoadedSettings();
            }
            catch
            {
                RecoveredFromCorruption = true;
                BackupCorruptedConfig();
                InitializeDefaults();
                Save();
            }
        }

        private void NormalizeLoadedSettings()
        {
            if (Settings.FavoriteProfileIds == null)
            {
                Settings.FavoriteProfileIds = new List<string>();
            }

            if (Settings.CustomProfiles == null)
            {
                Settings.CustomProfiles = new List<DnsProfile>();
            }

            if (string.IsNullOrWhiteSpace(Settings.LastChangeText))
            {
                Settings.LastChangeText = "Ready";
            }

            if (!Settings.InitializedProfiles && Settings.CustomProfiles.Count == 0)
            {
                Profiles = DnsProfile.GetDefaultProfiles();
                Settings.InitializedProfiles = true;
                Settings.CustomProfiles = Profiles.Select(p => p.Clone()).ToList();
            }
            else
            {
                Profiles = new List<DnsProfile>();
                foreach (var p in Settings.CustomProfiles)
                {
                    if (p != null && !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.PrimaryDns))
                    {
                        if (string.IsNullOrWhiteSpace(p.Id))
                        {
                            p.Id = Guid.NewGuid().ToString("N");
                        }
                        if (string.IsNullOrWhiteSpace(p.IconKey))
                        {
                            p.IconKey = "custom";
                        }
                        Profiles.Add(p);
                    }
                }

                // Sync FavoriteProfileIds if needed
                foreach (var favId in Settings.FavoriteProfileIds)
                {
                    var match = Profiles.FirstOrDefault(p => string.Equals(p.Id, favId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.IsFavorite = true;
                    }
                }
            }
        }

        private void InitializeDefaults()
        {
            Settings = new AppSettings();
            Profiles = DnsProfile.GetDefaultProfiles();
            Settings.InitializedProfiles = true;
            Settings.CustomProfiles = Profiles.Select(p => p.Clone()).ToList();
            Settings.FavoriteProfileIds = Profiles.Where(p => p.IsFavorite).Select(p => p.Id).ToList();
        }

        private void BackupCorruptedConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string backupPath = _configFilePath + ".corrupt." + timestamp + ".bak";
                    File.Copy(_configFilePath, backupPath, true);
                }
            }
            catch
            {
                // Ignore backup errors
            }
        }

        public bool Save()
        {
            lock (_saveLock)
            {
                try
                {
                    Settings.InitializedProfiles = true;
                    Settings.CustomProfiles = Profiles.Select(p => p.Clone()).ToList();
                    Settings.FavoriteProfileIds = Profiles.Where(p => p.IsFavorite).Select(p => p.Id).ToList();

                    string directory = Path.GetDirectoryName(_configFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var serializer = new JavaScriptSerializer();
                    string rawJson = serializer.Serialize(Settings);
                    string formattedJson = FormatJson(rawJson);

                    string tempPath = _configFilePath + ".tmp";
                    File.WriteAllText(tempPath, formattedJson, Encoding.UTF8);

                    if (File.Exists(_configFilePath))
                    {
                        try
                        {
                            File.Replace(tempPath, _configFilePath, null);
                        }
                        catch
                        {
                            File.Copy(tempPath, _configFilePath, true);
                            File.Delete(tempPath);
                        }
                    }
                    else
                    {
                        File.Move(tempPath, _configFilePath);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void ResetDefaultProfiles()
        {
            var defaults = DnsProfile.GetDefaultProfiles();
            foreach (var def in defaults)
            {
                var existingIndex = Profiles.FindIndex(p => string.Equals(p.Id, def.Id, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    Profiles[existingIndex] = def;
                }
                else
                {
                    Profiles.Add(def);
                }
            }
            Save();
        }

        public void ResetAllSettingsToDefaults()
        {
            InitializeDefaults();
            SetStartWithWindows(false);
            Save();
        }

        public bool SetStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        string value = "\"" + exePath + "\"" + (Settings.StartMinimized ? " --minimized" : "");
                        key.SetValue(RunValueName, value, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(RunValueName) != null)
                        {
                            key.DeleteValue(RunValueName, false);
                        }
                    }
                }

                Settings.StartWithWindows = enable;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            int indent = 0;
            bool inQuotes = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];

                if (escaped)
                {
                    sb.Append(ch);
                    escaped = false;
                    continue;
                }

                if (ch == '\\' && inQuotes)
                {
                    sb.Append(ch);
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(ch);
                    continue;
                }

                if (!inQuotes)
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case '}':
                        case ']':
                            sb.AppendLine();
                            indent = Math.Max(0, indent - 1);
                            sb.Append(new string(' ', indent * 2));
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case ':':
                            sb.Append(": ");
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
}
