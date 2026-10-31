using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes;

public sealed class FakeInputCompatibilityService : IInputCompatibilityService
{
    public bool IsApplied { get; private set; }
    public Task<InputCompatibilityStatus> CheckAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new InputCompatibilityStatus(true, IsApplied));
    }
    public Task<bool> ApplyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IsApplied = true;
        return Task.FromResult(true);
    }
}
