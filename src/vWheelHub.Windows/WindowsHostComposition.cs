using System;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;
using SimRacingHub.Input;
using SimRacingHub.Services;
using SimRacingHub.Services.Games;

namespace SimRacingHub.Windows;

// Construction is passive: no input capture, controller acquisition or cursor lock.
public sealed partial class WindowsHostCompositionOptions
{
    public required ProfileSession ProfileSession { get; init; }
    public SimRacingHub.Application.Localization.ILocalizationManager? Localization { get; init; }
    public IInputSource? InputSource { get; init; }
    public IInputSource? HotkeyInputSource { get; init; }
    public string? SettingsPath { get; init; }
    public IOutputFeederFactory? FeederFactory { get; init; }
    public IOutputDiagnostics? OutputDiagnostics { get; init; }
    public ICursorLockService? CursorLockService { get; init; }
    public IPrecisionTimer? PrecisionTimer { get; init; }
    public GameIntegrationRegistry? IntegrationRegistry { get; init; }
}

public sealed partial class WindowsHostServices : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    public IInputSource InputSource { get; }
    public IInputSource HotkeyInputSource { get; }
    public ICursorLockService CursorLockService { get; }
    public HubRuntimeEngine RuntimeEngine { get; }
    public GameIntegrationRegistry IntegrationRegistry { get; }
    public IPlatformCapabilities Capabilities { get; } = new WindowsPlatformCapabilities();
    public IOutputDiagnostics Diagnostics { get; }

    internal WindowsHostServices(WindowsHostCompositionOptions options)
    {
        Diagnostics = options.OutputDiagnostics ?? new WindowsOutputDiagnostics(localization: options.Localization,
            vjoyRequirements: () => VJoyRequirements.ForProfile(new VJoyInventoryService().TryResolveAutomatic(options.ProfileSession.ResolvedProfile)
                ?? options.ProfileSession.ResolvedProfile.VJoyDeviceId, true));
        InputSource = options.InputSource ?? new WindowsInputSource(options.SettingsPath);
        HotkeyInputSource = options.HotkeyInputSource ?? new WindowsInputSource(options.SettingsPath);
        CursorLockService = options.CursorLockService ?? new WindowManager();
        IntegrationRegistry = options.IntegrationRegistry ?? new GameIntegrationRegistry();

        HubRuntimeEngine? customEngine = null;
        InitializeExtraServices(options, ref customEngine);
        RuntimeEngine = customEngine ?? new HubRuntimeEngine(InputSource,
            options.FeederFactory ?? new WindowsOutputFeederFactory(), CursorLockService,
            options.PrecisionTimer ?? new WindowsPrecisionTimer(),
            options.ProfileSession, IntegrationRegistry, cursorShortcutInputSource: HotkeyInputSource);
        RuntimeEngine.PrepareOutputAsync = Diagnostics.EnsureOutputReadyAsync;
        AppDomain.CurrentDomain.ProcessExit += EmergencyCleanup;
        AppDomain.CurrentDomain.UnhandledException += EmergencyCleanup;
    }

    partial void InitializeExtraServices(WindowsHostCompositionOptions options, ref HubRuntimeEngine? customEngine);
    partial void DisposeExtraServices();
    partial void DisposeExtraServicesAsync(ref ValueTask extraTask);

    private void EmergencyCleanup(object? sender, EventArgs args) => CrashSafety.RunAll();
    private void UnsubscribeCleanup()
    {
        AppDomain.CurrentDomain.ProcessExit -= EmergencyCleanup;
        AppDomain.CurrentDomain.UnhandledException -= EmergencyCleanup;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnsubscribeCleanup();
        try { RuntimeEngine.Dispose(); }
        finally
        {
            CursorLockService.UnlockCursor();
            InputSource.Dispose();
            if (!ReferenceEquals(InputSource, HotkeyInputSource)) HotkeyInputSource.Dispose();
            DisposeExtraServices();
            IntegrationRegistry.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        UnsubscribeCleanup();
        try { await RuntimeEngine.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            CursorLockService.UnlockCursor();
            InputSource.Dispose();
            if (!ReferenceEquals(InputSource, HotkeyInputSource)) HotkeyInputSource.Dispose();
            ValueTask extraTask = default;
            DisposeExtraServicesAsync(ref extraTask);
            await extraTask.ConfigureAwait(false);
            IntegrationRegistry.Dispose();
        }
    }
}

public static class WindowsHostComposition
{
    public static WindowsHostServices CreateHostServices(WindowsHostCompositionOptions options)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows host requires Windows.");
        return new WindowsHostServices(options);
    }
}
