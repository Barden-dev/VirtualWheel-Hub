using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IOutputDiagnostics with configurable mode snapshots and cancellation support.
    /// </summary>
    public sealed class FakeOutputDiagnostics : IOutputDiagnostics
    {
        private readonly Dictionary<OutputDeviceMode, DeviceDiagnosticSnapshot> _configuredSnapshots = new();
        private readonly List<OutputDeviceMode> _recordedModes = new();

        public int CheckCallCount { get; private set; }
        public IReadOnlyList<OutputDeviceMode> RecordedModes => _recordedModes;

        public FakeOutputDiagnostics()
        {
            // Sensible defaults for preview and tests
            _configuredSnapshots[OutputDeviceMode.VJoy] = DeviceDiagnosticSnapshot.Available(OutputDeviceMode.VJoy, "vJoy Virtual Device Ready");
            _configuredSnapshots[OutputDeviceMode.Xbox360] = DeviceDiagnosticSnapshot.Available(OutputDeviceMode.Xbox360, "ViGEm Xbox 360 Controller Ready");
        }

        public void SetSnapshot(OutputDeviceMode mode, DeviceDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _configuredSnapshots[mode] = snapshot;
        }

        public Task<DeviceDiagnosticSnapshot> CheckAsync(OutputDeviceMode mode, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            CheckCallCount++;
            _recordedModes.Add(mode);

            if (_configuredSnapshots.TryGetValue(mode, out var snapshot))
            {
                return Task.FromResult(snapshot);
            }

            return Task.FromResult(DeviceDiagnosticSnapshot.Unavailable(mode, "Unknown device mode"));
        }

        public void Reset()
        {
            CheckCallCount = 0;
            _recordedModes.Clear();
            _configuredSnapshots.Clear();
            _configuredSnapshots[OutputDeviceMode.VJoy] = DeviceDiagnosticSnapshot.Available(OutputDeviceMode.VJoy, "vJoy Virtual Device Ready");
            _configuredSnapshots[OutputDeviceMode.Xbox360] = DeviceDiagnosticSnapshot.Available(OutputDeviceMode.Xbox360, "ViGEm Xbox 360 Controller Ready");
        }
    }
}
