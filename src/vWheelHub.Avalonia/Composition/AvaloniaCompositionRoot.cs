using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Fakes;
using SimRacingHub.Application.Features.AppSettings;
using SimRacingHub.Application.Features.Dashboard;
using SimRacingHub.Application.Features.InputDevices;
using SimRacingHub.Application.Features.KeyBindings;
using SimRacingHub.Application.Features.Output;
using SimRacingHub.Application.Features.Pedals;
using SimRacingHub.Application.Features.Profiles;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Application.Features.Steering;
using SimRacingHub.Application.Features.Supporters;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Avalonia.Adapters;
using SimRacingHub.Core;
using SimRacingHub.Input;
using SimRacingHub.Linux;
using SimRacingHub.Services;
using SimRacingHub.Services.Games;
using SimRacingHub.Windows;

namespace SimRacingHub.Avalonia.Composition
{
    /// <summary>
    /// Options for configuring Avalonia host composition.
    /// </summary>
    public sealed class AvaloniaCompositionOptions
    {
        public bool IsPreviewMode { get; set; }
        public IAppStorage? StorageOverride { get; set; }
        public IInputDeviceSelectionService? InputDeviceSelectionOverride { get; set; }
        public IUiDispatcher? UiDispatcherOverride { get; set; }
        public ILocalizationManager? LocalizationOverride { get; set; }
        public IUserDialogService? DialogServiceOverride { get; set; }
        public IFilePickerService? FilePickerServiceOverride { get; set; }
        public IClipboardService? ClipboardServiceOverride { get; set; }
        public IExternalLauncher? ExternalLauncherOverride { get; set; }
        public ISupportersFeed? SupportersFeedOverride { get; set; }
        // Presentation-only platform simulation; runtime remains isolated in preview.
        public IPlatformCapabilities? PreviewPlatformCapabilities { get; set; }
    }

    /// <summary>
    /// Container holding composed services, ViewModels, and platform adapters for Avalonia.
    /// </summary>
    public sealed partial class AvaloniaHostServices : IAsyncDisposable, IDisposable
    {
        public bool IsPreviewMode { get; }
        public IAppStorage Storage { get; }
        public IRuntimeSession RuntimeSession { get; }
        public ProfileSession ProfileSession { get; }
        public IUiDispatcher UiDispatcher { get; }
        public ILocalizationManager LocalizationManager { get; }
        public IAppMetadata AppMetadata { get; }
        public IPlatformCapabilities PlatformCapabilities { get; }
        public IUserDialogService DialogService { get; }
        public IFilePickerService FilePickerService { get; }
        public IClipboardService ClipboardService { get; }
        public IExternalLauncher ExternalLauncher { get; }
        public SteeringViewModel Steering { get; private set; } = null!;
        public PedalsViewModel Pedals { get; private set; } = null!;
        public TrailBrakingViewModel TrailBraking { get; private set; } = null!;
        public DashboardViewModel Dashboard { get; private set; } = null!;
        public ProfilesViewModel Profiles { get; }
        public OutputViewModel Output { get; }
        public KeyBindingsViewModel KeyBindings { get; }
        public AppSettingsViewModel AppSettings { get; private set; } = null!;
        public InputDevicesViewModel InputDevices { get; }
        public SupportersViewModel Supporters { get; }
        public AppShellViewModel ShellViewModel { get; }
        public LinuxHostServices? LinuxHostServices { get; }
        public WindowsHostServices? WindowsHostServices { get; }
        public DesktopSessionCoordinator? DesktopSession { get; internal set; }
        private readonly IOutputDiagnostics _diagnostics;
        private bool _disposed;

