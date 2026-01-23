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
            // User requirement: "2 seconds later, show Green aspect"
            RequestSignalAspect(signal, SignalAspect.Proceed, 2.0);
        }

        public void ReportTrainExitTrack(Train train, SimTrack track)
        {
            if (track == null) return;

            // Determine Logical Direction
            string trackMainDir = track.RailmlTrack.MainDir?.ToLower() ?? "up";
            string trainLogicalDir = (train.MoveDirection == TrainDirection.Up) ? trackMainDir : ((trackMainDir == "up") ? "down" : "up");

            var signals = track.RailmlTrack.OcsElements?.Signals?.SignalList?
                .Where(s => (s.Dir?.ToLower() ?? "unknown") == trainLogicalDir)
                .ToList();

            if (signals == null || signals.Count == 0) return;

            // 열차가 트랙을 빠져나가 비점유 상태가 되더라도 신호 상태를 변경하지 않음
        }

        public void ReportTrainEnterTrack(Train train, SimTrack track)
        {
            if (track == null) return;

            // Determine Train's Logical Direction on this track
            string trackMainDir = track.RailmlTrack.MainDir?.ToLower() ?? "up";
            string trainLogicalDir = (train.MoveDirection == TrainDirection.Up) ? trackMainDir : ((trackMainDir == "up") ? "down" : "up");

            var signals = track.RailmlTrack.OcsElements?.Signals?.SignalList?
                .Where(s => (s.Dir?.ToLower() ?? "unknown") == trainLogicalDir)
                .ToList();

            if (signals == null || signals.Count == 0) return;

            // User Requirement: 10 seconds later, set signal to Stop (Red)
            foreach (var sigData in signals)
            {
                if (_manager.Signals.TryGetValue(sigData.Id, out var simSig))
                {
                    RequestSignalAspect(simSig, SignalAspect.Stop, 10.0);
                }
            }
        }

        private void RequestSignalAspect(SimSignal signal, SignalAspect target, double delay)
        {
            // Prevent redundant enqueues
            // If already pending this state, skip.
            if (signal.PendingAspect == target) return;

            // If no pending state, but current state IS already the target, skip.
            if (signal.PendingAspect == null && signal.Aspect == target) return;

            // Mark as pending and enqueue
            signal.PendingAspect = target;
            var evt = new SignalChangeEvent(_manager.CurrentTime + delay, signal, target);
            _manager.EventQueue.Enqueue(evt);
        }
        
         // Method to request route / switch change (Generic)
        public bool RequestSwitch(SimSwitch sw, string desiredCourse)
        {
             sw.CurrentCourse = desiredCourse;
             return true;
        }
    }
}
