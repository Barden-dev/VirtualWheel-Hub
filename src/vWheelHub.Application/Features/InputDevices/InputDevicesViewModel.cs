using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.InputDevices;

/// <summary>Device choices, unsaved settings and diagnostics independent of a desktop view.</summary>
public partial class InputDevicesViewModel : ViewModelBase, IPendingSettingsChanges, IDisposable
{
    private readonly IInputDeviceSelectionService? _devices;
    private readonly IRuntimeSession _runtime;
    private readonly ILocalizationManager _language;
    private readonly InputDeviceCheckSession? _checks;
    private bool _active, _visible, _refreshing, _discovered, _selectionEdited, _saveFailed, _disposed;
    private string? _savedMouse, _savedKeyboard;
    private string _status = "InputDevices_Choose";
    private InputDeviceOption? _selectedMouse, _selectedKeyboard;
    private (bool Dirty, bool Ready, bool Busy, bool Error) _notifiedState;

    public event Action? Changed;
    public IReadOnlyList<InputDeviceOption> Mice { get; private set; } = Array.Empty<InputDeviceOption>();
    public IReadOnlyList<InputDeviceOption> Keyboards { get; private set; } = Array.Empty<InputDeviceOption>();
    public InputDeviceOption? SelectedMouse
    {
        get => _selectedMouse;
        set { if (SetProperty(ref _selectedMouse, value) && !_refreshing) SelectionChanged(); }
    }
    public InputDeviceOption? SelectedKeyboard
    {
        get => _selectedKeyboard;
        set { if (SetProperty(ref _selectedKeyboard, value) && !_refreshing) SelectionChanged(); }
    }
    public bool HasChanges => _selectionEdited && (SelectedMouse?.Path != _savedMouse || SelectedKeyboard?.Path != _savedKeyboard);
    public bool HasError => _saveFailed;
    public bool CanSave => !IsBusy && _checks?.IsPairIdentification != true && _devices != null && SelectedMouse != null && SelectedKeyboard != null;
    public bool IsPolling => !IsBusy && _checks?.IsChecking == true;
    public bool CanRefresh => !IsBusy;
    public bool CanSelectDevices => !IsBusy && _checks?.IsPairIdentification != true;
    public bool CanIdentifyPair => !IsBusy && Mice.Count > 0 && Keyboards.Count > 0;
    public bool CanIdentifyMouse => CanSelectDevices && Mice.Count > 0;
    public bool CanIdentifyKeyboard => CanSelectDevices && Keyboards.Count > 0;
    public bool CanCheck => !IsBusy && _devices != null && SelectedMouse != null && SelectedKeyboard != null;
    public bool ShowCheck => _checks?.IsChecking != true;
    public bool ShowStop => _checks?.IsChecking == true && _checks.Identifying == null && !IsBusy;
    public string IdentifyPairText => Text(_checks?.IsPairIdentification == true ? "InputDevices_CancelFind" : "InputDevices_FindPair");
    public string IdentifyMouseText => Text(_checks?.Identifying == InputDeviceKind.Mouse ? "InputDevices_CancelFind" : "InputDevices_FindMouse");
    public string IdentifyKeyboardText => Text(_checks?.Identifying == InputDeviceKind.Keyboard ? "InputDevices_CancelFind" : "InputDevices_FindKeyboard");
    public string StatusText => Text(_status);
    public bool ShowStatus => _status != "InputDevices_Choose";
    public string MouseActivityText => ActivityText(InputDeviceKind.Mouse, SelectedMouse, _savedMouse);
    public string KeyboardActivityText => ActivityText(InputDeviceKind.Keyboard, SelectedKeyboard, _savedKeyboard);

    public InputDevicesViewModel(IInputDeviceSelectionService? devices, IRuntimeSession runtime,
        ILocalizationManager language, Func<IDisposable?>? suspend = null, Func<long>? clock = null)
    {
        _devices = devices;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _language = language ?? throw new ArgumentNullException(nameof(language));
        if (devices != null) _checks = new InputDeviceCheckSession(devices, runtime, suspend, clock);
    }

