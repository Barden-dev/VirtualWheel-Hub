using System;
using System.Linq;
using Avalonia;

namespace SimRacingHub.Avalonia
{
    public static class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called.
        [STAThread]
        public static int Main(string[] args)
        {
            int applyIndex = Array.FindIndex(args, a => a.Equals("--apply-vjoy", StringComparison.OrdinalIgnoreCase));
            if (applyIndex >= 0)
            {
                if (args.Length != 2 || applyIndex != 0 || !Guid.TryParseExact(args[1], "N", out _) || !OperatingSystem.IsWindows())
                { Console.Error.WriteLine("--apply-vjoy requires Windows and one approved operation ID."); return 1; }
                return SimRacingHub.Core.VJoyRecoveryCommand.Apply(args[1], Console.Out);
            }
            int recoveryIndex = Array.FindIndex(args, a => a.Equals("--restore-vjoy", StringComparison.OrdinalIgnoreCase));
            if (recoveryIndex >= 0)
            {
                if (args.Length != 2 || recoveryIndex != 0 || !Guid.TryParseExact(args[1], "N", out _))
                { Console.Error.WriteLine("Usage: vWheelHub --restore-vjoy <operation-id>"); return 1; }
                if (!OperatingSystem.IsWindows())
                { Console.Error.WriteLine("vJoy recovery is only available on Windows."); return 1; }
                return SimRacingHub.Core.VJoyRecoveryCommand.Run(args[1], Console.Out);
            }
            if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/?", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("vWheel Hub - Cross-Platform Virtual Racing Wheel Manager (Avalonia UI)");
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  vWheelHub [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --preview      Launch in standalone preview mode (mock fixtures, no physical hardware required)");
                Console.WriteLine("  --preview-platform windows|linux  Preview the same UI with another host's capabilities (requires --preview)");
                Console.WriteLine("  --smoke        Execute self-test smoke validation (composition, degradation, and platform adapters)");
                Console.WriteLine("  --restore-vjoy <operation-id>  Restore a verified hub change (Windows, administrator rights; conflicts stop recovery)");
                Console.WriteLine("  --help, -h     Show help and usage information");
                return 0;
            }

            bool isPreview = args.Any(a => string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase));
            App.IsPreviewMode = isPreview;
            int platformIndex = Array.FindIndex(args, a => a.Equals("--preview-platform", StringComparison.OrdinalIgnoreCase));
            App.PreviewPlatformCapabilities = null;
            if (platformIndex >= 0)
            {
                if (!isPreview || platformIndex + 1 >= args.Length)
                {
                    Console.Error.WriteLine("--preview-platform requires --preview and windows or linux.");
                    return 1;
                }

                App.PreviewPlatformCapabilities = args[platformIndex + 1].ToLowerInvariant() switch
                {
                    "windows" => SimRacingHub.Application.Fakes.FakePlatformCapabilities.Windows(),
                    "linux" => SimRacingHub.Application.Fakes.FakePlatformCapabilities.Linux(),
                    _ => null
                };
                if (App.PreviewPlatformCapabilities == null)
                {
                    Console.Error.WriteLine("Unknown preview platform. Use windows or linux.");
                    return 1;
                }
            }

            if (args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)))
            {
                return RunSmokeValidation(isPreview);
            }

            using var instance = new Adapters.DesktopInstance(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "vWheelHub",
                isPreview ? "preview-instance" : "desktop-instance"));
            if (!instance.IsPrimary)
            {
                bool activated = instance.ActivateExistingAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                if (!activated) Console.Error.WriteLine("vWheel Hub is already running, but its window could not be activated. Try again shortly.");
                return activated ? 0 : 1;
            }
            App.Instance = instance;

            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)when (ex.Message.Contains("XOpenDisplay") || ex.Message.Contains("DISPLAY"))
            {
                Console.Error.WriteLine($"[vWheelHub] Display server unavailable ({ex.Message}). Set DISPLAY or WAYLAND_DISPLAY to run GUI.");
                return 1;
            }
            finally { App.Instance = null; }
        }

        private static int RunSmokeValidation(bool isPreview)
        {
            try
            {
                using var host = SimRacingHub.Avalonia.Composition.AvaloniaCompositionRoot.Create(
                    new SimRacingHub.Avalonia.Composition.AvaloniaCompositionOptions { IsPreviewMode = true });
                if (host.Steering == null || host.Pedals == null || host.Profiles == null || host.KeyBindings == null ||
                    host.Output == null || host.Dashboard == null || host.AppSettings == null)
                    throw new InvalidOperationException("A basic feature module is missing.");
                host.ShellViewModel.InitializeAsync().GetAwaiter().GetResult();
                if (host.RuntimeSession.IsRunning || host.RuntimeSession.IsLocked || host.ProfileSession.HasUnsavedChanges)
                    throw new InvalidOperationException("Startup changed runtime or profile state.");
                var en = host.LocalizationManager.GetAllKeys("en-US");
                var ru = host.LocalizationManager.GetAllKeys("ru-RU");
                if (en.Count == 0 || !en.Keys.OrderBy(k => k).SequenceEqual(ru.Keys.OrderBy(k => k)))
                    throw new InvalidOperationException("The interface languages have inconsistent keys.");
                Console.WriteLine("Free desktop smoke passed: composition, profiles, languages, passive startup.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}
