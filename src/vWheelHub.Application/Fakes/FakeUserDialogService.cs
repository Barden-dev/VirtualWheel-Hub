using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IUserDialogService with response queues, request capture, and cancellation support.
    /// </summary>
    public sealed class FakeUserDialogService : IUserDialogService
    {
        private readonly List<DialogNoticeRequest> _recordedNotices = new();
        private readonly List<DialogConfirmRequest> _recordedConfirms = new();
        public List<DialogProgressRequest> RecordedProgress { get; } = new();

        public async Task<bool> RunProgressAsync(DialogProgressRequest request,
            Func<CancellationToken, Task<bool>> operation, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            RecordedProgress.Add(request);
            return await operation(ct).ConfigureAwait(false);
        }
        private readonly Queue<DialogResult> _enqueuedConfirmResults = new();

        public DialogResult DefaultConfirmResult { get; set; } = DialogResult.Confirmed;

        public int ShowNoticeCallCount { get; private set; }
        public int ConfirmCallCount { get; private set; }

        public IReadOnlyList<DialogNoticeRequest> RecordedNotices => _recordedNotices;
        public IReadOnlyList<DialogConfirmRequest> RecordedConfirms => _recordedConfirms;

        public void EnqueueConfirmResult(DialogResult result) => _enqueuedConfirmResults.Enqueue(result);

        public Task ShowNoticeAsync(DialogNoticeRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();

            ShowNoticeCallCount++;
            _recordedNotices.Add(request);

            return Task.CompletedTask;
        }

        public Task<DialogResult> ConfirmAsync(DialogConfirmRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();

            ConfirmCallCount++;
            _recordedConfirms.Add(request);

            DialogResult result = _enqueuedConfirmResults.Count > 0
                ? _enqueuedConfirmResults.Dequeue()
                : DefaultConfirmResult;

            return Task.FromResult(result);
        }

        public Task ShowNoticeAsync(string title, string message, DialogSeverity severity = DialogSeverity.Info, CancellationToken ct = default)
        {
            return ShowNoticeAsync(new DialogNoticeRequest(title, message, severity), ct);
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel", DialogSeverity severity = DialogSeverity.Warning, CancellationToken ct = default)
        {
            var result = await ConfirmAsync(new DialogConfirmRequest(title, message, confirmText, cancelText, severity), ct);
            return result == DialogResult.Confirmed;
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText, CancellationToken ct)
        {
            return ShowConfirmAsync(title, message, confirmText, cancelText, DialogSeverity.Warning, ct);
        }

        public void Reset()
        {
            ShowNoticeCallCount = 0;
            ConfirmCallCount = 0;
            _recordedNotices.Clear();
            _recordedConfirms.Clear();
            _enqueuedConfirmResults.Clear();
        }
    }
}
