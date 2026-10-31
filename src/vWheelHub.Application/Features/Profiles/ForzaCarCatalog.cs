using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SimRacingHub.Application.Features.Profiles;

internal sealed record ForzaCarLabel(string Make, string Model, int Year)
{
    internal string DisplayName => $"{Make} {Model} {Year}";
}

internal sealed class ForzaCarCatalog
{
    internal const int MaxBytes = 2 * 1024 * 1024;
    internal static readonly Uri Source = new("https://raw.githubusercontent.com/bluemanos/forza-motorsport-car-track-ordinal/master/fm8/cars.json");
    internal static ForzaCarCatalog Default { get; } = new();
    private readonly IReadOnlyDictionary<int, ForzaCarLabel> _bundled;
    private IReadOnlyDictionary<int, ForzaCarLabel> _labels;
    private readonly SemaphoreSlim _updateGate = new(1);
    private bool _attemptedThisSession;

    internal ForzaCarCatalog()
    {
        using var stream = typeof(ForzaCarCatalog).Assembly.GetManifestResourceStream("ForzaCars.json")!;
        using var reader = new StreamReader(stream);
        _labels = _bundled = Parse(reader.ReadToEnd());
    }

    internal ForzaCarLabel? Find(string? key) => key?.StartsWith("Forza car ", StringComparison.Ordinal) == true &&
        int.TryParse(key.AsSpan(10), out int ordinal) && Volatile.Read(ref _labels).TryGetValue(ordinal, out var car) ? car : null;

    internal static IReadOnlyDictionary<int, ForzaCarLabel> Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Catalog is too large");
        var rows = JArray.Parse(json);
        if (rows.Count is < 1 or > 20000) throw new InvalidDataException("Invalid catalog count");
        var result = new Dictionary<int, ForzaCarLabel>();
        foreach (var row in rows)
        {
            if (row is not JObject item || item["CarOrdinal"]?.Type != JTokenType.Integer ||
                item["Year"]?.Type != JTokenType.Integer || item["Make"]?.Type != JTokenType.String ||
                item["Model"]?.Type is not (JTokenType.String or JTokenType.Integer))
                throw new InvalidDataException("Invalid catalog field types");
            int id = row.Value<int?>("CarOrdinal") ?? 0, year = row.Value<int?>("Year") ?? 0;
            string make = row.Value<string>("Make") ?? "", model = row.Value<string>("Model") ?? "";
            static bool Valid(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 160 && !text.Any(char.IsControl);
            if (id <= 0 || year is < 1880 or > 9999 || !Valid(make) || !Valid(model) ||
                !result.TryAdd(id, new(make.Trim(), model.Trim(), year)))
                throw new InvalidDataException("Invalid or duplicate catalog entry");
        }
        return result;
    }

    private void Apply(IReadOnlyDictionary<int, ForzaCarLabel> downloaded)
    {
        var merged = new Dictionary<int, ForzaCarLabel>(Volatile.Read(ref _labels));
        foreach (var entry in downloaded) merged[entry.Key] = entry.Value;
        Volatile.Write(ref _labels, merged);
    }

    internal async Task RefreshAsync(string cacheDirectory, HttpClient client, CancellationToken ct = default)
    {
        await _updateGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_attemptedThisSession) return;
            _attemptedThisSession = true;
            Directory.CreateDirectory(cacheDirectory);
            string cache = Path.Combine(cacheDirectory, "cars.json");
            if (File.Exists(cache) && new FileInfo(cache).Length <= MaxBytes)
            {
                try { Apply(Parse(await File.ReadAllTextAsync(cache, ct).ConfigureAwait(false))); }
                catch (Exception e) when (e is Newtonsoft.Json.JsonException or InvalidDataException or FormatException or OverflowException) { }
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await client.GetAsync(Source, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("Catalog is too large");
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (bytes.Length + read > MaxBytes) throw new InvalidDataException("Catalog is too large");
                bytes.Write(buffer, 0, read);
            }
            string json = Encoding.UTF8.GetString(bytes.ToArray());
            var labels = Parse(json);
            var merged = new Dictionary<int, ForzaCarLabel>(Volatile.Read(ref _labels));
            foreach (var entry in labels) merged[entry.Key] = entry.Value;
            string saved = Newtonsoft.Json.JsonConvert.SerializeObject(merged.Select(entry => new
            {
                CarOrdinal = entry.Key, entry.Value.Make, entry.Value.Model, entry.Value.Year
            }));
            if (Encoding.UTF8.GetByteCount(saved) > MaxBytes) throw new InvalidDataException("Merged catalog is too large");
            string pending = cache + ".tmp";
            await File.WriteAllTextAsync(pending, saved, timeout.Token).ConfigureAwait(false);
            File.Move(pending, cache, overwrite: true);
            Apply(labels);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or
            OperationCanceledException or Newtonsoft.Json.JsonException or FormatException or OverflowException)
        {
            // Offline/invalid updates leave the bundled or last valid catalog available.
        }
        finally { _updateGate.Release(); }
    }
}
