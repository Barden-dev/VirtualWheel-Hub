namespace SimRacingHub.Application.Contracts;

public sealed record VJoySetupPreview(string? OperationId, VJoyDeviceInfo Before, VJoyDeviceInfo Target,
    bool NeedsChange, bool ChangesExistingDevice, string? BackupPath, string ExpectedFingerprint);

public sealed record VJoySetupResult(bool Success, bool RequiresRestart, string? OperationId = null, string? ErrorKey = null, string? Detail = null);

public sealed record VJoyPendingSetup(string OperationId, int DeviceId, string CurrentFingerprint,
    string BackupPath, bool CanResume, bool CanCancel, bool CancelRemovesConfiguration, bool NeedsRegistryRestore = false,
    string Purpose = "wheel-create");
