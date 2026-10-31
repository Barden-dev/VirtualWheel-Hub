using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime;

/// <summary>Owns button edges and pulses; suspension always requires a fresh press afterwards.</summary>
public sealed class VirtualButtonProcessor
{
    private VirtualButtonBinding[] _bindings = [];
    private bool[] _wasDown = [], _armed = [];
    private double[] _remaining = [];
    private int _buttonCount = -1;
    private bool _valid;

    public void Reset()
    {
        Array.Clear(_armed);
        Array.Clear(_remaining);
    }

    public static string? Validate(VirtualButtonBinding[] bindings, int buttonCount, IEnumerable<int>? reservedKeys = null)
    {
        if (bindings.Length > 126) return "At most 126 user buttons are supported.";
        var keys = new HashSet<int>(reservedKeys?.Where(key => key != 0) ?? []);
        var outputs = new HashSet<int>();
        foreach (var binding in bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || binding.Name.Length > 80 ||
                binding.Key is < 0 or > 255 || binding.Button < 3 || binding.Button > Math.Min(128, buttonCount) ||
                !Enum.IsDefined(binding.Mode) || binding.PulseMs is < 20 or > 1000)
                return "Choose a named binding, a supported output (buttons 1 and 2 are reserved for gears), and a pulse of 20–1000 ms.";
            if (!outputs.Add(binding.Button) || binding.Key != 0 && !keys.Add(binding.Key))
                return "A key or output is already assigned. Each user button must have its own key and output.";
        }
        return null;
    }

    public OutputButtons Process(VirtualButtonBinding[] bindings, IInputSource input, bool enabled, bool inMenu,
        double elapsedSeconds, int buttonCount = 128)
    {
        if (!_bindings.SequenceEqual(bindings) || _buttonCount != buttonCount)
        {
            _bindings = bindings.ToArray();
            _wasDown = new bool[bindings.Length];
            _armed = new bool[bindings.Length];
            _remaining = new double[bindings.Length];
            _buttonCount = buttonCount;
            _valid = Validate(bindings, buttonCount) == null;
        }
        if (!_valid) { Reset(); return default; }
        var output = new OutputButtons();
        double elapsed = double.IsFinite(elapsedSeconds) ? Math.Max(0, elapsedSeconds) : 0;
        for (int i = 0; i < _bindings.Length; i++)
        {
            var binding = _bindings[i];
            bool down = binding.Key != 0 && input.IsKeyDown(binding.Key);
            if (!enabled || inMenu && !binding.AllowInMenu)
            { _armed[i] = false; _remaining[i] = 0; }
            else
            {
                if (!down) _armed[i] = true;
                _remaining[i] = Math.Max(0, _remaining[i] - elapsed);
                if (_armed[i] && down && !_wasDown[i] && binding.Mode == VirtualButtonMode.Pulse)
                    _remaining[i] = binding.PulseMs / 1000.0;
                if (_armed[i] && (binding.Mode == VirtualButtonMode.Hold ? down : _remaining[i] > 0))
                    output = output.With(binding.Button);
            }
            _wasDown[i] = down;
        }
        return output;
    }
}
