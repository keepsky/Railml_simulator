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
        
        private int _nextUpId = 1;
        private int _nextDownId = 2;
        private Random _spawnRandom = new Random();
        // 1.2.1 Random Distribution for Train Generation
        public double MeanInterArrivalTime { get; set; } = 30.0; // Seconds (Exponential Distribution)

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
            // Initialize Subsystems
            Interlocking = new InterlockingSystem(this);

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
                
                if (FindNextTrack(currentTrack, checkDir, out var prevTrack, out var entryPos, out var entryDir, out var crossedSwitch))
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

        public bool FindNextTrack(SimTrack currentTrack, TrainDirection currentDir, out SimTrack? nextTrack, out double nextPos, out TrainDirection nextDir, out SimSwitch? crossedSwitch)
        {
            nextTrack = null;
            nextPos = 0;
            nextDir = TrainDirection.Up;
            crossedSwitch = null;

            // 1. Identify Exit Node Connection
            Connection exitConn = null;
            var topology = currentTrack.RailmlTrack.TrackTopology;

            // Check for Switch at Exit (Trailing Switch from Current Track)
            double exitPos = (currentDir == TrainDirection.Up) ? currentTrack.Length : 0;
            var swAtExit = Switches.Values.FirstOrDefault(s => s.ParentTrack == currentTrack && Math.Abs(s.RailmlSwitch.Pos - exitPos) < 0.001);

            if (swAtExit != null)
            {
                crossedSwitch = swAtExit;
                
                // Exit Switch Routing Logic
                string orientation = swAtExit.GetOrientation(); 
                bool isUp = (currentDir == TrainDirection.Up);

                // Logic Matrix
                bool isSplit = false;
                if (isUp) isSplit = (orientation == "outgoing");
                else isSplit = (orientation == "incoming");

                if (isSplit)
                {
                    // If Split, we check State to see if we Diverge
                    string continueCourse = swAtExit.RailmlSwitch.TrackContinueCourse?.ToLower() ?? "straight";
                    string targetCourse = continueCourse;

                    if (swAtExit.State == SimSwitch.SwitchState.Reverse)
                    {
                        var divConn = swAtExit.RailmlSwitch.ConnectionList?.FirstOrDefault(c => (c.Course?.ToLower() ?? "") != continueCourse);
                        if (divConn != null) targetCourse = divConn.Course?.ToLower() ?? "";
                    }

                    if (targetCourse != continueCourse)
                    {
                        // We are taking the branch connection defined in the Switch
                        var branchedConn = swAtExit.RailmlSwitch.ConnectionList?.FirstOrDefault(c => (c.Course?.ToLower() ?? "") == targetCourse);
                        if (branchedConn != null)
                        {
                            exitConn = branchedConn;
                        }
                    }
                    // If Normal, targetCourse == continueCourse. exitConn remains null here.
                    // Fallback to standard Topology connection below will handle Main Path.
                }
                else
                {
                    // Merge Logic at Exit (?) - Rare/Invalid?
                    // For now, if Merge, we assume we just follow the track end.
                    // (User Rule 2.2 might apply here if logic inverts, but mostly Merge checks are on Entry).
                }
            }
            
            if (exitConn == null)
            {
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
            }

            if (exitConn == null) return false;

            // 2. Resolve Ref
            string targetRef = exitConn.Ref;
            if (string.IsNullOrEmpty(targetRef)) return false;

            if (!ConnectionMap.TryGetValue(targetRef, out var targetInfo)) return false;

            // 3. Handle Target
            if (targetInfo.Parent is SimTrack targetSimTrack)
            {
                double entryPosOnTarget = (targetInfo.NodeId == targetSimTrack.RailmlTrack.TrackTopology.TrackBegin.Id) ? 0 : targetSimTrack.Length;
                
                // Check if target track starts (or ends) with a switch at the entry point
                var swAtEntry = Switches.Values.FirstOrDefault(s => s.ParentTrack == targetSimTrack && Math.Abs(s.RailmlSwitch.Pos - entryPosOnTarget) < 0.001);
                
                if (swAtEntry != null)
                {
                    crossedSwitch = swAtEntry;
                    
                    // Complex Routing Logic
                    string orientation = swAtEntry.GetOrientation(); // "incoming" or "outgoing"
                    bool isUp = (currentDir == TrainDirection.Up);

                    // Determine Logical Operation: Split vs Merge
                    // Up + Outgoing => Split
                    // Up + Incoming => Merge
                    // Down + Incoming => Split
                    // Down + Outgoing => Merge
                    bool isSplit = false;
                    if (isUp) isSplit = (orientation == "outgoing");
                    else isSplit = (orientation == "incoming");

                    if (isSplit)
                    {
                        // SPLIT LOGIC: Select Path based on State
                        // Normal -> ContinueCourse
                        // Reverse -> Diverging Path
                        string continueCourse = swAtEntry.RailmlSwitch.TrackContinueCourse?.ToLower() ?? "straight";
                        string targetCourse = continueCourse;

                        if (swAtEntry.State == SimSwitch.SwitchState.Reverse)
                        {
                            var divConn = swAtEntry.RailmlSwitch.ConnectionList?.FirstOrDefault(c => (c.Course?.ToLower() ?? "") != continueCourse);
                            if (divConn != null) targetCourse = divConn.Course?.ToLower() ?? "";
                        }

                        if (targetCourse != continueCourse)
                        {
                            var branchedConn = swAtEntry.RailmlSwitch.ConnectionList?.FirstOrDefault(c => (c.Course?.ToLower() ?? "") == targetCourse);
                            if (branchedConn != null && ConnectionMap.TryGetValue(branchedConn.Ref, out var branchTarget) && branchTarget.Parent is SimTrack branchTrack)
                            {
                                nextTrack = branchTrack;
                                nextPos = (branchTarget.NodeId == branchTrack.RailmlTrack.TrackTopology.TrackBegin.Id) ? 0 : branchTrack.Length;
                                nextDir = (nextPos == 0) ? TrainDirection.Up : TrainDirection.Down;
                                return true;
                            }
                        }
                    }
                    else
                    {
                        // MERGE LOGIC: Validate Entry
                        // We need to know if we entered via Main (Trunk) or Branch (Connection)
                        // targetInfo.NodeId tells us strictly about the connection node type.
                        // But wait, "targetInfo" comes from "exitConn.Ref".
                        // If we are merging, we are entering the switch track.
                        // If we entered via TrackNode (Begin/End), it's Main Entry?
                        // If we entered via Switch Connection, it's Branch Entry?
                        
                        // Wait, if 1.2.1: "Connected by trackBegin". Structure: Track A -> Track B(Switch).
                        // Track A's End connects to Track B's Begin.
                        // This means we used effective connection Ref = "TrackB_Begin".
                        // So targetInfo.NodeId would be "trackBegin" of Track B.
                        
                        // If 1.2.2: "Connected by <switch><connection>".
                        // Track C's End connects to Switch Connection.
                        // This means we used effective connection Ref = "SwitchConnID".
                        // So targetInfo.NodeId would be "switch".

                        bool isMainEntry = (targetInfo.NodeId != "switch");
                        
                        // Validation Rule:
                        // Main Entry && Reverse => Derail
                        // Branch Entry && Normal => Derail
                        
                        bool derail = false;
                        if (isMainEntry && swAtEntry.State == SimSwitch.SwitchState.Reverse) derail = true;
                        if (!isMainEntry && swAtEntry.State == SimSwitch.SwitchState.Normal) derail = true;

                        if (derail)
                        {
                            ReportAccident($"Derailment at Switch {swAtEntry.RailmlSwitch.Id}: Invalid Trailing Move (State: {swAtEntry.State}, Entry: {(isMainEntry ? "Main" : "Branch")})");
                            return false; // Stop the train
                        }
                        
                        // Logical Move: Proceed to Trunk (which is simply continuing on this track)
                        // Since we are NOT splitting, we just land on 'targetSimTrack'.
                        // However, we must ensure we don't accidentally jump to a branch if we were main?
                        // No, 'targetSimTrack' IS the trunk track. We are just entering it.
                    }
                }

                nextTrack = targetSimTrack;
                nextPos = (targetInfo.NodeId == targetSimTrack.RailmlTrack.TrackTopology.TrackBegin.Id) ? 0 : targetSimTrack.Length;
                nextDir = (nextPos == 0) ? TrainDirection.Up : TrainDirection.Down;
                return true;
            }
            else if (targetInfo.Parent is SimSwitch simSwitch)
            {
                crossedSwitch = simSwitch;
                var parentTrack = simSwitch.ParentTrack;
                if (parentTrack != null)
                {
                    // Apply Complex Routing Logic for Direct Switch Connection Entry (Branch Entry)
                    string orientation = simSwitch.GetOrientation();
                    bool isUp = (currentDir == TrainDirection.Up);

                    // Determine Logical Operation
                    bool isSplit = false;
                    if (isUp) isSplit = (orientation == "outgoing");
                    else isSplit = (orientation == "incoming");

                    if (isSplit)
                    {
                        // Entering a switch via a connection in Split mode? 
                        // This implies we are BACK-TRACKING into a leg? 
                        // Or we entered the Trunk via a "Switch Connection"? (Uncommon topology but possible).
                        // If we are Splitting, we need to choose a path.
                        // But we are already AT a specific connection (targetInfo.Connection).
                        // So the path is determined by the connection we just used?
                        // Actually, if we entered via a specific connection, we are AT that leg.
                        // If operation is Split, we are traversing Trunk -> Legs.
                        // If we target a Leg connection, we are arriving AT the leg. 
                        // This sounds like we are moving Leg -> Trunk (Merge).
                        // Let's stick to the User's Definitions.
                        
                        // If Orientation says Split, but we entered via Connection (Leg).
                        // This is weird. "Outgoing" means Trunk -> Legs.
                        // If we Target a Switch Connection, we are Arriving at the Switch from a branch.
                        // So we are the "In" of "Outgoing"? No.
                        // Use strict Matrix.
                    }
                    else
                    {
                         // MERGE Logic
                         // We entered via Switch Connection => Branch Entry.
                         bool isMainEntry = false; // Targeted a switch connection directly

                         bool derail = false;
                         // Branch Entry Checks
                         if (!isMainEntry && simSwitch.State == SimSwitch.SwitchState.Normal) derail = true;

                         if (derail)
                         {
                             ReportAccident($"Derailment at Switch {simSwitch.RailmlSwitch.Id}: Invalid Trailing Move (State: {simSwitch.State}, Entry: Branch)");
                             return false;
                         }
                    }

                    // Resolve Tip Track (Trunk) - restoring Pass-Through logic
                    // We need to find the connection at the Trunk end of the switch
                    double swPos = simSwitch.RailmlSwitch.Pos;
                    var parentConn = (swPos < 0.001) 
                        ? parentTrack.RailmlTrack.TrackTopology.TrackBegin?.ConnectionList?.FirstOrDefault()
                        : parentTrack.RailmlTrack.TrackTopology.TrackEnd?.ConnectionList?.FirstOrDefault();

                    if (parentConn != null && ConnectionMap.TryGetValue(parentConn.Ref, out var tipInfo) && tipInfo.Parent is SimTrack tipTrack)
                    {
                        nextTrack = tipTrack;
                        double entryPosOnTip = (tipInfo.NodeId == tipTrack.RailmlTrack.TrackTopology.TrackBegin.Id) ? 0 : tipTrack.Length;
                        nextPos = entryPosOnTip;
                        nextDir = (nextPos == 0) ? TrainDirection.Up : TrainDirection.Down;
                        return true;
                    }
                    else
                    {
                         // Fallback: Just stay on ParentTrack
                         nextTrack = parentTrack;
                         nextPos = simSwitch.RailmlSwitch.Pos;
                         nextDir = TrainDirection.Up; 
                         return true;
                    }
                }
            }
            return false;
        }


        public void Start()
        {
            IsRunning = true;
            if (Interlocking != null) Interlocking.Start();
            
            // Search all tracks for OpenEnd boundaries to start spawning
            foreach (var kvp in Tracks)
            {
                var track = kvp.Value;
                var topo = track.RailmlTrack.TrackTopology;

                if (topo.TrackBegin?.OpenEnd != null)
                {
                    // Starts spawning at Begin (Up direction)
                    double nextInterval = -Settings.MeanInterArrivalTime * Math.Log(_spawnRandom.NextDouble());
                    System.Console.WriteLine($"[DEBUG] Scheduling initial UP train at {CurrentTime + nextInterval} (Interval: {nextInterval})");
                    EventQueue.Enqueue(new TrainSpawnEvent(CurrentTime + nextInterval, track, TrainDirection.Up));
                }

                if (topo.TrackEnd?.OpenEnd != null)
                {
                    // Starts spawning at End (Down direction)
                    double nextInterval = -Settings.MeanInterArrivalTime * Math.Log(_spawnRandom.NextDouble());
                    System.Console.WriteLine($"[DEBUG] Scheduling initial DOWN train at {CurrentTime + nextInterval} (Interval: {nextInterval})");
                    EventQueue.Enqueue(new TrainSpawnEvent(CurrentTime + nextInterval, track, TrainDirection.Down));
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

        public string GetNextTrainId(TrainDirection direction)
        {
            if (direction == TrainDirection.Up)
            {
                int id = _nextUpId;
                _nextUpId += 2;
                return id.ToString();
            }
            else
            {
                int id = _nextDownId;
                _nextDownId += 2;
                return id.ToString();
            }
        }


    }
}
