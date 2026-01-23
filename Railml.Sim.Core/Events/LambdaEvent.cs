using System;

namespace Railml.Sim.Core.Events
{
    public class LambdaEvent : DESEvent
    {
        private Action<SimulationContext> _action;
        private string _description;

        public LambdaEvent(double time, Action<SimulationContext> action, string description = "Lambda Action") : base(time)
        {
            _action = action;
            _description = description;
        }

        public override string GetLogInfo() => _description;

        public override void Execute(SimulationContext context)
        {
            _action?.Invoke(context);
        }
    }
}
