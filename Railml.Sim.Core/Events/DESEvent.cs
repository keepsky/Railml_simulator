using System;

namespace Railml.Sim.Core.Events
{
    public abstract class DESEvent : IComparable<DESEvent>
    {
        public double ExecutionTime { get; }
        public int Priority { get; set; } = 0; // Secondary sort key if needed

        protected DESEvent(double executionTime)
        {
            ExecutionTime = executionTime;
        }

        public abstract void Execute(SimulationContext context);

        public virtual string GetLogInfo() => "";

        public int CompareTo(DESEvent other)
        {
            int timeComparison = ExecutionTime.CompareTo(other.ExecutionTime);
            if (timeComparison != 0) return timeComparison;
            return Priority.CompareTo(other.Priority);
        }
    }
}
