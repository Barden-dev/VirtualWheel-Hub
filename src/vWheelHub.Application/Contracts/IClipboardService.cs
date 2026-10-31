using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// System clipboard abstraction for text copy/paste operations (e.g. profile share codes).
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Reads plain text currently in the system clipboard.
        /// Returns null if clipboard is empty or does not contain text.
        /// </summary>
        Task<string?> GetTextAsync(CancellationToken ct = default);

        /// <summary>
        /// Writes plain text to the system clipboard.
        /// </summary>
        Task SetTextAsync(string text, CancellationToken ct = default);
    }
}
