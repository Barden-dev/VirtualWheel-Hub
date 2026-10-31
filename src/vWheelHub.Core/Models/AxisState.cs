namespace SimRacingHub.Models
{
    public class AxisState
    {
        public double Steering { get; set; } = 0.0;
        public double TargetSteering { get; set; } = 0.0;
        public double GasVal { get; set; } = -32768.0;
        public double BrakeVal { get; set; } = -32768.0;
        
        public void Reset(double min)
        {
            Steering = 0;
            TargetSteering = 0;
            GasVal = min;
            BrakeVal = min;
        }
    }
}
