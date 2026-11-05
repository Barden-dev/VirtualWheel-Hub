using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Avalonia.Adapters;

namespace SimRacingHub.Avalonia
{
    public partial class MainWindow : Window
    {
        private bool _sidebarCollapsed;
        private readonly global::Avalonia.Threading.DispatcherTimer _sidebarAnimation = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly System.Diagnostics.Stopwatch _sidebarFrameClock = new();
        private double _sidebarStart, _sidebarTarget, _sidebarProgress;
        private void SidebarToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var workspace = this.FindControl<Grid>("ShellWorkspace")!;
            _sidebarStart = workspace.ColumnDefinitions[0].ActualWidth;
            _sidebarCollapsed = !_sidebarCollapsed;
            _sidebarTarget = _sidebarCollapsed ? 56 : 185;
            _sidebarProgress = 0;
            Classes.Set("sidebar-collapsed", _sidebarCollapsed);
            _sidebarFrameClock.Restart();
            _sidebarAnimation.Start();
        }
        private void SidebarAnimation_Tick(object? sender, EventArgs e)
        {
            // Cap advancement after a slow layout so the first visible frame cannot skip the motion.
            double elapsed = Math.Min(_sidebarFrameClock.Elapsed.TotalMilliseconds, 32);
            _sidebarFrameClock.Restart();
            _sidebarProgress = Math.Min(1, _sidebarProgress + elapsed / 180);
            double eased = 1 - Math.Pow(1 - _sidebarProgress, 3);
            double width = _sidebarStart + (_sidebarTarget - _sidebarStart) * eased;
            this.FindControl<Grid>("ShellWorkspace")!.ColumnDefinitions[0].Width = new GridLength(width);
            if (_sidebarProgress >= 1) { _sidebarAnimation.Stop(); _sidebarFrameClock.Stop(); }
        }
        private AppShellViewModel? _localizedModel;
        private string? _stringsCulture;
        private bool _closeApproved, _closePromptActive, _exitRequested, _windowInitialized;
        public DesktopWindowController? DesktopBehavior { get; set; }
        public Func<System.Threading.Tasks.Task<Dialogs.UnsavedChangesDialogResult>>? UnsavedChangesPrompt { get; set; }
        public void RequestAppExit()
        {
            DesktopBehavior?.RestoreWindow();
            _exitRequested = true;
            Close();
        }
        public MainWindow()
        {
            InitializeComponent();
            _sidebarAnimation.Tick += SidebarAnimation_Tick;
            Closed += (_, _) => _sidebarAnimation.Stop();
            InitializeGettingStarted();
            DataContextChanged += (_, _) =>
            {
                if (_localizedModel != null)
                    _localizedModel.PropertyChanged -= LocalizationChanged;
                _localizedModel = DataContext as AppShellViewModel;
                _stringsCulture = null;
                if (_localizedModel != null)
                {
                    _localizedModel.PropertyChanged += LocalizationChanged;
                    SyncWindowStrings();
                }
            };
            Closed += (_, _) =>
            {
                if (_localizedModel != null)
                    _localizedModel.PropertyChanged -= LocalizationChanged;
            };
            Opened += async (_, _) =>
            {
                if (_windowInitialized) return;
                _windowInitialized = true;
                if (DataContext is AppShellViewModel vm)
                {
                    await vm.InitializeAsync().ConfigureAwait(true);
                    SimRacingHub.Core.AppLogger.Instance.LogInfo("Application window opened", showInStatusBar: false);
                    SimRacingHub.Core.AppLogger.Instance.Flush();
                    if (App.Services is { IsPreviewMode: false, DesktopSession: { } catalogSession } catalogServices &&
                        global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                        catalogSession.ForzaDetected = () =>
                        {
                            _ = vm.Profiles?.RefreshForzaCatalogAsync(System.IO.Path.Combine(catalogServices.Storage.LocalDataDirectory, "Cache", "Forza"));
                        };
                    if (App.Services?.DesktopSession is { } desktopSession)
                        desktopSession.IsInteractionBlocked = () => _closePromptActive || IsGettingStartedOpen || !IsEnabled;
                    if (App.Services is { IsPreviewMode: false } && vm.AppSettings?.HasSeenWelcome == false &&
                        global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                        await ShowGettingStartedAsync();
                    if (_exitRequested || _closeApproved) return;
                    App.Services?.DesktopSession?.Start();
                }

                if (App.Services != null)
                {
                    var restored = AvaloniaWindowStateService.RestoreWindowState(this, App.Services.Storage.WindowSettingsPath, out _,
                        applyWindowState: DesktopBehavior?.StartMinimizedRequested != true);
                    DesktopBehavior?.SetRestoreState(restored);
                }

                if (DataContext is AppShellViewModel model && model.Dashboard?.Readiness is { } readiness)
                    await readiness.CheckReadinessAsync();
                if (DesktopBehavior != null) await DesktopBehavior.StartAsync();
            };
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_closeApproved || DataContext is not AppShellViewModel vm)
                return;
            if (!_exitRequested && DesktopBehavior?.TryCloseToTray(e) == true) return;
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime && UnsavedChangesPrompt == null)
            {
                await vm.ShutdownAsync();
                return;
            }

            e.Cancel = true;
            if (_closePromptActive)
                return;
            _closePromptActive = true;
            try
            {
                if (vm.HasUnsavedChanges)
                {
                    var choice = UnsavedChangesPrompt != null ? await UnsavedChangesPrompt() : await Dialogs.UnsavedChangesDialog.ShowAsync(this, vm.ProfileName);
                    if (choice == Dialogs.UnsavedChangesDialogResult.Cancel)
                        return;
                    if (choice == Dialogs.UnsavedChangesDialogResult.Save && !await vm.ExecuteSaveProfileAsync())
                        return;
                }

                if (App.Services != null)
                    AvaloniaWindowStateService.SaveWindowState(this, App.Services.Storage.WindowSettingsPath);
                App.Services?.DesktopSession?.Dispose();
                await vm.ShutdownAsync();
                _closeApproved = true;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(Close);
            }
            finally
            {
                _closePromptActive = false;
                if (!_closeApproved) _exitRequested = false;
            }
        }

