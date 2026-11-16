using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DNSParty.Services
{
    public class UpdateService
    {
        private readonly ConfigService _configService;
        private string _downloadedFilePath;
        private string _downloadUrl;

        public bool IsUpdateAvailable { get; private set; }
        public string LatestVersion { get; private set; }
        public string ReleaseNotes { get; private set; }
        public DateTime? ReleaseDate { get; private set; }
        
        public bool IsDownloading { get; private set; }
        public int DownloadProgress { get; private set; }

        public UpdateService(ConfigService configService)
        {
            _configService = configService;
        }

        public async Task CheckForUpdatesAsync()
        {
            if (_configService != null && !_configService.Settings.AutoCheckUpdates)
            {
                return;
            }

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "DNS-Party Updater");
                    string url = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest", VersionInfo.GitHubRepoOwner, VersionInfo.GitHubRepoName);
                    
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    string json = await response.Content.ReadAsStringAsync();

                    var serializer = new JavaScriptSerializer();
                    var data = serializer.DeserializeObject(json) as Dictionary<string, object>;

                    if (data == null) return;

                    if (data.TryGetValue("tag_name", out var tagObj) && tagObj != null)
                    {
                        string tagName = tagObj.ToString();
                        LatestVersion = tagName.TrimStart('v', 'V');
                        
                        if (Version.TryParse(VersionInfo.CurrentVersion, out Version current) &&
                            Version.TryParse(LatestVersion, out Version latest))
                        {
                            if (latest > current)
                            {
                                IsUpdateAvailable = true;
                            }
                        }
                    }

                    if (data.TryGetValue("body", out var bodyObj) && bodyObj != null)
                    {
                        ReleaseNotes = bodyObj.ToString();
                    }

                    if (data.TryGetValue("published_at", out var pubObj) && pubObj != null)
                    {
                        if (DateTime.TryParse(pubObj.ToString(), out DateTime pubDate))
                        {
                            ReleaseDate = pubDate;
                        }
                    }

                    if (data.TryGetValue("assets", out var assetsObj) && assetsObj is IEnumerable assets)
                    {
                        string fallbackExeUrl = null;
                        foreach (var item in assets)
                        {
                            if (item is Dictionary<string, object> asset)
                            {
                                if (asset.TryGetValue("browser_download_url", out var urlObj) && urlObj != null)
                                {
                                    string downloadUrl = urlObj.ToString();
                                    if (downloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (downloadUrl.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            _downloadUrl = downloadUrl;
                                            break;
                                        }
                                        if (fallbackExeUrl == null)
                                        {
                                            fallbackExeUrl = downloadUrl;
                                        }
                                    }
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(_downloadUrl))
                        {
                            _downloadUrl = fallbackExeUrl;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during update check
            }
        }

        public async Task DownloadUpdateAsync(Action<int> progressCallback)
        {
            if (string.IsNullOrEmpty(_downloadUrl))
                throw new InvalidOperationException("No download URL found.");

            string tempPath = Path.Combine(Path.GetTempPath(), "DNSParty_Update.exe");
            IsDownloading = true;

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "DNS-Party Updater");
                
                if (progressCallback != null)
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        DownloadProgress = e.ProgressPercentage;
                        progressCallback(e.ProgressPercentage);
                    };
                }

                await client.DownloadFileTaskAsync(new Uri(_downloadUrl), tempPath);
            }

            IsDownloading = false;
            _downloadedFilePath = tempPath;
        }

        public void InstallUpdateAndRestart()
        {
            if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
            {
                throw new InvalidOperationException("Update installer not found.");
            }

            Process.Start(_downloadedFilePath, "/S /UPDATE");
            Application.Exit();
        }
    }
}