using System;
using System.Collections.Generic;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Configuration options for opening a file via system file picker.
    /// </summary>
    public sealed record FilePickerOpenOptions
    {
        public string Title { get; init; } = "Open File";
        public string? DefaultExtension { get; init; }
        public IReadOnlyList<FileFilterItem>? Filters { get; init; }
        public string? SuggestedFileName { get; init; }

        public FilePickerOpenOptions(
            string title = "Open File",
            string? defaultExtension = null,
            IReadOnlyList<FileFilterItem>? filters = null,
            string? suggestedFileName = null)
        {
            this.Title = title ?? "Open File";
            this.DefaultExtension = defaultExtension;
            this.Filters = filters;
            this.SuggestedFileName = suggestedFileName;
        }

        public FilePickerOpenOptions()
        {
        }
    }
}
