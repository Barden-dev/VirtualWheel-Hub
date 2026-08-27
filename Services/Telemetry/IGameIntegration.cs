using System.Collections.Generic;

namespace SimRacingHub.Services.Telemetry
{
    public interface IGameIntegration
    {
        string GameId { get; }
        string DisplayName { get; }
        IGameDetector Detector { get; }
        IReadOnlyCollection<FeatureDescriptor> Features { get; }
    }

    public sealed record FeatureDescriptor(
        string Id,
        string DisplayName,
        bool IsSupported,
        string? UnavailableReason = null);
}
