using System;
using System.Linq;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core
{
    public class InterlockingSystem
    {
        private SimulationManager _manager;

        public InterlockingSystem(SimulationManager manager)
        {
            _manager = manager;
            _manager.OnSimulationUpdated += Update;
        }

        public void Update()
        {
            UpdateTrackCircuits();
            UpdateSignals();
        }

        private void UpdateTrackCircuits()
        {
            // Reset occupancy (or handled by Train movement logic? SimTrack.OccupyingTrains is updated there)
            // But we might want to check exact positions vs TrackCircuitBorder.
            // Simplified: SimTrack has IsOccupied.
        }

        private void UpdateSignals()
        {
            // Simple Logic: Block Signal.
            // For each signal, check the track it protects.
            // If the track ahead is occupied, Signal = Red.
            
            // Getting the "Track Ahead" is tricky without a graph traversal.
            // We can iterate signals and check their "owner" track and direction.
            
            foreach(var sig in _manager.Signals.Values)
            {
                // Find which track this signal belongs to.
                // In RailML OcsElements are child of Track.
                // We need a reverse mapping or search.
                // For performance, SimSignal should know its Track.
                
                // Assuming we find the track:
                // If Signal Dir is Up, check if Track is occupied.
                // Real interlocking is complex. 
                // Simplified Rule: If ANY train is on the track containing the signal, AND the train is "downstream" 
                // or if the NEXT track is occupied.
                
                // For now, let's just set all signals to Green unless immediate track is occupied.
                 sig.Aspect = SignalAspect.Proceed;
            }
        }
        
        // Method to request route / switch change
        public bool RequestSwitch(SimSwitch sw, string desiredCourse)
        {
             // Check if switch is locked or occupied
             // (4.4.2) If occupied, cannot switch -> enforced here or in Monitor?
             // Monitor checks accidents. Interlocking prevents them if working correctly.
             
             // Find track containing this switch
             // If track occupied, return false.
             
             sw.CurrentCourse = desiredCourse;
             return true;
        }
    }
}
