using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace SimRacingHub.Avalonia.Dialogs
{
    public partial class UnsavedChangesDialog : Window
    {
        public UnsavedChangesDialog()
        {
            InitializeComponent();
        }

        public UnsavedChangesDialog(UnsavedChangesDialogViewModel viewModel) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            DataContext = viewModel;
            viewModel.RequestClose += result => Close(result);
        }

        public static async Task<UnsavedChangesDialogResult> ShowAsync(
            Window owner,
            string? profileName = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ct.ThrowIfCancellationRequested();

            var vm = new UnsavedChangesDialogViewModel(profileName, localizationManager: App.Services?.LocalizationManager);
            var dialog = new UnsavedChangesDialog(vm);

            using var ctr = ct.Register(() => dialog.Close(UnsavedChangesDialogResult.Cancel));
            var result = await dialog.ShowDialog<UnsavedChangesDialogResult>(owner).ConfigureAwait(true);
            return result;
        }
    }
}
