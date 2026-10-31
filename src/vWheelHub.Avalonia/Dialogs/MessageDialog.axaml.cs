using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Dialogs
{
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
        }

        public MessageDialog(MessageDialogViewModel viewModel) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            DataContext = viewModel;
            viewModel.RequestClose += result => Close(result);
        }

        public static async Task ShowNoticeAsync(Window owner, DialogNoticeRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();

            var vm = new MessageDialogViewModel(request);
            var dialog = new MessageDialog(vm);

            using var ctr = ct.Register(() => dialog.Close(DialogResult.Cancelled));
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }

        public static async Task<DialogResult> ShowConfirmAsync(Window owner, DialogConfirmRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();

            var vm = new MessageDialogViewModel(confirm: request);
            var dialog = new MessageDialog(vm);

            using var ctr = ct.Register(() => dialog.Close(DialogResult.Cancelled));
            var result = await dialog.ShowDialog<DialogResult>(owner).ConfigureAwait(true);
            return result;
        }
    }
}
