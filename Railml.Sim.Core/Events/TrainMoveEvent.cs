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

            // Updated Occupancy (Visual + Logic)
            manager.UpdateTrainOccupancy(_train);

            // 2. Track Transition Logic
            bool endOfTrack = false;
            // Determine if we hit boundary
            bool hitEnd = (_train.MoveDirection == TrainDirection.Up && _train.PositionOnTrack >= _train.CurrentTrack.Length);
            bool hitStart = (_train.MoveDirection == TrainDirection.Down && _train.PositionOnTrack <= 0);

            if (hitEnd || hitStart)
            {
                // Calculate remainder ("overflow" distance)
                double remainder = 0;
                if (hitEnd) remainder = _train.PositionOnTrack - _train.CurrentTrack.Length;
                if (hitStart) remainder = System.Math.Abs(_train.PositionOnTrack); // 0 - (-dist) = dist

                // Try to find next track
                if (manager.FindNextTrack(_train.CurrentTrack, _train.MoveDirection, out var nextTrack, out var nextEntryPos, out var nextEntryDir))
                {
                    // 1. Check for Blocking Signal on Next Track
                    SimSignal blockingSignal = null;
                    if (nextTrack.RailmlTrack.OcsElements?.Signals?.SignalList != null && manager.Signals != null)
                    {
                        foreach (var sigData in nextTrack.RailmlTrack.OcsElements.Signals.SignalList)
                        {
                            if (manager.Signals.TryGetValue(sigData.Id, out var simSig))
                            {
                                // Simplified: Any Red signal on the target track blocks entry
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
                        // STOP and WAIT
                        _train.IsWaitingForSignal = true;
                        _train.WaitingSignal = blockingSignal;
                        _train.Speed = 0;
                        
                        // Stop at the very edge of current track
                        _train.PositionOnTrack = (hitEnd) ? _train.CurrentTrack.Length : 0; 
                        
                        manager.UpdateTrainOccupancy(_train);
                        return; // Do not schedule next move
                    }

                    // 2. ENTER Next Track
                    _train.CurrentTrack = nextTrack;
                    _train.PositionOnTrack = nextEntryPos + (remainder * (nextEntryDir == TrainDirection.Up ? 1 : -1)); 
                    // Note: If entering UP (0->L), we add remainder. If entering DOWN (L->0), we subtract remainder.
                    // nextEntryDir is the direction on the NEW track.
                    
                    _train.MoveDirection = nextEntryDir;

                    // 3. Trigger Signals on Entered Track
                    if (nextTrack.RailmlTrack.OcsElements?.Signals?.SignalList != null && manager.Signals != null)
                    {
                        foreach (var sigData in nextTrack.RailmlTrack.OcsElements.Signals.SignalList)
                        {
                            if (manager.Signals.TryGetValue(sigData.Id, out var simSig))
                            {
                                // Queue Red (Immediate)
                                manager.EventQueue.Enqueue(new SignalChangeEvent(manager.CurrentTime, simSig, SignalAspect.Stop));
                                // Queue Green (Delayed 15s)
                                manager.EventQueue.Enqueue(new SignalChangeEvent(manager.CurrentTime + 15.0, simSig, SignalAspect.Proceed));
                            }
                        }
                    }

                    // 4. Update Occupancy
                    manager.UpdateTrainOccupancy(_train);
                }
                else
                {
                     // No next track (Dead End or Open End)
                     var topo = _train.CurrentTrack.RailmlTrack.TrackTopology; 
                     bool isOpenEnd = false;

                     if (hitStart && topo?.TrackBegin?.OpenEnd != null) isOpenEnd = true;
                     if (hitEnd && topo?.TrackEnd?.OpenEnd != null) isOpenEnd = true;

                     if (isOpenEnd)
                     {
                         // Allow moving off-track
                         manager.UpdateTrainOccupancy(_train);

                         // Check for full removal
                         bool remove = false;
                         if (hitStart) // Moving to negative
                         {
                             if (_train.PositionOnTrack < -_train.Length) remove = true;
                         }
                         if (hitEnd) // Moving to positive
                         {
                             if (_train.PositionOnTrack > _train.CurrentTrack.Length + _train.Length) remove = true;
                         }

                         if (remove)
                         {
                             manager.RemoveTrain(_train);
                             return; 
                         }
                     }
                     else
                     {
                        // Stop at edge
                        if (hitEnd) _train.PositionOnTrack = _train.CurrentTrack.Length;
                        if (hitStart) _train.PositionOnTrack = 0;
                        _train.Speed = 0;
                        endOfTrack = true;
                        
                        manager.UpdateTrainOccupancy(_train);
                     }
                }
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
