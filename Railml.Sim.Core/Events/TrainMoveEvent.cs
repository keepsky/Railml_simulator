using System;
using Railml.Sim.Core.SimObjects;
using Railml.Sim.Core.Models;
using Railml.Sim.Core;

namespace Railml.Sim.Core.Events
{
    public class TrainMoveEvent : DESEvent
    {
        private Train _train;

        public TrainMoveEvent(double time, Train train) : base(time)
        {
            _train = train;
        }

        public override string GetLogInfo()
        {
            string trackId = _train.CurrentTrack.RailmlTrack.Id;
            string trackName = _train.CurrentTrack.RailmlTrack.Name ?? "N/A";
            return $"Train: {_train.Id}, Track: {trackName}({trackId}), Pos: {_train.PositionOnTrack:F2}m, Dir: {_train.MoveDirection}";
        }

        public override void Execute(SimulationContext context)
        {
            var manager = context as SimulationManager;
            if (manager == null || !manager.Trains.Contains(_train)) return;

            double dt = context.Settings.MovementUpdateInterval;
            
            // 1. Calculate new position
            double distance = _train.Speed * dt;
            
            if (_train.MoveDirection == TrainDirection.Up)
                _train.PositionOnTrack += distance;
            else
                _train.PositionOnTrack -= distance;

            // 2. Track Transition Logic
            bool endOfTrack = false;
            bool hitEnd = (_train.MoveDirection == TrainDirection.Up && _train.PositionOnTrack >= _train.CurrentTrack.Length);
            bool hitStart = (_train.MoveDirection == TrainDirection.Down && _train.PositionOnTrack <= 0);

            if (hitEnd || hitStart)
            {
                double remainder = 0;
                if (hitEnd) remainder = _train.PositionOnTrack - _train.CurrentTrack.Length;
                if (hitStart) remainder = System.Math.Abs(_train.PositionOnTrack);

                if (manager.FindNextTrack(_train.CurrentTrack, _train.MoveDirection, out var nextTrack, out var nextEntryPos, out var nextEntryDir, out var crossedSwitch))
                {
                    // [Safety Check] Ensure nextTrack is valid
                    if (nextTrack == null) return;

                    // [Safety Check] Derailment if crossing a moving switch
                    if (crossedSwitch != null && crossedSwitch.State == SimSwitch.SwitchState.Moving)
                    {
                         string info = $"Derailment detected!\n" +
                                       $"Switch: {crossedSwitch.RailmlSwitch.Id}\n" +
                                       $"Train: {_train.Id} ({_train.MoveDirection})\n" +
                                       $"Reason: Train entered switch while it was moving (Transitioning).";
                         manager.Stop();
                         manager.ReportAccident(info);
                         return;
                    }

                    // Check for Blocking Signal
                    string nextLogicalDir = GetTrainLogicalDirection(nextTrack!, nextEntryDir);
                    SimSignal blockingSignal = null;
                    if (nextTrack!.RailmlTrack.OcsElements?.Signals?.SignalList != null && manager.Signals != null)
                    {
                        foreach (var sigData in nextTrack.RailmlTrack.OcsElements.Signals.SignalList)
                        {
                            if ((sigData.Dir?.ToLower() ?? "unknown") != nextLogicalDir) continue;

                            // [Bugfix] 다음 트랙에 진입할 때, 진입점(nextEntryPos) 근처의 입구 신호기는 무시함.
                            // 실제 진행 방향으로 최소 2.0m 이상 떨어져 있는 '전방' 신호기만 블로킹 대상으로 간주.
                            if (nextEntryDir == TrainDirection.Up)
                            {
                                if (sigData.Pos <= nextEntryPos + 2.0) continue;
                            }
                            else
                            {
                                if (sigData.Pos >= nextEntryPos - 2.0) continue;
                            }

                            if (manager.Signals.TryGetValue(sigData.Id, out var simSig))
                            {
                                if (simSig.Aspect == SignalAspect.Stop)
                                {
                                    blockingSignal = simSig;
                                    break;
                                }
                            }
                        }
                    }

                    if (blockingSignal != null)
                    {
                        if (!_train.IsWaitingForSignal && manager.Interlocking != null)
                            manager.Interlocking.ReportTrainWaitingAtSignal(blockingSignal);

                        _train.IsWaitingForSignal = true;
                        _train.WaitingSignal = blockingSignal;
                        _train.Speed = 0;
                        _train.PositionOnTrack = (hitEnd) ? _train.CurrentTrack.Length : 0; 

                        // Update occupancy for final position (waiting at signal)
                        manager.UpdateTrainOccupancy(_train);
                        return;
                    }

                    // ENTER Next Track
                    _train.CurrentTrack = nextTrack!;
                    _train.PositionOnTrack = nextEntryPos + (remainder * (nextEntryDir == TrainDirection.Up ? 1 : -1)); 
                    _train.MoveDirection = nextEntryDir;
                }
                else
                {
                     var topo = _train.CurrentTrack.RailmlTrack.TrackTopology; 
                     bool isOpenEnd = false;
                     if (hitStart && topo?.TrackBegin?.OpenEnd != null) isOpenEnd = true;
                     if (hitEnd && topo?.TrackEnd?.OpenEnd != null) isOpenEnd = true;

                     if (isOpenEnd)
                     {
                         // Check for full removal
                         bool remove = false;
                         if (hitStart && _train.PositionOnTrack < -_train.Length) remove = true;
                         if (hitEnd && _train.PositionOnTrack > _train.CurrentTrack.Length + _train.Length) remove = true;

                         if (remove)
                         {
                             manager.RemoveTrain(_train);
                             return; 
                         }
                     }
                     else
                     {
                        if (hitEnd) _train.PositionOnTrack = _train.CurrentTrack.Length;
                        if (hitStart) _train.PositionOnTrack = 0;
                        _train.Speed = 0;
                        endOfTrack = true;
                     }
                }
            }

            // Consolidated Occupancy Update (Handles Enters/Exits and Interlocking Reports)
            manager.UpdateTrainOccupancy(_train);

            // 3. Signal Braking Logic
            bool brakingForSignal = false;
            if (_train.CurrentTrack.RailmlTrack.OcsElements?.Signals?.SignalList != null) 
            {
               foreach(var sigMeta in _train.CurrentTrack.RailmlTrack.OcsElements.Signals.SignalList)
               {
                   string trainLogicalDir = GetTrainLogicalDirection(_train.CurrentTrack, _train.MoveDirection);
                   if ((sigMeta.Dir?.ToLower() ?? "unknown") != trainLogicalDir) continue;
                   
                   double distToSignal = double.MaxValue;
                   if (_train.MoveDirection == TrainDirection.Up)
                   {
                       if (sigMeta.Pos > _train.PositionOnTrack)
                           distToSignal = sigMeta.Pos - _train.PositionOnTrack;
                   }
                   else
                   {
                       if (sigMeta.Pos < _train.PositionOnTrack)
                           distToSignal = _train.PositionOnTrack - sigMeta.Pos;
                   }
                   
                   if (distToSignal <= context.Settings.SignalRecognitionDistance)
                   {
                       // [Bugfix] 열차 머리가 신호를 막 통과했거나 통과 중인 경우 (2.0m 이내),
                       // 해당 신호가 Red로 바뀌더라도 무시함 (자기 점유에 의한 정지 방지 및 진행 방향 반영).
                       // Up: sig.Pos > Train.Pos + 2.0 (전방) / Down: sig.Pos < Train.Pos - 2.0 (전방)
                       if (_train.MoveDirection == TrainDirection.Up)
                       {
                           if (sigMeta.Pos <= _train.PositionOnTrack + 2.0) continue;
                       }
                       else
                       {
                           if (sigMeta.Pos >= _train.PositionOnTrack - 2.0) continue;
                       }

                       if (manager.Signals.TryGetValue(sigMeta.Id, out var signal))
                       {
                           if (signal.Aspect == SignalAspect.Stop)
                           {
                               brakingForSignal = true;
                               if (distToSignal < 1.0 || _train.Speed < 0.01)
                                {
                                    _train.Speed = 0;
                                    if (!_train.IsWaitingForSignal)
                                    {
                                        _train.IsWaitingForSignal = true;
                                        _train.WaitingSignal = signal;
                                        if (manager.Interlocking != null)
                                             manager.Interlocking.ReportTrainWaitingAtSignal(signal);
                                    }
                               }
                               else
                               {
                                   double decel = context.Settings.SignalBrakingDeceleration;
                                   _train.Speed -= decel * dt;
                                   if (_train.Speed < 0) _train.Speed = 0;
                               }
                           }
                           else if (signal.Aspect == SignalAspect.Proceed && _train.IsWaitingForSignal)
                           {
                               _train.IsWaitingForSignal = false;
                           }
                       }
                   }
               }
            }

            // Acceleration logic
            if (!endOfTrack && !brakingForSignal && !_train.IsWaitingForSignal && _train.Speed < _train.MaxSpeed)
            {
                _train.Speed += 0.5 * dt;
                if (_train.Speed > _train.MaxSpeed) _train.Speed = _train.MaxSpeed;
            }

            // 5. Check Accidents - Removed redundant check (Handled by SafetyMonitor after Execute)

            // Schedule next move
            if (!endOfTrack || _train.Speed > 0)
            {
                double nextDt = _train.Speed <= 0.001 ? 1.0 : dt;
                context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + nextDt, _train));
            }
        }

        private string GetTrainLogicalDirection(SimTrack track, TrainDirection moveDir)
        {
            // [Corrected] 선로의 mainDir과 상관없이 물리적 좌표 변화 방향이 RailML의 신호 방향(dir="up/down")과 일치함.
            return (moveDir == TrainDirection.Up) ? "up" : "down";
        }
    }
}
