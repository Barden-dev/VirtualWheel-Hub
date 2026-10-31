using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Avalonia.Dialogs;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters
{
    public class AvaloniaUserDialogService : IUserDialogService
    {
        private readonly IUiDispatcher _dispatcher;
        private readonly Func<Window?>? _windowProvider;
        private readonly List<DialogNoticeRequest> _recordedNotices = new();
        private readonly List<DialogConfirmRequest> _recordedConfirms = new();
        public IReadOnlyList<DialogNoticeRequest> RecordedNotices => _recordedNotices;
        public IReadOnlyList<DialogConfirmRequest> RecordedConfirms => _recordedConfirms;
        public Func<DialogNoticeRequest, Task>? NoticeHandler { get; set; }
        public Func<DialogConfirmRequest, Task<DialogResult>>? AsyncConfirmHandler { get; set; }

        public Func<DialogConfirmRequest, DialogResult>? ConfirmHandler
        {
            get => null;
            set
            {
                if (value != null)
                {
                    AsyncConfirmHandler = req => Task.FromResult(value(req));
                }
                else
                {
                    AsyncConfirmHandler = null;
                }
            }
        }

        public AvaloniaUserDialogService(IUiDispatcher? dispatcher = null, Func<Window?>? windowProvider = null)
        {
            _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
            _windowProvider = windowProvider;
        }

        public async Task ShowNoticeAsync(DialogNoticeRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();
            _recordedNotices.Add(request);
            AppLogger.Instance.LogInfo($"[Dialog Notice] [{request.Severity}] {request.Title}: {request.Message}");
            if (NoticeHandler != null)
            {
                await NoticeHandler(request).ConfigureAwait(false);
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                var owner = ResolveOwnerWindow();
                if (owner != null)
                {
                    await MessageDialog.ShowNoticeAsync(owner, request, ct).ConfigureAwait(true);
                }
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() =>
                {
                    var owner = ResolveOwnerWindow();
                    return owner != null ? MessageDialog.ShowNoticeAsync(owner, request, ct) : Task.CompletedTask;
                }, ct).ConfigureAwait(false);
                await task.ConfigureAwait(false);
            }
        }

        public async Task<DialogResult> ConfirmAsync(DialogConfirmRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();
            _recordedConfirms.Add(request);
            AppLogger.Instance.LogInfo($"[Dialog Confirm] [{request.Severity}] {request.Title}: {request.Message}");
            if (AsyncConfirmHandler != null)
            {
                return await AsyncConfirmHandler(request).ConfigureAwait(false);
            }

            if (_dispatcher.CheckAccess())
            {
                var owner = ResolveOwnerWindow();
                if (owner != null)
                {
                    return await MessageDialog.ShowConfirmAsync(owner, request, ct).ConfigureAwait(true);
                }

                return DialogResult.Confirmed;
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() =>
                {
                    var owner = ResolveOwnerWindow();
                    return owner != null ? MessageDialog.ShowConfirmAsync(owner, request, ct) : Task.FromResult(DialogResult.Confirmed);
                }, ct).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }
        }

        public Task ShowNoticeAsync(string title, string message, DialogSeverity severity = DialogSeverity.Info, CancellationToken ct = default)
        {
            return ShowNoticeAsync(new DialogNoticeRequest(title, message, severity), ct);
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel", DialogSeverity severity = DialogSeverity.Warning, CancellationToken ct = default)
        {
            var result = await ConfirmAsync(new DialogConfirmRequest(title, message, confirmText, cancelText, severity), ct).ConfigureAwait(false);
            return result == DialogResult.Confirmed;
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText, CancellationToken ct)
        {
            return ShowConfirmAsync(title, message, confirmText, cancelText, DialogSeverity.Warning, ct);
        }

        private Window? ResolveOwnerWindow()
        {
            try
            {
                if (_windowProvider != null)
                {
                    var custom = _windowProvider();
                    if (custom != null)
                        return custom;
                }

                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible) ?? desktop.MainWindow;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Failed to resolve owner window for dialog: {ex.Message}");
            }

            return null;
        }
    }
}
