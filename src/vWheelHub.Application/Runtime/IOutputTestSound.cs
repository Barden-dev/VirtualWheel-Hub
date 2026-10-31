using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Runtime;

/// <summary>UI cues for output assignment, independent of driving audio features.</summary>
public interface IOutputTestSound
{
    bool IsAvailable { get; }
    Task PlayAsync(int frequency, int duration, CancellationToken ct);
}
