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

        public override void Execute(SimulationContext context)
        {
            TargetSignal.Aspect = NewAspect;
            
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
                        
                        // Schedule immediate move
                        manager.EventQueue.Enqueue(new TrainMoveEvent(context.CurrentTime, train));
                    }
                }
            }
        }
    }
}
