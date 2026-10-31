using System;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Metadata header associated with a shared or exported profile.
    /// </summary>
    public sealed record ProfileMetadata(
        string Name = "",
        string Author = "",
        string Description = "",
        DateTime? CreatedAtUtc = null);
}
