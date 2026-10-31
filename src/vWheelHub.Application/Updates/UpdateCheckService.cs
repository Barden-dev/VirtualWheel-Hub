using System.Net.Http;
using Newtonsoft.Json;
using SimRacingHub.Application.Features.AppSettings;

namespace SimRacingHub.Application.Updates;

/// <summary>Reads the version manifest independently of UI state and release-link policy.</summary>
public sealed class UpdateCheckService : IUpdateCheckService
{
    private readonly Func<HttpClient> _createClient;

    public UpdateCheckService(Func<HttpClient>? createClient = null)
    {
        _createClient = createClient ?? (() => new HttpClient { Timeout = TimeSpan.FromSeconds(5) });
    }

    public async Task<UpdateCheckResult?> CheckAsync(string currentVersion, string updateUrl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var httpClient = _createClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("vWheelHub-UpdateChecker/1.0");
        foreach (string branch in new[] { "main", "master" })
        {
            try
            {
                using var response = await httpClient.GetAsync($"https://raw.githubusercontent.com/Barden-dev/vWheel-Hub/{branch}/version.json", ct);
                if (!response.IsSuccessStatusCode) continue;
                string json = await response.Content.ReadAsStringAsync(ct);
                var manifest = JsonConvert.DeserializeObject<VersionManifestDto>(json);
                if (manifest == null || !Version.TryParse(manifest.Version?.Trim(), out _)) continue;
                string remoteVersion = manifest.Version.Trim();
                return new UpdateCheckResult(CompareVersions(remoteVersion, currentVersion),
                    remoteVersion, currentVersion, updateUrl);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            catch (HttpRequestException) { }
            catch (JsonException) { }
        }
        ct.ThrowIfCancellationRequested();
        return null;
    }

    private sealed class VersionManifestDto
    {
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;
    }

    private static bool CompareVersions(string remote, string current)
    {
        if (Version.TryParse(remote, out var remoteVersion) && Version.TryParse(current, out var currentVersion))
            return remoteVersion > currentVersion;

        return !string.Equals(remote, current, StringComparison.OrdinalIgnoreCase);
    }
}
