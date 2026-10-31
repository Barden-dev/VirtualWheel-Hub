using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Application.Common
{
    /// <summary>
    /// Base class for application view models, providing observable object capabilities and busy indicators.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        private bool _isBusy;
        private string? _busyMessage;

        /// <summary>
        /// Gets a value indicating whether the view model is currently performing an asynchronous operation.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            protected set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Gets an optional message describing the current busy operation.
        /// </summary>
        public string? BusyMessage
        {
            get => _busyMessage;
            protected set => SetProperty(ref _busyMessage, value);
        }

        /// <summary>
        /// Updates the busy state and optional descriptive message.
        /// </summary>
        protected void SetBusy(bool isBusy, string? message = null)
        {
            IsBusy = isBusy;
            BusyMessage = isBusy ? message : null;
        }
    }
}
