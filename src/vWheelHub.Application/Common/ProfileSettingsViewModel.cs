using System.ComponentModel;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Common;

/// <summary>Observes the current editing session and reattaches when a profile is replaced.</summary>
public abstract class ProfileSettingsViewModel : ViewModelBase, IDisposable
{
    protected readonly ProfileSession _profileSession;
    private Profile? _observedProfile;
    private ResolvedProfile? _observedResolved;
    private bool _observing;
    protected bool IsDisposed { get; private set; }

    public ProfileSession ProfileSession => _profileSession;
    public Profile CurrentProfile => _profileSession.CurrentProfile;
    public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;

    protected ProfileSettingsViewModel(ProfileSession profileSession) =>
        _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));

    protected void StartObserving()
    {
        if (_observing || IsDisposed) return;
        _observing = true;
        _profileSession.PropertyChanged += SessionChanged;
        AttachProfiles();
    }

    private void AttachProfiles()
    {
        if (_observedProfile != null) _observedProfile.PropertyChanged -= OnCurrentProfilePropertyChanged;
        if (_observedResolved != null) _observedResolved.PropertyChanged -= OnResolvedProfilePropertyChanged;
        _observedProfile = CurrentProfile;
        _observedResolved = ResolvedProfile;
        if (_observedProfile != null) _observedProfile.PropertyChanged += OnCurrentProfilePropertyChanged;
        if (_observedResolved != null) _observedResolved.PropertyChanged += OnResolvedProfilePropertyChanged;
    }

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProfileSession.CurrentProfile) or nameof(ProfileSession.ResolvedProfile))
        {
            AttachProfiles();
            OnProfileReplaced();
        }
    }

    protected virtual void OnProfileReplaced() => RefreshSettings();
    protected abstract void RefreshSettings();
    protected abstract void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e);
    protected abstract void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e);

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (_observing) _profileSession.PropertyChanged -= SessionChanged;
        if (_observedProfile != null) _observedProfile.PropertyChanged -= OnCurrentProfilePropertyChanged;
        if (_observedResolved != null) _observedResolved.PropertyChanged -= OnResolvedProfilePropertyChanged;
        _observedProfile = null;
        _observedResolved = null;
        _observing = false;
        GC.SuppressFinalize(this);
    }
}
