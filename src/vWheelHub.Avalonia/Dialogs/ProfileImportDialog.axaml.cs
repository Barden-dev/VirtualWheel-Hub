using System;
using System.Linq;
using System.IO;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;

namespace SimRacingHub.Avalonia.Dialogs
{
    public partial class ProfileImportDialog : Window
    {
        public ProfileImportDialog()
        {
            InitializeComponent();
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
            AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
        }

        public ProfileImportDialog(ProfileImportDialogViewModel viewModel) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            DataContext = viewModel;
            viewModel.RequestClose += result => Close(result);
        }

        internal static PickedFile? GetDroppedFile(IDataTransfer data)
        {
            var items = data.TryGetFiles()?.Take(2).ToArray();
            if (items is not { Length: 1 } || items[0] is not IStorageFile file) return null;
            var extension = Path.GetExtension(file.Name);
            if (!new[] { ".json", ".vwhub", ".vwh" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return null;
            return new PickedFile { FileName = file.Name, OpenReadAsync = () => file.OpenReadAsync() };
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedFile(e.DataTransfer) != null ? e.DragEffects & DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ProfileImportDialogViewModel vm && GetDroppedFile(e.DataTransfer) is {} file)
                await vm.LoadFileAsync(file);
        }

        public static async Task<(ProfileImportDialogResult Result, ProfileSharePackage? Package)> ShowAsync(
            Window owner,
            ProfileContext? currentContext,
            IClipboardService clipboardService,
            IFilePickerService filePickerService,
            ProfileSharePackage? initialPackage = null,
            CancellationToken ct = default,
            PickedFile? initialFile = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(clipboardService);
            ArgumentNullException.ThrowIfNull(filePickerService);
            ct.ThrowIfCancellationRequested();

            var vm = new ProfileImportDialogViewModel(
                currentContext,
                clipboardService,
                filePickerService,
                localizationManager: App.Services?.LocalizationManager,
                initialPackage: initialPackage);

            if (initialFile != null) await vm.LoadFileAsync(initialFile);
            var dialog = new ProfileImportDialog(vm);

            using var ctr = ct.Register(() => dialog.Close(ProfileImportDialogResult.Cancel));
            var result = await dialog.ShowDialog<ProfileImportDialogResult>(owner).ConfigureAwait(true);
            return (result, vm.LoadedPackage);
        }
    }
}
