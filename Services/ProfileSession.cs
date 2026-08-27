using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Models;
using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public partial class ProfileSession : ObservableObject
    {
        private readonly ProfileManager _profileManager;

        [ObservableProperty]
        private Profile _currentProfile = new();
        
        [ObservableProperty]
        private ResolvedProfile _resolvedProfile = new();
        
        [ObservableProperty]
        private bool _hasUnsavedChanges;
        
        [ObservableProperty]
        private bool _isNewProfile;

        private class ProfileChange
        {
            public string PropertyName { get; set; } = string.Empty;
            public object? OldValue { get; set; }
            public int Timestamp { get; set; }
        }

        private string _savedSnapshotJson = string.Empty;
        private Stack<ProfileChange> _undoStack = new Stack<ProfileChange>();
        private bool _isUndoing = false;
        private bool _isSyncingProfile = false;
        
        public bool CanUndo => HasUnsavedChanges && _undoStack.Count > 0;
        
        public event EventHandler? UndoStateChanged;

        public ProfileSession(ProfileManager profileManager)
        {
            _profileManager = profileManager;
        }

        public bool CheckIsDirty()
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(_savedSnapshotJson)) return false;
            string currentJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            return !string.Equals(currentJson, _savedSnapshotJson, StringComparison.Ordinal);
        }

        private void UpdateDirtyState()
        {
            bool isDirty = CheckIsDirty();
            HasUnsavedChanges = isDirty;
            if (!isDirty)
            {
                _undoStack.Clear();
            }
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void LoadContext(ProfileContext context, bool supportsAbs, bool supportsTc, bool supportsSlipAudio)
        {
            if (CurrentProfile != null)
            {
                CurrentProfile.PropertyChanging -= CurrentProfile_PropertyChanging;
                CurrentProfile.PropertyChanged -= CurrentProfile_PropertyChanged;
            }
            
            var (active, resolved) = _profileManager.LoadContext(context);
            CurrentProfile = active;
            ResolvedProfile = resolved;
            
            _savedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            HasUnsavedChanges = false;
            
            CurrentProfile.PropertyChanging += CurrentProfile_PropertyChanging;
            CurrentProfile.PropertyChanged += CurrentProfile_PropertyChanged;
            
            string path = _profileManager.GetProfilePath(context);
            IsNewProfile = !System.IO.File.Exists(path);
            
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool SaveProfile(ProfileContext context)
        {
            if (!_profileManager.SaveContext(context, CurrentProfile)) return false;

            _savedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            HasUnsavedChanges = false;

            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);

            string path = _profileManager.GetProfilePath(context);
            IsNewProfile = !System.IO.File.Exists(path);
            return true;
        }

        public void RevertProfile()
        {
            if (_undoStack.Count == 0) return;
            
            var change = _undoStack.Pop();
            var propInfo = CurrentProfile.GetType().GetProperty(change.PropertyName);
            if (propInfo != null && propInfo.CanWrite)
            {
                _isUndoing = true;
                propInfo.SetValue(CurrentProfile, change.OldValue);
                _isUndoing = false;
            }
            
            UpdateDirtyState();
        }

        private static readonly HashSet<string> BindingPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(Profile.KeySteerLeft1), nameof(Profile.KeySteerLeft2), nameof(Profile.KeySteerRight1), nameof(Profile.KeySteerRight2),
            nameof(Profile.KeyGas1), nameof(Profile.KeyGas2), nameof(Profile.KeyBrake1), nameof(Profile.KeyBrake2),
            nameof(Profile.KeyShiftUp1), nameof(Profile.KeyShiftUp2), nameof(Profile.KeyShiftDown1), nameof(Profile.KeyShiftDown2),
            nameof(Profile.KeyCenterSteering1), nameof(Profile.KeyCenterSteering2), nameof(Profile.KeyToggleHub1), nameof(Profile.KeyToggleHub2),
            nameof(Profile.KeyToggleLock1), nameof(Profile.KeyToggleLock2), nameof(Profile.KeyUnlockCursor1), nameof(Profile.KeyUnlockCursor2)
        };

        private string? _pendingPropertyName;
        private object? _pendingOldProfileValue;
        private object? _pendingOldResolvedValue;

        public string BindingConflictMessage { get; private set; } = string.Empty;

        public event EventHandler? BindingConflictChanged;

        private void CurrentProfile_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            if (_isUndoing || _isSyncingProfile || CurrentProfile.IsAdjustingPedalRatesForThreshold || string.IsNullOrEmpty(e.PropertyName)) return;

            var propInfo = CurrentProfile.GetType().GetProperty(e.PropertyName);
            if (propInfo == null) return;

            _pendingPropertyName = e.PropertyName;
            _pendingOldProfileValue = propInfo.GetValue(CurrentProfile);
            _pendingOldResolvedValue = ResolvedProfile.GetType().GetProperty(e.PropertyName)?.GetValue(ResolvedProfile);

            if (_undoStack.Count > 0)
            {
                var last = _undoStack.Peek();
                if (last.PropertyName == e.PropertyName && Environment.TickCount - last.Timestamp < 500)
                {
                    last.Timestamp = Environment.TickCount;
                    return;
                }
            }

            _undoStack.Push(new ProfileChange { PropertyName = e.PropertyName, OldValue = _pendingOldProfileValue, Timestamp = Environment.TickCount });
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CurrentProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isSyncingProfile && !string.IsNullOrEmpty(e.PropertyName))
            {
                var curProp = CurrentProfile.GetType().GetProperty(e.PropertyName);
                var resProp = ResolvedProfile.GetType().GetProperty(e.PropertyName);
                if (curProp != null && resProp != null)
                {
                    var val = curProp.GetValue(CurrentProfile);
                    _isSyncingProfile = true;
                    if (val != null)
                    {
                        resProp.SetValue(ResolvedProfile, val);
                    }
                    _isSyncingProfile = false;

                    if (BindingPropertyNames.Contains(e.PropertyName) && val is int keyCode)
                    {
                        string actionName = GetBindingActionName(e.PropertyName);
                        if (ResolvedProfile.TryGetKeyBindingConflict(keyCode, actionName, out string conflicts))
                        {
                            _isSyncingProfile = true;
                            curProp.SetValue(CurrentProfile, _pendingOldProfileValue);
                            resProp.SetValue(ResolvedProfile, _pendingOldResolvedValue);
                            _isSyncingProfile = false;

                            BindingConflictMessage = $"{KeyNameFormatter.Format(keyCode)} already used by: {conflicts}";
                            BindingConflictChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }

            if (_isUndoing) return;

            UpdateDirtyState();
        }

        private static string GetBindingActionName(string propertyName)
        {
            return propertyName switch
            {
                nameof(Profile.KeySteerLeft1) or nameof(Profile.KeySteerLeft2) => "Steer Left",
                nameof(Profile.KeySteerRight1) or nameof(Profile.KeySteerRight2) => "Steer Right",
                nameof(Profile.KeyGas1) or nameof(Profile.KeyGas2) => "Throttle",
                nameof(Profile.KeyBrake1) or nameof(Profile.KeyBrake2) => "Brake",
                nameof(Profile.KeyShiftUp1) or nameof(Profile.KeyShiftUp2) => "Shift Up",
                nameof(Profile.KeyShiftDown1) or nameof(Profile.KeyShiftDown2) => "Shift Down",
                nameof(Profile.KeyCenterSteering1) or nameof(Profile.KeyCenterSteering2) => "Center Steer",
                nameof(Profile.KeyToggleHub1) or nameof(Profile.KeyToggleHub2) => "Toggle Hub",
                nameof(Profile.KeyToggleLock1) or nameof(Profile.KeyToggleLock2) => "Toggle Lock",
                nameof(Profile.KeyUnlockCursor1) or nameof(Profile.KeyUnlockCursor2) => "Unlock Cursor (Hold)",
                _ => propertyName
            };
        }
    }
}
