using System;

namespace Railml.Sim.Core
{
    public class SimulationSettings
    {
        // 1.2.1 Random Distribution for Train Generation
        public double MeanInterArrivalTime { get; set; } = 300.0; // Seconds (Exponential Distribution)

        // 1.2.2 Train Specs
        public double TrainLength { get; set; } = 200.0; // meters
        public double DefaultSpeed { get; set; } = 20.0; // m/s
        public double BrakingDeceleration { get; set; } = 1.0; // m/s^2 (Standard braking performance)
        
        // 1.2.3 Signal Recognition Distance
        public double SignalRecognitionDistance { get; set; } = 500.0; // meters
        
        // 1.2.4 Braking Performance for Signal Reaction (e.g., strong braking)
        public double SignalBrakingDeceleration { get; set; } = 1.5; // m/s^2 (X km/sec -> m/s^2 logic)

        // 1.2.5 Simulation Time Step (for continuous movement simulation via discrete events)
        public double MovementUpdateInterval { get; set; } = 1.0; // seconds

        public TrainDirection DefaultDirection { get; set; } = TrainDirection.Up;

        // Switch Logic
        public double SwitchTransitionTime { get; set; } = 10.0; // seconds
        
        // Spawn Control
        public bool TrainSpawnUp { get; set; } = true;
        public bool TrainSpawnDown { get; set; } = true;

        public static SimulationSettings Load(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    return System.Text.Json.JsonSerializer.Deserialize<SimulationSettings>(json);
                }
            }
            catch { }
            return new SimulationSettings();
        }

        public void Save(string filePath)
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var json = System.Text.Json.JsonSerializer.Serialize(this, options);
                System.IO.File.WriteAllText(filePath, json);
            }
            catch { }
        }
    }

    public enum TrainDirection
    {
        Up,
        Down
    }
}
