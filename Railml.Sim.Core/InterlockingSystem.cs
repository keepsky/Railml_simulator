using System;
using System.Linq;
using Railml.Sim.Core.SimObjects;
using Railml.Sim.Core.Events;

namespace Railml.Sim.Core
{
    public class InterlockingSystem
    {
        private SimulationManager _manager;

        public InterlockingSystem(SimulationManager manager)
        {
            _manager = manager;
            // Removed Update loop subscription in favor of DES events
        }

        public void Start()
        {
             // Schedule first auto-check
             ScheduleAutoCheck(_manager.CurrentTime + 1.0);
        }

        private void ScheduleAutoCheck(double time)
        {
            _manager.EventQueue.Enqueue(new LambdaEvent(time, PerformAutoInterlockingChecks));
        }

        private void PerformAutoInterlockingChecks(SimulationContext context)
        {
            // 1. Auto Switch Toggle (Example Logic)
            // Logic from SKILL.md 3.2: "If no trains are present... schedule SwitchMoveEvent"
            
            if (_manager.Trains.Count == 0)
            {
                 // Check if any switch is moving
                 bool anyMoving = _manager.Switches.Values.Any(s => s.State == SimSwitch.SwitchState.Moving);
                 if (!anyMoving)
                 {
                     foreach(var sw in _manager.Switches.Values)
                     {
                         var target = sw.State == SimSwitch.SwitchState.Normal ? SimSwitch.SwitchState.Reverse : SimSwitch.SwitchState.Normal;
                         // Schedule move
                         _manager.EventQueue.Enqueue(new SwitchMoveEvent(context.CurrentTime, sw, target, false));
                     }
                 }
            }

            // Reschedule check
            ScheduleAutoCheck(context.CurrentTime + 1.0);
        }

        public void ReportTrainWaitingAtSignal(SimSignal signal)
        {
            // User requirement: "5 seconds later, show Green aspect"
            var delay = 5.0;
            var evt = new SignalChangeEvent(_manager.CurrentTime + delay, signal, SignalAspect.Proceed);
            _manager.EventQueue.Enqueue(evt);
        }

        public void ReportTrainEnterTrack(Train train)
        {
            var track = train.CurrentTrack;
            if (track == null) return;

            // User requirement: "2 seconds later, set signal to Stop"
            // "When train enters track, set signal to Stop"
            // We need to find signals associated with this track.
            
            if (track.RailmlTrack.OcsElements?.Signals?.SignalList != null)
            {
                // Logic: Only set Stop for signals that face the train's direction?
                // User said: "If train direction differs from signal dir, signal should not change state."
                
                // Determine Train's Logical Direction on this track
                string trackMainDir = track.RailmlTrack.MainDir?.ToLower() ?? "up"; // Default to up
                string trainLogicalDir = "up";

                if (train.MoveDirection == TrainDirection.Up) // Moving 0 -> Length
                {
                    trainLogicalDir = trackMainDir; 
                }
                else // Moving Length -> 0
                {
                    trainLogicalDir = (trackMainDir == "up") ? "down" : "up";
                }

                // Filter Signals
                // Case-insensitive
                // Use ToLower() safely
                
                var signalIds = track.RailmlTrack.OcsElements.Signals.SignalList
                    .Where(s => (s.Dir?.ToLower() ?? "unknown") == trainLogicalDir)
                    .Select(s => s.Id).ToList();
                
                if (signalIds.Count == 0) return;

                // Schedule a lambda event to update them after 2 seconds
                var delay = 2.0;
                
                _manager.EventQueue.Enqueue(new LambdaEvent(_manager.CurrentTime + delay, (ctx) => 
                {
                    var mgr = ctx as SimulationManager;
                    if (mgr == null) return;
                    
                    foreach(var sigId in signalIds)
                    {
                        if (mgr.Signals.TryGetValue(sigId, out var simSignal))
                        {
                            // Trigger "Stop" event
                            mgr.EventQueue.Enqueue(new SignalChangeEvent(ctx.CurrentTime, simSignal, SignalAspect.Stop));
                        }
                    }
                }));
            }
        }
        
         // Method to request route / switch change (Generic)
        public bool RequestSwitch(SimSwitch sw, string desiredCourse)
        {
             sw.CurrentCourse = desiredCourse;
             return true;
        }
    }
}
