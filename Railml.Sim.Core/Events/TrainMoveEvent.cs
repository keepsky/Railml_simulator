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
                    
                    // 5. Report to Interlocking
                    if (manager.Interlocking != null)
                    {
                        manager.Interlocking.ReportTrainEnterTrack(_train.CurrentTrack);
                    }
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
            // ... (Signal Check Code Omitted for Brevity - Keeping existing logic) ...
            if (_train.CurrentTrack.RailmlTrack.OcsElements?.Signals?.SignalList != null && _train.CurrentTrack.IsOccupied) 
            {
               foreach(var sigMeta in _train.CurrentTrack.RailmlTrack.OcsElements.Signals.SignalList)
               {
                   // Determine Train's Logical Direction based on Track.MainDir
                   string trackMainDir = _train.CurrentTrack.RailmlTrack.MainDir ?? "up"; // Default to up
                   string trainLogicalDir = "up";

                   if (_train.MoveDirection == TrainDirection.Up) // Moving 0 -> Length
                   {
                       trainLogicalDir = trackMainDir; 
                   }
                   else // Moving Length -> 0
                   {
                       trainLogicalDir = (trackMainDir == "up") ? "down" : "up";
                   }

                   // Check direction match
                   // Signal applies if its Dir matches the Train's Logical Direction
                   if (sigMeta.Dir != trainLogicalDir) continue;
                   
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
            // (4.4.1) Collision: Check if another train is on same track?
            if (_train.CurrentTrack.OccupyingTrains.Count > 1)
            {
                foreach(var other in _train.CurrentTrack.OccupyingTrains)
                {
                    if (other == _train) continue;
                    
                    // Check if strictly on same track (OccupyingTrains includes logical, but we assume physical for now based on implementation)
                    // If shared occupancy logic puts them here, we verify track ID
                    if (other.CurrentTrack != _train.CurrentTrack) continue;

                    // Hit!
                    string type = (other.MoveDirection != _train.MoveDirection) ? "Head-on Collision" : "Rear-end Collision";
                    Console.WriteLine($"[ACCIDENT] {type} between {_train.Id} and {other.Id} on Track {_train.CurrentTrack.RailmlTrack.Id}");
                    manager.Stop(); // Stop Simulation
                    return; 
                }
            }

            // (4.4.2) Derailment: Check if passing through a MOVING switch
            // Check if current track IS a switch (not standard model) OR if we are at a switch connection.
            // Simplified: We assume Switch is a NODE connecting tracks. 
            // In RailML, Switch is <switch> element under <connections> at pos=0 (usually).
            // If we are at pos=0, check if this track has a switch at 0.
            
            // Actually, we need to check if we are *traversing* a switch. 
            // We just moved. 
            // The best place was "Track Transition Logic" but we also need to check "Current" if we are ON the switch.
            // RailML 2.4 model: Switch is a point.
            // Realistically, derailment happens if we pass over it while moving.
            // We just entered a new track or moved along it.
            
            // Check Track Begin (pos=0)
            if (_train.PositionOnTrack < 10.0) // Near begin
            {
                 // Check for Switch at Begin
                 // (Requires lookup)
            }
            // Check Track End
            if (_train.PositionOnTrack > _train.CurrentTrack.Length - 10.0)
            {
                // Check for Switch at End
            }
            
            // Optimized Derailment Check:
            // Iterate all switches, check if train is "on" it?
            // Expensive.
            // Better: When finding next track, we check switch state. If "Moving", then Derail?
            // BUT, the requirement says "switch is transitioning AND train is passing".
            // So if we are ON a track that is connected to a moving switch?
            
            // Let's implement the specific rule: "If switch is moving and train passes"
            // We can check if `_train.CurrentTrack` has any switches associated that are Moving?
            // Or simpler: Check all switches, if any is Moving, check if Train is near it.
            
            foreach(var sw in manager.Switches.Values)
            {
                if (sw.State == SimSwitch.SwitchState.Moving)
                {
                    // Check distance to this switch
                    // Switch is at Track, Pos.
                    // If Train is on sw.Track and close to sw.Pos?
                    // We need SimSwitch to know its Track/Pos logic. 
                    // Assuming SimSwitch has `RailmlSwitch`. We need to map it to SimTrack.
                    // This is hard without back-reference.
                    
                    // Alternative: Iterate Track's switches.
                    // (Assuming we added helper to SimTrack?)
                    // CurrentTrack.RailmlTrack.TrackTopology.Connections.Switch...
                }
            }

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
