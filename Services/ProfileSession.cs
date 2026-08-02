using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Models;

namespace SimRacingHub.Services
{
    public partial class ProfileSession : ObservableObject
    {
        private readonly ProfileManager _profileManager;

        [ObservableProperty]
        private Profile _currentProfile;
        
        [ObservableProperty]
        private ResolvedProfile _resolvedProfile;
        
        [ObservableProperty]
        private bool _hasUnsavedChanges;
        
        [ObservableProperty]
        private bool _isNewProfile;

        private class ProfileChange
        {
            public string PropertyName { get; set; }
            public object OldValue { get; set; }
            public int Timestamp { get; set; }
        }

        private string _savedSnapshotJson = string.Empty;
        private Stack<ProfileChange> _undoStack = new Stack<ProfileChange>();
        private bool _isUndoing = false;
        private bool _isSyncingProfile = false;
        
        public bool CanUndo => HasUnsavedChanges && _undoStack.Count > 0;
        
        public event EventHandler UndoStateChanged;

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
            if (ResolvedProfile != null)
            {
                ResolvedProfile.PropertyChanged -= ResolvedProfile_PropertyChanged;
            }
            
            var (active, resolved) = _profileManager.LoadContext(context);
            CurrentProfile = active;
            ResolvedProfile = resolved;
            
            CurrentProfile.PropertyChanging += CurrentProfile_PropertyChanging;
            CurrentProfile.PropertyChanged += CurrentProfile_PropertyChanged;
            ResolvedProfile.PropertyChanged += ResolvedProfile_PropertyChanged;
            
            _savedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            HasUnsavedChanges = false;
            
            string path = _profileManager.GetProfilePath(context);
            IsNewProfile = !System.IO.File.Exists(path);
            
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveProfile(ProfileContext context)
        {
            _profileManager.SaveContext(context, CurrentProfile);
            _savedSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(CurrentProfile);
            HasUnsavedChanges = false;
            
            _undoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
            
            string path = _profileManager.GetProfilePath(context);
            IsNewProfile = !System.IO.File.Exists(path);
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

        private void CurrentProfile_PropertyChanging(object sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            if (_isUndoing || _isSyncingProfile || string.IsNullOrEmpty(e.PropertyName)) return;
            
            var propInfo = CurrentProfile.GetType().GetProperty(e.PropertyName);
            if (propInfo != null)
            {
                var oldValue = propInfo.GetValue(CurrentProfile);
                
                if (_undoStack.Count > 0)
                {
                    var last = _undoStack.Peek();
                    if (last.PropertyName == e.PropertyName && Environment.TickCount - last.Timestamp < 500)
                    {
                        last.Timestamp = Environment.TickCount;
                        return;
                    }
                }

                _undoStack.Push(new ProfileChange { PropertyName = e.PropertyName, OldValue = oldValue, Timestamp = Environment.TickCount });
                OnPropertyChanged(nameof(CanUndo));
                UndoStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CurrentProfile_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isSyncingProfile && !string.IsNullOrEmpty(e.PropertyName))
            {
                var curProp = CurrentProfile.GetType().GetProperty(e.PropertyName);
                if (curProp != null)
                {
                    var val = curProp.GetValue(CurrentProfile);
                    _isSyncingProfile = true;
                    if (val != null)
                    {
                        var resProp = ResolvedProfile.GetType().GetProperty(e.PropertyName);
                        resProp?.SetValue(ResolvedProfile, val);
                    }
                    _isSyncingProfile = false;
                }
            }

            if (_isUndoing) return;

            UpdateDirtyState();
        }

        private void ResolvedProfile_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isSyncingProfile || string.IsNullOrEmpty(e.PropertyName)) return;

            var resProp = ResolvedProfile.GetType().GetProperty(e.PropertyName);
            var curProp = CurrentProfile.GetType().GetProperty(e.PropertyName);
            if (resProp != null && curProp != null)
            {
                var val = resProp.GetValue(ResolvedProfile);
                var curVal = curProp.GetValue(CurrentProfile);

                if (Equals(val, curVal)) return;

                _isSyncingProfile = true;
                curProp.SetValue(CurrentProfile, val);
                _isSyncingProfile = false;
                
                UpdateDirtyState();
            }
        }
    }
}
