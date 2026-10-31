using System;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Request parameters for displaying an informational or alert notice.
    /// </summary>
    public sealed record DialogNoticeRequest
    {
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public DialogSeverity Severity { get; init; } = DialogSeverity.Info;

        public DialogNoticeRequest(string title, string message, DialogSeverity severity = DialogSeverity.Info)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Severity = severity;
        }

        public DialogNoticeRequest()
        {
        }
    }
}
