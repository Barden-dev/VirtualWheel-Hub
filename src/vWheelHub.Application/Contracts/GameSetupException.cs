namespace SimRacingHub.Application.Contracts;

/// <summary>A recoverable configuration failure with a localized recovery instruction.</summary>
public sealed class GameSetupException(string reasonKey, string? details = null, Exception? inner = null)
    : InvalidOperationException(details ?? string.Empty, inner)
{
    public string ReasonKey { get; } = reasonKey;
}
