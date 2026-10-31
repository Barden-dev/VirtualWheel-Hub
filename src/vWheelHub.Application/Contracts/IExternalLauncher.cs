using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Service for launching external browser URIs and local file explorer directories.
    /// </summary>
    public interface IExternalLauncher
    {
        /// <summary>
        /// Opens a URI in the default web browser.
        /// </summary>
        Task<bool> OpenUriAsync(Uri uri, CancellationToken ct = default);

        /// <summary>
        /// Opens a folder path in the operating system's default file manager.
        /// </summary>
        Task<bool> OpenDirectoryAsync(string directoryPath, CancellationToken ct = default);
    }
}
