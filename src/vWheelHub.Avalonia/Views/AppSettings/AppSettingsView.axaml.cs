using Avalonia;
using Avalonia.Controls;
using SimRacingHub.Application.Features.AppSettings;

namespace SimRacingHub.Avalonia.Views.AppSettings
{
    /// <summary>
    /// UserControl view for application settings, language switching, system autostart,
    /// update checker, learning cache management, simulator setup, and external links.
    /// Bound to AppSettingsViewModel.
    /// </summary>
    public partial class AppSettingsView : UserControl
    {
        public AppSettingsView()
        {
            InitializeComponent();
        }

        public AppSettingsView(AppSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void LanguageSelectorOpened(object? sender, EventArgs e)
        {
            if (DataContext is AppSettingsViewModel vm) vm.RefreshLanguageChoices();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.AppSettings != null)
            {
                DataContext = App.Services.AppSettings;
            }
        }
    }
}
