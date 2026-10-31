using SimRacingHub.Application.Features.AppSettings;

namespace SimRacingHub.Application.Updates;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult?> CheckAsync(string currentVersion, string updateUrl, CancellationToken ct = default);
}
