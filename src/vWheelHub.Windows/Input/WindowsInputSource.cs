using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Input;

/// <summary>Physical device routing. An unset choice preserves aggregate Windows input.</summary>
public sealed class WindowsInputSource : IInputSource
{
    private readonly object _lifecycle = new();
    private readonly string _settingsPath;
    private IDisposable? _lease;
    private WindowsInputState? _state;
    private bool _disposed;

    public WindowsInputSource(string? settingsPath = null) => _settingsPath = settingsPath ?? StoragePaths.AppSettingsPath;
    public bool IsRunning => Volatile.Read(ref _lease) != null && _state?.Connected == true && WindowsRawInputHub.IsRunning;

    public void Start()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning) return;
            Stop();
            var settings = AppSettings.Load(_settingsPath);
            if (!string.IsNullOrWhiteSpace(settings.WindowsSelectedMousePath) || !string.IsNullOrWhiteSpace(settings.WindowsSelectedKeyboardPath))
            {
                var devices = WindowsRawDevices.Discover();
                if (WindowsDevicePathResolver.Restore(settings, devices)) settings.Save(_settingsPath);
                WindowsDevicePathResolver.EnsureConnected(settings, devices);
            }
            var state = new WindowsInputState(settings.WindowsSelectedMousePath, settings.WindowsSelectedKeyboardPath,
                Enumerable.Range(1, 254).Where(GlobalKeyboard.IsKeyDown));
            _state = state;
            _lease = WindowsRawInputHub.Subscribe(state.Accept);
        }
    }

    public void Stop()
    {
        lock (_lifecycle)
        {
            Interlocked.Exchange(ref _lease, null)?.Dispose();
            Interlocked.Exchange(ref _state, null)?.Clear();
        }
    }

    public int ConsumeDeltaX() => IsRunning ? Volatile.Read(ref _state)?.ConsumeDeltaX() ?? 0 : 0;
    public bool IsKeyDown(int keyCode)
    {
        var state = _state;
        if (state == null || !IsRunning) return false;
        bool mouse = keyCode is 1 or 2 or 4 or 5 or 6;
        return (mouse ? state.Mouse : state.Keyboard) == null
            ? GlobalKeyboard.IsKeyDown(keyCode) : state.IsKeyDown(keyCode);
    }

    public void Dispose()
    {
        lock (_lifecycle) { if (_disposed) return; _disposed = true; Stop(); }
    }
}

/// <summary>Only packet state is locked; native lifecycle and UI work use separate locks.</summary>
internal sealed class WindowsInputState(string? mouse, string? keyboard, IEnumerable<int>? initiallyHeld = null)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<int>> _keys = new(StringComparer.OrdinalIgnoreCase);
    // Windows cannot query held keys per device. Never arm a key already held when capture starts.
    private readonly HashSet<int> _blocked = new(initiallyHeld ?? Array.Empty<int>());
    private long _delta;
    private volatile bool _connected = true;
    public string? Mouse { get; } = mouse;
    public string? Keyboard { get; } = keyboard;
    public bool Connected => _connected;

    public void Accept(WindowsRawPacket packet)
    {
        lock (_gate)
        {
            if (packet.Reset) { Clear(); return; }
            if (packet.Removed)
            {
                _keys.Remove(packet.Path);
                if (Same(packet.Path, Mouse) || Same(packet.Path, Keyboard))
                { _connected = false; _keys.Clear(); Interlocked.Exchange(ref _delta, 0); }
                return;
            }
            if (!_connected) return;
            string? selected = packet.Kind == InputDeviceKind.Mouse ? Mouse : Keyboard;
            if (selected != null && !Same(selected, packet.Path)) return;
            if (packet.Kind == InputDeviceKind.Mouse) Interlocked.Add(ref _delta, packet.X);
            if (packet.Key is <= 0 or > 254) return;
            int generic = packet.Key is 160 or 161 ? 16 : packet.Key is 162 or 163 ? 17 : packet.Key is 164 or 165 ? 18 : packet.Key;
            if (_blocked.Contains(packet.Key) || _blocked.Contains(generic))
            {
                if (!packet.Down) { _blocked.Remove(packet.Key); _blocked.Remove(generic); }
                return;
            }
            if (!_keys.TryGetValue(packet.Path, out var held)) _keys[packet.Path] = held = new();
            if (packet.Down) held.Add(packet.Key); else held.Remove(packet.Key);
        }
    }

    private static bool Same(string? a, string? b) => b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    public int ConsumeDeltaX()
    {
        long value = Interlocked.Exchange(ref _delta, 0);
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
    public bool IsKeyDown(int key)
    {
        lock (_gate)
        {
            if (!_connected) return false;
            foreach (var keys in _keys.Values)
            {
                bool held = key switch
                {
                    16 => keys.Contains(160) || keys.Contains(161),
                    17 => keys.Contains(162) || keys.Contains(163),
                    18 => keys.Contains(164) || keys.Contains(165),
                    _ => keys.Contains(key)
                };
                if (held) return true;
            }
            return false;
        }
    }

    public void Clear() { lock (_gate) { _connected = false; _keys.Clear(); Interlocked.Exchange(ref _delta, 0); } }
}
