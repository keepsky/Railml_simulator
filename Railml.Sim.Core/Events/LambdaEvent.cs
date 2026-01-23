using System;

namespace Railml.Sim.Core.Events
{
    public class LambdaEvent : DESEvent
    {
        private Action<SimulationContext> _action;

        public LambdaEvent(double time, Action<SimulationContext> action) : base(time)
        {
            _action = action;
        }

        public override void Execute(SimulationContext context)
        {
            _action?.Invoke(context);
        }
    }
}
