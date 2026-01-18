using System;
using System.Collections.Generic;
using System.Linq;
using Railml.Sim.Core.Events;
using Railml.Sim.Core.Models;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core
{
    public class SimulationManager : SimulationContext
    {
        public EventQueue EventQueue { get; } = new EventQueue();
        public SimulationSettings Settings { get; }
        private Models.Railml _model;
        private Infrastructure _infrastructure => _model.Infrastructure;

        public Dictionary<string, SimTrack> Tracks { get; } = new Dictionary<string, SimTrack>();
        public Dictionary<string, SimSwitch> Switches { get; } = new Dictionary<string, SimSwitch>();
        public Dictionary<string, SimSignal> Signals { get; } = new Dictionary<string, SimSignal>();
        public List<Train> Trains { get; } = new List<Train>();

        public double CurrentTime { get; private set; } = 0.0;
        public bool IsRunning { get; private set; } = false;

        public event Action<Train> OnTrainAdded;
        public event Action<Train> OnTrainRemoved;
        public event Action OnSimulationUpdated;

        public SimulationManager(Models.Railml model, SimulationSettings settings)
        {
            _model = model;
            Settings = settings;
            InitializeWorld();
        }

        private void InitializeWorld()
        {
            // Build SimObjects
            foreach (var track in _infrastructure.Tracks.TrackList)
            {
                Tracks[track.Id] = new SimTrack(track);
            }

            foreach (var track in _infrastructure.Tracks.TrackList)
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

            // Parse Visualization
            // Logic: Iterate InfrastructureVisualizations -> LineVis -> TrackVis
            // Find Track by Ref -> Get SimTrack
            // Iterate TrackElementVis -> Match Ref to TrackBegin/TrackEnd Id -> Set Point
            if (_model.InfrastructureVisualizations?.VisualizationList != null)
            {
                foreach (var vis in _model.InfrastructureVisualizations.VisualizationList)
                {
                    foreach (var lineVis in vis.LineVisList)
                    {
                        foreach (var trackVis in lineVis.TrackVisList)
                        {
                            if (Tracks.TryGetValue(trackVis.Ref, out var simTrack))
                            {
                                foreach (var elementVis in trackVis.TrackElementVisList)
                                {
                                    if (elementVis.Ref == simTrack.RailmlTrack.TrackTopology.TrackBegin.Id)
                                    {
                                        simTrack.StartScreenPos = new Point(elementVis.Position.X, elementVis.Position.Y);
                                    }
                                    else if (elementVis.Ref == simTrack.RailmlTrack.TrackTopology.TrackEnd.Id)
                                    {
                                        simTrack.EndScreenPos = new Point(elementVis.Position.X, elementVis.Position.Y);
                                    }
                                }
                            }
                        }
                    }
                    
                    // ObjectVis for Signals/Switches?
                    // sim.railml has <objectVis ref="sw1">...
                    foreach(var objVis in vis.ObjectVisList)
                    {
                         // Check Switches
                         if (Switches.TryGetValue(objVis.Ref, out var simSwitch))
                         {
                             // simSwitch.ScreenPos ?
                             // We probably need to store this in SimSwitch too if we want to render it precisely.
                         }
                         // Check Signals ?
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
