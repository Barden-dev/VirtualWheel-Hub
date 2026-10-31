using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts;

/// <summary>Read-only input access probe; never opens a capture session.</summary>
public interface IInputReadinessDiagnostics
{
    Task<InputReadinessSnapshot> CheckInputAsync(CancellationToken ct = default);
}

public sealed record InputReadinessSnapshot(bool IsAvailable, string StatusText,
    string? ErrorDetails = null);
