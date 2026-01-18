using System;
using System.Collections.Generic;
using System.Linq;
using Railml.Simulation.Core.Events;
using Railml.Simulation.Core.Models;
using Railml.Simulation.Core.SimObjects;

namespace Railml.Simulation.Core
{
    public class SimulationManager : SimulationContext
    {
        public EventQueue EventQueue { get; } = new EventQueue();
        public SimulationSettings Settings { get; }
        public Infrastructure Infrastructure { get; }

        public Dictionary<string, SimTrack> Tracks { get; } = new Dictionary<string, SimTrack>();
        public Dictionary<string, SimSwitch> Switches { get; } = new Dictionary<string, SimSwitch>();
        public Dictionary<string, SimSignal> Signals { get; } = new Dictionary<string, SimSignal>();
        public List<Train> Trains { get; } = new List<Train>();

        public double CurrentTime { get; private set; } = 0.0;
        public bool IsRunning { get; private set; } = false;

        public event Action<Train> OnTrainAdded;
        public event Action<Train> OnTrainRemoved;
        public event Action OnSimulationUpdated;

        public SimulationManager(Infrastructure infrastructure, SimulationSettings settings)
        {
            Infrastructure = infrastructure;
            Settings = settings;
            InitializeWorld();
        }

        private void InitializeWorld()
        {
            // Build SimObjects
            foreach (var track in Infrastructure.Tracks.TrackList)
            {
                Tracks[track.Id] = new SimTrack(track);
            }

            foreach (var track in Infrastructure.Tracks.TrackList)
            {
                // Switches are inside Connections usually, or global?
                // RailML 2.5: <connections><switch>... or <track><trackTopology><connections><switch>
                // In the loaded model, Track -> TrackTopology -> Connections -> Switches
                
                if (track.TrackTopology?.Connections?.Switches != null)
                {
                    foreach (var sw in track.TrackTopology.Connections.Switches)
                    {
                        if (!Switches.ContainsKey(sw.Id))
                        {
                            Switches[sw.Id] = new SimSwitch(sw);
                        }
                    }
                }

                // Signals in OcsElements
                if (track.OcsElements?.Signals?.SignalList != null)
                {
                    foreach (var sig in track.OcsElements.Signals.SignalList)
                    {
                        Signals[sig.Id] = new SimSignal(sig);
                    }
                }
            }
        }

        public void Start()
        {
            IsRunning = true;
            EventQueue.Enqueue(new TrainSpawnEvent(CurrentTime + 1.0));
            ProcessEvents();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Update()
        {
             if (!IsRunning) return;
             ProcessEvents();
             OnSimulationUpdated?.Invoke();
        }

        private void ProcessEvents()
        {
            // Process events up to "Now" + delta? 
            // Or just process one step? 
            // The request says: "DES event ... newest ... loop".
            // Implementation: We should process events as long as their time is <= TargetTime?
            // Or typically DES runs as fast as possible or synchronized to real time.
            // For UI, we probably want to update incrementally.
            
            // Let's assume we are called from a UI loop.
            // We shouldn't block. We should process events until the queue is empty or time travels too far? 
            // Actually, in pure DES, "Time" jumps to the next event. 
            // But if we want smooth animation, we need to sync simulation time with wall clock.
            
            // Allow manual stepping or wall-clock sync.
            // For now, let's just expose a method to "RunUntil(time)".

        }
        
        public void RunUntil(double targetTime)
        {
            while (IsRunning && !EventQueue.IsEmpty && EventQueue.NextEventTime <= targetTime)
            {
                var evt = EventQueue.Dequeue();
                CurrentTime = evt.ExecutionTime;
                evt.Execute(this);
            }
            CurrentTime = targetTime;
        }

        public void AddTrain(Train train)
        {
            Trains.Add(train);
            OnTrainAdded?.Invoke(train);
        }

        public void RemoveTrain(Train train)
        {
            Trains.Remove(train);
            // also remove from SimTrack
            train.CurrentTrack?.OccupyingTrains.Remove(train);
            OnTrainRemoved?.Invoke(train);
        }
    }
}
