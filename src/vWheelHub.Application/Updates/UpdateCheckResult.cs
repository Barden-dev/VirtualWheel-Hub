namespace SimRacingHub.Application.Features.AppSettings;

// Preserve the existing namespace for callers of the update banner API.
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string LatestVersion,
    string CurrentVersion,
    string UpdateUrl);
