using Railml.Simulation.Core.Models;

namespace Railml.Simulation.Core.SimObjects
{
    public enum SignalAspect
    {
        Stop, // Red
        Proceed // Green
    }

    public class SimSignal
    {
        public Signal RailmlSignal { get; }
        public SignalAspect Aspect { get; set; } = SignalAspect.Stop;

        public SimSignal(Signal signal)
        {
            RailmlSignal = signal;
        }
    }
}