        private AvaloniaHostServices(
            bool isPreviewMode,
            IAppStorage storage,
            IRuntimeSession runtimeSession,
            ProfileSession profileSession,
            IUiDispatcher uiDispatcher,
            ILocalizationManager localizationManager,
            IAppMetadata appMetadata,
            IPlatformCapabilities platformCapabilities,
            IUserDialogService dialogService,
            AppShellViewModel shellViewModel,
            LinuxHostServices? linuxHostServices,
            IFilePickerService? filePickerService,
            IClipboardService? clipboardService,
            IExternalLauncher? externalLauncher,
            TrailBrakingViewModel? trailBraking,
            ProfilesViewModel? profiles,
            OutputViewModel? output,
            KeyBindingsViewModel? keyBindings,
            WindowsHostServices? windowsHostServices,
            ISupportersFeed? supportersFeed,
            IInputDeviceSelectionService? inputDeviceSelection,
            bool deferEditionWiring)
        {
            IsPreviewMode = isPreviewMode;
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            RuntimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
            ProfileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            UiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            LocalizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            AppMetadata = appMetadata ?? throw new ArgumentNullException(nameof(appMetadata));
            PlatformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
            DialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            FilePickerService = filePickerService ?? new AvaloniaFilePickerService(uiDispatcher);
            ClipboardService = clipboardService ?? new AvaloniaClipboardService(uiDispatcher);
            ExternalLauncher = externalLauncher ?? new AvaloniaExternalLauncher();
            ShellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
            LinuxHostServices = linuxHostServices;
            WindowsHostServices = windowsHostServices;

            var devices = inputDeviceSelection ?? (isPreviewMode ? null : OperatingSystem.IsWindows()
                ? (IInputDeviceSelectionService)new WindowsInputDeviceSelectionService(storage.SettingsFilePath)
                : new LinuxInputDeviceSelectionService(storage.SettingsFilePath));
            InputDevices = new InputDevicesViewModel(devices, runtimeSession, localizationManager,
                () => DesktopSession?.SuspendInputDeviceSelection());

            _diagnostics = (OperatingSystem.IsWindows() ? windowsHostServices?.Diagnostics : null)
                ?? linuxHostServices?.Diagnostics
                ?? (isPreviewMode ? new FakeOutputDiagnostics() : throw new InvalidOperationException("Production output diagnostics are missing."));

            TrailBraking = trailBraking ?? shellViewModel.TrailBraking ?? new TrailBrakingViewModel(
                profileSession,
                localizationManager);
            shellViewModel.TrailBraking = TrailBraking;

            Profiles = profiles ?? shellViewModel.Profiles ?? new ProfilesViewModel(
                profileSession,
                profileSession.ProfileManager,
                new ProfileTransferUseCases(FilePickerService, ClipboardService, DialogService),
                DialogService,
                FilePickerService,
                ClipboardService,
                localizationManager);
            shellViewModel.Profiles = Profiles;

            Output = output ?? shellViewModel.Output ?? new OutputViewModel(
                profileSession,
                _diagnostics,
                platformCapabilities,
                localizationManager,
                uiDispatcher,
                dialogService,
                runtimeSession,
                ExternalLauncher);
            shellViewModel.Output = Output;

            KeyBindings = keyBindings ?? shellViewModel.KeyBindings ?? new KeyBindingsViewModel(
                profileSession,
                localizationManager,
                OperatingSystem.IsWindows() && windowsHostServices != null ? new WindowsKeyCaptureSource(storage.SettingsFilePath) : null,
                uiDispatcher);
            shellViewModel.KeyBindings = KeyBindings;
            KeyBindings.VirtualButtons = Output;

            Supporters = new SupportersViewModel(supportersFeed ?? new SupportersFeed(storage, allowNetwork: !isPreviewMode), uiDispatcher);
            shellViewModel.Supporters = Supporters;

            _ = deferEditionWiring;
        }

        public AvaloniaHostServices(
            bool isPreviewMode,
            IAppStorage storage,
            IRuntimeSession runtimeSession,
            ProfileSession profileSession,
            IUiDispatcher uiDispatcher,
            ILocalizationManager localizationManager,
            IAppMetadata appMetadata,
            IPlatformCapabilities platformCapabilities,
            IUserDialogService dialogService,
            AppShellViewModel shellViewModel,
            LinuxHostServices? linuxHostServices = null,
            IFilePickerService? filePickerService = null,
            IClipboardService? clipboardService = null,
            IExternalLauncher? externalLauncher = null,
            SteeringViewModel? steering = null,
            PedalsViewModel? pedals = null,
            DashboardViewModel? dashboard = null,
            ProfilesViewModel? profiles = null,
            OutputViewModel? output = null,
            KeyBindingsViewModel? keyBindings = null,
            AppSettingsViewModel? appSettings = null,
            WindowsHostServices? windowsHostServices = null,
            ISupportersFeed? supportersFeed = null,
            IInputDeviceSelectionService? inputDeviceSelection = null)
            : this(isPreviewMode, storage, runtimeSession, profileSession, uiDispatcher, localizationManager, appMetadata, platformCapabilities, dialogService, shellViewModel, linuxHostServices, filePickerService, clipboardService, externalLauncher, trailBraking: null, profiles: profiles, output: output, keyBindings: keyBindings, windowsHostServices: windowsHostServices, supportersFeed: supportersFeed, inputDeviceSelection: inputDeviceSelection, deferEditionWiring: false)
        {
            Steering = steering ?? shellViewModel.Steering ?? new SteeringViewModel(profileSession, localizationManager);
            shellViewModel.Steering = Steering;

            Pedals = pedals ?? shellViewModel.Pedals ?? new PedalsViewModel(profileSession, localizationManager);
            shellViewModel.Pedals = Pedals;

            Dashboard = dashboard ?? shellViewModel.Dashboard ?? new DashboardViewModel(runtimeSession, profileSession, _diagnostics, localizationManager, uiDispatcher);
            shellViewModel.Dashboard = Dashboard;

            IInputCompatibilityService? inputCompatibility = isPreviewMode
                ? new FakeInputCompatibilityService()
                : OperatingSystem.IsWindows() ? new WindowsInputCompatibilityService() : null;
            AppSettings = appSettings ?? shellViewModel.AppSettings ?? new AppSettingsViewModel(
                storage, appMetadata, localizationManager, ExternalLauncher, dialogService, platformCapabilities, uiDispatcher, inputCompatibility);
            shellViewModel.AppSettings = AppSettings;

            CompleteCommonPostWiring();
        }

