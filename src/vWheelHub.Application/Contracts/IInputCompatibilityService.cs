using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts;

public interface IInputCompatibilityService
{
    Task<InputCompatibilityStatus> CheckAsync(CancellationToken ct = default);
    Task<bool> ApplyAsync(CancellationToken ct = default);
}
