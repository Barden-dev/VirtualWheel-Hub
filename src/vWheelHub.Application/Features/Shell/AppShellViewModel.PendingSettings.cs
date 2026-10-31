using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Features.Shell;

public partial class AppShellViewModel
{
    public void RegisterPendingSettings(IPendingSettingsChanges? pending) =>
        _profileSave.RegisterPendingSettings(pending);

    private void ProfileSaveChanged() => RunOnUi(() =>
    {
        NotifyProfileProperties();
        OnPropertyChanged(nameof(HasProfileSaveError));
    });

    private void PendingContextChanged() => RunOnUi(() =>
    {
        OnPropertyChanged(nameof(PendingGameContext));
        OnPropertyChanged(nameof(HasPendingContext));
    });

    public Task<bool> ExecuteSaveProfileAsync() => _profileSave.SaveAsync(CurrentGameContext);
}
