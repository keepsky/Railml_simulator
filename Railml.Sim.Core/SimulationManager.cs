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
        private Dictionary<string, List<SimTrack>> _tracksByName = new Dictionary<string, List<SimTrack>>();

        public InterlockingSystem Interlocking { get; private set; } = null!;
        public SafetyMonitor Safety { get; private set; } = null!;

        public double CurrentTime { get; private set; } = 0.0;
        public bool IsRunning { get; private set; } = false;

        public event Action<Train> OnTrainAdded;
        public event Action<Train> OnTrainRemoved;
        public event Action OnSimulationUpdated;
        public event Action<string> OnAccident;

        public SimulationManager(Models.Railml model, SimulationSettings settings)
        {
            _model = model;
            Settings = settings;
            InitializeWorld();
            System.Console.WriteLine($"[DEBUG] Manager Initialized. Interlocking is {(Interlocking == null ? "NULL" : "SET")}");
        }

        private void InitializeWorld()
        {
            // Build SimObjects
            foreach (var track in _infrastructure.Tracks.TrackList)
            {
                var simTrack = new SimTrack(track);
                Tracks[track.Id] = simTrack;

                // Populate TracksByName for logical occupancy
                if (!string.IsNullOrEmpty(track.Name))
                {
                    if (!_tracksByName.ContainsKey(track.Name))
                    {
                        _tracksByName[track.Name] = new List<SimTrack>();
                    }
                    _tracksByName[track.Name].Add(simTrack);
                }
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
                        // Strict Topology Rule: Switch must be at pos="0"
                        if (System.Math.Abs(sw.Pos) > 0.001)
                        {
                             throw new TopologyException($"Switch {sw.Id} is located at pos={sw.Pos}. According to domain rules, switches must be at pos=0 (split track model).");
                        }

                        if (!Switches.ContainsKey(sw.Id))
                        {
                            var simSwitch = new SimSwitch(sw);
                            if (Tracks.TryGetValue(track.Id, out var sTrack))
                            {
                                simSwitch.ScreenPos = sTrack.StartScreenPos; // Default to Track Start
                                simSwitch.ParentTrack = sTrack;
                            }
                            Switches[sw.Id] = simSwitch;
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
                             simSwitch.ScreenPos = new Point(objVis.Position.X, objVis.Position.Y);
                         }
                         // Check Signals ?
                    }
                }
            }

            Interlocking = new InterlockingSystem(this);
            Safety = new SafetyMonitor(this);
            BuildConnectionMap();
        }

        // Connection ID -> (Parent Object, Connection Element, Node Type/Info)
        // Parent can be SimTrack or SimSwitch
        // We use a helper class or tuple
        public class ConnectionInfo
        {
            public object Parent { get; set; }
            public Connection Connection { get; set; }
            public string NodeId { get; set; } // For Tracks, "tb1" etc
        }

        public Dictionary<string, ConnectionInfo> ConnectionMap { get; } = new Dictionary<string, ConnectionInfo>();

        private void BuildConnectionMap()
        {
            foreach (var track in Tracks.Values)
            {
                var t = track.RailmlTrack;
                // Track Begin
                if (t.TrackTopology?.TrackBegin?.ConnectionList != null)
                {
                    foreach (var c in t.TrackTopology.TrackBegin.ConnectionList)
                        ConnectionMap[c.Id] = new ConnectionInfo { Parent = track, Connection = c, NodeId = t.TrackTopology.TrackBegin.Id };
                }
                // Track End
                if (t.TrackTopology?.TrackEnd?.ConnectionList != null)
                {
                    foreach (var c in t.TrackTopology.TrackEnd.ConnectionList)
                        ConnectionMap[c.Id] = new ConnectionInfo { Parent = track, Connection = c, NodeId = t.TrackTopology.TrackEnd.Id };
                }
            }

            foreach (var sw in Switches.Values)
            {
                if (sw.RailmlSwitch?.ConnectionList != null)
                {
                    foreach (var c in sw.RailmlSwitch.ConnectionList)
                        ConnectionMap[c.Id] = new ConnectionInfo { Parent = sw, Connection = c, NodeId = "switch" };
                }
            }
        }

        public void RemoveTrain(Train train)
        {
            if (Trains.Contains(train))
            {
                Trains.Remove(train);
                // Clear occupancy
                foreach (var t in train.OccupiedTracks)
                {
                    t.OccupyingTrains.Remove(train);
                }
                train.OccupiedTracks.Clear();
                
                OnTrainRemoved?.Invoke(train);
            }
        }

        public void UpdateTrainOccupancy(Train train)
        {
            if (train == null || train.CurrentTrack == null) return;

            var newOccupiedTracks = new List<SimTrack>();
            var currentTrack = train.CurrentTrack;
            var currentPos = train.PositionOnTrack;
            var currentDir = train.MoveDirection;
            double remainingLen = train.Length;

            int safety = 0;
            while (remainingLen > 0 && currentTrack != null && safety++ < 20)
            {
                // Check Intersection with Track Limits
                // Track Range [0, currentTrack.Length]
                double segmentStart, segmentEnd;
                
                if (currentDir == TrainDirection.Up)
                {
                    // Train segment on this 'line': [Head - remainingLen, Head]
                    // (Actually we start at currentPos and go back 'usedLen')
                    // But effectively the segment covering this track is defined by [currentPos - Min(Pos, Rem), currentPos]
                    // Wait, simplistic view:
                    // Train spans [currentPos - remainingLen, currentPos] locally?
                    // No, that assumes we stay on this track.
                    
                    // Simple check: Does the train cover any part of [0, Length]?
                    // Current Head is at currentPos.
                    // We consume 'usedLen' backwards.
                    // So we cover [currentPos - usedLen, currentPos].
                    
                    double usedLen = System.Math.Min(currentPos, remainingLen);
                    // Intersection of [currentPos - usedLen, currentPos] and [0, currentTrack.Length]
                    double sStart = currentPos - usedLen;
                    double sEnd = currentPos;
                    
                    double iStart = System.Math.Max(0, sStart);
                    double iEnd = System.Math.Min(currentTrack.Length, sEnd);
                    
                    if (iEnd > iStart + 0.001)
                    {
                        newOccupiedTracks.Add(currentTrack);
                    }
                    
                    remainingLen -= usedLen;
                }
                else
                {
                    // Down: Head at currentPos. Body extends to currentPos + remainingLen.
                    // usedLen is derived from Min(Length - Pos, Rem).
                    // We cover [currentPos, currentPos + usedLen].
                    
                    double usedLen = System.Math.Min(currentTrack.Length - currentPos, remainingLen);
                    // Intersection of [currentPos, currentPos + usedLen] with [0, Length]
                    double sStart = currentPos;
                    double sEnd = currentPos + usedLen;
                    
                    double iStart = System.Math.Max(0, sStart);
                    double iEnd = System.Math.Min(currentTrack.Length, sEnd);
                    
                    if (iEnd > iStart + 0.001)
                    {
                        newOccupiedTracks.Add(currentTrack);
                    }

                    remainingLen -= usedLen;
                }

                if (remainingLen <= 0.001) break;

                // Find previous track
                var checkDir = (currentDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                
                if (FindNextTrack(currentTrack, checkDir, out var prevTrack, out var entryPos, out var entryDir))
                {
                    currentTrack = prevTrack;
                    currentPos = entryPos;
                    currentDir = (entryDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                }
                else
                {
                    break; 
                }
            }

            // Identify enters/exited tracks
            if (Interlocking != null)
            {
                // Newly Occupied (Enters)
                foreach (var nt in newOccupiedTracks)
                {
                    if (!train.OccupiedTracks.Contains(nt))
                    {
                        Interlocking.ReportTrainEnterTrack(train, nt);
                    }
                }

                // No longer Occupied (Exits)
                foreach (var oldTrack in train.OccupiedTracks)
                {
                    if (!newOccupiedTracks.Contains(oldTrack))
                    {
                        Interlocking.ReportTrainExitTrack(train, oldTrack);
                    }
                }
            }

            // Expand to Logical Occupancy (Siblings by Name)
            // If a track is occupied, all tracks with the same Name are also occupied.
            var logicalOccupied = new System.Collections.Generic.HashSet<SimTrack>(newOccupiedTracks);
            foreach (var track in newOccupiedTracks)
            {
                var name = track.RailmlTrack.Name;
                if (!string.IsNullOrEmpty(name) && _tracksByName.TryGetValue(name, out var siblings))
                {
                    foreach (var sibling in siblings)
                    {
                         logicalOccupied.Add(sibling);
                    }
                }
            }
            
            // Use logical list for syncing
            var finalOccupiedList = new List<SimTrack>(logicalOccupied);

            // Sync with Old Occupancy
            // 1. Remove from tracks no longer occupied
            foreach (var t in train.OccupiedTracks)
            {
                if (!logicalOccupied.Contains(t))
                {
                    t.OccupyingTrains.Remove(train);
                }
            }

            // 2. Add to newly occupied tracks
            foreach (var t in finalOccupiedList)
            {
                if (!t.OccupyingTrains.Contains(train))
                {
                    t.OccupyingTrains.Add(train);
                }
            }

            // 3. Update Train's list
            train.OccupiedTracks = finalOccupiedList;
        }

        public bool FindNextTrack(SimTrack currentTrack, TrainDirection currentDir, out SimTrack nextTrack, out double nextPos, out TrainDirection nextDir)
        {
            nextTrack = null;
            nextPos = 0;
            nextDir = TrainDirection.Up;

            // 1. Identify Exit Node Connection
            Connection exitConn = null;
            var topology = currentTrack.RailmlTrack.TrackTopology;
            
            if (currentDir == TrainDirection.Up)
            {
                // Exiting at End
                exitConn = topology.TrackEnd?.ConnectionList?.FirstOrDefault();
            }
            else
            {
                // Exiting at Begin
                exitConn = topology.TrackBegin?.ConnectionList?.FirstOrDefault();
            }

            if (exitConn == null) return false;

            // 2. Resolve Ref
            string targetRef = exitConn.Ref;
            if (string.IsNullOrEmpty(targetRef)) return false;

            if (!ConnectionMap.TryGetValue(targetRef, out var targetInfo)) return false;

            // 3. Handle Target
            if (targetInfo.Parent is SimTrack targetSimTrack)
            {
                nextTrack = targetSimTrack;
                // Check if we entered at Begin or End
                // If NodeId == TrackBegin ID -> We are at Begin. Proceed UP. Pos = 0.
                string beginId = targetSimTrack.RailmlTrack.TrackTopology.TrackBegin.Id;
                
                // Note: The ConnectionInfo stores the node ID where the connection lives.
                if (targetInfo.NodeId == beginId)
                {
                    nextPos = 0;
                    nextDir = TrainDirection.Up;
                }
                else
                {
                    nextPos = targetSimTrack.Length;
                    nextDir = TrainDirection.Down;
                }
                return true;
            }
            else if (targetInfo.Parent is SimSwitch simSwitch)
            {
                // 4. Handle Switch Traversal
                // targetInfo.Connection is the Entry Port on the switch.
                // We need to find the Exit Port based on Switch Position.
                
                // Simplified Switch Logic:
                // If Entry is "Tip" (Orientation="incoming"? Depends on RailML usage).
                // RailML 2.5: <switch ...> <connection id="..." orientation="incoming/outgoing" course="left/right/straight" />
                // Logic:
                // 1. Determine current switch state: simSwitch.CurrentPosition ("left", "right", "straight")
                // 2. Find connection that matches this course.
                // 3. BUT, we must know if we are entering from the Tip or the Legs.
                
                // incoming connection to switch -> Ref was to a switch connection.
                var entryConn = targetInfo.Connection;
                
                // If entryConn course is "straight" (e.g. Tip), output is determined by Position.
                // If entryConn course is "left" (Leg), output is Tip? (Merge)
                
                // Let's assume:
                // If Switch is set to X. We can travel A->B if path A-B matches X.
                
                // Find "Other" connection.
                Connection exitSwitchConn = null;

                // Case 1: Entering from Tip (Commonly orientation="incoming" or course="straight" in some conventions, but RailML varies).
                // Let's look at the example `sw1`.
                // <switch id="sw1" ...>
                //   <connection id="c1-cb6" ref="cb6" orientation="outgoing" course="left" />
                // </switch>
                // It has ONLY ONE connection listed?? RailML usually lists all 3.
                // Wait, `sw1` in `sim.railml` line 66 only has ONE connection...
                // "c1-cb6" ref "cb6" (outgoing, left).
                // Where is the incoming?
                // `cb5` ref `ce4`. `ce5` ref `cb3`. 
                // Wait. `tr5` End `te5` connects to `ce5` (ref `cb3` - tr3 begin).
                // `tr5` Connections block has `sw1`.
                // This implies the switch is *embedded* in `tr5`?
                // RailML 2.5: <switch> is usually a node connecting tracks.
                // BUT in `sim.railml` line 64: `<switch ...>` is inside `<connections>` of `trackTopology` of `tr5`.
                // And it connects to `cb6`.
                // This means `tr5` itself is the switch track?
                // `track` `tr5`. End connects to `cb3` (tr3).
                // But it also has a Switch defining a branch to `cb6`.
                // This is an "Internal Switch" or "Branching Track".

                // In this specific model structure:
                // Switch is Inside Track Topology.
                // It seems to define the *branching* off this track.
                // The main path is `te5` -> `ce5`.
                // The switch path is `sw1` -> `c1-cb6` -> `cb6` (tr6 Begin).
                
                // Complex Logic for Embedded Switch:
                // If train is at end of `tr5`.
                // It checks `sw1`.
                // If `sw1` is "left", it goes to `cb6` (`tr6`).
                // If `sw1` is "straight", it goes to `ce5` (`tr3`).
                
                // We need to handle this specific structure.
                // Identify if current track HAS a switch at the end?
                
                // My `FindNextTrack` logic started by looking at `TrackEnd.Connection`.
                // `tr5` TrackEnd `te5` has `connection id="ce5"`.
                // But the `connections` element has `switch`.
                // Standard RailML: Track connects to Switch Node. Switch Node connects to 2 Tracks.
                // This file: Track contains Switch *definition*.
                
                // Let's assume the standard case first (Track -> Connection -> Track).
                // AND handle this file's case:
                // If Track has `Connections/Switch`, we check that FIRST before `TrackEnd`.
                
                // Check for embedded switch at the exit end.
                var sw = currentTrack.RailmlTrack.TrackTopology.Connections?.Switches?.FirstOrDefault();
                if (sw != null)
                {
                    // Check if switch applies to this end?
                    // Usually `pos` indicates where. `pos="0"`? 
                    // `tr5`: `pos="0"` for switch. `tr5` range 1200-1500 (len 300).
                    // If pos=0, it's at the BEGINNING?
                    // `tr5` mainDir="down".
                    // If Moving Down (towards End), we are at Pos=Length (30).
                    // Switch is at 0. So we passed it?
                    // Wait. `tr5` start 1200. end 1500. `switch pos="0"`.
                    // Pos is usually valid from 0..Length.
                    
                    // Let's look at `tr5` again.
                    // `tb5` (0) -> `ce4`.
                    // `te5` (30) -> `ce5`.
                    // Switch at 0.
                    // This suggests the branch is at the *Beginning*.
                    // If coming from `tr4` (Up/Down?), we enter `tr5`.
                    // We can go to `Tr5 main` OR `Tr6`?
                    
                    // Let's re-read `tr5` connection.
                    // `tr4` (1000-1200). `te4` (20) connects to `cb5`.
                    // We enter `tr5` at `tb5` (0).
                    // AT 0, there is `sw1`.
                    // If `sw1` is left, we go to `c1-cb6` -> `cb6` -> `tr6`.
                    // If `sw1` is straight, we go into `tr5` body?
                    
                    // This means `FindNextTrack` logic:
                    // If we are AT the entry of a track that has a switch at the entry...
                    // But we are usually calling `FindNextTrack` when we reach the *End* of the current track.
                    
                    // User Problem: "Train at end... not proceeding".
                    // Train is at End of a track.
                    // It needs to go to Neighbor.
                    
                    // If the user is testing the switch case:
                    // Train on `tr1` -> `tr2`. (Straight).
                    // Train on `tr2` -> `ce2` (1000) -> `cb4`. `tr4` (1000).
                    // `tr4` (Down). Start 1000. End 1200.
                    // Train enters `tr4` at 1000 (`tb4`). `tr4` is Down, so `tb4` is...
                    // `tr4` Topology: Begin 1000. End 1200.
                    // `Code="plain"`.
                    // If `MainDir="down"`.
                    // Movement is usually against metric? Or just property?
                    // My `TrainMoveEvent`: Up = Pos++, Down = Pos--.
                    // If I enter at 1000 (Begin).
                    // If I want to move to 1200. I must move UP (Pos++).
                    // Does `MainDir="down"` restrict movement direction? Or just labeling?
                    // Usually "down" means nominal direction is decreasing absPos?
                    // `tr4`: Begin 1000. End 1200.
                    // AbsPos increases.
                    // If I am at 1000. I move to 1200. That is +200.
                    // My logic: Update Position += Distance.
                    // So I am moving UP.
                    
                    // User says: "Train stops at end".
                    // Case 1: Track -> Track.
                    // `tr2` (Up) -> `tr4` (Down?).
                    // `tr2` End: 1000. `cb4` matches `tr4` Begin (1000).
                    // Train leaves `tr2` (Pos 1000).
                    // Enters `tr4` (Pos 0 relative? RailML `pos` usually 0..Len).
                    // `tr4` Begin `pos=0`.
                    // `tr4` End `pos=20`.
                    // So `tr4` is 20m long.
                    // `tr2` ends at 1000.
                    // `tr4` begins at 1000.
                    // Transition: `tr2` End -> `tr4` Begin.
                    // New Pos = 0.
                    // New Dir = Up (Since we move 0->20).
                    
                    // Logic must use `ConnectionMap`.
                    // `tr2` End has `ce2`.
                    // `ce2` ref `cb4`.
                    // `cb4` is in `tr4` Begin.
                    // Map[`cb4`] -> `(tr4, cb4, NodeId=tb4)`.
                    // `FindNextTrack` returns `tr4`.
                    
                    // So, implementation plan covers this.
                    // IMPORTANT: The `targetRef` might be THE connection ID on the other side.
                    // `ce2` ref `cb4`.
                    // My Map stores `cb4` -> `tr4` info.
                    // So `ConnectionMap.TryGetValue(targetRef)` works.
                    
                }
            }

            return false;
        }

        public void Start()
        {
            IsRunning = true;
            
            // Search all tracks for OpenEnd boundaries to start spawning
            foreach (var kvp in Tracks)
            {
                var track = kvp.Value;
                var topo = track.RailmlTrack.TrackTopology;

                if (topo.TrackBegin?.OpenEnd != null)
                {
                    // Starts spawning at Begin (Up direction)
                    EventQueue.Enqueue(new TrainSpawnEvent(CurrentTime + 1.0, track, TrainDirection.Up));
                }

                if (topo.TrackEnd?.OpenEnd != null)
                {
                    // Starts spawning at End (Down direction)
                    EventQueue.Enqueue(new TrainSpawnEvent(CurrentTime + 1.5, track, TrainDirection.Down));
                }
            }

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
                EventQueue.CurrentTime = CurrentTime; // Sync queue time for Enqueues during Execute
                evt.Execute(this);
                Safety.CheckSafety();
            }
            CurrentTime = targetTime;
            EventQueue.CurrentTime = CurrentTime; // Ensure final sync
        }

        public void AddTrain(Train train)
        {
            Trains.Add(train);
            OnTrainAdded?.Invoke(train);
        }

        public void ReportAccident(string message)
        {
            OnAccident?.Invoke(message);
        }


    }
}
