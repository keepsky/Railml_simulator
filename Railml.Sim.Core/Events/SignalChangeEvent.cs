using Railml.Sim.Core.SimObjects;
using Railml.Sim.Core;

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

        public override string GetLogInfo()
        {
            // Note: TargetSignal.Aspect is the *Current* (Old) aspect before Execute runs.
            string name = TargetSignal.RailmlSignal.AdditionalName?.Name ?? "NoName";
            string dir = TargetSignal.RailmlSignal.Dir ?? "unknown";
            return $"Signal: {TargetSignal.RailmlSignal.Id} ({name}), Dir: {dir}, Change: {TargetSignal.Aspect} -> {NewAspect}";
        }

        public override void Execute(SimulationContext context)
        {
            if (TargetSignal.Aspect == NewAspect) return;

            System.Console.WriteLine($"[DEBUG] SignalChangeEvent Executed for {TargetSignal.RailmlSignal.Id}. NewAspect: {NewAspect} at {context.CurrentTime}");
            TargetSignal.Aspect = NewAspect;
            TargetSignal.PendingAspect = null;
            
            // If Signal turns Green, check for waiting trains
            if (NewAspect == SignalAspect.Proceed && context is SimulationManager manager)
            {
                foreach (var train in manager.Trains)
                {
                    if (train.IsWaitingForSignal && train.WaitingSignal == TargetSignal)
                    {
                        // Resume Train
                        train.IsWaitingForSignal = false;
                        train.WaitingSignal = null;
                        
                        // Schedule immediate move with reaction time (1s)
                        manager.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime + 1.0, train));
                    }
                }
            }
        }
    }
}
