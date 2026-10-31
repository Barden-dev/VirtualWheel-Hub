using System;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Request parameters for prompting user confirmation.
    /// </summary>
    public sealed record DialogConfirmRequest
    {
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string ConfirmText { get; init; } = "OK";
        public string CancelText { get; init; } = "Cancel";
        public DialogSeverity Severity { get; init; } = DialogSeverity.Warning;

        public DialogConfirmRequest(
            string title,
            string message,
            string confirmText = "OK",
            string cancelText = "Cancel",
            DialogSeverity severity = DialogSeverity.Warning)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ConfirmText = confirmText ?? "OK";
            CancelText = cancelText ?? "Cancel";
            Severity = severity;
        }

        public DialogConfirmRequest()
        {
        }
    }
}
