using System;
using System.Collections.Generic;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// File extension filter entry for file picker dialogs.
    /// </summary>
    public sealed record FileFilterItem
    {
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

        public FileFilterItem(string name, IReadOnlyList<string> extensions)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        }

        public FileFilterItem()
        {
        }
    }
}
