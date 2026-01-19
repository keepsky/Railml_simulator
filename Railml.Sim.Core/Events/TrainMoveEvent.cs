using System;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core.Events
{
    public class TrainMoveEvent : DESEvent
    {
        private Train _train;

        public TrainMoveEvent(double time, Train train) : base(time)
        {
            _train = train;
        }

        public override void Execute(SimulationContext context)
        {
            var manager = context as SimulationManager;
            if (manager == null || !manager.Trains.Contains(_train)) return;

            double dt = context.Settings.MovementUpdateInterval;
            
            // 1. Calculate new position
            // Distance = Speed * dt
            double distance = _train.Speed * dt;
            
            // Update Position
            if (_train.MoveDirection == TrainDirection.Up)
                _train.PositionOnTrack += distance;
            else
                _train.PositionOnTrack -= distance;

            // 2. Track Transition Logic
            // If Position > Length (Up) or < 0 (Down)
            bool endOfTrack = false;
            if (_train.MoveDirection == TrainDirection.Up && _train.PositionOnTrack > _train.CurrentTrack.Length)
            {
                // Check connections at End
                // Simplified: Just loop or stop for now.
                // TODO: Implement Switch/Connection traversal
                _train.PositionOnTrack = _train.CurrentTrack.Length;
                _train.Speed = 0; // Stop at end
                endOfTrack = true;
            }
            else if (_train.MoveDirection == TrainDirection.Down && _train.PositionOnTrack < 0)
            {
                // Check connections at Begin
                _train.PositionOnTrack = 0;
                _train.Speed = 0;
                endOfTrack = true;
            }

            // Track if we are braking for any signal
            bool brakingForSignal = false;

            // 3. Signal Logic
            // Check signals ahead. 
            // Look for signals on current track in direction of travel.
            if (_train.CurrentTrack.RailmlTrack.OcsElements?.Signals?.SignalList != null && _train.CurrentTrack.IsOccupied) 
            {
               foreach(var sigMeta in _train.CurrentTrack.RailmlTrack.OcsElements.Signals.SignalList)
               {
                   // Check direction match
                   if (sigMeta.Dir != "up" && _train.MoveDirection == TrainDirection.Up) continue;
                   if (sigMeta.Dir != "down" && _train.MoveDirection == TrainDirection.Down) continue;
                   
                   // Check position
                   // Up: Train Pos < Sig Pos.
                   // Down: Train Pos > Sig Pos.
                   // And within braking/recog distance.
                   
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
                       // Found meaningful signal using ID
                       if (manager.Signals.TryGetValue(sigMeta.Id, out var signal))
                       {
                           if (signal.Aspect == SignalAspect.Stop)
                           {
                               brakingForSignal = true;

                               // We must stop.
                               // Simplified: If at signal (dist approx 0), stop.
                               // Else, brake.
                               
                               if (distToSignal < 1.0 || _train.Speed < 0.01) // At signal or Stopped
                               {
                                   _train.Speed = 0;
                                   
                                   // Notified Interlocking?
                                   if (!_train.IsWaitingForSignal)
                                   {
                                       _train.IsWaitingForSignal = true;
                                       if (manager.Interlocking != null)
                                       {
                                            manager.Interlocking.ReportTrainWaitingAtSignal(signal);
                                       }
                                   }
                               }
                               else
                               {
                                   // Brake
                                   // v_new = v_old - decel * dt
                                   double decel = context.Settings.SignalBrakingDeceleration;
                                   
                                   // Only brake if we need to? 
                                   // Simple logic: If Red, simplify brake to stop.
                                   // If we are stopped (handled above), we wait.
                                   // If moving, brake.
                                   _train.Speed -= decel * dt;
                                   if (_train.Speed < 0) _train.Speed = 0;
                               }
                           }
                           else if (signal.Aspect == SignalAspect.Proceed)
                           {
                               if (_train.IsWaitingForSignal)
                               {
                                   _train.IsWaitingForSignal = false; // Resolved
                               }
                           }
                       }
                   }
               }
            }
            // Default acceleration if not braking for signal
            if (!endOfTrack && !brakingForSignal && !_train.IsWaitingForSignal && _train.Speed < _train.MaxSpeed)
            {
                _train.Speed += 0.5 * dt; // Simple acceleration
                if (_train.Speed > _train.MaxSpeed) _train.Speed = _train.MaxSpeed;
            }

            // 5. Check Accidents (Monitor)
            // (4.4.1) Collision: Check if another train is on same track within range?
            // (4.4.2) Derailment: Checked during switch traversal (not implemented fully here yet)

            // Schedule next move
            if (!endOfTrack || _train.Speed > 0)
            {
                // If stopped (Speed 0), wait longer to save CPU? or same dt?
                // If waiting for signal, we can wait 1 sec.
                double nextDt = _train.Speed <= 0.001 ? 1.0 : dt;
                context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + nextDt, _train));
            }
        }
    }
}
