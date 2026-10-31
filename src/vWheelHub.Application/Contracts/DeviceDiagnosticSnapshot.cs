using System;
using System.Collections.Generic;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Diagnostic snapshot of a virtual output device mode (vJoy, ViGEm, uinput).
    /// </summary>
    public sealed record DeviceDiagnosticSnapshot(
        OutputDeviceMode Mode,
        bool IsAvailable,
        string StatusText,
        string? ErrorDetails = null,
        IReadOnlyList<string>? RecommendedActions = null,
        bool CanRepair = false)
    {
        public static DeviceDiagnosticSnapshot Available(OutputDeviceMode mode, string statusText = "Ready") =>
            new(mode, true, statusText);

        public static DeviceDiagnosticSnapshot Unavailable(
            OutputDeviceMode mode,
            string statusText,
            string? errorDetails = null,
            IReadOnlyList<string>? recommendedActions = null,
            bool canRepair = false) =>
            new(mode, false, statusText, errorDetails, recommendedActions, canRepair);
    }
}
