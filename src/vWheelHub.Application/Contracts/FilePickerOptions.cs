using System.Collections.Generic;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// General file picker options record, compatible with CONTRACTS.md.
    /// </summary>
    public sealed record FilePickerOptions(
        string Title,
        string? DefaultExtension = null,
        IReadOnlyList<FileFilterItem>? Filters = null,
        string? SuggestedFileName = null)
    {
        public FilePickerOpenOptions ToOpenOptions() =>
            new(Title, DefaultExtension, Filters, SuggestedFileName);

        public FilePickerSaveOptions ToSaveOptions() =>
            new(Title, DefaultExtension, Filters, SuggestedFileName);
    }
}
