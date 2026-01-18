using System;

namespace Railml.Simulation.Core
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
    }

    public enum TrainDirection
    {
        Up,
        Down
    }
}
