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

        private bool _hasPerformedEmptyTrackTest = false;

        private void PerformAutoInterlockingChecks(SimulationContext context)
        {
            // [사용자 요청 사항 (1)] 선로전환기 제어 시험 기능
            // 트랙에 열차가 하나도 없을 때 모든 선로전환기에 대해서 반대 방향으로 전환하는 이벤트를 추가
            if (_manager.Trains.Count > 0)
            {
                // 열차가 있으면 플래그 리셋 (다음 공백기에 다시 테스트하기 위함)
                _hasPerformedEmptyTrackTest = false;
            }
            else
            {
                // 열차가 없고, 아직 테스트를 수행하지 않았을 때만 실행
                if (!_hasPerformedEmptyTrackTest)
                {
                     // 현재 이동 중인 선로전환기가 하나라도 있으면 모든 선로전환기가 완료될 때까지 대기
                     bool anyMoving = _manager.Switches.Values.Any(s => s.State == SimSwitch.SwitchState.Moving);
                     if (!anyMoving)
                     {
                         foreach(var sw in _manager.Switches.Values)
                         {
                             // 현재 상태의 반대 방향으로 타겟 설정 (Normal <-> Reverse)
                             var target = sw.State == SimSwitch.SwitchState.Normal ? SimSwitch.SwitchState.Reverse : SimSwitch.SwitchState.Normal;
                             
                             // 제어 명령(이벤트) 투입
                             _manager.EventQueue.Enqueue(new SwitchMoveEvent(context.CurrentTime, sw, target, false));
                         }
                         
                         // 실행 완료 표시 (열차가 다시 나타났다가 사라질 때까지 재실행 안 함)
                         _hasPerformedEmptyTrackTest = true;
                     }
                }
            }

            // 주기적인 체크를 위해 1초 후 다시 스케줄링
            ScheduleAutoCheck(context.CurrentTime + 1.0);
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
