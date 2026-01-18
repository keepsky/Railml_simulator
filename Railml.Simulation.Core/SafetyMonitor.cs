using System;
using System.Linq;

namespace Railml.Simulation.Core
{
    public class SafetyMonitor
    {
         private SimulationManager _manager;

        public SafetyMonitor(SimulationManager manager)
        {
            _manager = manager;
            _manager.OnSimulationUpdated += CheckSafety;
        }

        public void CheckSafety()
        {
            // (4.4.1) Collision
            foreach(var track in _manager.Tracks.Values)
            {
                if (track.OccupyingTrains.Count > 1)
                {
                    // Check if they physically overlap or just on same track?
                    // "Same track occupied" -> Collision event per requirements (4.4.1)
                    // But typically multiple trains can be on a long track if spaced out.
                    // Let's assume strict block system: >1 train = Collision.
                    
                    Console.WriteLine($"[ACCIDENT] Collision detected on Track {track.RailmlTrack.Id}");
                    // Stop simulation or flag accident
                }
            }

            // (4.4.2) Derailment (Switch moving under train)
            // This is hard to check "after the fact" unless we log switch moves.
            // Or if train is ON a switch and switch course != train path?
        }
    }
}
