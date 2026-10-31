namespace SimRacingHub.Application.Runtime
{
    public sealed record RuntimeSnapshot(bool IsRunning, bool IsLocked, double WheelAxis, double GasAxis, double BrakeAxis, string? GameTitle, string? StatusMessage, bool HasFault = false, string? FaultMessage = null)
    {
        public RuntimeInputSnapshot? Inputs { get; init; }

        public static RuntimeSnapshot Stopped() => new(false, false, 0, 0, 0, null, "Stopped");
    }

    public sealed record RuntimeInputSnapshot(double Wheel, double Gas, double Brake);
}
