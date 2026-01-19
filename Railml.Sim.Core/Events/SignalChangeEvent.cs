using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core.Events
{
    public class SignalChangeEvent : DESEvent
    {
        public SimSignal TargetSignal { get; }
        public SignalAspect NewAspect { get; }

        public SignalChangeEvent(double time, SimSignal signal, SignalAspect aspect) : base(time)
        {
            TargetSignal = signal;
            NewAspect = aspect;
        }

        public override void Execute(SimulationContext context)
        {
            TargetSignal.Aspect = NewAspect;
            // Optionally notify context/manager about update?
            // SimulationManager usually polls updates or UI renders periodically.
        }
    }
}