    public void Activate(bool visible)
    {
        if (_disposed) return;
        if (!_active) { _active = true; _language.LanguageChanged += LanguageChanged; }
        _visible = visible;
        if (!_discovered || visible) RefreshDevices();
        NotifyState();
    }

    public void SetVisible(bool visible)
    {
        if (_disposed || _visible == visible) return;
        _visible = visible;
        if (!visible) _checks?.Stop();
        else RefreshDevices();
        NotifyState();
    }

    public void Deactivate()
    {
        _visible = false;
        _checks?.Stop();
        if (_active) _language.LanguageChanged -= LanguageChanged;
        _active = false;
        NotifyState();
    }

    [RelayCommand]
    public void RefreshDevices()
    {
        if (_disposed || IsBusy || _devices == null) return;
        _checks?.Stop();
        try
        {
            bool keepDraft = _discovered && HasChanges;
            string? mouse = keepDraft ? SelectedMouse?.Path : null;
            string? keyboard = keepDraft ? SelectedKeyboard?.Path : null;
            var found = _devices.Discover();
            _savedMouse = found.SelectedMouse; _savedKeyboard = found.SelectedKeyboard;
            if (!keepDraft) { mouse = _savedMouse; keyboard = _savedKeyboard; }
            _refreshing = true;
            Mice = found.Mice; Keyboards = found.Keyboards;
            OnPropertyChanged(nameof(Mice)); OnPropertyChanged(nameof(Keyboards));
            SelectedMouse = Mice.FirstOrDefault(d => d.Path == mouse);
            SelectedKeyboard = Keyboards.FirstOrDefault(d => d.Path == keyboard);
            _discovered = true;
            _status = Mice.Count == 0 || Keyboards.Count == 0 ? "InputDevices_None" :
                (found.SelectedMouse != null && SelectedMouse == null) || (found.SelectedKeyboard != null && SelectedKeyboard == null)
                    ? "InputDevices_Missing" : "InputDevices_Choose";
        }
        catch (Exception ex) { Error(ex); }
        finally { _refreshing = false; }
        NotifyState();
    }

    private void SelectionChanged()
    {
        _selectionEdited = true; _saveFailed = false;
        _checks?.Stop();
        _status = "InputDevices_Choose";
        NotifyState();
    }

    public void Revert()
    {
        if (_disposed || IsBusy) return;
        _checks?.Stop();
        _refreshing = true;
        try
        {
            SelectedMouse = Mice.FirstOrDefault(d => d.Path == _savedMouse);
            SelectedKeyboard = Keyboards.FirstOrDefault(d => d.Path == _savedKeyboard);
        }
        finally { _refreshing = false; }
        _selectionEdited = false; _saveFailed = false;
        _status = "InputDevices_Choose";
        NotifyState();
    }

    [RelayCommand]
    public async Task<bool> BeginCheckingAsync()
    {
        if (_checks?.IsChecking == true) return true;
        if (_disposed || IsBusy || _checks == null || !_visible) return false;
        IsBusy = true;
        NotifyState();
        try
        {
            if (!await _checks.StartAsync(Mice.ToArray(), Keyboards.ToArray())) return false;
            _status = _checks.StatusKey;
            return true;
        }
        catch (Exception ex) { Error(ex); return false; }
        finally { IsBusy = false; NotifyState(); }
    }

    [RelayCommand]
    public void CancelChecking()
    {
        _checks?.Stop();
        _status = "InputDevices_Choose";
        NotifyState();
    }

    [RelayCommand]
    public Task IdentifyMouseAsync() => IdentifyAsync(InputDeviceKind.Mouse);
    [RelayCommand]
    public Task IdentifyKeyboardAsync() => IdentifyAsync(InputDeviceKind.Keyboard);

