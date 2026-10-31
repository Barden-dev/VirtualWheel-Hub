using System;
using System.Collections.Generic;
using System.Linq;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.KeyBindings;

/// <summary>Typed access to a nullable profile assignment and its inherited value.</summary>
public sealed record KeyBindingSlotDefinition(string PropertyName, Func<Profile, int?> ReadCurrent,
    Func<ResolvedProfile, int> ReadResolved, Action<Profile, int?> Write)
{
    public int Read(Profile? current, ResolvedProfile? resolved) =>
        (current == null ? null : ReadCurrent(current)) ?? (resolved == null ? 0 : ReadResolved(resolved));
}

public sealed record KeyBindingAction(KeyActionId Id, string Name, string ResourceKey, string ToolTipResourceKey,
    KeyBindingSlotDefinition Primary, KeyBindingSlotDefinition Secondary, IReadOnlyList<string> Notifications)
{
    public KeyBindingSlotDefinition Slot(bool secondary) => secondary ? Secondary : Primary;
}

/// <summary>One ordered definition for rows, profile access, reset, and change notifications.</summary>
public static class KeyBindingCatalog
{
    public static IReadOnlyList<KeyBindingAction> Actions { get; } = Array.AsReadOnly(new KeyBindingAction[]
    {
        new(KeyActionId.SteerLeft, "Steer Left", "Page_Keys_SteerLeft", "Page_Keys_SteerLeft_Primary_ToolTip",
            new(nameof(Profile.KeySteerLeft1), p => p.KeySteerLeft1, p => p.KeySteerLeft1, (p, value) => p.KeySteerLeft1 = value),
            new(nameof(Profile.KeySteerLeft2), p => p.KeySteerLeft2, p => p.KeySteerLeft2, (p, value) => p.KeySteerLeft2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeySteerLeft1), nameof(KeyBindingsViewModel.KeySteerLeft2), nameof(KeyBindingsViewModel.EffectiveKeySteerLeft1), nameof(KeyBindingsViewModel.EffectiveKeySteerLeft2), nameof(KeyBindingsViewModel.KeySteerLeft1Text), nameof(KeyBindingsViewModel.KeySteerLeft2Text) })),
        new(KeyActionId.SteerRight, "Steer Right", "Page_Keys_SteerRight", "Page_Keys_SteerRight_Primary_ToolTip",
            new(nameof(Profile.KeySteerRight1), p => p.KeySteerRight1, p => p.KeySteerRight1, (p, value) => p.KeySteerRight1 = value),
            new(nameof(Profile.KeySteerRight2), p => p.KeySteerRight2, p => p.KeySteerRight2, (p, value) => p.KeySteerRight2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeySteerRight1), nameof(KeyBindingsViewModel.KeySteerRight2), nameof(KeyBindingsViewModel.EffectiveKeySteerRight1), nameof(KeyBindingsViewModel.EffectiveKeySteerRight2), nameof(KeyBindingsViewModel.KeySteerRight1Text), nameof(KeyBindingsViewModel.KeySteerRight2Text) })),
        new(KeyActionId.Throttle, "Throttle", "Page_Keys_Gas", "Page_Keys_Gas_Primary_ToolTip",
            new(nameof(Profile.KeyGas1), p => p.KeyGas1, p => p.KeyGas1, (p, value) => p.KeyGas1 = value),
            new(nameof(Profile.KeyGas2), p => p.KeyGas2, p => p.KeyGas2, (p, value) => p.KeyGas2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyGas1), nameof(KeyBindingsViewModel.KeyGas2), nameof(KeyBindingsViewModel.KeyThrottle1), nameof(KeyBindingsViewModel.KeyThrottle2), nameof(KeyBindingsViewModel.EffectiveKeyGas1), nameof(KeyBindingsViewModel.EffectiveKeyGas2), nameof(KeyBindingsViewModel.EffectiveKeyThrottle1), nameof(KeyBindingsViewModel.EffectiveKeyThrottle2), nameof(KeyBindingsViewModel.KeyGas1Text), nameof(KeyBindingsViewModel.KeyGas2Text), nameof(KeyBindingsViewModel.KeyThrottle1Text), nameof(KeyBindingsViewModel.KeyThrottle2Text) })),
        new(KeyActionId.Brake, "Brake", "Page_Keys_Brake", "Page_Keys_Brake_Primary_ToolTip",
            new(nameof(Profile.KeyBrake1), p => p.KeyBrake1, p => p.KeyBrake1, (p, value) => p.KeyBrake1 = value),
            new(nameof(Profile.KeyBrake2), p => p.KeyBrake2, p => p.KeyBrake2, (p, value) => p.KeyBrake2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyBrake1), nameof(KeyBindingsViewModel.KeyBrake2), nameof(KeyBindingsViewModel.EffectiveKeyBrake1), nameof(KeyBindingsViewModel.EffectiveKeyBrake2), nameof(KeyBindingsViewModel.KeyBrake1Text), nameof(KeyBindingsViewModel.KeyBrake2Text) })),
        new(KeyActionId.ShiftUp, "Shift Up", "Page_Keys_ShiftUp", "Page_Keys_ShiftUp_Primary_ToolTip",
            new(nameof(Profile.KeyShiftUp1), p => p.KeyShiftUp1, p => p.KeyShiftUp1, (p, value) => p.KeyShiftUp1 = value),
            new(nameof(Profile.KeyShiftUp2), p => p.KeyShiftUp2, p => p.KeyShiftUp2, (p, value) => p.KeyShiftUp2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyShiftUp1), nameof(KeyBindingsViewModel.KeyShiftUp2), nameof(KeyBindingsViewModel.EffectiveKeyShiftUp1), nameof(KeyBindingsViewModel.EffectiveKeyShiftUp2), nameof(KeyBindingsViewModel.KeyShiftUp1Text), nameof(KeyBindingsViewModel.KeyShiftUp2Text) })),
        new(KeyActionId.ShiftDown, "Shift Down", "Page_Keys_ShiftDown", "Page_Keys_ShiftDown_Primary_ToolTip",
            new(nameof(Profile.KeyShiftDown1), p => p.KeyShiftDown1, p => p.KeyShiftDown1, (p, value) => p.KeyShiftDown1 = value),
            new(nameof(Profile.KeyShiftDown2), p => p.KeyShiftDown2, p => p.KeyShiftDown2, (p, value) => p.KeyShiftDown2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyShiftDown1), nameof(KeyBindingsViewModel.KeyShiftDown2), nameof(KeyBindingsViewModel.EffectiveKeyShiftDown1), nameof(KeyBindingsViewModel.EffectiveKeyShiftDown2), nameof(KeyBindingsViewModel.KeyShiftDown1Text), nameof(KeyBindingsViewModel.KeyShiftDown2Text) })),
        new(KeyActionId.CenterSteering, "Center Steer", "Page_Keys_CenterSteer", "Page_Keys_CenterSteer_Primary_ToolTip",
            new(nameof(Profile.KeyCenterSteering1), p => p.KeyCenterSteering1, p => p.KeyCenterSteering1, (p, value) => p.KeyCenterSteering1 = value),
            new(nameof(Profile.KeyCenterSteering2), p => p.KeyCenterSteering2, p => p.KeyCenterSteering2, (p, value) => p.KeyCenterSteering2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyCenterSteering1), nameof(KeyBindingsViewModel.KeyCenterSteering2), nameof(KeyBindingsViewModel.EffectiveKeyCenterSteering1), nameof(KeyBindingsViewModel.EffectiveKeyCenterSteering2), nameof(KeyBindingsViewModel.KeyCenterSteering1Text), nameof(KeyBindingsViewModel.KeyCenterSteering2Text) })),
        new(KeyActionId.ToggleHub, "Toggle Hub", "Page_Keys_ToggleHub", "Page_Keys_ToggleHub_Primary_ToolTip",
            new(nameof(Profile.KeyToggleHub1), p => p.KeyToggleHub1, p => p.KeyToggleHub1, (p, value) => p.KeyToggleHub1 = value),
            new(nameof(Profile.KeyToggleHub2), p => p.KeyToggleHub2, p => p.KeyToggleHub2, (p, value) => p.KeyToggleHub2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyToggleHub1), nameof(KeyBindingsViewModel.KeyToggleHub2), nameof(KeyBindingsViewModel.EffectiveKeyToggleHub1), nameof(KeyBindingsViewModel.EffectiveKeyToggleHub2), nameof(KeyBindingsViewModel.KeyToggleHub1Text), nameof(KeyBindingsViewModel.KeyToggleHub2Text) })),
        new(KeyActionId.ToggleLock, "Toggle Lock", "Page_Keys_ToggleLock", "Page_Keys_ToggleLock_Primary_ToolTip",
            new(nameof(Profile.KeyToggleLock1), p => p.KeyToggleLock1, p => p.KeyToggleLock1, (p, value) => p.KeyToggleLock1 = value),
            new(nameof(Profile.KeyToggleLock2), p => p.KeyToggleLock2, p => p.KeyToggleLock2, (p, value) => p.KeyToggleLock2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyToggleLock1), nameof(KeyBindingsViewModel.KeyToggleLock2), nameof(KeyBindingsViewModel.EffectiveKeyToggleLock1), nameof(KeyBindingsViewModel.EffectiveKeyToggleLock2), nameof(KeyBindingsViewModel.KeyToggleLock1Text), nameof(KeyBindingsViewModel.KeyToggleLock2Text) })),
        new(KeyActionId.UnlockCursor, "Unlock Cursor (Hold)", "Page_Keys_UnlockCursor", "Page_Keys_UnlockCursor_Primary_ToolTip",
            new(nameof(Profile.KeyUnlockCursor1), p => p.KeyUnlockCursor1, p => p.KeyUnlockCursor1, (p, value) => p.KeyUnlockCursor1 = value),
            new(nameof(Profile.KeyUnlockCursor2), p => p.KeyUnlockCursor2, p => p.KeyUnlockCursor2, (p, value) => p.KeyUnlockCursor2 = value),
            Array.AsReadOnly(new[] { nameof(KeyBindingsViewModel.KeyUnlockCursor1), nameof(KeyBindingsViewModel.KeyUnlockCursor2), nameof(KeyBindingsViewModel.EffectiveKeyUnlockCursor1), nameof(KeyBindingsViewModel.EffectiveKeyUnlockCursor2), nameof(KeyBindingsViewModel.KeyUnlockCursor1Text), nameof(KeyBindingsViewModel.KeyUnlockCursor2Text) })),
    });
    private static readonly IReadOnlyDictionary<KeyActionId, KeyBindingAction> ByAction = Actions.ToDictionary(a => a.Id);
    private static readonly IReadOnlyDictionary<string, KeyBindingAction> ByProperty = Actions
        .SelectMany(a => new[] { (a.Primary.PropertyName, Action: a), (a.Secondary.PropertyName, Action: a) })
        .ToDictionary(p => p.PropertyName, p => p.Action, StringComparer.Ordinal);
    public static KeyBindingAction? Find(KeyActionId id) => ByAction.TryGetValue(id, out var action) ? action : null;
    public static KeyBindingAction? FindProperty(string propertyName) => ByProperty.TryGetValue(propertyName, out var action) ? action : null;
}