        private async void HubToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not AppShellViewModel vm || sender is not ToggleSwitch toggle)
                return;
            toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, vm.IsHubRunning);
            toggle.IsEnabled = false;
            try
            {
                await vm.ToggleHubAsync();
            }
            finally
            {
                toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, vm.IsHubRunning);
                toggle.IsEnabled = true;
            }
        }

        private void MouseLockToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not AppShellViewModel vm || sender is not ToggleSwitch toggle)
                return;
            vm.Dashboard?.ToggleLockCommand.Execute(null);
            if (vm.Dashboard != null)
                vm.IsLocked = vm.Dashboard.IsLocked;
            toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, vm.IsLocked);
        }

        private void TitleLanguageButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not Button button || DataContext is not AppShellViewModel vm || vm.AppSettings is not { } settings) return;
            settings.RefreshLanguageChoices();
            var menu = new MenuFlyout
            {
                Placement = global::Avalonia.Controls.PlacementMode.BottomEdgeAlignedRight,
                VerticalOffset = 6,
                FlyoutPresenterTheme = (global::Avalonia.Styling.ControlTheme)this.FindResource("HubLanguageMenu")!
            };
            foreach (var language in settings.AvailableLanguages)
            {
                var item = new MenuItem
                {
                    Header = language.DisplayName,
                    Tag = language.CultureCode,
                    ToggleType = MenuItemToggleType.Radio,
                    GroupName = "InterfaceLanguage",
                    IsChecked = string.Equals(settings.SelectedLanguage, language.CultureCode, StringComparison.OrdinalIgnoreCase),
                    Theme = (global::Avalonia.Styling.ControlTheme)this.FindResource("HubLanguageMenuItem")!
                };
                item.Click += (_, _) => settings.SelectedLanguageInfo = language;
                menu.Items.Add(item);
            }
            global::Avalonia.Controls.Primitives.FlyoutBase.SetAttachedFlyout(button, menu);
            menu.ShowAt(button);
        }

        private void LocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.CurrentCultureCode))
                SyncWindowStrings();
        }

        private void SyncWindowStrings()
        {
            if (_localizedModel == null)
                return;
            if (_stringsCulture == _localizedModel.CurrentCultureCode)
                return;
            _stringsCulture = _localizedModel.CurrentCultureCode;
            if (global::Avalonia.Application.Current is { } app)
                LocalizationResources.Apply(app, _localizedModel.LocalizationManager.GetAllKeys(_localizedModel.CurrentCultureCode));
            foreach (var text in this.GetVisualDescendants().OfType<TextBlock>())
                Controls.LocalizedText.Refresh(text);
        }
    }
}
