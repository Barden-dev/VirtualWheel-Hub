using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Fakes
{
    public sealed class FakeRuntimeSession : IRuntimeSession
    {
        private readonly List<RuntimeStartOptions> _recordedStartOptions = new();
        private readonly List<(Profile? Profile, ResolvedProfile Resolved)> _recordedProfiles = new();
        public bool IsRunning { get; set; }
        public bool IsLocked { get; set; }
        public bool HasFault { get; set; }
        public string? FaultMessage { get; set; }
        public RuntimeSnapshot CurrentSnapshot { get; set; } = RuntimeSnapshot.Stopped();
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int UpdateProfileCallCount { get; private set; }
        public int GetSnapshotCallCount { get; private set; }
        public int DisposeAsyncCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }
        public IReadOnlyList<RuntimeStartOptions> RecordedStartOptions => _recordedStartOptions;
        public IReadOnlyList<(Profile? Profile, ResolvedProfile Resolved)> RecordedProfiles => _recordedProfiles;

        public event Action<RuntimeSnapshot>? SnapshotUpdated;
        public event Action<string>? FaultOccurred;
        public Task StartAsync(RuntimeStartOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();
            StartCallCount++;
            _recordedStartOptions.Add(options);
            IsRunning = true;
            IsLocked = options.LockCursorOnStart;
            CurrentSnapshot = new RuntimeSnapshot(IsRunning: true, IsLocked: IsLocked, WheelAxis: 0, GasAxis: 0, BrakeAxis: 0, GameTitle: "Fake Simulator", StatusMessage: "Running");
            SnapshotUpdated?.Invoke(CurrentSnapshot);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            StopCallCount++;
            IsRunning = false;
            IsLocked = false;
            CurrentSnapshot = RuntimeSnapshot.Stopped();
            SnapshotUpdated?.Invoke(CurrentSnapshot);
            return Task.CompletedTask;
        }

        public void UpdateProfile(Profile profile, ResolvedProfile resolved)
        {
            ArgumentNullException.ThrowIfNull(resolved);
            UpdateProfileCallCount++;
            _recordedProfiles.Add((profile, resolved));
        }

        public void UpdateProfile(ResolvedProfile resolved)
        {
            ArgumentNullException.ThrowIfNull(resolved);
            UpdateProfileCallCount++;
            _recordedProfiles.Add((null, resolved));
        }

        public RuntimeSnapshot GetSnapshot()
        {
            GetSnapshotCallCount++;
            return CurrentSnapshot;
        }

        public void PublishSnapshot(RuntimeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            CurrentSnapshot = snapshot;
            SnapshotUpdated?.Invoke(snapshot);
        }

        public void TriggerFault(string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(errorMessage);
            HasFault = true;
            FaultMessage = errorMessage;
            FaultOccurred?.Invoke(errorMessage);
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCallCount++;
            IsRunning = false;
            IsLocked = false;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCallCount++;
            IsRunning = false;
            IsLocked = false;
        }

        public void Reset()
        {
            StartCallCount = 0;
            StopCallCount = 0;
            UpdateProfileCallCount = 0;
            GetSnapshotCallCount = 0;
            DisposeAsyncCallCount = 0;
            DisposeCallCount = 0;
            IsRunning = false;
            IsLocked = false;
            HasFault = false;
            FaultMessage = null;
            CurrentSnapshot = RuntimeSnapshot.Stopped();
            _recordedStartOptions.Clear();
            _recordedProfiles.Clear();
        }
    }
}
