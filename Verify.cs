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
        Console.WriteLine("Starting Verification...");
        try 
        {
            var model = new Railml.Sim.Core.Models.Railml
            {
                Infrastructure = new Infrastructure
                {
                    Tracks = new Tracks 
                    { 
                        TrackList = new List<Track>
                        {
                            new Track { Id = "tr1", Length = 100, MainDir = "up", 
                                        TrackTopology = new TrackTopology { TrackBegin = new TrackNode { Id="tb1", OpenEnd = new OpenEnd() }, TrackEnd = new TrackNode { Id="te1" } } },
                        }
                    }
                }
            };
            var settings = new SimulationSettings();
            // Just simple init
            var manager = new SimulationManager(model, settings);
            
            manager.Start();
            Console.WriteLine("Manager Started. Running for 5 seconds...");
            
            // Step loop
            for(int i=0; i<5; i++)
            {
                manager.RunUntil(manager.CurrentTime + 1.0);
                Console.WriteLine($"Time: {manager.CurrentTime:F1}, Trains: {manager.Trains.Count}");
                if(manager.Trains.Count > 0)
                {
                    var t = manager.Trains[0];
                     Console.WriteLine($"Train0 Pos: {t.PositionOnTrack:F1}");
                }
            }
            Console.WriteLine("Finished Verification.");
        }
        catch(Exception ex)
        {
            Console.WriteLine("Verification Error: " + ex);
        }
    }
}
