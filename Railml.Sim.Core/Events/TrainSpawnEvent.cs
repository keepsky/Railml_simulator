using System;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;

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
                // Find any track with OpenEnd in TrackTopology
                // User requirement: "start at <openEnd>..."
                foreach(var kvp in manager.Tracks)
                {
                    var track = kvp.Value;
                    var topo = track.RailmlTrack.TrackTopology;
                    
                    if (topo.TrackBegin?.OpenEnd != null)
                    {
                        // Spawn at Beginning (Pos=0), Move Up (physically 0->L)
                        train.CurrentTrack = track;
                        train.PositionOnTrack = 0;
                        train.MoveDirection = TrainDirection.Up; 
                        
                        manager.AddTrain(train);
                        track.OccupyingTrains.Add(train);
                        context.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + context.Settings.MovementUpdateInterval, train));
                        break;
                    }
                    else if (topo.TrackEnd?.OpenEnd != null)
                    {
                        // Spawn at End (Pos=Length), Move Down (physically L->0)
                        train.CurrentTrack = track;
                        train.PositionOnTrack = track.Length;
                        train.MoveDirection = TrainDirection.Down; 
                        
                        manager.AddTrain(train);
                        track.OccupyingTrains.Add(train);
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
