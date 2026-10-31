using System;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime;

/// <summary>Owns the feeder and serializes lifecycle, driving and test reports using the runtime state lock.</summary>
internal sealed class OutputConnection : IOutputFeeder
{
    private const int MaxFailuresBeforeReacquire = 10;
    private readonly IOutputFeederFactory _factory;
    private readonly object _sync;
    private IOutputFeeder? _feeder;
    private OutputDeviceMode _mode = OutputDeviceMode.VJoy;
    private int _deviceId = 1;

    internal OutputConnection(IOutputFeederFactory factory, object sync) { _factory = factory; _sync = sync; }
    internal int ResolveVJoyDeviceId(ResolvedProfile profile)
    {
        try { return _factory.ResolveVJoyDeviceId(profile); }
        catch (Exception error) { LastStartFailure = error; return 0; }
    }
    internal bool HasFeeder { get { lock (_sync) return _feeder != null; } }
    internal Exception? LastStartFailure { get; private set; }
    internal bool LastWriteReacquired { get; private set; }
    public string Name { get { lock (_sync) return _feeder?.Name ?? "Virtual output"; } }
    public int ConsecutiveFailures { get { lock (_sync) return _feeder?.ConsecutiveFailures ?? 0; } }
    public Exception? LastError { get { lock (_sync) return _feeder?.LastError; } }
    public bool IsConnected { get { lock (_sync) return _feeder?.IsConnected ?? false; } }
    public OutputTestCapabilities TestCapabilities { get { lock (_sync) return _feeder?.TestCapabilities ?? new(0); } }

    private void Replace(OutputDeviceMode mode, int deviceId = 1)
    {
        ReleaseAndDispose();
        _mode = mode;
        _deviceId = Math.Clamp(deviceId, 1, 16);
        _feeder = _factory.CreateFeeder(mode, _deviceId);
    }

    internal bool PrepareForStart(OutputDeviceMode mode, int deviceId = 1)
    {
        mode = ResolveMode(mode);
        lock (_sync)
        {
            if (mode == OutputDeviceMode.VJoy && deviceId is < 1 or > 16)
            {
                LastStartFailure ??= new InvalidOperationException("The selected vJoy output is unavailable. Select it explicitly; no fallback device was used.");
                return false;
            }
            LastStartFailure = null;
            deviceId = Math.Clamp(deviceId, 1, 16);
            if (_feeder == null || _mode != mode || (mode == OutputDeviceMode.VJoy && _deviceId != deviceId))
            {
                Replace(mode, deviceId);
                if (!_feeder!.Acquire())
                {
                    string name = _feeder.Name;
                    LastStartFailure = _feeder.LastError ?? new InvalidOperationException($"Failed to acquire {name}.");
                    _feeder.Dispose();
                    _feeder = null;
                    AppLogger.Instance.LogError($"Failed to acquire {name}");
                    return false;
                }
            }
            else if (!_feeder.IsConnected && !_feeder.Acquire())
            {
                LastStartFailure = _feeder.LastError ?? new InvalidOperationException($"Failed to re-acquire {_feeder.Name}.");
                AppLogger.Instance.LogError($"Failed to re-acquire {_feeder.Name}");
                return false;
            }
            return true;
        }
    }

    internal bool SynchronizeProfile(OutputDeviceMode mode, OutputDeviceMode previousMode, bool wasRunning, int deviceId = 1)
    {
        mode = ResolveMode(mode);
        previousMode = ResolveMode(previousMode);
        lock (_sync)
        {
            if (mode == OutputDeviceMode.VJoy && deviceId is < 1 or > 16) { ReleaseAndDispose(); return false; }
            deviceId = Math.Clamp(deviceId, 1, 16);
            if (mode == OutputDeviceMode.VJoy && _feeder != null && _deviceId != deviceId)
                ReleaseAndDispose();
            // Preserve both existing phases, including Xbox acquisition while stopped.
            if (mode == OutputDeviceMode.Xbox360)
            {
                if (_feeder == null || _mode != OutputDeviceMode.Xbox360 || !_feeder.IsConnected)
                {
                    Replace(OutputDeviceMode.Xbox360);
                    _feeder!.Acquire();
                }
            }
            else if (_feeder != null && _mode == OutputDeviceMode.Xbox360) ReleaseAndDispose();

            if (wasRunning)
            {
                if (_feeder == null || previousMode != mode)
                {
                    Replace(mode, deviceId);
                    if (!_feeder!.Acquire())
                    {
                        AppLogger.Instance.LogError($"Failed to switch output to {_feeder.Name}");
                        return false;
                    }
                }
                else if (!_feeder.IsConnected && !_feeder.Acquire())
                {
                    AppLogger.Instance.LogError($"Failed to re-acquire {_feeder.Name}");
                    return false;
                }
            }
            return true;
        }
    }

