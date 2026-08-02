namespace SimRacingHub.Models
{
    public class TelemetryData
    {
        public float ScaleFactor { get; set; } = 1.0f;
        public float TcScaleFactor { get; set; } = 1.0f;
        public float MouseSensMultiplier { get; set; } = 1.0f;
        public float DisableSyntheticTb { get; set; } = 0.0f;
        public bool IsValid { get; set; } = true;
        
        // Vehicle information
        public string CarClass { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        
        // Extended telemetry for ABS
        public double LocalVelZ { get; set; }
        public double LocalVelX { get; set; }
        public double UnfilteredBrake { get; set; }
        public double UnfilteredSteering { get; set; }
        public double[] WheelRotations { get; set; } = new double[4];
        public double[] WheelRadii { get; set; } = new double[4];
        public double[] WheelLatPatchVel { get; set; } = new double[4];
        public bool[] WheelDetached { get; set; } = new bool[4];
        public bool[] WheelFlat { get; set; } = new bool[4];
        public bool IsOffTrack { get; set; }
        
        // Weather telemetry
        public double Raining { get; set; }
        public double AvgPathWetness { get; set; }
        public double TrackTemp { get; set; }
        public double AmbientTemp { get; set; }
    }
}
