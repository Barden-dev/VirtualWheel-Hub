using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// Avalonia UI thread dispatcher adapter implementing <see cref="IUiDispatcher"/>.
    /// </summary>
    public sealed class AvaloniaUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
                {
                    return true;
                }

                return Dispatcher.UIThread.CheckAccess();
            }
            catch
            {
                return true;
            }
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (CheckAccess())
            {
                action();
                return;
            }

            try
            {
                Dispatcher.UIThread.Post(action);
            }
            catch
            {
                action();
            }
        }

        public async Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            if (CheckAccess())
            {
                action();
                return;
            }

            var op = Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
            var completed = await Task.WhenAny(op.GetTask(), Task.Delay(250, cancellationToken)).ConfigureAwait(false);
            if (completed == op.GetTask())
            {
                await op.GetTask().ConfigureAwait(false);
            }
            else
            {
                // Fallback for environments without an active dispatcher pumping loop
                action();
            }
        }

        public async Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(callback);
            cancellationToken.ThrowIfCancellationRequested();

            if (CheckAccess())
            {
                return callback();
            }

            var op = Dispatcher.UIThread.InvokeAsync(callback, DispatcherPriority.Normal, cancellationToken);
            var completed = await Task.WhenAny(op.GetTask(), Task.Delay(250, cancellationToken)).ConfigureAwait(false);
            if (completed == op.GetTask())
            {
                return await op.GetTask().ConfigureAwait(false);
            }
            else
            {
                // Fallback for environments without an active dispatcher pumping loop
                return callback();
            }
        }
    }
}
