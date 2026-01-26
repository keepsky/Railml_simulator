using System;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;

namespace Railml.Sim.Core.Events
{
    public class TrainSpawnEvent : DESEvent
    {
        public SimObjects.SimTrack TargetTrack { get; }
        public TrainDirection Direction { get; }

        public TrainSpawnEvent(double time, SimObjects.SimTrack track, TrainDirection direction) : base(time) 
        { 
            TargetTrack = track;
            Direction = direction;
        }

        public override void Execute(SimulationContext context)
        {
            var manager = context as SimulationManager;
            if (manager == null || TargetTrack == null) return;

            // Check if track is occupied
            if (TargetTrack.OccupyingTrains.Count > 0)
            {
                System.Console.WriteLine($"[DEBUG] TrainSpawnEvent skipped: Track {TargetTrack.RailmlTrack.Id} is occupied.");
            }
            else
            {
                // Create a new train with direction-based ID
                var trainId = manager.GetNextTrainId(Direction);
                var train = new Railml.Sim.Core.SimObjects.Train(trainId, context.Settings);

                // Set Position based on direction
                if (Direction == TrainDirection.Up)
                {
                    // Spawn at Beginning (Pos=0)
                    train.CurrentTrack = TargetTrack;
                    train.PositionOnTrack = 0;
                    train.MoveDirection = TrainDirection.Up; 
                }
                else
                {
                    // Spawn at End (Pos=Length)
                    train.CurrentTrack = TargetTrack;
                    train.PositionOnTrack = TargetTrack.Length;
                    train.MoveDirection = TrainDirection.Down; 
                }
                
                manager.AddTrain(train);
                TargetTrack.OccupyingTrains.Add(train);
                context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + context.Settings.MovementUpdateInterval, train));
            }

            // Schedule next spawn for THIS specific point regardless of whether this one succeeded
            var rand = new Random(); // Consider sharing this in SimulationManager/Context later
            double nextInterval = -context.Settings.MeanInterArrivalTime * Math.Log(rand.NextDouble());
            context.EventQueue.Enqueue(new TrainSpawnEvent(context.CurrentTime + nextInterval, TargetTrack, Direction));
        }
    }
}
