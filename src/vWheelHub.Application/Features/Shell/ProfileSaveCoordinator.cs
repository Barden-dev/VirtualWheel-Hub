using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Shell;

/// <summary>Coordinates profile and global pending-settings writes without claiming cross-store transactions.</summary>
internal sealed class ProfileSaveCoordinator : IDisposable
{
    private readonly ProfileSession _session;
    private readonly IRuntimeSession _runtime;
    private IPendingSettingsChanges? _pending;

    public ProfileSaveCoordinator(ProfileSession session, IRuntimeSession runtime)
    {
        _session = session;
        _runtime = runtime;
    }

    public event Action? Changed;

    public bool IsSaving { get; private set; }

    public bool HasUnsavedChanges => _session.HasUnsavedChanges || _pending?.HasChanges == true;

    public bool HasSaveError => _session.HasSaveError || _pending?.HasError == true;

    public bool CanUndo => !IsSaving && _pending?.IsBusy != true &&
        (_session.CanUndo || _pending?.HasChanges == true);

    public bool CanSave => !IsSaving && _pending?.IsBusy != true &&
        (_pending?.HasChanges != true || _pending.CanSave) &&
        (HasUnsavedChanges || _session.IsNewProfile);

    public bool IsBusy => IsSaving || _pending?.IsBusy == true;

    public void RegisterPendingSettings(IPendingSettingsChanges? pending)
    {
        if (ReferenceEquals(_pending, pending))
        {
            return;
        }

        if (_pending != null)
        {
            _pending.Changed -= PendingChanged;
        }

        _pending = pending;
        if (_pending != null)
        {
            _pending.Changed += PendingChanged;
        }

        Changed?.Invoke();
    }

    public async Task<bool> SaveAsync(ProfileContext context)
    {
        if (IsSaving || _pending is { IsBusy: true } ||
            _pending is { HasChanges: true, CanSave: false })
        {
            return false;
        }

        IsSaving = true;
        Changed?.Invoke();
        try
        {
            // The stores are intentionally ordered, not transactional. A profile
            // failure must leave the global device draft untouched.
            if ((_session.HasUnsavedChanges || _session.IsNewProfile || _pending?.HasChanges != true) &&
                !SaveProfile(context))
            {
                return false;
            }

            if (_pending is { HasChanges: true } pending && !await pending.SaveAsync())
            {
                return false;
            }

            return true;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    public bool SaveSynchronously(ProfileContext context)
    {
        // Legacy synchronous callers cannot persist asynchronous global edits.
        if (_pending?.HasChanges == true || IsSaving)
        {
            return false;
        }

        return SaveProfile(context);
    }

    public void DiscardAll(ProfileContext context)
    {
        if (IsBusy)
        {
            return;
        }

        _pending?.Revert();
        _session.DiscardChanges();
        UpdateRuntime(context);
        Changed?.Invoke();
    }

    public void Revert(ProfileContext context)
    {
        if (IsBusy)
        {
            return;
        }

        _pending?.Revert();
        _session.RevertProfile();
        UpdateRuntime(context);
        Changed?.Invoke();
    }

    public void RevertPendingSettings()
    {
        _pending?.Revert();
        Changed?.Invoke();
    }

    private bool SaveProfile(ProfileContext context)
    {
        if (!_session.SaveProfile(context))
        {
            return false;
        }

        UpdateRuntime(context);
        Changed?.Invoke();
        return true;
    }

    private void UpdateRuntime(ProfileContext context)
    {
        if (_runtime.IsRunning)
        {
            _runtime.UpdateProfile(_session.ResolvedProfile, context);
        }
    }

    private void PendingChanged() => Changed?.Invoke();

    public void Dispose()
    {
        if (_pending != null)
        {
            _pending.Changed -= PendingChanged;
            _pending = null;
        }
    }
}
