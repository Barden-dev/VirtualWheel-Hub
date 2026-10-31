using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.KeyBindings;

/// <summary>Owns the active input slot and capture transitions; assignment validation stays with the editor.</summary>
public partial class KeyCaptureSession : ObservableObject
{
    private readonly Action _clearConflict;
    private readonly Func<KeyActionId, bool, int, bool> _assign;
    public event EventHandler? CaptureStarted;
    public event EventHandler? CaptureCancelled;
    public KeyCaptureSession(Action clearConflict, Func<KeyActionId, bool, int, bool> assign)
    {
        _clearConflict = clearConflict ?? throw new ArgumentNullException(nameof(clearConflict));
        _assign = assign ?? throw new ArgumentNullException(nameof(assign));
    }
    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private KeyActionId? _capturingActionId;

    [ObservableProperty]
    private bool? _capturingIsSecondary;

    [ObservableProperty]
    private KeyBindingSlotViewModel? _capturingSlot;

    public void StartCapture(KeyBindingSlotViewModel? slot)
    {
        if (slot == null || !slot.IsEnabled) return;

        if (IsCapturing && CapturingSlot != null && CapturingSlot != slot)
        {
            CapturingSlot.IsListening = false;
            CapturingSlot.Refresh();
        }

        slot.ClearConflict();
        _clearConflict();

        IsCapturing = true;
        CapturingSlot = slot;
        CapturingActionId = slot.ActionId;
        CapturingIsSecondary = slot.IsSecondary;

        slot.IsListening = true;
        slot.Refresh();

        CaptureStarted?.Invoke(this, EventArgs.Empty);
    }

    public void CancelCapture()
    {
        if (!IsCapturing) return;

        if (CapturingSlot != null)
        {
            CapturingSlot.IsListening = false;
            CapturingSlot.Refresh();
        }

        IsCapturing = false;
        CapturingActionId = null;
        CapturingIsSecondary = null;
        CapturingSlot = null;

        CaptureCancelled?.Invoke(this, EventArgs.Empty);
    }

    public void OnLostFocus()
    {
        if (IsCapturing)
        {
            CancelCapture();
        }
    }

    public bool ApplyKey(int virtualKeyCode)
    {
        if (!IsCapturing || CapturingSlot == null)
        {
            return false;
        }

        if (virtualKeyCode == CanonicalKeyMapper.Escape)
        {
            CancelCapture();
            return false;
        }

        var slot = CapturingSlot;
        bool success = _assign(slot.ActionId, slot.IsSecondary, virtualKeyCode);
        if (success)
        {
            slot.IsListening = false;
            slot.Refresh();

            IsCapturing = false;
            CapturingActionId = null;
            CapturingIsSecondary = null;
            CapturingSlot = null;
        }

        return success;
    }

    public bool ApplyMouseButton(AppMouseButton mouseButton)
    {
        return ApplyKey(CanonicalKeyMapper.ToVirtualKey(mouseButton));
    }

    public bool ApplyMouseButton(int virtualKeyCode)
    {
        return ApplyKey(virtualKeyCode);
    }

}