    internal void Stop(bool retainConnection)
    {
        lock (_sync)
        {
            if (_feeder == null) return;
            _feeder.UpdateAxesAndButtons(16384, 0, 0, false, false);
            if (_mode != OutputDeviceMode.Xbox360 && !retainConnection) ReleaseAndDispose();
        }
    }

    internal OutputTestCapabilities GetTestCapabilities(OutputDeviceMode mode, int deviceId = 1)
    {
        mode = ResolveMode(mode);
        lock (_sync)
        {
            if (mode == OutputDeviceMode.VJoy && deviceId is < 1 or > 16) return new(0);
            if (_feeder != null) return _feeder.TestCapabilities;
            using var probe = _factory.CreateFeeder(mode, Math.Clamp(deviceId, 1, 16));
            return probe.TestCapabilities;
        }
    }

    internal bool SendWithRecovery(int steer, int gas, int brake, bool shiftDown, bool shiftUp, OutputButtons extraButtons = default)
    {
        lock (_sync)
        {
            LastWriteReacquired = false;
            if (_feeder == null) return true;
            var buttons = new OutputButtons(extraButtons.Low | OutputButtons.Gears(shiftDown, shiftUp).Low, extraButtons.High);
            bool sent = extraButtons == default ? _feeder.UpdateAxesAndButtons(steer, gas, brake, shiftDown, shiftUp)
                : _feeder.UpdateReport(new(steer, gas, brake, buttons));
            if (sent) return true;
            if (_feeder.ConsecutiveFailures <= MaxFailuresBeforeReacquire) return true;
            AppLogger.Instance.LogWarning($"{_feeder.Name} stopped accepting data ({_feeder.ConsecutiveFailures} failed attempts), trying to re-acquire it");
            if (_feeder.LastError != null) AppLogger.Instance.LogError($"Last error reported by {_feeder.Name}", _feeder.LastError);
            if (_feeder.TryReacquire())
            {
                LastWriteReacquired = true;
                AppLogger.Instance.LogInfo($"{_feeder.Name} re-acquired, vWheel Hub continues to run");
                return true;
            }
            return false;
        }
    }

    public bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp)
    {
        lock (_sync) return _feeder?.UpdateAxesAndButtons(steer, gas, brake, shiftDown, shiftUp) ?? true;
    }
    public bool UpdateReport(OutputReport report)
    {
        lock (_sync) return report.Buttons.High == 0 && (report.Buttons.Low & ~3UL) == 0
            ? _feeder?.UpdateAxesAndButtons(report.Steer, report.Gas, report.Brake, report.Buttons.IsDown(1), report.Buttons.IsDown(2)) ?? true
            : _feeder?.UpdateReport(report) ?? true;
    }
    public bool UpdateTestReport(OutputTestReport report) { lock (_sync) return _feeder?.UpdateTestReport(report) ?? false; }
    public bool Acquire() { lock (_sync) return _feeder?.Acquire() ?? false; }
    public void Release() { lock (_sync) _feeder?.Release(); }
    public bool TryReacquire() { lock (_sync) return _feeder?.TryReacquire() ?? false; }
    internal void ReleaseAndDispose()
    {
        lock (_sync)
        {
            if (_feeder?.IsConnected == true)
                _feeder.UpdateAxesAndButtons(16384, 0, 0, false, false);
            _feeder?.Release();
            _feeder?.Dispose();
            _feeder = null;
        }
    }
    public void Dispose() => ReleaseAndDispose();
    private OutputDeviceMode ResolveMode(OutputDeviceMode mode) =>
        _factory is IOutputModePolicy policy ? policy.ResolveOutputMode(mode) : mode;
}
