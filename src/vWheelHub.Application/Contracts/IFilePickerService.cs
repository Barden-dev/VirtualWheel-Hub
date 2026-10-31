using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// File picker service abstracting system open/save file dialogs.
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// Prompts the user to select an existing file to open.
        /// Returns null if cancelled.
        /// </summary>
        Task<PickedFile?> OpenFileAsync(FilePickerOpenOptions options, CancellationToken ct = default);

        /// <summary>
        /// Prompts the user to specify a file destination to save.
        /// Returns null if cancelled.
        /// </summary>
        Task<PickedFile?> SaveFileAsync(FilePickerSaveOptions options, CancellationToken ct = default);

        /// <summary>
        /// Convenience overload accepting general FilePickerOptions.
        /// </summary>
        Task<PickedFile?> OpenFileAsync(FilePickerOptions options, CancellationToken ct = default);

        /// <summary>
        /// Convenience overload accepting general FilePickerOptions.
        /// </summary>
        Task<PickedFile?> SaveFileAsync(FilePickerOptions options, CancellationToken ct = default);
    }
}
