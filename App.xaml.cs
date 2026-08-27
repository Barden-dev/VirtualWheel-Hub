using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimRacingHub.Core;
using SimRacingHub.Services;
using SimRacingHub.ViewModels;

namespace SimRacingHub
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));

            var services = new ServiceCollection();
            
            // Services
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<WindowManager>();
            services.AddSingleton<SimRacingHub.Core.VJoyDiagnosticService>();
            services.AddSingleton<SimRacingHub.Services.Telemetry.GameIntegrationRegistry>();
            services.AddSingleton<UpdateService>();
            
            // ViewModels
            services.AddSingleton<MainViewModel>();
            
            // Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<Views.Pages.DashboardPage>();
            services.AddTransient<Views.Pages.SteeringPage>();
            services.AddTransient<Views.Pages.TrailBrakingPage>();
            services.AddTransient<Views.Pages.GasLogicPage>();
            services.AddTransient<Views.Pages.BrakeLogicPage>();
            services.AddTransient<Views.Pages.SlipAudioPage>();
            services.AddTransient<Views.Pages.VjoyAxesPage>();
            services.AddTransient<Views.Pages.KeyBindingsPage>();

            ServiceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppLogger.Instance.LogInfo("Application startup", showInStatusBar: false);
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AppLogger.Instance.LogInfo($"Application exit (code {e.ApplicationExitCode})", showInStatusBar: false);

                SimRacingHub.Services.Telemetry.AdaptiveGridCache.ShutdownIfActive();

                CrashSafety.RunAll();
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Cleanup on exit failed", ex);
            }
            finally
            {
                AppLogger.Instance.Flush();
                base.OnExit(e);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleFatal("Unhandled exception on the UI thread", e.Exception, showDialog: true);

            e.Handled = true;
            Shutdown(1);
        }

        private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            HandleFatal("Unhandled exception on a background thread", e.ExceptionObject as Exception, showDialog: false);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            AppLogger.Instance.LogError("Unobserved task exception", e.Exception);
            e.SetObserved();
        }

        private static void HandleFatal(string context, Exception? ex, bool showDialog)
        {
            try { AppLogger.Instance.LogCritical(context, ex); }
            catch (Exception logEx) { Debug.WriteLine($"[App] could not log fatal error: {logEx.Message}"); }

            try { CrashSafety.RunAll(); }
            catch (Exception cleanupEx) { Debug.WriteLine($"[App] emergency cleanup failed: {cleanupEx.Message}"); }

            try { AppLogger.Instance.Flush(); }
            catch (Exception flushEx) { Debug.WriteLine($"[App] could not flush the log: {flushEx.Message}"); }

            if (showDialog) ShowCrashDialog(context, ex);
        }

        private static void ShowCrashDialog(string context, Exception? ex)
        {
            try
            {
                string logPath = AppLogger.Instance.CurrentLogFilePath;
                string message =
                    "vWheel Hub ran into an unexpected error and has to close." + Environment.NewLine + Environment.NewLine +
                    context + ":" + Environment.NewLine +
                    (ex?.Message ?? "unknown error") + Environment.NewLine + Environment.NewLine +
                    "The mouse cursor has been released and the vJoy device freed." + Environment.NewLine + Environment.NewLine +
                    "Details were written to:" + Environment.NewLine + logPath + Environment.NewLine + Environment.NewLine +
                    "Open the log file now?";

                var result = MessageBox.Show(message, "vWheel Hub - unexpected error",
                    MessageBoxButton.YesNo, MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes) OpenLogLocation(logPath);
            }
            catch (Exception dialogEx)
            {
                Debug.WriteLine($"[App] could not show the crash dialog: {dialogEx.Message}");
            }
        }

        private static void OpenLogLocation(string logPath)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath)) return;

                string target = File.Exists(logPath) ? logPath : (Path.GetDirectoryName(logPath) ?? string.Empty);
                if (string.IsNullOrEmpty(target)) return;

                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] could not open '{logPath}': {ex.Message}");
            }
        }
    }
}
