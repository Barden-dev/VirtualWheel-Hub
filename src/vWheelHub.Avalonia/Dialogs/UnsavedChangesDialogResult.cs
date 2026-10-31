namespace SimRacingHub.Avalonia.Dialogs
{
    /// <summary>
    /// User decision outcome from the unsaved profile changes dialog.
    /// </summary>
    public enum UnsavedChangesDialogResult
    {
        /// <summary>
        /// Cancel the operation and keep the current profile active with unsaved changes.
        /// </summary>
        Cancel,

        /// <summary>
        /// Save changes to the current profile and proceed with the operation.
        /// </summary>
        Save,

        /// <summary>
        /// Discard changes made to the current profile and proceed with the operation.
        /// </summary>
        Discard
    }
}
