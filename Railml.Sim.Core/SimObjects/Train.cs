using System;

namespace Railml.Sim.Core.SimObjects
{
    public class Train
    {
        public string Id { get; set; }
        public double Length { get; set; }
        public double Speed { get; set; } // m/s
        public double MaxSpeed { get; set; } // m/s (from settings or train type)
        public double BrakingDeceleration { get; set; } // m/s^2

        // Position State
        public SimTrack CurrentTrack { get; set; }
        public double PositionOnTrack { get; set; } // meters from Track Begin
        public TrainDirection MoveDirection { get; set; } = TrainDirection.Up; // Up (Pos increasing) or Down (Pos decreasing)
        
        // Tracks physically occupied by this train (Head + Tail)
        public System.Collections.Generic.List<SimTrack> OccupiedTracks { get; set; } = new System.Collections.Generic.List<SimTrack>();

        // State for signal logic
        public bool IsWaitingForSignal { get; set; } = false;
        public SimSignal WaitingSignal { get; set; } = null;

        public Train(string id, SimulationSettings settings)
        {
            Id = id;
            Length = settings.TrainLength;
            MaxSpeed = settings.DefaultSpeed;
            BrakingDeceleration = settings.BrakingDeceleration; 
        }

        public enum TrainState
        {
            Stopped,
            Accelerating,
            Coasting,
            Braking,
            Dwelling
        }

        public TrainState State { get; set; } = TrainState.Stopped;

        // Logic to update position will be handled by Events, but we can have helper methods here.
    }
}
