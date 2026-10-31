using SimRacingHub.Application.Contracts;
using SimRacingHub.Services;

namespace SimRacingHub.Windows;

public sealed class WindowsInputCompatibilityService : IInputCompatibilityService
{
    private readonly LmuInputFixService _input = new();
    public Task<InputCompatibilityStatus> CheckAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        var state = _input.Check("LMU");
        return new InputCompatibilityStatus(state.IsAvailable, state.IsEnabled);
    }, ct);
    public Task<bool> ApplyAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        return _input.Apply().Success;
    }, ct);
}
