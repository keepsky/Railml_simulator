using System;

namespace Railml.Sim.Core.Events
{
    public class TrainSpawnEvent : DESEvent
    {
        public TrainSpawnEvent(double time) : base(time) { }

        public override void Execute(SimulationContext context)
        {
            // Create a new train
            var trainId = $"T_{context.CurrentTime:F0}";
            var train = new Railml.Sim.Core.SimObjects.Train(trainId, context.Settings);

            // Find start track (Logic to pick a start track? Random or first in list?)
            // User requirement: "trackBegin openEnd point...".
            // We need to find tracks with "openEnd" at trackBegin or trackEnd? 
            // (4.2) "If start at <trackBegin><openEnd>, move direction = same as mainDir..."
            
            // Simple logic: Find first track with OpenEnd.
            var manager = context as SimulationManager;
            if (manager != null)
            {
                // For now, hardcode or randomness.
                // Let's look for any track with OpenEnd in TrackTopology
                foreach(var kvp in manager.Tracks)
                {
                    var track = kvp.Value;
                    if (track.RailmlTrack.TrackTopology.TrackBegin?.OpenEnd != null)
                    {
                        train.CurrentTrack = track;
                        train.PositionOnTrack = 0;
                        train.MoveDirection = track.RailmlTrack.MainDir == "up" ? TrainDirection.Up : TrainDirection.Down; // Simplified
                        // Actually logic (4.2) says: 
                        // if start at Begin OpenEnd -> Dir = MainDir
                        // We will assume 'Up' aligns with MainDir for now.
                        
                        manager.AddTrain(train);
                        track.OccupyingTrains.Add(train);
                        
                        // Schedule first move
                        context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + context.Settings.MovementUpdateInterval, train));
                        break;
                    }
                }
            }

            // Schedule next spawn
            // Exponential distribution: -mean * ln(uniform_random)
            var rand = new Random();
            double nextInterval = -context.Settings.MeanInterArrivalTime * Math.Log(rand.NextDouble());
            context.EventQueue.Enqueue(new TrainSpawnEvent(context.CurrentTime + nextInterval));
        }
    }
}
