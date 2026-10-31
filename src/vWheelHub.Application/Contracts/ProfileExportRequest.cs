using System;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Parameters for exporting an active profile to a file or share code.
    /// </summary>
    public sealed record ProfileExportRequest
    {
        public Profile Profile { get; init; } = new Profile();
        public ProfileMetadata? Metadata { get; init; }
        public string? DestinationFilePath { get; init; }
        public bool GenerateShareCode { get; init; }

        public ProfileExportRequest(
            Profile profile,
            ProfileMetadata? metadata = null,
            string? destinationFilePath = null,
            bool generateShareCode = false)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Metadata = metadata;
            DestinationFilePath = destinationFilePath;
            GenerateShareCode = generateShareCode;
        }

        public ProfileExportRequest()
        {
        }
    }
}
