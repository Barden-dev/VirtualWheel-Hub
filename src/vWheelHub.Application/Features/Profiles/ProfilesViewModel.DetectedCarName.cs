using Newtonsoft.Json.Linq;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Profiles;

public partial class ProfilesViewModel
{
    public bool CanEditDetectedCarName => IsForzaCarContext(CurrentContext);
    private static bool IsForzaCarContext(ProfileContext context) =>
        string.Equals(context.Game, "Forza Motorsport", StringComparison.OrdinalIgnoreCase) &&
        context.Car?.StartsWith("Forza car ", StringComparison.Ordinal) == true &&
        int.TryParse(context.Car.AsSpan(10), out int ordinal) && ordinal > 0;

    partial void OnCurrentContextChanged(ProfileContext value) => OnPropertyChanged(nameof(CanEditDetectedCarName));

    public event Action? ForzaCatalogChanged;

    public async Task RefreshForzaCatalogAsync(string cacheDirectory, CancellationToken ct = default)
    {
        using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
        using var client = new System.Net.Http.HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("vWheelHub/2.0");
        await ForzaCarCatalog.Default.RefreshAsync(cacheDirectory, client, ct);
        RefreshAvailableProfiles();
        OnPropertyChanged(nameof(ContextDisplayPath));
        ForzaCatalogChanged?.Invoke();
    }

    private string DisplayNameForDetectedCar(ProfileContext context)
    {
        if (!IsForzaCarContext(context)) return context.Car ?? string.Empty;
        try
        {
            string path = _profileManager.GetProfilePath(context);
            if (new FileInfo(path) is { Exists: true, Length: <= 1024 * 1024 })
            {
                string? name = JObject.Parse(File.ReadAllText(path))["Name"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(name) && name != context.Car) return name;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException) { }
        return DetectedProfileNames.Car(context, null) is { } known && known != context.Car
            ? known : string.Format(_localizationManager.GetString("Profiles_ForzaCarFallback"), context.Car![10..]);
    }
}

// Display labels only: never use them for storage paths, detection or inheritance.
internal static class DetectedProfileNames
{
    internal static string? Car(ProfileContext context, string? name)
    {
        if (string.IsNullOrEmpty(context.Car)) return context.Car;
        if (!string.Equals(context.Game, "Forza Motorsport", StringComparison.OrdinalIgnoreCase))
            return context.Car;
        if (!string.IsNullOrWhiteSpace(name) && name != context.Car) return name;
        return ForzaCarCatalog.Default.Find(context.Car)?.DisplayName ?? context.Car;
    }

    internal static string? Class(ProfileContext context) =>
        string.Equals(context.Game, "Forza Motorsport", StringComparison.OrdinalIgnoreCase)
            ? context.CarClass switch
            {
                "Forza class 0" => "E",
                "Forza class 1" => "D",
                "Forza class 2" => "C",
                "Forza class 3" => "B",
                "Forza class 4" => "A",
                "Forza class 5" => "S",
                "Forza class 6" => "R",
                "Forza class 7" => "P",
                _ => context.CarClass
            } : context.CarClass;

    internal static string Path(ProfileContext context, string? name = null) =>
        string.Join(" / ", new[] { context.Game,
            string.Equals(context.Game, "Forza Motorsport", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(context.Car)
                ? ForzaCarCatalog.Default.Find(context.Car)?.Make : Class(context), Car(context, name) }
            .Where(part => !string.IsNullOrEmpty(part)));
}
