using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// Avalonia implementation of <see cref="IClipboardService"/> using <see cref="IClipboard"/>
    /// with retry backoff for transient concurrency errors and cancellation support.
    /// </summary>
    public sealed class AvaloniaClipboardService : IClipboardService
    {
        private const int MaxAttempts = 5;
        private const int BaseBackoffMs = 50;

        private readonly IUiDispatcher _dispatcher;
        private readonly Func<IClipboard?>? _clipboardResolver;

        /// <summary>
        /// Optional delegate to intercept clipboard read for unit testing.
        /// </summary>
        public Func<Task<string?>>? ClipboardGetter { get; set; }

        /// <summary>
        /// Optional delegate to intercept clipboard write for unit testing.
        /// </summary>
        public Func<string, Task>? ClipboardSetter { get; set; }

        public AvaloniaClipboardService(
            IUiDispatcher? dispatcher = null,
            Func<IClipboard?>? clipboardResolver = null)
        {
            _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
            _clipboardResolver = clipboardResolver;
        }

        public async Task<string?> GetTextAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (ClipboardGetter != null)
            {
                return await ClipboardGetter().ConfigureAwait(false);
            }

            if (_dispatcher.CheckAccess())
            {
                return await ReadClipboardCoreAsync(ct).ConfigureAwait(false);
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() => ReadClipboardCoreAsync(ct), ct).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }
        }

        private async Task<string?> ReadClipboardCoreAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var clipboard = _clipboardResolver?.Invoke() ?? ResolveClipboard();
            if (clipboard == null)
            {
                AppLogger.Instance.LogWarning("System clipboard is not accessible on this platform/window.");
                return null;
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    return await clipboard.TryGetTextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < MaxAttempts)
                {
                    await Task.Delay(BaseBackoffMs * attempt, ct).ConfigureAwait(false);
                }
            }

            return await clipboard.TryGetTextAsync().ConfigureAwait(false);
        }

        public async Task SetTextAsync(string text, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ct.ThrowIfCancellationRequested();

            if (ClipboardSetter != null)
            {
                await ClipboardSetter(text).ConfigureAwait(false);
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                await WriteClipboardCoreAsync(text, ct).ConfigureAwait(false);
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() => WriteClipboardCoreAsync(text, ct), ct).ConfigureAwait(false);
                await task.ConfigureAwait(false);
            }
        }

        private async Task WriteClipboardCoreAsync(string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var clipboard = _clipboardResolver?.Invoke() ?? ResolveClipboard();
            if (clipboard == null)
            {
                AppLogger.Instance.LogWarning("System clipboard is not accessible on this platform/window.");
                return;
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await clipboard.SetTextAsync(text).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < MaxAttempts)
                {
                    await Task.Delay(BaseBackoffMs * attempt, ct).ConfigureAwait(false);
                }
            }

            await clipboard.SetTextAsync(text).ConfigureAwait(false);
        }

        private static IClipboard? ResolveClipboard()
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible) ?? desktop.MainWindow;
                    if (activeWindow?.Clipboard != null)
                    {
                        return activeWindow.Clipboard;
                    }

                    if (desktop.MainWindow?.Clipboard != null)
                    {
                        return desktop.MainWindow.Clipboard;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Failed to resolve TopLevel clipboard: {ex.Message}");
            }

            return null;
        }

        private static bool IsTransientException(Exception ex)
        {
            return ex is ExternalException ||
                   ex is TimeoutException ||
                   ex is InvalidOperationException;
        }
    }
}
