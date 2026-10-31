namespace SimRacingHub.Application.Contracts;

/// <summary>Application-wide edits saved and reverted with the shell's shared actions.</summary>
public interface IPendingSettingsChanges
{
    event Action? Changed;
    bool HasChanges { get; }
    bool CanSave { get; }
    bool IsBusy { get; }
    bool HasError { get; }
    Task<bool> SaveAsync();
    void Revert();
}
