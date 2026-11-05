using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Service for diagnosing availability, driver integrity, and configuration of virtual output devices.
    /// </summary>
    public interface IOutputDiagnostics
    {
        /// <summary>
        /// Probes availability and diagnostic health of the specified output device mode.
        /// </summary>
        Task<DeviceDiagnosticSnapshot> CheckAsync(OutputDeviceMode mode, CancellationToken ct = default);
        bool SupportsAutomaticSetup => false;
        Task<DeviceDiagnosticSnapshot> EnsureOutputReadyAsync(OutputDeviceMode mode, CancellationToken ct = default) => CheckAsync(mode, ct);

        /// <summary>
        /// Attempts explicit repair or file sync for the virtual output device when actionable.
        /// </summary>
        bool CanRollbackRepair => false;
        bool CanResumeWhenAccessible => false;
        string? BootSessionId => null;
        Task<bool> RollbackRepairAsync(CancellationToken ct = default) => Task.FromResult(true);
        Task<bool> RequestRestartAsync(CancellationToken ct = default) => Task.FromResult(false);
        bool RequiresRestart => false;
        string? PreparationDescription => null;
        Task<bool> PrepareInputAsync(IUserDialogService dialogs, SimRacingHub.Application.Localization.ILocalizationManager localization, CancellationToken ct = default) => Task.FromResult(true);

        Task<bool> TryRepairAsync(OutputDeviceMode mode, CancellationToken ct = default) => Task.FromResult(false);
        Task<bool> ConfigureWheelAsync(int deviceId, CancellationToken ct = default) => Task.FromResult(false);
        Task<string?> BackupVJoyAsync(int deviceId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        Task<VJoyDeviceInventory> InspectVJoyAsync(CancellationToken ct = default) =>
            Task.FromResult(new VJoyDeviceInventory(Array.Empty<VJoyDeviceInfo>(), null));
        Task<bool> SelectVJoyAsync(VJoyDeviceInfo inspectedDevice, bool wheelCompatibility, CancellationToken ct = default) => Task.FromResult(false);
        Task<bool> CreateVJoyAsync(VJoyDeviceInfo inspectedDevice, CancellationToken ct = default) => Task.FromResult(false);
        Task<VJoySetupPreview?> PreviewVJoyAsync(VJoyDeviceInfo inspectedDevice, bool changeExisting, CancellationToken ct = default) =>
            Task.FromResult<VJoySetupPreview?>(null);
        Task<VJoySetupResult> ApplyVJoyAsync(VJoySetupPreview preview, CancellationToken ct = default) =>
            Task.FromResult(new VJoySetupResult(false, false));
        Task<VJoySetupResult> RefreshVJoyAsync(CancellationToken ct = default) =>
            Task.FromResult(new VJoySetupResult(false, false, ErrorKey: "Output_VJoyActivationPending"));
        Task<VJoySetupPreview?> PreviewVJoyRemovalAsync(CancellationToken ct = default) => Task.FromResult<VJoySetupPreview?>(null);
        Task<VJoySetupPreview?> PreviewVJoyRemovalAsync(VJoyDeviceInfo inspectedDevice, CancellationToken ct = default) => Task.FromResult<VJoySetupPreview?>(null);
        Task<VJoySetupPreview?> PreviewMissingVJoyRestoreAsync(CancellationToken ct = default) => Task.FromResult<VJoySetupPreview?>(null);
        Task<VJoyPendingSetup?> InspectPendingVJoyAsync(CancellationToken ct = default) => Task.FromResult<VJoyPendingSetup?>(null);
        Task<VJoySetupResult> ResolvePendingVJoyAsync(VJoyPendingSetup preview, bool resume, CancellationToken ct = default) =>
            Task.FromResult(new VJoySetupResult(false, false));
    }
}