    private async Task IdentifyAsync(InputDeviceKind kind)
    {
        if (_checks?.Identifying == kind) { CancelChecking(); return; }
        if (_checks?.IsChecking == true) _checks.Stop();
        if (!await BeginCheckingAsync()) return;
        _checks!.Identify(kind);
        _status = _checks.StatusKey;
        NotifyState();
    }

    [RelayCommand]
    public async Task IdentifyPairAsync()
    {
        if (_checks?.IsPairIdentification == true) { CancelChecking(); return; }
        if (_checks?.IsChecking == true) _checks.Stop();
        if (!await BeginCheckingAsync()) return;
        _checks!.Identify(InputDeviceKind.Mouse, pair: true);
        _status = _checks.StatusKey;
        NotifyState();
    }

    public void Poll()
    {
        if (!_visible || _runtime.IsRunning) { _checks?.Stop(); NotifyState(); return; }
        if (_checks?.IsChecking != true) return;
        try
        {
            var found = _checks.Poll();
            if (found is { } identified)
            {
                _refreshing = true;
                try
                {
                    if (identified.Kind == InputDeviceKind.Mouse) SelectedMouse = identified.Device;
                    else SelectedKeyboard = identified.Device;
                }
                finally { _refreshing = false; }
                _selectionEdited = true; _saveFailed = false;
            }
            _status = _checks.StatusKey;
        }
        catch (Exception ex) { _checks.Stop(); Error(ex); }
        NotifyState();
    }

    public async Task<bool> SaveAsync()
    {
        if (_disposed || !CanSave || !HasChanges || _devices == null || SelectedMouse is not { } mouse || SelectedKeyboard is not { } keyboard) return false;
        IsBusy = true;
        _checks!.PrepareSave();
        NotifyState();
        IDisposable? applyLease = null;
        try
        {
            applyLease = _checks!.TakeSaveLease();
            await _runtime.StopAsync();
            await Task.Run(() => _devices.Save(mouse, keyboard));
            _savedMouse = mouse.Path; _savedKeyboard = keyboard.Path;
            if (_runtime.IsRunning) await _runtime.StopAsync();
            _selectionEdited = false; _saveFailed = false;
            _status = "InputDevices_Saved";
            return true;
        }
        catch (Exception ex) { _saveFailed = true; Error(ex); return false; }
        finally { IsBusy = false; _checks?.Stop(); applyLease?.Dispose(); NotifyState(); }
    }

    private string Text(string key) => _language.GetString(key);
    private string ActivityText(InputDeviceKind kind, InputDeviceOption? device, string? savedPath) => Text(
        _checks?.ActivityStatusKey(kind, device, savedPath) ?? (device == null ? "InputDevices_Select" : device.Path == savedPath ? "InputDevices_SavedShort" : "InputDevices_Unsaved"));
    private void LanguageChanged(string _) => NotifyState();
    private void Error(Exception ex)
    {
        AppLogger.Instance.LogWarning("Input device selection: " + ex.Message);
        _status = "InputDevices_Error";
    }

    private void NotifyState()
    {
        foreach (string name in new[] { nameof(HasChanges), nameof(HasError), nameof(CanSave), nameof(IsPolling), nameof(CanRefresh),
            nameof(CanSelectDevices), nameof(CanIdentifyPair), nameof(CanIdentifyMouse), nameof(CanIdentifyKeyboard), nameof(CanCheck),
            nameof(ShowCheck), nameof(ShowStop), nameof(IdentifyPairText), nameof(IdentifyMouseText), nameof(IdentifyKeyboardText),
            nameof(StatusText), nameof(ShowStatus), nameof(MouseActivityText), nameof(KeyboardActivityText) }) OnPropertyChanged(name);
        var state = (HasChanges, CanSave, IsBusy, HasError);
        if (state != _notifiedState) { _notifiedState = state; Changed?.Invoke(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Deactivate();
        _checks?.Dispose();
    }
}
