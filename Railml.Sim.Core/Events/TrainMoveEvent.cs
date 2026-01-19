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
                // Try to find next track
                if (manager.FindNextTrack(_train.CurrentTrack, _train.MoveDirection, out var nextTrack, out var nextPos, out var nextDir))
                {
                    // Check Signal on Next Track? (User requirements)
                    // Simplified: If next track has a signal at entry (or guarding it), check it.
                    // Since we don't have block logic, let's looking for a Signal on the new track
                    // that is facing us and 'close' to the entry?
                    // Actually, if there is a signal *at the connection*, it's technically on one of the tracks.
                    
                    // Strategy: Move the train logically to the new track.
                    // Then run the Signal Logic loop immediately (in this same tick or next).
                    // If I move it now, the Position will be fresh.
                    // The Signal Logic below (Step 3) checks "signals ahead".
                    // If I move the train to the new track, Step 3 will run on the new track.
                    // It will find the signal at 15m (for example).
                    // If that signal is Red, it will stop the train.
                    // BUT, the train is already *on* the new track?
                    // User says: "Train stops at end ... if next Green ... proceed".
                    // This implies we should *peek* before entering.
                    
                    bool canEnter = true;
                    // Peek signal
                    // Find signal on nextTrack closest to nextPos in nextDir
                    // If nextDir=Up, look for signals > nextPos.
                    // If nextDir=Down, look for signals < nextPos.
                    // If one is very close (< 20m?), check aspect.
                    
                    // Using existing signal loop logic for "Lookahead":
                    var signals = nextTrack.RailmlTrack.OcsElements?.Signals?.SignalList;
                    if (signals != null)
                    {
                        foreach(var sig in signals)
                        {
                             // Match direction
                             if (sig.Dir != "up" && nextDir == TrainDirection.Up) continue;
                             if (sig.Dir != "down" && nextDir == TrainDirection.Down) continue;
                             
                             double dist = double.MaxValue;
                             if (nextDir == TrainDirection.Up)
                             {
                                 if (sig.Pos >= nextPos) dist = sig.Pos - nextPos;
                             }
                             else
                             {
                                 if (sig.Pos <= nextPos) dist = nextPos - sig.Pos;
                             }
                             
                             // If signal is within recognition distance (e.g. 200m) AND it is Red -> Stop.
                             // User requirement: "If next signal is Green -> proceed".
                             // Implies: If Red -> Don't enter.
                             
                             if (dist < 200 && manager.Signals.TryGetValue(sig.Id, out var simSig))
                             {
                                 if (simSig.Aspect == SignalAspect.Stop)
                                 {
                                     canEnter = false;
                                     // We stay at edge
                                     break;
                                 }
                             }
                        }
                    }

                    if (canEnter)
                    {
                        _train.CurrentTrack = nextTrack;
                        _train.PositionOnTrack = nextPos;
                        _train.MoveDirection = nextDir;
                        
                        // Current distance consumed? 
                        // We moved 'distance' on old track. 
                        // Realistically we should subtract the part used.
                        // Simplified: Just place at start.
                    }
                    else
                    {
                        // Stop at edge
                        if (hitEnd) _train.PositionOnTrack = _train.CurrentTrack.Length;
                        if (hitStart) _train.PositionOnTrack = 0;
                        _train.Speed = 0;
                        endOfTrack = true;
                    }
                }
                else
                {
                     // No next track (Dead End or Open End)
                     var topo = _train.CurrentTrack.RailmlTrack.TrackTopology; // Assuming accessible
                     bool isOpenEnd = false;

                     if (hitStart && topo?.TrackBegin?.OpenEnd != null) isOpenEnd = true;
                     if (hitEnd && topo?.TrackEnd?.OpenEnd != null) isOpenEnd = true;

                     if (isOpenEnd)
                     {
                         // Allow moving off-track (PositionOnTrack is already updated)
                         // Just update occupancy
                         manager.UpdateTrainOccupancy(_train);

                         // Check for full removal
                         bool remove = false;
                         if (hitStart) // Moving to negative
                         {
                             // Tail is at Pos + Length (assuming Up means Pulling from 100)
                             // Actually, simpler: If Pos < -Length, assumes fully generated.
                             // But wait, if I am consistently using logic that Up=Pulling(100->0).
                             // Then Tail is at Pos+Length.
                             // If Pos+Length < 0 => Pos < -Length.
                             if (_train.PositionOnTrack < -_train.Length) remove = true;
                         }
                         if (hitEnd) // Moving to positive
                         {
                             // Tail is at Pos - Length (Down = Pushing from 0->100?)
                             // Or Pulling 0->100?
                             // If Down (Head increases). Tail < Head.
                             // Tail = Head - Length.
                             // Tail > TrackLen => Head - Len > TrackLen => Head > TrackLen + Len.
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
