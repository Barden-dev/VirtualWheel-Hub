using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// Cross-platform Avalonia implementation of <see cref="IExternalLauncher"/>
    /// for opening web browser URIs and file manager directories on Windows and Linux.
    /// </summary>
    public sealed class AvaloniaExternalLauncher : IExternalLauncher
    {
        /// <summary>
        /// Optional delegate to intercept URI launches for unit tests and headless environments.
        /// </summary>
        public Func<Uri, bool>? OpenUriHandler { get; set; }

        /// <summary>
        /// Optional delegate to intercept directory launches for unit tests and headless environments.
        /// </summary>
        public Func<string, bool>? OpenDirectoryHandler { get; set; }

        public Task<bool> OpenUriAsync(Uri uri, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ct.ThrowIfCancellationRequested();

            if (OpenUriHandler != null)
            {
                return Task.FromResult(OpenUriHandler(uri));
            }

            // Security guard: only allow http, https, and mailto schemes
            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeMailto)
            {
                AppLogger.Instance.LogWarning($"Refused to launch external URI with non-whitelisted scheme: {uri.Scheme}");
                return Task.FromResult(false);
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = uri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    using var proc = Process.Start(psi);
                    return Task.FromResult(true);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = $"\"{uri.AbsoluteUri}\"",
                            UseShellExecute = false
                        };
                        using var proc = Process.Start(psi);
                        return Task.FromResult(true);
                    }
                    catch
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = uri.AbsoluteUri,
                            UseShellExecute = true
                        };
                        using var proc = Process.Start(psi);
                        return Task.FromResult(true);
                    }
                }
                else
                {
                    // macOS or other fallback
                    var psi = new ProcessStartInfo
                    {
                        FileName = uri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    using var proc = Process.Start(psi);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to launch external URI '{uri}': {ex.Message}", ex);
                return Task.FromResult(false);
            }
        }

        public Task<bool> OpenDirectoryAsync(string directoryPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(false);
            }

            if (OpenDirectoryHandler != null)
            {
                return Task.FromResult(OpenDirectoryHandler(directoryPath));
            }

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    AppLogger.Instance.LogWarning($"Cannot open non-existent directory: {directoryPath}");
                    return Task.FromResult(false);
                }

                string fullPath = Path.GetFullPath(directoryPath);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{fullPath}\"",
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    return Task.FromResult(true);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = $"\"{fullPath}\"",
                            UseShellExecute = false
                        };
                        using var proc = Process.Start(psi);
                        return Task.FromResult(true);
                    }
                    catch
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true
                        };
                        using var proc = Process.Start(psi);
                        return Task.FromResult(true);
                    }
                }
                else
                {
                    // macOS open command
                    var psi = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{fullPath}\"",
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to open directory '{directoryPath}': {ex.Message}", ex);
                return Task.FromResult(false);
            }
        }
    }
}
