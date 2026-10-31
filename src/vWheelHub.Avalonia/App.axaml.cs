using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SimRacingHub.Avalonia.Composition;

namespace SimRacingHub.Avalonia
{
    public partial class App : global::Avalonia.Application
    {
        public static AvaloniaHostServices? Services { get; set; }
        public static bool IsPreviewMode { get; set; }
        public static Adapters.DesktopInstance? Instance { get; set; }
        public static SimRacingHub.Application.Contracts.IPlatformCapabilities? PreviewPlatformCapabilities { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            var defaults = new SimRacingHub.Application.Localization.LocalizationManager();
            Adapters.LocalizationResources.Apply(this, defaults.GetAllKeys("en-US"));
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var hostServices = AvaloniaCompositionRoot.Create(new AvaloniaCompositionOptions
                {
                    IsPreviewMode = IsPreviewMode,
                    PreviewPlatformCapabilities = PreviewPlatformCapabilities
                });

                Services = hostServices;

                // Wire dynamic localization resource updates
                SyncLocalizationResources(hostServices.LocalizationManager.CurrentCultureCode);
                hostServices.LocalizationManager.LanguageChanged += culture =>
                {
                    Dispatcher.UIThread.Post(() => SyncLocalizationResources(culture));
                };

                var mainWindow = new MainWindow
                {
                    DataContext = hostServices.ShellViewModel
                };

                var desktopBehavior = new Adapters.DesktopWindowController(mainWindow, hostServices,
                    new Adapters.DesktopTray(), allowAutoStart: !hostServices.IsPreviewMode,
                    sounds: hostServices.IsPreviewMode ? null : new Adapters.DesktopSoundPlayer());
                Instance?.RegisterActivationHandler(() => Dispatcher.UIThread.Post(desktopBehavior.RestoreWindow));
                mainWindow.DesktopBehavior = desktopBehavior;
                if (hostServices.AppSettings.StartMinimized && hostServices.AppSettings.HasSeenWelcome)
                    mainWindow.WindowState = global::Avalonia.Controls.WindowState.Minimized;
                desktop.MainWindow = mainWindow;

                desktop.Exit += async (_, _) =>
                {
                    desktopBehavior.Dispose();
                    if (Services != null)
                    {
                        await Services.DisposeAsync().ConfigureAwait(false);
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void SyncLocalizationResources(string cultureCode)
        {
            if (Services == null) return;

            try
            {
                var keys = Services.LocalizationManager.GetAllKeys(cultureCode);
                Adapters.LocalizationResources.Apply(this, keys);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to sync localization resources: {ex.Message}");
            }
        }
    }
}
