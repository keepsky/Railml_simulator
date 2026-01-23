using System;
using System.Collections.Generic;
using Railml.Sim.Core;
using Railml.Sim.Core.Events;
using Railml.Sim.Core.Models;

namespace Railml.Sim.Verification
{
    class VerifyLogEvent
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Verifying Log Event...");
            
            // Setup Simulation
            var simSettings = new SimulationSettings();
            // Setup minimal RailML
            var railModel = new Railml.Sim.Core.Models.Railml();
            railModel.Infrastructure = new Infrastructure();
            railModel.Infrastructure.Tracks = new Tracks();
            railModel.Infrastructure.Tracks.TrackList = new List<Track>();
            
            var manager = new SimulationManager(railModel, simSettings);

            bool enqueueLogged = false;
            bool dequeueLogged = false;

            manager.EventQueue.OnLog += (time, type, msg, info) =>
            {
                Console.WriteLine($"[LOGGED] {time:F2} [{type}]: {msg}");
                if (type == "Enqueue") enqueueLogged = true;
                if (type == "Dequeue") dequeueLogged = true;
            };

            // manually enqueue an event
            var dummyEvent = new DummyEvent(1.0);
            Console.WriteLine("Enqueuing DummyEvent...");
            manager.EventQueue.Enqueue(dummyEvent);

            if (!enqueueLogged)
            {
                Console.WriteLine("FAIL: Enqueue event not logged immediately (or at all).");
            }
            else
            {
                Console.WriteLine("PASS: Enqueue event logged.");
            }

            // Run simulation to trigger dequeue
            Console.WriteLine("Starting and Running simulation...");
            manager.Start(); // Ensure IsRunning is true
            manager.RunUntil(2.0);

            if (!dequeueLogged)
            {
                Console.WriteLine("FAIL: Dequeue event not logged.");
            }
            else
            {
                Console.WriteLine("PASS: Dequeue event logged.");
            }

            if (enqueueLogged && dequeueLogged)
            {
                Console.WriteLine("VERIFICATION PASSED");
            }
            else
            {
                 Console.WriteLine("VERIFICATION FAILED");
            }
        }
    }

    class DummyEvent : DESEvent
    {
        public DummyEvent(double time) : base(time) { }
        public override void Execute(SimulationContext context) { }
        public override string ToString() => "DummyEvent";
    }
}
