namespace SimRacingHub.Application.Contracts;

/// <summary>Optional physical input capture when bindings must follow a selected device pair.</summary>
public interface IKeyCaptureSource
{
    // Null keeps the platform UI's aggregate capture in automatic mode.
    IDisposable? Listen(Action<int> keyPressed);
}
