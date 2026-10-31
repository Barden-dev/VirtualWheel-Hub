namespace SimRacingHub.Models;

public enum VehicleIdentityState { NotRunning, Waiting, Confirmed, Unsupported, DetectionDisabled }

/// <summary>Current game evidence, independent of the profile being edited.</summary>
public sealed record GameVehicleStatus(string? Game, VehicleIdentityState State,
    string? Car = null, string? CarClass = null);
