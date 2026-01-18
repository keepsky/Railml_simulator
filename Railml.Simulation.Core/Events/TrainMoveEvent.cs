using System;
using Railml.Simulation.Core.SimObjects;

namespace Railml.Simulation.Core.Events
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

            // 3. Signal Logic
            // Check signals ahead. 
            // If Signal within settings.SignalRecognitionDistance and Aspect is Red.
            // Target Speed = 0 at Signal.Pos
            // Decel = v^2 / 2d
            
            // TODO: Signal Search
            
            // 4. Update Speed
            // Default acceleration/deceleration if not braking for signal
            if (!endOfTrack && _train.Speed < _train.MaxSpeed)
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
                context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + dt, _train));
            }
        }
    }
}
