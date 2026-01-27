using System;
using System.IO;
using System.Collections.Generic;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;
using Railml.Sim.Core.SimObjects;

class Verify
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting Verification: Switch Derailment Test");
        try 
        {
            // Setup Model: Track tr1 -> Switch sw1
            var model = new Railml.Sim.Core.Models.Railml
            {
                Infrastructure = new Infrastructure
                {
                    Tracks = new Tracks 
                    { 
                        TrackList = new List<Track>
                        {
                            new Track { 
                                Id = "tr1", Length = 100, MainDir = "up", 
                                TrackTopology = new TrackTopology { 
                                    TrackBegin = new TrackNode { Id="tb1", OpenEnd = new OpenEnd() }, // Train enters here
                                    TrackEnd = new TrackNode { Id="te1", ConnectionList = new List<Connection> { new Connection { Ref = "c1" } } } // Connects to switch
                                } 
                            }
                        }
                    },
                    TrackTopology = new TrackTopology { // Global topology container if needed, but here switches are separate usually?
                         // In RailML 2.4, switches are under <connections> inside <trackTopology> usually?
                         // No, in this object model, Infrastructure has Tracks. Switches are likely under Connections?
                         // Let's check Infrastructure definition. 
                         // Assuming Infrastructure has Switches or TrackTopology has Connections?
                    }
                }
            };

            // Wait, we need to add the Switch to the model. 
            // The simulation manager builds switches from... Infrastructure.Tracks? No.
            // Let's check SimulationManager.InitializeWorld.
            // It iterates _infrastructure.Tracks.TrackList.
            // It also iterates Switches? 
            
            // Let's look at SimulationManager.cs again. 
            // It iterates Tracks.TrackList.
            // Switches are found where? 
            // SimSwitch ... ???
            
            // Ah, I need to check how Switches are loaded.
            // SKILL.md says switches are in <trackTopology><connections>.
            // So they are inside Track objects?
            // "Switch Location: A switch must ALWAYS be defined inside <trackTopology><connections> at the trackBegin".
            
            // So I need to define a track 'tr2' (the switch leg?) or 'tr_sw'? 
            // Actually, the switch itself is an element inside <connections>.
            
            // Let's try to mimic sim3.railml structure in code.
            // Track tr1 (100m) -> Switch sw1 (at pos=100 of tr1?) No, switch is at trackBegin of its OWN track?
            // "Split Rule: NEVER place a <switch> in the middle... track MUST be split."
            // "Switch... at trackBegin (pos=0)."
            
            // So let's define:
            // Track tr1 (0-100m).
            // Track tr2 (starts with switch sw1).
            // tr1 end connects to tr2 begin.
            
            var tr1 = new Track { Id = "tr1", Length = 100, Name="Approach", TrackTopology = new TrackTopology { 
                TrackBegin = new TrackNode { Id="tb1", OpenEnd = new OpenEnd() },
                TrackEnd = new TrackNode { Id="te1", ConnectionList = new List<Connection> { new Connection { Ref = "conn_sw1" } } }
            }};

            var sw1 = new Switch { Id = "sw1", Pos = 0, TrackContinueCourse = "straight", NormalPosition = "straight", 
                                   ConnectionList = new List<Connection> { new Connection { Course="left", Ref="conn_left" } } };
            
            var tr2 = new Track { Id = "tr2", Length = 100, Name="SwitchTrack", TrackTopology = new TrackTopology {
                TrackBegin = new TrackNode { Id="tb2", ConnectionList = new List<Connection> { new Connection { Id="conn_sw1", Ref="te1" } } },
                TrackEnd = new TrackNode { Id="te2", OpenEnd = new OpenEnd() },
                Connections = new Connections { Switches = new List<Switch> { sw1 } }
            }};

            model.Infrastructure.Tracks.TrackList = new List<Track> { tr1, tr2 };

            var settings = new SimulationSettings();
            settings.SwitchTransitionTime = 10.0;
            
            var manager = new SimulationManager(model, settings);
            
            bool accidentReported = false;
            manager.OnAccident += (msg) => {
                Console.WriteLine("!!! ACCIDENT REPORTED !!!");
                Console.WriteLine(msg);
                accidentReported = true;
            };

            manager.Start();
            
            // 1. Start Switch Moving
            // We need to access the SimSwitch object.
            var simSw = manager.Switches["sw1"];
            Console.WriteLine($"Switch State: {simSw.State}");
            
            // Force move event
            manager.EventQueue.Enqueue(new Railml.Sim.Core.Events.SwitchMoveEvent(manager.CurrentTime, simSw, SimSwitch.SwitchState.Reverse, false));
            
            // 2. Spawn Train on tr1, close to tr2
            var train = new Train("t1", settings) { CurrentTrack = manager.Tracks["tr1"], PositionOnTrack = 90, Speed = 20, MoveDirection = TrainDirection.Up };
            train.OccupiedTracks.Add(manager.Tracks["tr1"]);
            manager.AddTrain(train);
            
            Console.WriteLine("Train spawned at 90m, Speed 20m/s. Switch starts moving.");

            // 3. Run Simulation
            for(int i=0; i<10; i++)
            {
                manager.RunUntil(manager.CurrentTime + 0.5);
                Console.WriteLine($"Time: {manager.CurrentTime:F1}, Switch: {simSw.State}, TrainPos: {train.PositionOnTrack:F1}, Track: {train.CurrentTrack.RailmlTrack.Id}");
                
                if (accidentReported) break;
            }

            if (accidentReported) Console.WriteLine("TEST PASSED: Derailment detected.");
            else Console.WriteLine("TEST FAILED: No derailment reported.");

        }
        catch(Exception ex)
        {
            Console.WriteLine("Verification Error: " + ex);
        }
    }
}
