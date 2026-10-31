using System;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimRacingHub.Application.Features.Profiles;
using SimRacingHub.Avalonia.Dialogs;

namespace SimRacingHub.Avalonia.Views.Profiles
{
    /// <summary>
    /// UserControl view for Profile Management, supporting creation, duplication, renaming, deletion,
    /// persistence (save/revert), and importing/exporting profiles via native dialogs and share codes.
    /// Bound to ProfilesViewModel.
    /// </summary>
    public partial class ProfilesView : UserControl
    {
        private bool _changingSelection;
        private ProfilesViewModel? _observedModel;
        public ProfilesView()
        {
            InitializeComponent();
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnProfileDragOver, handledEventsToo: true);
            AddHandler(DragDrop.DropEvent, OnProfileDrop, handledEventsToo: true);
            DataContextChanged += (_, _) => ObserveModel();
        }

        public ProfilesView(ProfilesViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Profiles != null)
            {
                DataContext = App.Services.Profiles;
            }
            ObserveModel();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_observedModel != null) _observedModel.PropertyChanged -= OnBrowserModelChanged;
            _observedModel = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void ObserveModel()
        {
            if (_observedModel != null) _observedModel.PropertyChanged -= OnBrowserModelChanged;
            _observedModel = DataContext as ProfilesViewModel;
            if (_observedModel != null) _observedModel.PropertyChanged += OnBrowserModelChanged;
        }
        private void OnBrowserModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfilesViewModel.SelectedBrowserRow))
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                { if (_observedModel?.SelectedBrowserRow is {} row) ProfilesListBox.ScrollIntoView(row); });
        }

        private void OnSearchFilterChanged(object? sender, TextChangedEventArgs e)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            { if (DataContext is ProfilesViewModel vm && vm.IsSearchingProfiles && vm.BrowserRows.Count > 0) ProfilesListBox.ScrollIntoView(vm.BrowserRows[0]); });
        }

        private void OnProfileGroupClicked(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ProfilesViewModel vm && sender is Control { DataContext: ProfileBrowserRow row }) vm.ToggleProfileGroup(row);
        }
        private void OnRevealSelectedClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ProfilesViewModel vm) return;
            vm.RevealSelectedProfile();
            if (vm.SelectedBrowserRow != null) ProfilesListBox.ScrollIntoView(vm.SelectedBrowserRow);
            ProfilesListBox.Focus();
        }
        private void OnOpenDifferenceSettings(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ProfilesViewModel vm && sender is Control { DataContext: ProfileDifferenceGroup group })
                vm.OpenDifferenceSettings(group);
        }
        private void OnBrowserKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not ProfilesViewModel vm) return;
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            { SearchFilterBox.Focus(); SearchFilterBox.SelectAll(); e.Handled = true; return; }
            if (e.Key == Key.Escape && vm.IsSearchingProfiles)
            { vm.ClearProfileSearch(); e.Handled = true; return; }
            if (SearchFilterBox.IsKeyboardFocusWithin || ProfilesListBox.SelectedItem is not ProfileBrowserRow row || vm.IsSearchingProfiles) return;
            if (e.Key == Key.Right && row.CanExpand && !row.IsExpanded || e.Key == Key.Left && row.CanExpand && row.IsExpanded)
            { vm.ToggleProfileGroup(row); e.Handled = true; }
            else if (e.Key == Key.Left && row.Depth > 0)
            {
                var parent = vm.BrowserRows.Take(vm.BrowserRows.IndexOf(row)).LastOrDefault(r => r.Depth < row.Depth);
                if (parent != null) ProfilesListBox.SelectedItem = parent;
                e.Handled = true;
            }
        }

        private async System.Threading.Tasks.Task<bool> ConfirmLeavingProfileAsync(ProfilesViewModel vm)
        {
            if (!vm.HasUnsavedChanges) return true;
            if (TopLevel.GetTopLevel(this) is not Window owner) return false;
            var choice = await UnsavedChangesDialog.ShowAsync(owner, vm.CurrentProfile.Name);
            if (choice == UnsavedChangesDialogResult.Cancel) return false;
            return choice != UnsavedChangesDialogResult.Save || vm.ProfileSession.SaveProfile(vm.CurrentContext);
        }

        private async void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_changingSelection && DataContext is ProfilesViewModel groupVm && ProfilesListBox?.SelectedItem is ProfileBrowserRow { IsNavigationGroup: true } group)
            {
                groupVm.ToggleProfileGroup(group);
                ProfilesListBox.SelectedItem = groupVm.SelectedBrowserRow;
                return;
            }
            if (_changingSelection || DataContext is not ProfilesViewModel vm || ProfilesListBox?.SelectedItem is not ProfileBrowserRow next || ReferenceEquals(next, vm.SelectedBrowserRow) || ReferenceEquals(next.Profile, vm.SelectedProfile)) return;
            _changingSelection = true;
            ProfilesListBox.IsEnabled = false;
            try
            {
                // Finish Avalonia's selection transaction before loading a profile can update the list.
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, global::Avalonia.Threading.DispatcherPriority.Background);
                if (!ReferenceEquals(DataContext, vm) || _observedModel != vm) return;
                if (await ConfirmLeavingProfileAsync(vm)) vm.SelectedProfile = next.Profile;
            }
            finally { ProfilesListBox.SelectedItem = vm.SelectedBrowserRow; ProfilesListBox.IsEnabled = true; _changingSelection = false; }
        }

        private async void OnNewProfileClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ProfilesViewModel vm || TopLevel.GetTopLevel(this) is not Window owner || !await ConfirmLeavingProfileAsync(vm)) return;
            var name = await new ProfileNameDialog(vm.LocalizationManager.GetString("Ui_NewProfile", "New profile")).ShowDialog<string?>(owner);
            if (!string.IsNullOrWhiteSpace(name)) await vm.CreateProfileAsync(name);
        }

        private async void OnDuplicateProfileClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ProfilesViewModel vm && await ConfirmLeavingProfileAsync(vm)) await vm.DuplicateProfileAsync();
        }

        private async void OnRenameProfileClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ProfilesViewModel vm || vm.SelectedProfile?.CanRename != true || TopLevel.GetTopLevel(this) is not Window owner) return;
            var name = await new ProfileNameDialog(vm.CurrentProfile.Name ?? vm.SelectedProfile.Name).ShowDialog<string?>(owner);
            if (!string.IsNullOrWhiteSpace(name)) await vm.RenameProfileAsync(name);
        }

        private async System.Threading.Tasks.Task ApplyImportDecisionAsync(ProfilesViewModel vm,
            ProfileImportDialogResult result, SimRacingHub.Models.ProfileSharePackage? package)
        {
            if (result == ProfileImportDialogResult.Cancel || package?.Settings == null || !await ConfirmLeavingProfileAsync(vm)) return;
            if (result == ProfileImportDialogResult.SaveToTargetSlot)
            {
                await vm.SaveImportedToTargetAsync(package);
            }
            else if (result == ProfileImportDialogResult.ApplyToCurrentContext)
            {
                var name = vm.CurrentProfile.Name;
                vm.CurrentProfile.CopyFrom(package.Settings);
                vm.CurrentProfile.Name = name;
                vm.SaveProfile();
            }
        }

        private bool _importingDrop;
        private void OnProfileDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = !_importingDrop && ProfileImportDialog.GetDroppedFile(e.DataTransfer) != null
                ? e.DragEffects & DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        private async void OnProfileDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            if (_importingDrop || DataContext is not ProfilesViewModel vm || TopLevel.GetTopLevel(this) is not Window owner ||
                ProfileImportDialog.GetDroppedFile(e.DataTransfer) is not {} file) return;
            _importingDrop = true;
            try
            {
                var (result, package) = await ProfileImportDialog.ShowAsync(owner, vm.CurrentContext,
                    vm.ClipboardService, vm.FilePickerService, initialFile: file);
                await ApplyImportDecisionAsync(vm, result, package);
            }
            finally { _importingDrop = false; }
        }

        private async void OnOpenImportDialogClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this) as Window;
            if (topLevel != null && DataContext is ProfilesViewModel vm)
            {
                var (result, package) = await ProfileImportDialog.ShowAsync(
                    topLevel,
                    vm.CurrentContext,
                    vm.ClipboardService,
                    vm.FilePickerService);

                await ApplyImportDecisionAsync(vm, result, package);
            }
        }

        private async void OnOpenExportDialogClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this) as Window;
            if (topLevel != null && DataContext is ProfilesViewModel vm)
            {
                await ProfileExportDialog.ShowAsync(
                    topLevel,
                    vm.CurrentProfile,
                    vm.SelectedProfile?.Context ?? vm.CurrentContext,
                    vm.ClipboardService,
                    vm.FilePickerService);
            }
        }
    }

    public sealed class ProfileDepthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => new Thickness(value is int depth ? depth * 16 : 0, 0, 0, 0);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