        private void CompleteCommonPostWiring()
        {
            InputDevices.ConfigureInputMode(AppSettings);
            Dashboard.Readiness = new DeviceReadinessViewModel(ProfileSession, _diagnostics,
                PlatformCapabilities, LocalizationManager, UiDispatcher,
                () => ShellViewModel.OpenDeviceDiagnosticsCommand.Execute(null), async () =>
                {
                    ShellViewModel.NavigateTo(PageId.Dashboard);
                    Dashboard.IsDeviceSetupOpen = true;
                    await Output.ConnectWheelAsync();
                });
            Output.Readiness = Dashboard.Readiness;
            Dashboard.DeviceSetup = Output;
            Output.OutputRecovered = () =>
            {
                ShellViewModel.AcknowledgeOutputRecovery();
                Dashboard.AcknowledgeOutputRecovery();
                OnOutputRecoveredExtra();
            };
            Output.NavigateGameBindings = () => ShellViewModel.NavigateTo(PageId.ControlAssignment);
        }

        partial void OnOutputRecoveredExtra();
        partial void DisposeExtraViewModels();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DesktopSession?.Dispose();
            InputDevices.Dispose();
            Dashboard.Readiness?.Dispose();
            Supporters.Dispose();
            Dashboard.Dispose();
            DisposeExtraViewModels();
            Output.Dispose();
            KeyBindings.Dispose();
            AppSettings.Dispose();
            Steering.Dispose();
            Pedals.Dispose();
            TrailBraking.Dispose();
            ShellViewModel.Dispose();
            if (WindowsHostServices == null && LinuxHostServices == null) RuntimeSession.Dispose();
            LinuxHostServices?.Dispose();
            if (OperatingSystem.IsWindows()) WindowsHostServices?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            DesktopSession?.Dispose();
            InputDevices.Dispose();
            Dashboard.Readiness?.Dispose();
            Supporters.Dispose();
            Dashboard.Dispose();
            DisposeExtraViewModels();
            Output.Dispose();
            KeyBindings.Dispose();
            AppSettings.Dispose();
            Steering.Dispose();
            Pedals.Dispose();
            TrailBraking.Dispose();
            await ShellViewModel.DisposeAsync().ConfigureAwait(false);
            if (WindowsHostServices == null && LinuxHostServices == null) await RuntimeSession.DisposeAsync().ConfigureAwait(false);
            if (OperatingSystem.IsWindows() && WindowsHostServices != null) await WindowsHostServices.DisposeAsync().ConfigureAwait(false);
            if (LinuxHostServices != null)
            {
                await LinuxHostServices.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Composition root assembling services for the Avalonia host.
    /// Handles both production platform wiring and --preview isolation mode.
    /// </summary>
    public static partial class AvaloniaCompositionRoot
    {
        public static AvaloniaHostServices Create(AvaloniaCompositionOptions? options = null)
        {
            var opt = options ?? new AvaloniaCompositionOptions();
            var uiDispatcher = opt.UiDispatcherOverride ?? new AvaloniaUiDispatcher();
            var localizationManager = opt.LocalizationOverride ?? new LocalizationManager();
            localizationManager.Initialize();

            var dialogService = opt.DialogServiceOverride ?? new AvaloniaUserDialogService(uiDispatcher);
            var filePickerService = opt.FilePickerServiceOverride ?? new AvaloniaFilePickerService(uiDispatcher);
            var clipboardService = opt.ClipboardServiceOverride ?? new AvaloniaClipboardService(uiDispatcher);
            var externalLauncher = opt.ExternalLauncherOverride ?? new AvaloniaExternalLauncher();

            AvaloniaHostServices? custom = null;
            TryCreateCustom(opt, uiDispatcher, localizationManager, dialogService, filePickerService, clipboardService, externalLauncher, ref custom);
            if (custom != null) return custom;

            if (opt.IsPreviewMode)
            {
                var tempStorage = opt.StorageOverride ?? AppStorage.CreateTempStorage(prefix: "vWheelHub_Preview_");
                var profileManager = new ProfileManager(tempStorage.ProfilesDirectory);
                var profileSession = new ProfileSession(profileManager);
                var runtimeSession = new FakeRuntimeSession();
                var appMetadata = new AvaloniaAppMetadata(isPreview: true);
                var platformCapabilities = opt.PreviewPlatformCapabilities ??
                    (OperatingSystem.IsLinux() ? FakePlatformCapabilities.Linux() : FakePlatformCapabilities.Windows());

                var shellViewModel = new AppShellViewModel(
                    runtimeSession,
                    profileSession,
                    uiDispatcher,
                    localizationManager,
                    appMetadata,
                    platformCapabilities,
                    dialogService);
                shellViewModel.IsPreviewMode = true;

                return new AvaloniaHostServices(
                    isPreviewMode: true,
                    tempStorage,
                    runtimeSession,
                    profileSession,
                    uiDispatcher,
                    localizationManager,
                    appMetadata,
                    platformCapabilities,
                    dialogService,
                    shellViewModel,
                    linuxHostServices: null,
                    filePickerService: filePickerService,
                    clipboardService: clipboardService,
                    externalLauncher: externalLauncher,
                    supportersFeed: opt.SupportersFeedOverride,
                    inputDeviceSelection: opt.InputDeviceSelectionOverride);
            }

            var storage = opt.StorageOverride ?? AppStorage.CreateDefault();
            var normalProfileManager = new ProfileManager(storage.ProfilesDirectory);
            var normalProfileSession = new ProfileSession(normalProfileManager);
            var normalAppMetadata = new AvaloniaAppMetadata(isPreview: false);

            LinuxHostServices? linux = null;
            WindowsHostServices? windows = null;
            IRuntimeSession runtime;
            IPlatformCapabilities capabilities;
            GameIntegrationRegistry registry;
            HotkeyService? hotkeys = null;

            if (OperatingSystem.IsWindows())
            {
                windows = WindowsHostComposition.CreateHostServices(new WindowsHostCompositionOptions
                { ProfileSession = normalProfileSession, Localization = localizationManager, SettingsPath = storage.SettingsFilePath });
                runtime = windows.RuntimeEngine;
                capabilities = windows.Capabilities;
                registry = windows.IntegrationRegistry;
                hotkeys = new HotkeyService(windows.HotkeyInputSource, windows.CursorLockService, manageInputSource: true);
            }
            else if (OperatingSystem.IsLinux())
            {
                linux = LinuxHostComposition.CreateHostServices(new LinuxHostCompositionOptions
                { ProfileSession = normalProfileSession, Localization = localizationManager });
                runtime = linux.RuntimeSession;
                capabilities = linux.Capabilities;
                registry = linux.IntegrationRegistry;
                hotkeys = new HotkeyService(linux.HotkeyInputSource, linux.CursorLockService, manageInputSource: true);
            }
            else throw new PlatformNotSupportedException("Supported desktop hosts: Windows and Linux.");

            var shell = new AppShellViewModel(runtime, normalProfileSession, uiDispatcher, localizationManager,
                normalAppMetadata, capabilities, dialogService);
            var services = new AvaloniaHostServices(false, storage, runtime, normalProfileSession,
                uiDispatcher, localizationManager, normalAppMetadata, capabilities,
                dialogService, shell, linux, filePickerService, clipboardService, externalLauncher,
                windowsHostServices: windows, supportersFeed: opt.SupportersFeedOverride, inputDeviceSelection: opt.InputDeviceSelectionOverride);
            services.DesktopSession = new DesktopSessionCoordinator(shell, runtime, registry, uiDispatcher, hotkeys);
            return services;
        }

        static partial void TryCreateCustom(
            AvaloniaCompositionOptions opt,
            IUiDispatcher uiDispatcher,
            ILocalizationManager localizationManager,
            IUserDialogService dialogService,
            IFilePickerService filePickerService,
            IClipboardService clipboardService,
            IExternalLauncher externalLauncher,
            ref AvaloniaHostServices? services);
    }
}
