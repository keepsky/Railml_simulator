using Railml.Simulation.Core.Models;

namespace Railml.Simulation.Core.SimObjects
{
    public class SimSwitch
    {
        public Switch RailmlSwitch { get; }
        
        // We need to know which connection is currently active.
        // For simplicity, let's store the 'Course' or connection ID.
        public string CurrentCourse { get; set; }

        public SimSwitch(Switch sw)
        {
            RailmlSwitch = sw;
            CurrentCourse = sw.TrackContinueCourse ?? "Straight"; // Default
        }
    }
}
