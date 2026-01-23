using Railml.Sim.Core.Models;

namespace Railml.Sim.Core.SimObjects
{
    public class SimSwitch
    {
        public Switch RailmlSwitch { get; }
        public Point ScreenPos { get; set; }
        
        public enum SwitchState
        {
            Normal,     // Corresponds to 'Straight' usually, or the default path
            Reverse,    // Corresponds to 'Diverging' path
            Moving      // In transition (10s delay)
        }

        // We need to know which connection is currently active.
        // For simplicity, let's store the 'Course' or connection ID.
        public string CurrentCourse { get; set; }

        public SwitchState State { get; set; } = SwitchState.Normal;
        public SwitchState TargetState { get; set; } = SwitchState.Normal;
        public double SwitchingStartTime { get; set; } = 0.0;
        public double SwitchingDuration { get; set; } = 10.0; // Will be overwritten by Settings

        public SimSwitch(Switch sw)
        {
            RailmlSwitch = sw;
            CurrentCourse = sw.TrackContinueCourse ?? "Straight"; // Default
            
            // Map initial course to State if possible, or default to Normal
            State = SwitchState.Normal; 
        }
    }
}
