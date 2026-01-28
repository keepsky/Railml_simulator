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
             // Schedule first switch timer with "skip first value" logic
             // 1. Generate first sample (Discard)
             SwitchTimerEvent.GetNextDelay(_manager.Settings.SwitchTransitionTime);
             
             // 2. Generate second sample (Use)
             double delay = SwitchTimerEvent.GetNextDelay(_manager.Settings.SwitchTransitionTime);
             
             ScheduleNextSwitchToggle(_manager.CurrentTime + delay);
        }

        private void ScheduleNextSwitchToggle(double time)
        {
            _manager.EventQueue.Enqueue(new SwitchTimerEvent(time));
        }

        public void ReportTrainWaitingAtSignal(SimSignal signal)
        {
            // User requirement: "5 seconds later, show Green aspect"
            RequestSignalAspect(signal, SignalAspect.Proceed, 5.0);
        }

        public void ReportTrainExitTrack(Train train, SimTrack track)
        {
            if (track == null) return;

            // [Corrected] RailML 표준에 맞춰 물리적 이동 방향에 기반하여 신호 방향을 매칭함
            string trainLogicalDir = (train.MoveDirection == TrainDirection.Up) ? "up" : "down";

            var signals = track.RailmlTrack.OcsElements?.Signals?.SignalList?
                .Where(s => (s.Dir?.ToLower() ?? "unknown") == trainLogicalDir)
                .ToList();

            if (signals == null || signals.Count == 0) return;

            // 열차가 트랙을 빠져나가 비점유 상태가 되더라도 신호 상태를 변경하지 않음
        }

        public void ReportTrainEnterTrack(Train train, SimTrack track)
        {
            if (track == null) return;

            // [Corrected] RailML 표준에 맞춰 물리적 이동 방향에 기반하여 신호 방향을 매칭함
            string trainLogicalDir = (train.MoveDirection == TrainDirection.Up) ? "up" : "down";

            var signals = track.RailmlTrack.OcsElements?.Signals?.SignalList?
                .Where(s => (s.Dir?.ToLower() ?? "unknown") == trainLogicalDir)
                .ToList();

            if (signals == null || signals.Count == 0) return;

            // User Requirement: 10 seconds (configurable) later, set signal to Stop (Red)
            foreach (var sigData in signals)
            {
                if (_manager.Signals.TryGetValue(sigData.Id, out var simSig))
                {
                    RequestSignalAspect(simSig, SignalAspect.Stop, _manager.Settings.TrackResponseTime);
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
