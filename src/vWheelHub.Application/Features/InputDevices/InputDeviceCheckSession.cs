using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.InputDevices;

public readonly record struct IdentifiedInputDevice(InputDeviceKind Kind, InputDeviceOption Device);

/// <summary>Owns probes, shortcut suspension and the mouse/keyboard identification sequence.</summary>
public sealed class InputDeviceCheckSession : IDisposable
{
    private readonly IInputDeviceSelectionService _devices;
    private readonly IRuntimeSession _runtime;
    private readonly Func<IDisposable?>? _suspend;
    private readonly Func<long> _clock;
    private readonly List<Watch> _watches = new();
    private IDisposable? _lease;
    private int _generation;
    private bool _starting, _disposed;
    private long _armedAt, _deadline;

    public bool IsChecking { get; private set; }
    public bool IsPairIdentification { get; private set; }
    public InputDeviceKind? Identifying { get; private set; }
    public string StatusKey { get; private set; } = "InputDevices_Choose";

    private sealed class Watch(InputDeviceKind kind, InputDeviceOption option, IInputDeviceActivityProbe? probe)
    {
        public InputDeviceKind Kind { get; } = kind;
        public InputDeviceOption Option { get; } = option;
        public IInputDeviceActivityProbe? Probe { get; } = probe;
        public InputDeviceActivity Previous;
        public bool Connected;
        public long LastActivity;
    }

    public InputDeviceCheckSession(IInputDeviceSelectionService devices, IRuntimeSession runtime,
        Func<IDisposable?>? suspend = null, Func<long>? clock = null)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _suspend = suspend;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public async Task<bool> StartAsync(IReadOnlyList<InputDeviceOption> mice, IReadOnlyList<InputDeviceOption> keyboards)
    {
        if (_disposed || _starting) return false;
        if (IsChecking) return true;
        _starting = true;
        int generation = ++_generation;
        List<Watch>? opened = null;
        try
        {
            _lease = _suspend?.Invoke();
            await _runtime.StopAsync();
            if (generation != _generation) return false;
            opened = await Task.Run(() => OpenProbes(mice, keyboards));
            if (generation != _generation || _runtime.IsRunning) return false;
            foreach (var watch in opened)
            {
                watch.Previous = watch.Probe?.Read() ?? default;
                watch.Connected = watch.Previous.IsConnected;
            }
            _watches.AddRange(opened);
            opened = null; // Ownership passes to Stop only after every initial read succeeded.
            IsChecking = true;
            StatusKey = "InputDevices_Checking";
            return true;
        }
        finally
        {
            if (opened != null) foreach (var watch in opened) Release(watch.Probe);
            _starting = false;
            if (!IsChecking) Stop();
        }
    }

    private List<Watch> OpenProbes(IReadOnlyList<InputDeviceOption> mice, IReadOnlyList<InputDeviceOption> keyboards)
    {
        var opened = new List<Watch>();
        foreach (var (kind, options) in new[] { (InputDeviceKind.Mouse, mice), (InputDeviceKind.Keyboard, keyboards) })
            foreach (var device in options)
            {
                try { opened.Add(new Watch(kind, device, _devices.Watch(kind, device))); }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogWarning("Input device probe '" + device.Path + "': " + ex.Message);
                    opened.Add(new Watch(kind, device, null));
                }
            }
        return opened;
    }

    public void Identify(InputDeviceKind kind, bool pair = false)
    {
        if (!IsChecking) return;
        IsPairIdentification = pair;
        Arm(kind);
    }

    private void Arm(InputDeviceKind kind)
    {
        Identifying = kind;
        _armedAt = _clock() + 300;
        _deadline = _clock() + 10000;
        StatusKey = IsPairIdentification
            ? kind == InputDeviceKind.Mouse ? "InputDevices_PairMouse" : "InputDevices_PairKeyboard"
            : kind == InputDeviceKind.Mouse ? "InputDevices_MoveMouse" : "InputDevices_PressKey";
    }

    public IdentifiedInputDevice? Poll()
    {
        if (!IsChecking) return null;
        if (_runtime.IsRunning) { Stop(); return null; }
        long now = _clock();
        foreach (var watch in _watches)
        {
            var current = watch.Probe?.Read() ?? default;
            watch.Connected = current.IsConnected;
            bool moved = current.Movement != watch.Previous.Movement;
            bool pressed = current.Presses != watch.Previous.Presses;
            watch.Previous = current;
            if (!current.IsConnected) continue;
            if (moved || pressed) watch.LastActivity = now;
            if (Identifying != watch.Kind || now < _armedAt || !(watch.Kind == InputDeviceKind.Mouse ? moved : pressed)) continue;

            var found = new IdentifiedInputDevice(watch.Kind, watch.Option);
            bool nextKeyboard = IsPairIdentification && watch.Kind == InputDeviceKind.Mouse;
            bool pairComplete = IsPairIdentification && watch.Kind == InputDeviceKind.Keyboard;
            if (nextKeyboard)
            {
                // A press recorded before the keyboard prompt cannot complete the second stage.
                foreach (var entry in _watches) entry.Previous = entry.Probe?.Read() ?? default;
                Arm(InputDeviceKind.Keyboard);
            }
            else
            {
                Stop();
                StatusKey = pairComplete ? "InputDevices_PairFound" : "InputDevices_Found";
            }
            return found;
        }
        if (Identifying != null && now >= _deadline) { Stop(); StatusKey = "InputDevices_Timeout"; }
        return null;
    }

    public string ActivityStatusKey(InputDeviceKind kind, InputDeviceOption? device, string? savedPath)
    {
        var watch = _watches.FirstOrDefault(w => w.Kind == kind && w.Option == device);
        return Identifying == kind ? kind == InputDeviceKind.Mouse ? "InputDevices_MoveMouse" : "InputDevices_PressKey" :
            !IsChecking ? device == null ? "InputDevices_Select" : device.Path == savedPath ? "InputDevices_SavedShort" : "InputDevices_Unsaved" :
            watch?.Connected != true ? "InputDevices_Unavailable" :
            watch.LastActivity > 0 && _clock() - watch.LastActivity < 600 ? "InputDevices_Activity" : "InputDevices_Idle";
    }

    internal void PrepareSave()
    {
        ++_generation;
        Identifying = null;
    }

    internal IDisposable? TakeSaveLease()
    {
        var lease = _lease ?? _suspend?.Invoke();
        _lease = null;
        return lease;
    }

    public void Stop()
    {
        ++_generation;
        Identifying = null;
        IsPairIdentification = false;
        IsChecking = false;
        foreach (var watch in _watches) Release(watch.Probe);
        _watches.Clear();
        var lease = _lease;
        _lease = null;
        Release(lease);
    }

    private static void Release(IDisposable? resource)
    {
        try { resource?.Dispose(); }
        catch (Exception ex) { AppLogger.Instance.LogWarning("Input device check cleanup: " + ex.Message); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
