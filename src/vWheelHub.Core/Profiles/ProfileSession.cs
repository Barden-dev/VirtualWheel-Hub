using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;
using SimRacingHub.Models;

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
        [ObservableProperty]
        private bool _hasSaveError;
        private sealed class ProfileChange
        {
            public string PropertyName { get; init; } = string.Empty;
            public Dictionary<string, object?> Values { get; init; } = new(StringComparer.Ordinal);
            public Dictionary<string, object?> ResolvedValues { get; init; } = new(StringComparer.Ordinal);
            public int Timestamp { get; set; }
        }

        private ResolvedProfile _inheritedProfile = new();
        private ProfileChange? _pendingChange, _groupChange;
        private readonly Dictionary<string, int> _changingProperties = new(StringComparer.Ordinal);
        private int _editGroupDepth;
        private string _savedSnapshotJson = string.Empty;
        private string _savedResolvedSnapshotJson = string.Empty;
        private readonly Dictionary<string, object?> _savedValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _savedResolvedValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _savedEffectiveValues = new(StringComparer.Ordinal);
        private static readonly System.Reflection.PropertyInfo[] EditableProperties = typeof(Profile).GetProperties().Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true && !Attribute.IsDefined(p, typeof(Newtonsoft.Json.JsonIgnoreAttribute))).ToArray();
        private static readonly Dictionary<string, System.Reflection.PropertyInfo> EditablePropertyMap = EditableProperties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        private static readonly Dictionary<string, System.Reflection.PropertyInfo> ResolvedPropertyMap = typeof(ResolvedProfile).GetProperties().Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true).ToDictionary(p => p.Name, StringComparer.Ordinal);
        private static string StoredSettingName(string name) => name switch
        {
            nameof(Profile.GasAttackTurboSeconds) => nameof(Profile.GasAttackTurbo),
            nameof(Profile.GasAttackFastSeconds) => nameof(Profile.GasAttackFast),
            nameof(Profile.GasAttackSlowSeconds) => nameof(Profile.GasAttackSlow),
            nameof(Profile.GasDecaySeconds) => nameof(Profile.GasDecay),
            nameof(Profile.GasInstantCutSeconds) => nameof(Profile.GasInstantCut),
            nameof(Profile.BrakeAttackFastSeconds) => nameof(Profile.BrakeAttackFast),
            nameof(Profile.BrakeAttackSlowSeconds) => nameof(Profile.BrakeAttackSlow),
            nameof(Profile.BrakeDecaySeconds) => nameof(Profile.BrakeDecay),
            nameof(Profile.TbDecaySeconds) => nameof(Profile.TbDecay),
            _ => name
        };
        private static bool SettingValuesEqual(object? left, object? right)
        {
            if (left is double a && right is double b)
                return a.Equals(b) || (double.IsFinite(a) && double.IsFinite(b) &&
                    Math.Abs(a - b) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))));
            if (Equals(left, right)) return true;
            if (left == null || right == null || left is string || right is string ||
                left.GetType().IsValueType || right.GetType().IsValueType) return false;
            return SettingTokensEqual(Newtonsoft.Json.Linq.JToken.FromObject(left), Newtonsoft.Json.Linq.JToken.FromObject(right));
        }

        private static bool SettingTokensEqual(Newtonsoft.Json.Linq.JToken left, Newtonsoft.Json.Linq.JToken right)
        {
            if (left is Newtonsoft.Json.Linq.JObject a && right is Newtonsoft.Json.Linq.JObject b)
                return a.Count == b.Count && a.Properties().All(p => b.TryGetValue(p.Name, StringComparison.Ordinal, out var value) && SettingTokensEqual(p.Value, value));
            if (left is Newtonsoft.Json.Linq.JArray x && right is Newtonsoft.Json.Linq.JArray y)
                return x.Count == y.Count && x.Zip(y).All(pair => SettingTokensEqual(pair.First, pair.Second));
            if (left.Type == Newtonsoft.Json.Linq.JTokenType.Float && right.Type == Newtonsoft.Json.Linq.JTokenType.Float)
                return SettingValuesEqual((double)left, (double)right);
            return Newtonsoft.Json.Linq.JToken.DeepEquals(left, right);
        }

        private bool MatchesSavedValue(System.Reflection.PropertyInfo property)
        {
            if (!_savedValues.TryGetValue(property.Name, out var saved)) return true;
            var current = property.GetValue(CurrentProfile);
            if (SettingValuesEqual(current, saved)) return true;
            return saved == null && current != null && property.PropertyType != typeof(string) &&
                _savedEffectiveValues.TryGetValue(property.Name, out var effective) && SettingValuesEqual(current, effective);
        }

        private void RemoveReturnedOverrides()
        {
            // Keep nullable inheritance when a numeric or immutable setting returns
            // to its saved effective value. Booleans retain explicit dependency intent.
            _isSyncingProfile = true;
            try
            {
                foreach (var property in EditableProperties)
                {
                    if (property.PropertyType == typeof(string) || Nullable.GetUnderlyingType(property.PropertyType) == typeof(bool) ||
                        !_savedValues.TryGetValue(property.Name, out var saved) || saved != null ||
                        property.GetValue(CurrentProfile) == null || !MatchesSavedValue(property) ||
                        !ResolvedPropertyMap.TryGetValue(property.Name, out var resolved)) continue;
                    property.SetValue(CurrentProfile, null);
                    resolved.SetValue(ResolvedProfile, _savedResolvedValues[property.Name]);
                }
            }
            finally { _isSyncingProfile = false; }
        }

        private void CaptureSavedState()
        {
            CurrentProfile.SetSavedPedalRates(ResolvedProfile);
            _savedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            _savedResolvedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(ResolvedProfile);
            _savedValues.Clear();
            foreach (var property in EditableProperties)
                _savedValues[property.Name] = property.GetValue(CurrentProfile);
            _savedResolvedValues.Clear(); _savedEffectiveValues.Clear();
            foreach (var property in ResolvedPropertyMap.Values)
                _savedResolvedValues[property.Name] = _savedEffectiveValues[property.Name] = property.GetValue(ResolvedProfile);
        }

        private double DurationFraction(string rate, bool saved)
        {
            string? boundary = rate switch
            {
                nameof(Profile.GasAttackFast) or nameof(Profile.GasAttackSlow) => nameof(Profile.GasThreshold),
                nameof(Profile.BrakeAttackFast) or nameof(Profile.BrakeAttackSlow) => nameof(Profile.BrakeThreshold),
                _ => null
            };
            if (boundary == null) return 1;
            double fraction = saved ? (double)_savedEffectiveValues[boundary]! : (double)ResolvedPropertyMap[boundary].GetValue(ResolvedProfile)!;
            return rate is nameof(Profile.GasAttackSlow) or nameof(Profile.BrakeAttackSlow) ? 1 - fraction : fraction;
        }

        private static double? DisplayedDuration(double? rate, double fraction) =>
            rate is > 0 && fraction > 0 ? Math.Round(65536 * fraction / rate.Value, 3, MidpointRounding.AwayFromZero) : null;

        private bool TryGetDurations(string name, out double? current, out double? saved)
        {
            current = saved = null;
            string rate = StoredSettingName(name);
            if (name == rate || !_savedEffectiveValues.TryGetValue(rate, out var baseline) ||
                !ResolvedPropertyMap.TryGetValue(rate, out var property)) return false;
            current = DisplayedDuration((double?)property.GetValue(ResolvedProfile), DurationFraction(rate, false));
            saved = DisplayedDuration((double?)baseline, DurationFraction(rate, true));
            return true;
        }

        /// <summary>Compares the setting in its displayed units against the saved state.</summary>
        public bool IsPropertyModified(string propertyName)
        {
            if (TryGetDurations(propertyName, out var currentDuration, out var savedDuration))
            {
                string stored = StoredSettingName(propertyName);
                // Explicitly removing a saved override remains an edit.
                if (_savedValues.GetValueOrDefault(stored) != null && EditablePropertyMap[stored].GetValue(CurrentProfile) == null)
                    return true;
                return !SettingValuesEqual(currentDuration, savedDuration);
            }
            propertyName = StoredSettingName(propertyName);
            return EditablePropertyMap.TryGetValue(propertyName, out var property) && !MatchesSavedValue(property);
        }

        public void RestoreSavedProperty(string propertyName)
        {
            if (TryGetDurations(propertyName, out _, out _) && IsPropertyModified(propertyName))
            {
                string rate = StoredSettingName(propertyName);
                double fraction = DurationFraction(rate, false), savedFraction = DurationFraction(rate, true);
                if (fraction > 0 && savedFraction > 0 && _savedEffectiveValues[rate] is double savedRate)
                {
                    // Restore the saved duration at today's boundary, preserving
                    // exact rates and any independent edit to that boundary.
                    double restoredRate = savedRate * fraction / savedFraction;
                    object? value = _savedValues[rate] == null && SettingValuesEqual(fraction, savedFraction) ? null : restoredRate;
                    EditablePropertyMap[rate].SetValue(CurrentProfile, value);
                    if (value == null) ResolvedPropertyMap[rate].SetValue(ResolvedProfile, restoredRate);
                    UpdateDirtyState();
                    return;
                }
            }
            propertyName = StoredSettingName(propertyName);
            if (!IsPropertyModified(propertyName))
                return;
            var property = typeof(Profile).GetProperty(propertyName);
            if (property?.CanWrite != true)
                return;
            property.SetValue(CurrentProfile, _savedValues[propertyName]);
            if (_savedValues[propertyName] == null && !string.IsNullOrEmpty(_savedResolvedSnapshotJson))
            {
                var savedResolved = Newtonsoft.Json.JsonConvert.DeserializeObject<ResolvedProfile>(_savedResolvedSnapshotJson);
                var resolvedProperty = typeof(ResolvedProfile).GetProperty(propertyName);
                if (savedResolved != null && resolvedProperty?.CanWrite == true)
                    resolvedProperty.SetValue(ResolvedProfile, resolvedProperty.GetValue(savedResolved));
            }

            UpdateDirtyState();
        }

        public void DiscardChanges()
        {
            if (string.IsNullOrEmpty(_savedSnapshotJson) || !HasUnsavedChanges)
                return;
            var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Profile>(_savedSnapshotJson);
            var resolved = Newtonsoft.Json.JsonConvert.DeserializeObject<ResolvedProfile>(_savedResolvedSnapshotJson);
            if (saved == null || resolved == null)
                return;
            CurrentProfile.PropertyChanging -= CurrentProfile_PropertyChanging;
            CurrentProfile.PropertyChanged -= CurrentProfile_PropertyChanged;
            CurrentProfile = saved;
            CurrentProfile.SetInheritedPedalThresholds(_inheritedProfile.GasThreshold, _inheritedProfile.BrakeThreshold);
            ResolvedProfile = resolved;
            CurrentProfile.SetSavedPedalRates(ResolvedProfile);
            CurrentProfile.PropertyChanging += CurrentProfile_PropertyChanging;
            CurrentProfile.PropertyChanged += CurrentProfile_PropertyChanged;
            HasSaveError = false;
            UpdateDirtyState();
        }

        private Stack<ProfileChange> _undoStack = new Stack<ProfileChange>();
        private bool _isUndoing = false;
        private bool _isSyncingProfile = false;
        public bool CanUndo => HasUnsavedChanges && _undoStack.Count > 0;

        public event EventHandler? UndoStateChanged;
        public ProfileManager ProfileManager => _profileManager;

        public ProfileSession(ProfileManager profileManager)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        }

        public bool CheckIsDirty()
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(_savedSnapshotJson))
                return false;
            return EditableProperties.Any(property => !MatchesSavedValue(property));
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

        public void LoadContext(ProfileContext context)
        {
            if (CurrentProfile != null)
            {
                CurrentProfile.PropertyChanging -= CurrentProfile_PropertyChanging;
                CurrentProfile.PropertyChanged -= CurrentProfile_PropertyChanged;
            }

            HasSaveError = false;
            var(active, resolved) = _profileManager.LoadContext(context);
            _loadedContext = new(context.Game, context.CarClass, context.Car);
            _inheritedProfile = _profileManager.GetComparisonBaseline(context).Resolved;
            _pendingChange = _groupChange = null;
            _changingProperties.Clear();
            _editGroupDepth = 0;
            CurrentProfile = active;
            CurrentProfile.SetInheritedPedalThresholds(_inheritedProfile.GasThreshold, _inheritedProfile.BrakeThreshold);
            ResolvedProfile = resolved;
            CaptureSavedState();
            HasUnsavedChanges = false;
            CurrentProfile.PropertyChanging += CurrentProfile_PropertyChanging;
            CurrentProfile.PropertyChanged += CurrentProfile_PropertyChanged;
            string path = _profileManager.GetProfilePath(context);
            IsNewProfile = !System.IO.File.Exists(path);
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReloadLoadedContext() => LoadContext(LoadedContext ?? throw new InvalidOperationException("No profile context is loaded."));

        public bool SaveProfile(ProfileContext context)
        {
            if (!_profileManager.SaveContext(context, CurrentProfile))
            {
                HasSaveError = true;
                return false;
            }

            HasSaveError = false;
            CaptureSavedState();
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
            if (_undoStack.Count == 0)
                return;
            var change = _undoStack.Pop();
            _isUndoing = true;
            try
            {
                if (EditablePropertyMap.TryGetValue(change.PropertyName, out var primary))
                    primary.SetValue(CurrentProfile, change.Values[change.PropertyName]);
                foreach (var pair in change.Values)
                    EditablePropertyMap[pair.Key].SetValue(CurrentProfile, pair.Value);
                foreach (var pair in change.ResolvedValues)
                    ResolvedPropertyMap[pair.Key].SetValue(ResolvedProfile, pair.Value);
            }
            finally
            {
                _isUndoing = false;
            }

            UpdateDirtyState();
        }

        private ProfileChange CaptureChange(string name) => new()
        {
            PropertyName = name,
            Timestamp = Environment.TickCount,
            Values = EditableProperties.ToDictionary(p => p.Name, p => p.GetValue(CurrentProfile), StringComparer.Ordinal),
            ResolvedValues = ResolvedPropertyMap.Values.ToDictionary(p => p.Name, p => p.GetValue(ResolvedProfile), StringComparer.Ordinal)
        };
        private void CommitChange(ProfileChange change)
        {
            if (!change.Values.Any(p => !SettingValuesEqual(p.Value, EditablePropertyMap[p.Key].GetValue(CurrentProfile))))
                return;
            if (_undoStack.TryPeek(out var last) && last.PropertyName == change.PropertyName && (uint)(Environment.TickCount - last.Timestamp) < 500)
                last.Timestamp = change.Timestamp;
            else
                _undoStack.Push(change);
        }

        public IDisposable BeginEditGroup(string actionName)
        {
            if (_editGroupDepth++ == 0)
                _groupChange = CaptureChange(actionName);
            return new EditGroup(this, CurrentProfile);
        }

        public void ApplySettings(Profile settings, string actionName = "ApplyProfile")
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (ReferenceEquals(settings, CurrentProfile))
                return;
            using var edit = BeginEditGroup(actionName);
            foreach (string name in BindingPropertyNames)
                EditablePropertyMap[name].SetValue(CurrentProfile, 0);
            CurrentProfile.CopyFrom(settings);
        }

        private sealed class EditGroup(ProfileSession session, Profile profile) : IDisposable
        {
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (!ReferenceEquals(session.CurrentProfile, profile) || session._editGroupDepth == 0)
                    return;
                if (--session._editGroupDepth == 0 && session._groupChange is { } change)
                {
                    session._groupChange = null;
                    session.RemoveReturnedOverrides();
                    session.CommitChange(change);
                    session.UpdateDirtyState();
                }
            }
        }

        private static readonly HashSet<string> BindingPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(Profile.KeySteerLeft1),
            nameof(Profile.KeySteerLeft2),
            nameof(Profile.KeySteerRight1),
            nameof(Profile.KeySteerRight2),
            nameof(Profile.KeyGas1),
            nameof(Profile.KeyGas2),
            nameof(Profile.KeyBrake1),
            nameof(Profile.KeyBrake2),
            nameof(Profile.KeyShiftUp1),
            nameof(Profile.KeyShiftUp2),
            nameof(Profile.KeyShiftDown1),
            nameof(Profile.KeyShiftDown2),
            nameof(Profile.KeyCenterSteering1),
            nameof(Profile.KeyCenterSteering2),
            nameof(Profile.KeyToggleHub1),
            nameof(Profile.KeyToggleHub2),
            nameof(Profile.KeyToggleLock1),
            nameof(Profile.KeyToggleLock2),
            nameof(Profile.KeyUnlockCursor1),
            nameof(Profile.KeyUnlockCursor2)
        };
        public string BindingConflictMessage { get; private set; } = string.Empty;

        public event EventHandler? BindingConflictChanged;
        private void CurrentProfile_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            if (_isUndoing || _isSyncingProfile || e.PropertyName == null || !EditablePropertyMap.ContainsKey(e.PropertyName))
                return;
            _pendingChange ??= CaptureChange(e.PropertyName);
            _changingProperties[e.PropertyName] = _changingProperties.GetValueOrDefault(e.PropertyName) + 1;
        }

        private void CurrentProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isSyncingProfile || string.IsNullOrEmpty(e.PropertyName))
                return;
            if (EditablePropertyMap.TryGetValue(e.PropertyName, out var stored) && ResolvedPropertyMap.TryGetValue(e.PropertyName, out var resolved))
            {
                var value = stored.GetValue(CurrentProfile);
                _isSyncingProfile = true;
                try
                {
                    resolved.SetValue(ResolvedProfile, value ?? resolved.GetValue(_inheritedProfile));
                    if (!_isUndoing && BindingPropertyNames.Contains(e.PropertyName) && value is int code && ResolvedProfile.TryGetKeyBindingConflict(code, GetBindingActionName(e.PropertyName), out string conflicts))
                    {
                        stored.SetValue(CurrentProfile, _pendingChange?.Values[e.PropertyName]);
                        if (_pendingChange?.ResolvedValues.TryGetValue(e.PropertyName, out var oldResolved) == true)
                            resolved.SetValue(ResolvedProfile, oldResolved);
                        BindingConflictMessage = $"{KeyNameFormatter.Format(code)} already used by: {conflicts}";
                        BindingConflictChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                finally
                {
                    _isSyncingProfile = false;
                }
            }

            if (_isUndoing)
                return;
            if (_changingProperties.TryGetValue(e.PropertyName, out int count))
            {
                if (count > 1)
                    _changingProperties[e.PropertyName] = count - 1;
                else
                    _changingProperties.Remove(e.PropertyName);
            }

            if (_changingProperties.Count != 0)
                return;
            if (_editGroupDepth == 0) RemoveReturnedOverrides();
            if (_pendingChange is { } change)
            {
                _pendingChange = null;
                if (_editGroupDepth == 0)
                    CommitChange(change);
            }

            if (_editGroupDepth == 0)
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
