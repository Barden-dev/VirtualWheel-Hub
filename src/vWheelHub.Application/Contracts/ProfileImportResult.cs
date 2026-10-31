using SimRacingHub.Models;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Result outcome of importing a profile.
    /// </summary>
    public sealed record ProfileImportResult(
        bool Success,
        Profile? ImportedProfile = null,
        ProfileMetadata? Metadata = null,
        ProfileSharePackage? Package = null,
        string? ErrorMessage = null)
    {
        public static ProfileImportResult Succeeded(Profile profile, ProfileMetadata? metadata = null, ProfileSharePackage? package = null) =>
            new(true, profile, metadata, package);

        public static ProfileImportResult Failed(string errorMessage) =>
            new(false, ErrorMessage: errorMessage);
    }
}
