using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Localization;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.KeyBindings;

/// <summary>Coordinates key assignment, existing conflict validation, and row presentation.</summary>
public partial class KeyBindingsViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private SimRacingHub.Application.Features.Output.OutputViewModel? _virtualButtons;
    public KeyCaptureSession Capture { get; }
    public bool IsCapturing { get => Capture.IsCapturing; set => Capture.IsCapturing = value; }
    public KeyActionId? CapturingActionId { get => Capture.CapturingActionId; set => Capture.CapturingActionId = value; }
    public bool? CapturingIsSecondary { get => Capture.CapturingIsSecondary; set => Capture.CapturingIsSecondary = value; }
    public KeyBindingSlotViewModel? CapturingSlot { get => Capture.CapturingSlot; set => Capture.CapturingSlot = value; }
    private void CaptureChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
    private void CaptureChanging(object? sender, PropertyChangingEventArgs e) => OnPropertyChanging(e);
    private void OnCaptureStarted(object? sender, EventArgs e) => CaptureStarted?.Invoke(this, e);
    private void OnCaptureCancelled(object? sender, EventArgs e) => CaptureCancelled?.Invoke(this, e);
    private readonly ProfileSession _profileSession;

    private readonly ILocalizationManager _localizationManager;

    private Profile? _subscribedCurrentProfile;

    private ResolvedProfile? _subscribedResolvedProfile;

    private bool _eventsSubscribed;

    private bool _isDisposed;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string? _conflictMessage;

    private readonly List<KeyBindingRowViewModel> _rows;

    public ProfileSession ProfileSession => _profileSession;

    public Profile CurrentProfile => _profileSession.CurrentProfile;

    public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;

    public ILocalizationManager LocalizationManager => _localizationManager;

    public IReadOnlyList<KeyBindingRowViewModel> Rows => _rows;

    public IReadOnlyList<KeyBindingRowViewModel> Bindings => _rows;

    public bool IsKeyboardSteeringEnabled => ResolvedProfile?.UseKeyboardOnlyMode ?? false;

    public string PressAnyKeyText => _localizationManager.GetString("KeyBind_PressAnyKey", "Press any key...");

    public string NoneText => _localizationManager.GetString("KeyBind_None", "None");

    public string ConflictFormat => _localizationManager.GetString("KeyBind_ConflictFormat", "{0} already used by: {1}");

    public string WarningText => _localizationManager.GetString("Page_Keys_Warning",
        "Steer Left and Steer Right key bindings are active only when 'Keyboard only' mode is enabled in Steering settings.");

    public event EventHandler? ConflictOccurred;

    public event EventHandler? BindingAssigned;

    public event EventHandler? CaptureStarted;

    public event EventHandler? CaptureCancelled;

    public KeyBindingsViewModel(
        ProfileSession profileSession,
        ILocalizationManager localizationManager)
    {
        _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));

        Capture = new KeyCaptureSession(ClearConflict, TryAssignKey);
        Capture.PropertyChanged += CaptureChanged;
        Capture.PropertyChanging += CaptureChanging;
        Capture.CaptureStarted += OnCaptureStarted;
        Capture.CaptureCancelled += OnCaptureCancelled;
        _rows = KeyBindingCatalog.Actions.Select(a =>
            new KeyBindingRowViewModel(this, a.Id, a.Name, a.ResourceKey, a.ToolTipResourceKey)).ToList();

        SubscribeEvents();
    }

    public KeyBindingRowViewModel? GetRow(KeyActionId actionId)
    {
        return _rows.FirstOrDefault(r => r.ActionId == actionId);
    }

    // Steer Left
    public int? KeySteerLeft1
    {
        get => CurrentProfile?.KeySteerLeft1;
        set => TryAssignKey(KeyActionId.SteerLeft, isSecondary: false, value ?? 0);
    }

    public int? KeySteerLeft2
    {
        get => CurrentProfile?.KeySteerLeft2;
        set => TryAssignKey(KeyActionId.SteerLeft, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeySteerLeft1 => CurrentProfile?.KeySteerLeft1 ?? ResolvedProfile?.KeySteerLeft1 ?? 0;

    public int EffectiveKeySteerLeft2 => CurrentProfile?.KeySteerLeft2 ?? ResolvedProfile?.KeySteerLeft2 ?? 0;

    public string KeySteerLeft1Text => CanonicalKeyMapper.Format(EffectiveKeySteerLeft1);

    public string KeySteerLeft2Text => CanonicalKeyMapper.Format(EffectiveKeySteerLeft2);

    // Steer Right
    public int? KeySteerRight1
    {
        get => CurrentProfile?.KeySteerRight1;
        set => TryAssignKey(KeyActionId.SteerRight, isSecondary: false, value ?? 0);
    }

    public int? KeySteerRight2
    {
        get => CurrentProfile?.KeySteerRight2;
        set => TryAssignKey(KeyActionId.SteerRight, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeySteerRight1 => CurrentProfile?.KeySteerRight1 ?? ResolvedProfile?.KeySteerRight1 ?? 0;

    public int EffectiveKeySteerRight2 => CurrentProfile?.KeySteerRight2 ?? ResolvedProfile?.KeySteerRight2 ?? 0;

    public string KeySteerRight1Text => CanonicalKeyMapper.Format(EffectiveKeySteerRight1);

    public string KeySteerRight2Text => CanonicalKeyMapper.Format(EffectiveKeySteerRight2);

    // Throttle / Gas
    public int? KeyGas1
    {
        get => CurrentProfile?.KeyGas1;
        set => TryAssignKey(KeyActionId.Throttle, isSecondary: false, value ?? 0);
    }

    public int? KeyGas2
    {
        get => CurrentProfile?.KeyGas2;
        set => TryAssignKey(KeyActionId.Throttle, isSecondary: true, value ?? 0);
    }

    public int? KeyThrottle1 { get => KeyGas1; set => KeyGas1 = value; }

    public int? KeyThrottle2 { get => KeyGas2; set => KeyGas2 = value; }

    public int EffectiveKeyGas1 => CurrentProfile?.KeyGas1 ?? ResolvedProfile?.KeyGas1 ?? 0;

    public int EffectiveKeyGas2 => CurrentProfile?.KeyGas2 ?? ResolvedProfile?.KeyGas2 ?? 0;

    public int EffectiveKeyThrottle1 => EffectiveKeyGas1;

    public int EffectiveKeyThrottle2 => EffectiveKeyGas2;

    public string KeyGas1Text => CanonicalKeyMapper.Format(EffectiveKeyGas1);

    public string KeyGas2Text => CanonicalKeyMapper.Format(EffectiveKeyGas2);

    public string KeyThrottle1Text => KeyGas1Text;

    public string KeyThrottle2Text => KeyGas2Text;

    // Brake
    public int? KeyBrake1
    {
        get => CurrentProfile?.KeyBrake1;
        set => TryAssignKey(KeyActionId.Brake, isSecondary: false, value ?? 0);
    }

    public int? KeyBrake2
    {
        get => CurrentProfile?.KeyBrake2;
        set => TryAssignKey(KeyActionId.Brake, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyBrake1 => CurrentProfile?.KeyBrake1 ?? ResolvedProfile?.KeyBrake1 ?? 0;

    public int EffectiveKeyBrake2 => CurrentProfile?.KeyBrake2 ?? ResolvedProfile?.KeyBrake2 ?? 0;

    public string KeyBrake1Text => CanonicalKeyMapper.Format(EffectiveKeyBrake1);

    public string KeyBrake2Text => CanonicalKeyMapper.Format(EffectiveKeyBrake2);

    // Shift Up
    public int? KeyShiftUp1
    {
        get => CurrentProfile?.KeyShiftUp1;
        set => TryAssignKey(KeyActionId.ShiftUp, isSecondary: false, value ?? 0);
    }

    public int? KeyShiftUp2
    {
        get => CurrentProfile?.KeyShiftUp2;
        set => TryAssignKey(KeyActionId.ShiftUp, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyShiftUp1 => CurrentProfile?.KeyShiftUp1 ?? ResolvedProfile?.KeyShiftUp1 ?? 0;

    public int EffectiveKeyShiftUp2 => CurrentProfile?.KeyShiftUp2 ?? ResolvedProfile?.KeyShiftUp2 ?? 0;

    public string KeyShiftUp1Text => CanonicalKeyMapper.Format(EffectiveKeyShiftUp1);

    public string KeyShiftUp2Text => CanonicalKeyMapper.Format(EffectiveKeyShiftUp2);

    // Shift Down
    public int? KeyShiftDown1
    {
        get => CurrentProfile?.KeyShiftDown1;
        set => TryAssignKey(KeyActionId.ShiftDown, isSecondary: false, value ?? 0);
    }

    public int? KeyShiftDown2
    {
        get => CurrentProfile?.KeyShiftDown2;
        set => TryAssignKey(KeyActionId.ShiftDown, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyShiftDown1 => CurrentProfile?.KeyShiftDown1 ?? ResolvedProfile?.KeyShiftDown1 ?? 0;

    public int EffectiveKeyShiftDown2 => CurrentProfile?.KeyShiftDown2 ?? ResolvedProfile?.KeyShiftDown2 ?? 0;

    public string KeyShiftDown1Text => CanonicalKeyMapper.Format(EffectiveKeyShiftDown1);

    public string KeyShiftDown2Text => CanonicalKeyMapper.Format(EffectiveKeyShiftDown2);

    // Center Steering
    public int? KeyCenterSteering1
    {
        get => CurrentProfile?.KeyCenterSteering1;
        set => TryAssignKey(KeyActionId.CenterSteering, isSecondary: false, value ?? 0);
    }

    public int? KeyCenterSteering2
    {
        get => CurrentProfile?.KeyCenterSteering2;
        set => TryAssignKey(KeyActionId.CenterSteering, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyCenterSteering1 => CurrentProfile?.KeyCenterSteering1 ?? ResolvedProfile?.KeyCenterSteering1 ?? 0;

    public int EffectiveKeyCenterSteering2 => CurrentProfile?.KeyCenterSteering2 ?? ResolvedProfile?.KeyCenterSteering2 ?? 0;

    public string KeyCenterSteering1Text => CanonicalKeyMapper.Format(EffectiveKeyCenterSteering1);

    public string KeyCenterSteering2Text => CanonicalKeyMapper.Format(EffectiveKeyCenterSteering2);

    // Toggle Hub
    public int? KeyToggleHub1
    {
        get => CurrentProfile?.KeyToggleHub1;
        set => TryAssignKey(KeyActionId.ToggleHub, isSecondary: false, value ?? 0);
    }

    public int? KeyToggleHub2
    {
        get => CurrentProfile?.KeyToggleHub2;
        set => TryAssignKey(KeyActionId.ToggleHub, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyToggleHub1 => CurrentProfile?.KeyToggleHub1 ?? ResolvedProfile?.KeyToggleHub1 ?? 0;

    public int EffectiveKeyToggleHub2 => CurrentProfile?.KeyToggleHub2 ?? ResolvedProfile?.KeyToggleHub2 ?? 0;

    public string KeyToggleHub1Text => CanonicalKeyMapper.Format(EffectiveKeyToggleHub1);

    public string KeyToggleHub2Text => CanonicalKeyMapper.Format(EffectiveKeyToggleHub2);

    // Toggle Lock
    public int? KeyToggleLock1
    {
        get => CurrentProfile?.KeyToggleLock1;
        set => TryAssignKey(KeyActionId.ToggleLock, isSecondary: false, value ?? 0);
    }

    public int? KeyToggleLock2
    {
        get => CurrentProfile?.KeyToggleLock2;
        set => TryAssignKey(KeyActionId.ToggleLock, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyToggleLock1 => CurrentProfile?.KeyToggleLock1 ?? ResolvedProfile?.KeyToggleLock1 ?? 0;

    public int EffectiveKeyToggleLock2 => CurrentProfile?.KeyToggleLock2 ?? ResolvedProfile?.KeyToggleLock2 ?? 0;

    public string KeyToggleLock1Text => CanonicalKeyMapper.Format(EffectiveKeyToggleLock1);

    public string KeyToggleLock2Text => CanonicalKeyMapper.Format(EffectiveKeyToggleLock2);

    // Unlock Cursor
    public int? KeyUnlockCursor1
    {
        get => CurrentProfile?.KeyUnlockCursor1;
        set => TryAssignKey(KeyActionId.UnlockCursor, isSecondary: false, value ?? 0);
    }

    public int? KeyUnlockCursor2
    {
        get => CurrentProfile?.KeyUnlockCursor2;
        set => TryAssignKey(KeyActionId.UnlockCursor, isSecondary: true, value ?? 0);
    }

    public int EffectiveKeyUnlockCursor1 => CurrentProfile?.KeyUnlockCursor1 ?? ResolvedProfile?.KeyUnlockCursor1 ?? 0;

    public int EffectiveKeyUnlockCursor2 => CurrentProfile?.KeyUnlockCursor2 ?? ResolvedProfile?.KeyUnlockCursor2 ?? 0;

    public string KeyUnlockCursor1Text => CanonicalKeyMapper.Format(EffectiveKeyUnlockCursor1);

    public string KeyUnlockCursor2Text => CanonicalKeyMapper.Format(EffectiveKeyUnlockCursor2);

    internal int GetActionKey(KeyActionId actionId, bool isSecondary) =>
        KeyBindingCatalog.Find(actionId)?.Slot(isSecondary).Read(CurrentProfile, ResolvedProfile) ?? 0;

    internal void SetActionKey(KeyActionId actionId, bool isSecondary, int keyCode)
    {
        if (CurrentProfile == null) return;
        KeyBindingCatalog.Find(actionId)?.Slot(isSecondary).Write(CurrentProfile, keyCode == 0 ? 0 : keyCode);
    }

    public bool CheckConflict(int virtualKeyCode, string actionName, out string? conflictingActions)
    {
        conflictingActions = null;
        if (virtualKeyCode == 0) return false;

        if (ResolvedProfile != null &&
            ResolvedProfile.TryGetKeyBindingConflict(virtualKeyCode, actionName, out string conflicts))
        {
            conflictingActions = conflicts;
            return true;
        }

        return false;
    }

    public bool TryAssignKey(KeyActionId actionId, bool isSecondary, int keyCode)
    {
        var row = GetRow(actionId);
        string actionName = row?.ActionName ?? actionId.ToString();

        if (keyCode != 0 && CheckConflict(keyCode, actionName, out string? conflicting))
        {
            string message = string.Format(ConflictFormat, CanonicalKeyMapper.Format(keyCode), conflicting);
            SetConflictState(actionId, isSecondary, message);
            return false;
        }

        SetActionKey(actionId, isSecondary, keyCode);
        ClearConflictState(actionId, isSecondary);
        NotifyActionProperties(actionId);
        row?.Refresh();
        BindingAssigned?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void SetConflictState(KeyActionId actionId, bool isSecondary, string message)
    {
        var row = GetRow(actionId);
        var slot = isSecondary ? row?.Secondary : row?.Primary;
        slot?.SetConflict(message);
        row?.Refresh();

        HasConflict = true;
        ConflictMessage = message;
        ConflictOccurred?.Invoke(this, EventArgs.Empty);
    }

    private void ClearConflictState(KeyActionId actionId, bool isSecondary)
    {
        var row = GetRow(actionId);
        var slot = isSecondary ? row?.Secondary : row?.Primary;
        slot?.ClearConflict();
        row?.Refresh();

        if (!_rows.Any(r => r.HasConflict))
        {
            HasConflict = false;
            ConflictMessage = null;
        }
    }

    public void ClearConflict()
    {
        HasConflict = false;
        ConflictMessage = null;
        ClearAllRowConflicts();
    }

    private void ClearAllRowConflicts()
    {
        foreach (var row in _rows)
        {
            row.ClearConflict();
        }
    }

    public IReadOnlyList<(int KeyCode, string Actions)> GetAllConflicts()
    {
        var bindings = new List<(string Name, int KeyCode)>();

        foreach (var row in _rows)
        {
            int p = row.PrimaryVirtualKeyCode;
            int s = row.SecondaryVirtualKeyCode;
            if (p != 0) bindings.Add((row.ActionName, p));
            if (s != 0) bindings.Add((row.ActionName, s));
        }

        return bindings
            .Where(b => b.KeyCode != 0)
            .GroupBy(b => b.KeyCode)
            .Select(g => new
            {
                g.Key,
                Actions = g.Select(b => b.Name).Distinct().ToList()
            })
            .Where(c => c.Actions.Count > 1)
            .Select(c => (c.Key, string.Join(", ", c.Actions)))
            .ToList();
    }

    [RelayCommand]
    public void StartCapture(KeyBindingSlotViewModel? slot) => Capture.StartCapture(slot);

    public void StartCapture(KeyActionId actionId, bool isSecondary)
    {
        var row = GetRow(actionId);
        var slot = isSecondary ? row?.Secondary : row?.Primary;
        if (slot != null)
        {
            StartCapture(slot);
        }
    }

    [RelayCommand]
    public void CancelCapture() => Capture.CancelCapture();

    public void OnLostFocus() => Capture.OnLostFocus();

    public bool ApplyKey(int virtualKeyCode) => Capture.ApplyKey(virtualKeyCode);

    public bool ApplyMouseButton(AppMouseButton mouseButton)
    {
        return ApplyKey(CanonicalKeyMapper.ToVirtualKey(mouseButton));
    }

    public bool ApplyMouseButton(int virtualKeyCode)
    {
        return ApplyKey(virtualKeyCode);
    }

    [RelayCommand]
    public void ClearBinding(KeyBindingSlotViewModel? slot)
    {
        if (slot == null) return;
        ClearBinding(slot.ActionId, slot.IsSecondary);
    }

    public void ClearBinding(KeyActionId actionId, bool isSecondary)
    {
        TryAssignKey(actionId, isSecondary, 0);
        ClearConflictState(actionId, isSecondary);

        if (IsCapturing && CapturingActionId == actionId && CapturingIsSecondary == isSecondary)
        {
            CancelCapture();
        }
    }

    [RelayCommand]
    public void ClearAllBindings()
    {
        using var edit = _profileSession.BeginEditGroup(nameof(ClearAllBindings));
        if (CurrentProfile == null) return;

        CancelCapture();
        ClearConflict();

        foreach (var action in KeyBindingCatalog.Actions)
        {
            action.Primary.Write(CurrentProfile, 0);
            action.Secondary.Write(CurrentProfile, 0);
        }

        NotifyAllProperties();
        RefreshAllRows();
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        using var edit = _profileSession.BeginEditGroup(nameof(ResetToDefaults));
        if (CurrentProfile == null) return;

        CancelCapture();
        ClearConflict();

        ClearAllBindings(); // Free old assignments before applying a potentially overlapping default layout.
        var def = ProfileDefaults.CreateDefault();
        foreach (var action in KeyBindingCatalog.Actions)
        {
            action.Primary.Write(CurrentProfile, action.Primary.ReadCurrent(def));
            action.Secondary.Write(CurrentProfile, action.Secondary.ReadCurrent(def));
        }

        NotifyAllProperties();
        RefreshAllRows();
    }

    [RelayCommand]
    public void RevertProfile()
    {
        CancelCapture();
        ClearConflict();

        _profileSession.RevertProfile();

        NotifyAllProperties();
        RefreshAllRows();
    }

    internal string GetLocalizedString(string key, string fallback)
    {
        return _localizationManager.GetString(key, fallback);
    }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed) return;

        _profileSession.PropertyChanged += OnProfileSessionPropertyChanged;
        _profileSession.BindingConflictChanged += OnProfileSessionBindingConflictChanged;

        AttachProfileListeners();

        _localizationManager.LanguageChanged += OnLanguageChanged;
        _localizationManager.PropertyChanged += OnLocalizationPropertyChanged;

        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed) return;

        _profileSession.PropertyChanged -= OnProfileSessionPropertyChanged;
        _profileSession.BindingConflictChanged -= OnProfileSessionBindingConflictChanged;

        DetachProfileListeners();

        _localizationManager.LanguageChanged -= OnLanguageChanged;
        _localizationManager.PropertyChanged -= OnLocalizationPropertyChanged;

        _eventsSubscribed = false;
    }

    private void AttachProfileListeners()
    {
        if (_profileSession.CurrentProfile != null)
        {
            _subscribedCurrentProfile = _profileSession.CurrentProfile;
            _subscribedCurrentProfile.PropertyChanged += OnCurrentProfilePropertyChanged;
        }

        if (_profileSession.ResolvedProfile != null)
        {
            _subscribedResolvedProfile = _profileSession.ResolvedProfile;
            _subscribedResolvedProfile.PropertyChanged += OnResolvedProfilePropertyChanged;
        }
    }

    private void DetachProfileListeners()
    {
        if (_subscribedCurrentProfile != null)
        {
            _subscribedCurrentProfile.PropertyChanged -= OnCurrentProfilePropertyChanged;
            _subscribedCurrentProfile = null;
        }

        if (_subscribedResolvedProfile != null)
        {
            _subscribedResolvedProfile.PropertyChanged -= OnResolvedProfilePropertyChanged;
            _subscribedResolvedProfile = null;
        }
    }

    private void OnProfileSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileSession.CurrentProfile) ||
            e.PropertyName == nameof(ProfileSession.ResolvedProfile))
        {
            DetachProfileListeners();
            AttachProfileListeners();
            NotifyAllProperties();
            RefreshAllRows();
        }
    }

    private void OnProfileSessionBindingConflictChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_profileSession.BindingConflictMessage))
        {
            HasConflict = true;
            ConflictMessage = _profileSession.BindingConflictMessage;
            ConflictOccurred?.Invoke(this, EventArgs.Empty);
        }
        else if (!_rows.Any(r => r.HasConflict))
        {
            HasConflict = false;
            ConflictMessage = null;
        }
    }

    private void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) return;

        NotifyMappedProperty(e.PropertyName);
        RefreshMappedRow(e.PropertyName);
    }

    private void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) return;

        if (e.PropertyName == nameof(ResolvedProfile.UseKeyboardOnlyMode))
        {
            OnPropertyChanged(nameof(IsKeyboardSteeringEnabled));
            GetRow(KeyActionId.SteerLeft)?.Refresh();
            GetRow(KeyActionId.SteerRight)?.Refresh();
        }

        NotifyMappedProperty(e.PropertyName);
        RefreshMappedRow(e.PropertyName);
    }

    private void OnLanguageChanged(string culture)
    {
        OnPropertyChanged(nameof(PressAnyKeyText));
        OnPropertyChanged(nameof(NoneText));
        OnPropertyChanged(nameof(ConflictFormat));
        OnPropertyChanged(nameof(WarningText));
        RefreshAllRows();
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PressAnyKeyText));
        OnPropertyChanged(nameof(NoneText));
        OnPropertyChanged(nameof(ConflictFormat));
        OnPropertyChanged(nameof(WarningText));
        RefreshAllRows();
    }

    private void NotifyActionProperties(KeyActionId actionId)
    {
        if (KeyBindingCatalog.Find(actionId) is not {} action) return;
        foreach (string name in action.Notifications) OnPropertyChanged(name);
    }

    private void NotifyMappedProperty(string propertyName)
    {
        if (KeyBindingCatalog.FindProperty(propertyName) is {} action) NotifyActionProperties(action.Id);
    }

    private void RefreshMappedRow(string propertyName)
    {
        if (KeyBindingCatalog.FindProperty(propertyName) is {} action) GetRow(action.Id)?.Refresh();
    }

    private void NotifyAllProperties()
    {
        foreach (var action in KeyBindingCatalog.Actions)
        {
            NotifyActionProperties(action.Id);
        }
        OnPropertyChanged(nameof(IsKeyboardSteeringEnabled));
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(PressAnyKeyText));
        OnPropertyChanged(nameof(NoneText));
        OnPropertyChanged(nameof(ConflictFormat));
    }

    private void RefreshAllRows()
    {
        foreach (var row in _rows)
        {
            row.Refresh();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        UnsubscribeEvents();
        Capture.PropertyChanged -= CaptureChanged;
        Capture.PropertyChanging -= CaptureChanging;
        Capture.CaptureStarted -= OnCaptureStarted;
        Capture.CaptureCancelled -= OnCaptureCancelled;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

}
