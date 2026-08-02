using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public class VersionManifest
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string UpdateUrl { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        public const string CurrentVersion = "1.0.18";
        public static bool IsProVersion { get; set; } = false;

        public const string GitHubRepoUrl = "https://github.com/Barden-dev/vWheel-Hub";
        public const string BoostyUrl = "https://boosty.to/barden_dev";

        private const string PrimaryRawUrl = "https://raw.githubusercontent.com/Barden-dev/vWheel-Hub/main/version.json";
        private const string FallbackRawUrl = "https://raw.githubusercontent.com/Barden-dev/vWheel-Hub/master/version.json";

        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("vWheelHub-UpdateChecker/1.0");
        }

        public string TargetUpdateUrl => IsProVersion ? BoostyUrl : GitHubRepoUrl;

        public async Task<UpdateCheckResult?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string? jsonContent = await FetchVersionJsonAsync(PrimaryRawUrl, cancellationToken);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    jsonContent = await FetchVersionJsonAsync(FallbackRawUrl, cancellationToken);
                }

                if (string.IsNullOrEmpty(jsonContent))
                {
                    AppLogger.Instance.LogInfo("Update check: version.json not found on GitHub repository (empty repository or offline).");
                    return null;
                }

                var manifest = JsonConvert.DeserializeObject<VersionManifest>(jsonContent);
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
                {
                    return null;
                }

                string remoteVersionStr = manifest.Version.Trim();
                bool isNewer = CompareVersions(remoteVersionStr, CurrentVersion);

                return new UpdateCheckResult
                {
                    IsUpdateAvailable = isNewer,
                    LatestVersion = remoteVersionStr,
                    CurrentVersion = CurrentVersion,
                    UpdateUrl = TargetUpdateUrl
                };
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to check for remote updates", ex);
                return null;
            }
        }

        private async Task<string?> FetchVersionJsonAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                // Convert github.com/.../blob/... to github.com/.../raw/... if necessary
                if (url.Contains("github.com") && url.Contains("/blob/"))
                {
                    url = url.Replace("/blob/", "/raw/");
                }

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(cancellationToken);
                    content = content?.Trim() ?? string.Empty;
                    // Ensure content is not HTML page
                    if (!content.StartsWith("<") && content.StartsWith("{"))
                    {
                        return content;
                    }
                }
            }
            catch
            {
                // Silence individual endpoint fetch failures (e.g. 404 on branch)
            }
            return null;
        }

        private static bool CompareVersions(string remote, string current)
        {
            if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(current, out var currentVer))
            {
                return remoteVer > currentVer;
            }
            return !string.Equals(remote, current, StringComparison.OrdinalIgnoreCase);
        }
    }
}
