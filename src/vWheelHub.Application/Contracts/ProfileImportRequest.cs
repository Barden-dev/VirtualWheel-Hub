namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Parameters for importing a profile from a file path or serialized share-code string.
    /// </summary>
    public sealed record ProfileImportRequest(
        string? SourceFilePath = null,
        string? ShareCode = null,
        string? TargetGame = null,
        string? TargetCarClass = null,
        string? TargetCarName = null);
}
