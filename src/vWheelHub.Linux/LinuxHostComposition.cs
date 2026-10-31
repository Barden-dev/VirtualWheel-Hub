using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;
using SimRacingHub.Services;
using SimRacingHub.Services.Games;

namespace SimRacingHub.Linux
{
    public sealed class LinuxHostCompositionOptions
    {
        public string? MousePathOverride { get; set; }
        public string? KeyboardPathOverride { get; set; }
        public ILinuxDiagnosticsProbe? DiagnosticsProbe { get; set; }
        public ProfileSession? ProfileSession { get; set; }
        public SimRacingHub.Application.Localization.ILocalizationManager? Localization { get; set; }
    }

    public sealed class LinuxHostServices : IAsyncDisposable, IDisposable
    {
        private bool _disposed;
        public IPlatformCapabilities Capabilities { get; }
        public IOutputDiagnostics Diagnostics { get; }
        public IInputSource InputSource { get; }
        public IInputSource HotkeyInputSource { get; }
        public IOutputFeederFactory FeederFactory { get; }
        public ICursorLockService CursorLockService { get; }
        public IPrecisionTimer PrecisionTimer { get; }
        public ProfileSession? ProfileSession { get; }
        public GameIntegrationRegistry IntegrationRegistry { get; }
        public HubRuntimeEngine RuntimeEngine { get; }
        public IRuntimeSession RuntimeSession => RuntimeEngine;

        public LinuxHostServices(IPlatformCapabilities capabilities, IOutputDiagnostics diagnostics, IInputSource inputSource, IOutputFeederFactory feederFactory, ICursorLockService cursorLockService, IPrecisionTimer precisionTimer, GameIntegrationRegistry integrationRegistry, HubRuntimeEngine runtimeEngine, ProfileSession? profileSession = null)
        {
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            ;
            InputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            HotkeyInputSource = inputSource is EvdevInputSource evdev
                ? evdev.CreateKeyStateObserver() : new EvdevInputSource(queryKeyState: true);
            FeederFactory = feederFactory ?? throw new ArgumentNullException(nameof(feederFactory));
            CursorLockService = cursorLockService ?? throw new ArgumentNullException(nameof(cursorLockService));
            PrecisionTimer = precisionTimer ?? throw new ArgumentNullException(nameof(precisionTimer));
            ;
            IntegrationRegistry = integrationRegistry ?? throw new ArgumentNullException(nameof(integrationRegistry));
            RuntimeEngine = runtimeEngine ?? throw new ArgumentNullException(nameof(runtimeEngine));
            ;
            ProfileSession = profileSession;
            ;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            RuntimeEngine.Dispose();
            ;
            ;
            InputSource.Dispose();
            HotkeyInputSource.Dispose();
            IntegrationRegistry.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await RuntimeEngine.DisposeAsync().ConfigureAwait(false);
            ;
            ;
            InputSource.Dispose();
            HotkeyInputSource.Dispose();
            IntegrationRegistry.Dispose();
        }
    }



    public static class LinuxHostComposition
    {
        static LinuxHostComposition() => GameWindowFocus.PlatformProbe = X11GameWindow.MatchesForeground;
        public static IPlatformCapabilities CreateCapabilities()
        {
            return LinuxPlatformCapabilities.Instance;
        }

        public static IOutputDiagnostics CreateDiagnostics(ILinuxDiagnosticsProbe? probe = null, SimRacingHub.Application.Localization.ILocalizationManager? localization = null)
        {
            return new LinuxOutputDiagnostics(probe, localization);
        }

        public static IInputSource CreateInputSource(string? mousePath = null, string? keyboardPath = null)
        {
            return new EvdevInputSource(mousePath, keyboardPath);
        }

        public static IOutputFeederFactory CreateFeederFactory()
        {
            return new UInputFeederFactory();
        }

        public static ICursorLockService CreateCursorLockService(IInputSource inputSource)
        {
            if (inputSource is EvdevInputSource evdev)
            {
                return new EvdevCursorLockService(evdev);
            }

            return new EvdevCursorLockService(() => -1);
        }

        public static ICursorLockService CreateCursorLockService(Func<int> getMouseFd)
        {
            return new EvdevCursorLockService(getMouseFd);
        }

        public static IPrecisionTimer CreatePrecisionTimer()
        {
            return new PosixPrecisionTimer();
        }

        public static GameIntegrationRegistry CreateGameIntegrationRegistry() => new GameIntegrationRegistry();

        public static HubRuntimeEngine CreateRuntimeEngine(IInputSource? inputSource = null, IOutputFeederFactory? feederFactory = null, ICursorLockService? cursorLockService = null, IPrecisionTimer? precisionTimer = null, ProfileSession? profileSession = null, GameIntegrationRegistry? registry = null)
        {
            var finalInputSource = inputSource ?? CreateInputSource();
            var finalFeederFactory = feederFactory ?? CreateFeederFactory();
            var finalCursorLock = cursorLockService ?? CreateCursorLockService(finalInputSource);
            var finalTimer = precisionTimer ?? CreatePrecisionTimer();
            var finalRegistry = registry ?? CreateGameIntegrationRegistry();
            return new HubRuntimeEngine(finalInputSource, finalFeederFactory, finalCursorLock, finalTimer, profileSession, finalRegistry);
        }

        public static LinuxHostServices CreateHostServices(LinuxHostCompositionOptions? options = null)
        {
            var opt = options ?? new LinuxHostCompositionOptions();
            var capabilities = CreateCapabilities();
            var diagnostics = CreateDiagnostics(opt.DiagnosticsProbe, opt.Localization);
            ;
            var inputSource = CreateInputSource(opt.MousePathOverride, opt.KeyboardPathOverride);
            var feederFactory = CreateFeederFactory();
            var cursorLock = CreateCursorLockService(inputSource);
            var timer = CreatePrecisionTimer();
            ;
            ;
            var registry = CreateGameIntegrationRegistry();
            var engine = new HubRuntimeEngine(inputSource, feederFactory, cursorLock, timer, opt.ProfileSession, registry);
            return new LinuxHostServices(capabilities, diagnostics, inputSource, feederFactory, cursorLock, timer, registry, engine, opt.ProfileSession);
        }
    }
}
