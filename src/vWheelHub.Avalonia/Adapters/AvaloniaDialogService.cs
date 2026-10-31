using System;
using Avalonia.Controls;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// User dialog service for Avalonia host.
    /// Inherits from <see cref="AvaloniaUserDialogService"/> for backward compatibility.
    /// </summary>
    public class AvaloniaDialogService : AvaloniaUserDialogService
    {
        public AvaloniaDialogService(
            IUiDispatcher? dispatcher = null,
            Func<Window?>? windowProvider = null)
            : base(dispatcher, windowProvider)
        {
        }
    }
}
