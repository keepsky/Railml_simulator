using Railml.Sim.Core.Models;

namespace Railml.Sim.Core.SimObjects
{
    public enum SignalAspect
    {
        Stop, // Red
        Proceed // Green
    }

    public class SimSignal
    {
        public Signal RailmlSignal { get; }
        public SignalAspect Aspect { get; set; } = SignalAspect.Proceed;

        public SimSignal(Signal signal)
        {
            RailmlSignal = signal;
        }
    }
}
