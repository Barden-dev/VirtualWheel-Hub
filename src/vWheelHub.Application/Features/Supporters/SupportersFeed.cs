using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.Supporters;

public enum SupporterTier { Vip, Supporter, Thanks }
public sealed record Supporter(string Name, SupporterTier Tier);

public interface ISupportersFeed
{
    IReadOnlyList<Supporter> ReadCached();
    Task<IReadOnlyList<Supporter>> RefreshAsync(CancellationToken ct = default);
}

/// <summary>Public credits only. No accounts, payment verification or licence decisions.</summary>
public sealed class SupportersFeed : ISupportersFeed
{
    public const string PublicUrl = "https://raw.githubusercontent.com/Barden-dev/VirtualWheel-Hub/main/supporters.json";
    private const int MaxBytes = 128 * 1024;
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly HttpClient _client;
    private readonly Uri _url;
    private readonly string _cachePath, _bundledPath;
    private readonly bool _allowNetwork;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Supporter> _entries;
    private DateTimeOffset _nextCheck;

    public SupportersFeed(IAppStorage storage, bool allowNetwork = true, HttpClient? client = null,
        string? bundledPath = null, Uri? url = null, Func<DateTimeOffset>? now = null)
    {
        _cachePath = Path.Combine(storage.CacheDirectory, "supporters.json");
        _bundledPath = bundledPath ?? Path.Combine(AppContext.BaseDirectory, "Resources", "supporters.json");
        _allowNetwork = allowNetwork; _client = client ?? SharedClient; _url = url ?? new Uri(PublicUrl);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _entries = TryRead(_cachePath) ?? TryRead(_bundledPath) ?? Array.Empty<Supporter>();
    }

    public IReadOnlyList<Supporter> ReadCached() => _entries;

    public static IReadOnlyList<Supporter> Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new JsonException("Supporter list is too large.");
        using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
        var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        while (reader.Read())
            if (reader.TokenType != JsonToken.Comment) throw new JsonException("Trailing JSON content.");
        if (root["schemaVersion"]?.Type != JTokenType.Integer || root["schemaVersion"]!.ToString(Formatting.None) != "1" ||
            root["supporters"] is not JArray rows || rows.Count > 1000)
            throw new JsonException("Unsupported supporter list format.");
        var entries = new List<Supporter>();
        foreach (var row in rows)
        {
            if (row is not JObject || row["name"]?.Type != JTokenType.String || row["tier"]?.Type != JTokenType.String)
                throw new JsonException("Each supporter needs a name and tier.");
            string name = ((string)row["name"]!).Trim();
            if (name.Length is < 1 or > 80 || name.Any(char.IsControl)) throw new JsonException("Invalid display name.");
            var tier = ((string)row["tier"]!).ToLowerInvariant() switch {
                "vip" => SupporterTier.Vip, "supporter" => SupporterTier.Supporter, "thanks" => SupporterTier.Thanks,
                _ => throw new JsonException("Unknown supporter tier.") };
            entries.Add(new Supporter(name, tier));
        }
        // If someone was accidentally entered twice, keep their highest group and the file's order within each group.
        return Array.AsReadOnly(entries.OrderBy(e => e.Tier).DistinctBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<IReadOnlyList<Supporter>> RefreshAsync(CancellationToken ct = default)
    {
        if (!_allowNetwork) return _entries;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_now() < _nextCheck) return _entries;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(6));
                var requestToken = timeout.Token;
                using var response = await _client.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxBytes) throw new IOException("Supporter list is too large.");
                await using var stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
                using var bytes = new MemoryStream();
                var buffer = new byte[4096];
                int count;
                while ((count = await stream.ReadAsync(buffer, requestToken).ConfigureAwait(false)) > 0)
                {
                    if (bytes.Length + count > MaxBytes) throw new IOException("Supporter list is too large.");
                    bytes.Write(buffer, 0, count);
                }
                string json = new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
                var parsed = Parse(json);
                _entries = parsed;
                _nextCheck = _now().AddHours(4);
                SaveCache(json);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException or DecoderFallbackException)
            {
                _nextCheck = _now().AddMinutes(15);
                AppLogger.Instance.LogWarning($"Supporter list could not be refreshed: {ex.GetType().Name}. Keeping the last valid list.");
            }
            return _entries;
        }
        finally { _gate.Release(); }
    }

    private static IReadOnlyList<Supporter>? TryRead(string path)
    {
        try { return File.Exists(path) && new FileInfo(path).Length <= MaxBytes ? Parse(File.ReadAllText(path)) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    private void SaveCache(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath + ".tmp", json);
            File.Move(_cachePath + ".tmp", _cachePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { AppLogger.Instance.LogWarning("Could not save the supporter cache. The current list remains available in memory."); }
    }
}
