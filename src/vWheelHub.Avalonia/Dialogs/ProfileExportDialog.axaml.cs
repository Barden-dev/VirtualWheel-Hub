using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;

namespace SimRacingHub.Avalonia.Dialogs
{
    public partial class ProfileExportDialog : Window
    {
        public ProfileExportDialog()
        {
            InitializeComponent();
        }

        public ProfileExportDialog(ProfileExportDialogViewModel viewModel) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
        }

        public static async Task ShowAsync(
            Window owner,
            Profile profile,
            ProfileContext context,
            IClipboardService clipboardService,
            IFilePickerService filePickerService,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(clipboardService);
            ArgumentNullException.ThrowIfNull(filePickerService);
            ct.ThrowIfCancellationRequested();

            var vm = new ProfileExportDialogViewModel(profile, context, clipboardService, filePickerService, App.Services?.LocalizationManager);
            var dialog = new ProfileExportDialog(vm);

            using var ctr = ct.Register(() => dialog.Close());
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }
    }
}
