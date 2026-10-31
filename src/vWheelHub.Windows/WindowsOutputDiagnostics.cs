using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Windows
{
    /// <summary>
    /// Windows implementation of IOutputDiagnostics checking the vJoy driver, DLL,
    /// and device state under Windows.
    /// </summary>
    public sealed class WindowsOutputDiagnostics : IOutputDiagnostics, IOutputModePolicy
    {
        public OutputDeviceMode ResolveOutputMode(OutputDeviceMode savedMode) =>
            savedMode == OutputDeviceMode.Xbox360 ? OutputDeviceMode.VJoy : savedMode;
        private readonly SimRacingHub.Application.Localization.ILocalizationManager _localization;
        private string L(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.GetCultureInfo(_localization.CurrentCultureCode), _localization.GetString(key), args);

        private readonly Func<VJoyDiagnosticResult> _vjoyDiagnosticFunc;
        private readonly Func<VJoyRequirements> _vjoyRequirements;
        private readonly Func<uint, string?> _selectionAdvice;

        public WindowsOutputDiagnostics(
            VJoyDiagnosticService? vjoyDiagnosticService = null,
            Func<VJoyDiagnosticResult>? vjoyDiagnosticFunc = null, SimRacingHub.Application.Localization.ILocalizationManager? localization = null,
            Func<VJoyRequirements>? vjoyRequirements = null)
        {
            _localization = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();
            var service = vjoyDiagnosticService ?? new VJoyDiagnosticService(_localization);
            _vjoyRequirements = vjoyRequirements ?? (() => new VJoyRequirements());
            _vjoyDiagnosticFunc = vjoyDiagnosticFunc ?? (() => service.PerformDiagnostic(_vjoyRequirements()));
            _selectionAdvice = vjoyDiagnosticFunc != null ? _ => null
                : id => new HubVJoyDeviceStore().ValidateForAcquire((int)id, new WindowsVJoyConfigurationStore());
        }

        public Task<DeviceDiagnosticSnapshot> CheckAsync(OutputDeviceMode mode, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (mode == OutputDeviceMode.Xbox360) mode = OutputDeviceMode.VJoy;
            switch (mode)
            {
                case OutputDeviceMode.VJoy:
                    return Task.Run(() =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var result = _vjoyDiagnosticFunc();
                        ct.ThrowIfCancellationRequested();
                        string? selectionAdvice = result.IsDriverMissing ? null : _selectionAdvice(result.TargetDeviceId);
                        if (selectionAdvice != null)
                            return DeviceDiagnosticSnapshot.Unavailable(OutputDeviceMode.VJoy,
                                L("Output_VJoySelectionIssue"), selectionAdvice, [selectionAdvice]);

                        if (result.IsValid)
                        {
                            RequiresRestart = false;
                            string status = !string.IsNullOrWhiteSpace(result.SummaryMessage)
                                ? result.SummaryMessage
                                : L("DiagPlatform_WindowsOutputDiagnostics_131");
                            return DeviceDiagnosticSnapshot.Available(OutputDeviceMode.VJoy, status);
                        }

                        var actions = new List<string>();
                        if (!string.IsNullOrWhiteSpace(result.ActionableAdvice))
                        {
                            actions.Add(result.ActionableAdvice);
                        }

                        string errorDetails = L("DiagPlatform_WindowsOutputDiagnostics_132", result.DeviceStatusText, result.TargetDeviceId);
                        if (result.MissingAxes.Count > 0)
                        {
                            errorDetails += L("DiagPlatform_WindowsOutputDiagnostics_133", string.Join(", ", result.MissingAxes));
                        }

                        return DeviceDiagnosticSnapshot.Unavailable(
                            OutputDeviceMode.VJoy,
                            !string.IsNullOrWhiteSpace(result.SummaryMessage) ? result.SummaryMessage : L("DiagPlatform_WindowsOutputDiagnostics_134"),
                            errorDetails,
                            actions,
                            canRepair: result.CanSyncDll || result.IsDriverMissing);
                    }, ct);

                default:
                    return Task.FromResult(DeviceDiagnosticSnapshot.Unavailable(
                        mode,
                        L("DiagPlatform_WindowsOutputDiagnostics_137", mode)));
            }
        }

        public Task<bool> RequestRestartAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
            return Task.FromResult(process != null);
        }
        public bool RequiresRestart { get; private set; }
        public string? BootSessionId => WindowsBootSession.Read();
        public string PreparationDescription => _localization.GetString("Prep_WindowsChanges");

        public async Task<bool> TryRepairAsync(OutputDeviceMode mode, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            RequiresRestart = false;
            if (mode == OutputDeviceMode.Xbox360) mode = OutputDeviceMode.VJoy;
            if (mode != OutputDeviceMode.VJoy) return false;
            bool missing = _vjoyDiagnosticFunc().IsDriverMissing;
            if (missing && !(mode == OutputDeviceMode.VJoy && _vjoyDiagnosticFunc().CanSyncDll))
            {
                int exitCode = await new WindowsDriverInstaller().InstallAsync(mode, ct).ConfigureAwait(false);
                if (exitCode == 1602) throw new SimRacingHub.Application.Contracts.GameSetupException("Prep_Error_DriverCancelled", "Installer exit 1602");
                RequiresRestart = exitCode is 3010 or 1641;
                if (exitCode != 0 && !RequiresRestart) throw new SimRacingHub.Application.Contracts.GameSetupException("Prep_Error_DriverInstall", "Installer exit " + exitCode);
                if (RequiresRestart) return true;
            }
            if (mode == OutputDeviceMode.VJoy)
            {
                var result = _vjoyDiagnosticFunc();
                if (result.CanSyncDll)
                {
                    var sync = VJoyDiagnosticService.SyncSystemDll(localization: _localization);
                    if (!sync.Success) throw new SimRacingHub.Application.Contracts.GameSetupException("Prep_Error_Output", sync.Message);
                    result = _vjoyDiagnosticFunc();
                }
                if (result.CanConfigureDevice)
                {
                    throw new GameSetupException("Prep_Error_Output", L("Output_VJoyUseSetupCard"));
                }
            }
            return (await CheckAsync(mode, ct).ConfigureAwait(false)).IsAvailable;
        }

        public Task<string?> BackupVJoyAsync(int deviceId, CancellationToken ct = default) => Task.Run<string?>(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (deviceId is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(deviceId));
            var snapshot = new WindowsVJoyConfigurationStore().Read((uint)deviceId);
            ct.ThrowIfCancellationRequested();
            var journal = new VJoyConfigurationJournal();
            var record = journal.Prepare(snapshot, backupOnly: true);
            return journal.BackupPath(record.Id);
        }, ct);

        public Task<VJoyDeviceInventory> InspectVJoyAsync(CancellationToken ct = default) =>
            Task.Run(() => new VJoyInventoryService().Inspect(ct), ct);

        public Task<bool> SelectVJoyAsync(VJoyDeviceInfo inspectedDevice, bool wheelCompatibility, CancellationToken ct = default) =>
            Task.Run(() => { ct.ThrowIfCancellationRequested(); return new VJoyInventoryService().Select(inspectedDevice, wheelCompatibility); }, ct);

        public Task<VJoySetupPreview?> PreviewVJoyAsync(VJoyDeviceInfo inspectedDevice, bool changeExisting, CancellationToken ct = default) =>
            Task.Run<VJoySetupPreview?>(() => { ct.ThrowIfCancellationRequested(); return new VJoySetupService().Preview(inspectedDevice, changeExisting); }, ct);

        public async Task<VJoySetupResult> ApplyVJoyAsync(VJoySetupPreview preview, CancellationToken ct = default)
        {
            var result = await new VJoySetupService().ApplyAsync(preview, ct).ConfigureAwait(false);
            RequiresRestart = result.RequiresRestart;
            return result;
        }

        public Task<VJoySetupPreview?> PreviewVJoyRemovalAsync(CancellationToken ct = default) =>
            Task.Run<VJoySetupPreview?>(() => { ct.ThrowIfCancellationRequested(); return new VJoyMaintenanceService().PreviewRemoval(); }, ct);

        public Task<VJoySetupPreview?> PreviewVJoyRemovalAsync(VJoyDeviceInfo inspectedDevice, CancellationToken ct = default) =>
            Task.Run<VJoySetupPreview?>(() => { ct.ThrowIfCancellationRequested(); return new VJoyMaintenanceService().PreviewRemoval(inspectedDevice); }, ct);

        public Task<VJoySetupPreview?> PreviewMissingVJoyRestoreAsync(CancellationToken ct = default) =>
            Task.Run<VJoySetupPreview?>(() => { ct.ThrowIfCancellationRequested(); return new VJoyMaintenanceService().PreviewMissingRestore(); }, ct);

        public Task<VJoyPendingSetup?> InspectPendingVJoyAsync(CancellationToken ct = default) =>
            Task.Run(() => { ct.ThrowIfCancellationRequested(); return new VJoyMaintenanceService().InspectPending(); }, ct);

        public async Task<VJoySetupResult> ResolvePendingVJoyAsync(VJoyPendingSetup preview, bool resume, CancellationToken ct = default)
        {
            var result = await new VJoyMaintenanceService().ResolvePendingAsync(preview, resume, ct).ConfigureAwait(false);
            RequiresRestart = result.RequiresRestart;
            return result;
        }

        public Task<bool> CreateVJoyAsync(VJoyDeviceInfo inspectedDevice, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            throw new GameSetupException("Prep_Error_Output", L("Output_VJoyUseSetupCard"));
        }

        public Task<bool> ConfigureWheelAsync(int deviceId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var result = new VJoyDiagnosticService(_localization).PerformDiagnostic(VJoyRequirements.ForProfile(deviceId, true));
            if (result.IsValid) return Task.FromResult(true);
            throw new GameSetupException("Prep_Error_Output", L("Output_VJoyUseSetupCard"));
        }
    }
}
