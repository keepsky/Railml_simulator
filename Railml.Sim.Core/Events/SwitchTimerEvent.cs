using System;
using System.Linq;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.Core.Events
{
    public class SwitchTimerEvent : DESEvent
    {
        private static Random _random = new Random();

        public SwitchTimerEvent(double time) : base(time)
        {
        }

        public static double GetNextDelay(double mean)
        {
            // Safety check for 0 or negative mean
            if (mean <= 0.1) mean = 0.1;

            // Exponential Distribution: T = -mean * ln(U)
            double u = _random.NextDouble();
            // Avoid ln(0) although rare
            if (u == 0) u = 0.0000001;
            
            return -mean * Math.Log(u);
        }

        public override void Execute(SimulationContext context)
        {
            var manager = context as SimulationManager;
            if (manager == null) return;

            // 1. Perform Global Switch Toggle
            if (manager.Switches.Count > 0)
            {
                foreach (var sw in manager.Switches.Values)
                {
                    // Determine Target State (Toggle)
                    var nextState = sw.State == SimSwitch.SwitchState.Normal ? SimSwitch.SwitchState.Reverse : SimSwitch.SwitchState.Normal;
                    
                    // Enqueue Actual Switch Action (All occur at the same logical time)
                    manager.EventQueue.Enqueue(new SwitchMoveEvent(context.CurrentTime, sw, nextState, false));
                }
            }

            // 2. Schedule Next Timer
            double delay = GetNextDelay(manager.Settings.SwitchTransitionTime);
            manager.EventQueue.Enqueue(new SwitchTimerEvent(context.CurrentTime + delay));
        }

        public override string GetLogInfo()
        {
            return "Auto-Switch Timer (Global)";
        }
    }
}
