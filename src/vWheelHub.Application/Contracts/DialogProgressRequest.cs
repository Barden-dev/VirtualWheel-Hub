namespace SimRacingHub.Application.Contracts;

/// <summary>A silent operation window with a countdown until its next phase.</summary>
public sealed record DialogProgressRequest(string Title, string Message, int CountdownSeconds,
    string CountdownFormat, string ElapsedMessage);
