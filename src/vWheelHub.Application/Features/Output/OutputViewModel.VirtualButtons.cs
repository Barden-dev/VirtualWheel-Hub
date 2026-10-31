using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Output;

public sealed partial class VirtualButtonRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Func<int, string> _label;
    private bool _automaticName;
    [ObservableProperty] private string _name;
    [ObservableProperty] private int _key;
    [ObservableProperty] private string? _keyError;
    [ObservableProperty] private int _button;
    [ObservableProperty] private bool _pulse;
    [ObservableProperty] private int _pulseMs;
    [ObservableProperty] private bool _allowInMenu;
    public IRelayCommand DeleteCommand { get; }
    public string TargetLabel => _label(Button);
    internal VirtualButtonRowViewModel(VirtualButtonBinding binding, Action changed, Action<VirtualButtonRowViewModel> delete, Func<int, string> label)
    {
        _automaticName = IsGeneratedName(binding.Name) || binding.Name == label(binding.Button);
        _name = DisplayName(binding, label);
        _key = binding.Key; _button = binding.Button; _pulse = binding.Mode == VirtualButtonMode.Pulse;
        _pulseMs = binding.PulseMs; _allowInMenu = binding.AllowInMenu; _changed = changed; _label = label;
        DeleteCommand = new RelayCommand(() => delete(this));
        PropertyChanged += (_, e) => { if (e.PropertyName != nameof(TargetLabel) && e.PropertyName != nameof(KeyError)) _changed(); };
    }
    partial void OnNameChanged(string value) => _automaticName = IsGeneratedName(value) || value == _label(Button);
    public static string DisplayName(VirtualButtonBinding binding, Func<int, string> label) =>
        IsGeneratedName(binding.Name) || binding.Name == label(binding.Button) ? label(binding.Button) : binding.Name;
    partial void OnButtonChanged(int value)
    {
        if (_automaticName) Name = _label(value);
        OnPropertyChanged(nameof(TargetLabel));
    }
    private static bool IsGeneratedName(string? name)
    {
        if (name == null) return false;
        foreach (string prefix in new[] { "Button ", "Кнопка " })
            if (name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(name.AsSpan(prefix.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int number) && number is >= 1 and <= 128)
                return true;
        return false;
    }
    internal void RefreshTargetLabel() => OnPropertyChanged(nameof(TargetLabel));
    internal VirtualButtonBinding Binding => new(Name, Key, Button, Pulse ? VirtualButtonMode.Pulse : VirtualButtonMode.Hold, PulseMs, AllowInMenu);
}

public partial class OutputViewModel
{
    public ObservableCollection<VirtualButtonRowViewModel> VirtualButtonRows { get; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVirtualButtonError))]
    private string? _virtualButtonError;
    public bool HasVirtualButtonError => !string.IsNullOrEmpty(VirtualButtonError);
    private bool _refreshingButtons;
    public int VirtualButtonLimit
    {
        get
        {
            if (SelectedOutputMode == OutputDeviceMode.Xbox360) return IsUInputSupported ? 8 : 14;
            if (IsUInputSupported) return 42;
            var device = EffectiveVJoyDevice;
            return device == null ? 128 : Math.Clamp(device.Buttons, 0, 128);
        }
    }

    public string VirtualButtonTargetLabel(int button)
    {
        string[] xbox = ["LB", "RB", "A", "B", "X", "Y", "Back", "Start", "Left stick", "Right stick", "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right"];
        return SelectedOutputMode == OutputDeviceMode.Xbox360 && button >= 1 && button <= xbox.Length ? xbox[button - 1] : "Button " + button;
    }

    private void RefreshVirtualButtons()
    {
        if (_refreshingButtons) return;
        var bindings = CurrentProfile?.VirtualButtons ?? ResolvedProfile?.VirtualButtons ?? [];
        if (VirtualButtonRows.Select(row => row.Binding).SequenceEqual(bindings)) { ValidateVirtualButtonRows(); return; }
        _refreshingButtons = true;
        try
        {
            VirtualButtonRows.Clear();
            foreach (var binding in bindings.Where(binding => binding != null))
                VirtualButtonRows.Add(new(binding, SaveVirtualButtons, DeleteVirtualButton, VirtualButtonTargetLabel));
            ValidateVirtualButtonRows();
        }
        finally { _refreshingButtons = false; }
    }

    private void ValidateVirtualButtonRows()
    {
        if (ResolvedProfile == null) return;
        UpdateVirtualButtonKeyErrors();
        string? error = VirtualButtonProcessor.Validate(VirtualButtonRows.Select(row => row.Binding).ToArray(),
            VirtualButtonLimit, ResolvedProfile.GetReservedVirtualButtonKeys());
        VirtualButtonError = error == null ? null : _localizationManager.GetString("Output_ButtonsInvalid") + " " + error;
    }

    private void SaveVirtualButtons()
    {
        if (_refreshingButtons || CurrentProfile == null || ResolvedProfile == null) return;
        UpdateVirtualButtonKeyErrors();
        if (_runtime?.IsRunning == true) { VirtualButtonError = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        var bindings = VirtualButtonRows.Select(row => row.Binding).ToArray();
        string? error = VirtualButtonProcessor.Validate(bindings, VirtualButtonLimit, ResolvedProfile.GetReservedVirtualButtonKeys());
        VirtualButtonError = error == null ? null : _localizationManager.GetString("Output_ButtonsInvalid") + " " + error;
        if (error != null) return;
        _refreshingButtons = true;
        try { CurrentProfile.VirtualButtons = bindings; }
        finally { _refreshingButtons = false; }
    }

    private void UpdateVirtualButtonKeyErrors()
    {
        if (ResolvedProfile is not { } profile) return;
        var reserved = profile.GetReservedVirtualButtonKeys().Where(key => key != 0).ToHashSet();
        foreach (var row in VirtualButtonRows)
            row.KeyError = row.Key != 0 && (reserved.Contains(row.Key) ||
                VirtualButtonRows.Any(other => !ReferenceEquals(other, row) && other.Key == row.Key))
                ? string.Format(_localizationManager.GetString("Output_ButtonsKeyConflict"),
                    SimRacingHub.Core.KeyNameFormatter.Format(row.Key))
                : null;
    }

    [RelayCommand]
    public void AddVirtualButton()
    {
        if (_runtime?.IsRunning == true) { VirtualButtonError = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        int next = Enumerable.Range(3, Math.Max(0, VirtualButtonLimit - 2)).FirstOrDefault(id => VirtualButtonRows.All(row => row.Button != id));
        if (next == 0) return;
        VirtualButtonRows.Add(new(new(VirtualButtonTargetLabel(next), 0, next), SaveVirtualButtons, DeleteVirtualButton, VirtualButtonTargetLabel));
        SaveVirtualButtons();
    }

    private void DeleteVirtualButton(VirtualButtonRowViewModel row)
    {
        if (_runtime?.IsRunning == true) { VirtualButtonError = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        VirtualButtonRows.Remove(row);
        SaveVirtualButtons();
    }
}
