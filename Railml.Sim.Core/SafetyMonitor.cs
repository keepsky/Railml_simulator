using System;
using System.Linq;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core
{
    public class SafetyMonitor
    {
         private SimulationManager _manager = null!;

         public SafetyMonitor(SimulationManager manager)
         {
             _manager = manager;
             _manager.OnSimulationUpdated += CheckSafety;
         }

         public void CheckSafety()
         {
             // 1. Collision & Rear-end Checks
             foreach(var track in _manager.Tracks.Values)
             {
                 if (track.OccupyingTrains.Count > 1)
                 {
                     // For simplicity, just report the first pair found
                     var t1 = track.OccupyingTrains[0];
                     var t2 = track.OccupyingTrains[1];

                     string accidentType = (t1.MoveDirection == t2.MoveDirection) ? "Rear-end Collision" : "Head-on Collision";
                     
                     string info = $"{accidentType} detected!\n" +
                                  $"Track: {track.RailmlTrack.Name}({track.RailmlTrack.Id})\n" +
                                  $"Train 1: {t1.Id} ({t1.MoveDirection})\n" +
                                  $"Train 2: {t2.Id} ({t2.MoveDirection})";

                     TriggerAccident(info);
                     return;
                 }
             }

             // 2. Derailment (Switch moving under train)
             foreach (var sw in _manager.Switches.Values)
             {
                 if (sw.State == SimSwitch.SwitchState.Moving)
                {
                    // Check if parent track has any trains
                    if (sw.ParentTrack != null && sw.ParentTrack.OccupyingTrains.Count > 0)
                    {
                        var train = sw.ParentTrack.OccupyingTrains[0];
                        string info = $"Derailment detected!\n" +
                                     $"Switch: {sw.RailmlSwitch.Id}\n" +
                                     $"Train: {train.Id} ({train.MoveDirection})";

                        TriggerAccident(info);
                        return;
                    }
                }
            }
        }

        private void TriggerAccident(string message)
        {
            _manager.Stop(); 
            _manager.ReportAccident(message);
        }
    }
}
