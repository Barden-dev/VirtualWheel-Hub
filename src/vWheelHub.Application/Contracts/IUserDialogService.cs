using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Platform-agnostic dialog service for informational notices and user confirmations.
    /// </summary>
    public interface IUserDialogService
    {
        Task<bool> RunProgressAsync(DialogProgressRequest request,
            Func<CancellationToken, Task<bool>> operation, CancellationToken ct = default) => operation(ct);

        /// <summary>
        /// Displays an informational or alert message asynchronously.
        /// </summary>
        Task ShowNoticeAsync(DialogNoticeRequest request, CancellationToken ct = default);

        /// <summary>
        /// Displays a confirmation dialog asynchronously, returning user decision.
        /// </summary>
        Task<DialogResult> ConfirmAsync(DialogConfirmRequest request, CancellationToken ct = default);

        /// <summary>
        /// Convenience overload for displaying a notice with scalar arguments.
        /// </summary>
        Task ShowNoticeAsync(string title, string message, DialogSeverity severity = DialogSeverity.Info, CancellationToken ct = default);

        /// <summary>
        /// Convenience overload for requesting a binary confirmation with scalar arguments.
        /// Returns true if confirmed, false if cancelled or dismissed.
        /// </summary>
        Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel", DialogSeverity severity = DialogSeverity.Warning, CancellationToken ct = default);
        Task<bool> ShowConfirmAsync(string title, string message, string confirmText, string cancelText, CancellationToken ct);
    }
}
