namespace SimRacingHub.Avalonia.Dialogs
{
    /// <summary>
    /// User decision outcome from the profile preset import dialog.
    /// </summary>
    public enum ProfileImportDialogResult
    {
        /// <summary>
        /// Import cancelled by user. No profile state modified.
        /// </summary>
        Cancel,

        /// <summary>
        /// Save the imported preset into its target slot (e.g. Profiles/Game/Class/Car).
        /// </summary>
        SaveToTargetSlot,

        /// <summary>
        /// Apply the imported settings immediately to the active configuration context.
        /// </summary>
        ApplyToCurrentContext,

        /// <summary>
        /// Import preset settings (alias for ApplyToCurrentContext).
        /// </summary>
        Import = ApplyToCurrentContext
    }
}
