namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Metadata describing the host application executable.
    /// Injected by the host executable so common assemblies do not query executing assembly.
    /// </summary>
    public interface IAppMetadata
    {
        /// <summary>
        /// Display name of the application.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Version string (e.g. "1.1.0").
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Application edition (e.g. "Community", "Pro", "Linux Native").
        /// </summary>
        string Edition { get; }

        /// <summary>
        /// Copyright and legal attribution string.
        /// </summary>
        string Copyright { get; }

        /// <summary>
        /// Convenience alias matching CONTRACTS.md.
        /// </summary>
        string AppName => ApplicationName;
    }
}
