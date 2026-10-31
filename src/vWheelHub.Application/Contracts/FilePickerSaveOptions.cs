using System;
using System.Collections.Generic;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Configuration options for saving a file via system file picker.
    /// </summary>
    public sealed record FilePickerSaveOptions
    {
        public string Title { get; init; } = "Save File";
        public string? DefaultExtension { get; init; }
        public IReadOnlyList<FileFilterItem>? Filters { get; init; }
        public string? SuggestedFileName { get; init; }

        public FilePickerSaveOptions(
            string title = "Save File",
            string? defaultExtension = null,
            IReadOnlyList<FileFilterItem>? filters = null,
            string? suggestedFileName = null)
        {
            this.Title = title ?? "Save File";
            this.DefaultExtension = defaultExtension;
            this.Filters = filters;
            this.SuggestedFileName = suggestedFileName;
        }

        public FilePickerSaveOptions()
        {
        }
    }
}
