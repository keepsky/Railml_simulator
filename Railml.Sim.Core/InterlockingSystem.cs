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

        public void ReportTrainEnterTrack(SimTrack track)
        {
            // User requirement: "2 seconds later, set signal to Stop"
            // "When train enters track, set signal to Stop"
            // We need to find signals associated with this track.
            
            if (track.RailmlTrack.OcsElements?.Signals?.SignalList != null)
            {
                // We have a list of Railml Signal objects. We need to find the corresponding SimSignal objects.
                // Since _manager.Signals is a dictionary by ID, we can look them up.
                
                var signalIds = track.RailmlTrack.OcsElements.Signals.SignalList.Select(s => s.Id).ToList();
                
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
                            // Trigger "Stop" event or just set it directly via event?
                            // Using SignalChangeEvent for consistency and visualization updates if any
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
