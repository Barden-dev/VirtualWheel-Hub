using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Shell;

/// <summary>Owns context-switch decisions and the single pending context shown by the shell.</summary>
internal sealed class ProfileContextChangeCoordinator
{
    private readonly ProfileSaveCoordinator _save;
    private readonly IUserDialogService _dialogs;
    private readonly ILocalizationManager _localization;

    public ProfileContextChangeCoordinator(
        ProfileSaveCoordinator save,
        IUserDialogService dialogs,
        ILocalizationManager localization)
    {
        _save = save;
        _dialogs = dialogs;
        _localization = localization;
    }

    public event Action? PendingContextChanged;

    public ProfileContext? PendingContext { get; private set; }

    public void SetPendingContext(ProfileContext current, ProfileContext context, Action<ProfileContext> apply)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (current.Equals(context))
        {
            SetPending(null);
        }
        else if (!_save.HasUnsavedChanges)
        {
            SetPending(null);
            apply(context);
        }
        else
        {
            SetPending(context);
        }
    }

    public void SwitchToPendingContext(Action<ProfileContext> apply)
    {
        if (_save.IsBusy || PendingContext == null)
        {
            return;
        }

        ProfileContext target = PendingContext;
        _save.RevertPendingSettings();
        SetPending(null);
        apply(target);
    }

    public async Task SaveAndSwitchToPendingContextAsync(ProfileContext current, Action<ProfileContext> apply)
    {
        if (PendingContext == null)
        {
            return;
        }

        ProfileContext target = PendingContext;
        if (!await _save.SaveAsync(current))
        {
            return;
        }

        SetPending(null);
        apply(target);
    }

    public void SaveAndSwitchToPendingContextSynchronously(ProfileContext current, Action<ProfileContext> apply)
    {
        if (PendingContext == null)
        {
            return;
        }

        ProfileContext target = PendingContext;
        if (!_save.SaveSynchronously(current))
        {
            return;
        }

        SetPending(null);
        apply(target);
    }

    public void IgnorePendingContext() => SetPending(null);

    public void ClearPendingContext() => SetPending(null);

    public async Task<bool> RequestChangeContextAsync(
        ProfileContext current,
        ProfileContext newContext,
        string profileName,
        Action<ProfileContext> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newContext);
        if (current.Equals(newContext))
        {
            return true;
        }

        if (!_save.HasUnsavedChanges)
        {
            apply(newContext);
            return true;
        }

        string prompt = string.Format(
            _localization.GetString(
                "Msg_UnsavedChanges_Prompt",
                "You have unsaved changes in profile '{0}'. Save before switching to '{1}'?"),
            profileName,
            newContext.DisplayPath);
        var request = new DialogConfirmRequest(
            _localization.GetString("Title_UnsavedChanges", "Unsaved Changes"),
            prompt,
            _localization.GetString("Btn_Save", "Save"),
            _localization.GetString("Btn_Discard", "Discard"),
            DialogSeverity.Warning);

        DialogResult result = await _dialogs.ConfirmAsync(request, cancellationToken).ConfigureAwait(true);
        if (result == DialogResult.Confirmed)
        {
            if (!await _save.SaveAsync(current))
            {
                return false;
            }
        }
        else
        {
            _save.RevertPendingSettings();
        }

        apply(newContext);
        return result == DialogResult.Confirmed;
    }

    private void SetPending(ProfileContext? context)
    {
        if (Equals(PendingContext, context))
        {
            return;
        }

        PendingContext = context;
        PendingContextChanged?.Invoke();
    }
}
