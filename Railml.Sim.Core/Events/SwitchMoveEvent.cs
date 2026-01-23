using System;
using Railml.Sim.Core.SimObjects;
using Railml.Sim.Core;

namespace Railml.Sim.Core.Events
{
    public class SwitchMoveEvent : DESEvent
    {
        private SimSwitch _switch;
        private SimSwitch.SwitchState _targetState;
        private bool _isCompletionEvent;

        public SwitchMoveEvent(double time, SimSwitch sw, SimSwitch.SwitchState targetState, bool isCompletion) : base(time)
        {
            _switch = sw;
            _targetState = targetState;
            _isCompletionEvent = isCompletion;
        }

        public override void Execute(SimulationContext context)
        {
            var manager = context as SimulationManager;
            if (manager == null) return;

            if (!_isCompletionEvent)
            {
                // Start Switching
                // Check if already moving? (Simplified: Ignore if already moving matching target, or overwrite)
                if (_switch.State == SimSwitch.SwitchState.Moving && _switch.TargetState == _targetState)
                    return;

                _switch.State = SimSwitch.SwitchState.Moving;
                _switch.TargetState = _targetState;
                _switch.SwitchingStartTime = context.CurrentTime;
                
                // Get Duration from Settings
                _switch.SwitchingDuration = context.Settings.SwitchTransitionTime;

                // Schedule Completion
                manager.EventQueue.Enqueue(new SwitchMoveEvent(context.CurrentTime + _switch.SwitchingDuration, _switch, _targetState, true));
                
                // Console.WriteLine($"Time {Time:F1}: Switch {_switch.RailmlSwitch.Id} START moving to {_targetState}");
            }
            else
            {
                // Complete Switching
                // Only complete if we are still moving to this target (canceling logic not fully implemented but safe check)
                if (_switch.State == SimSwitch.SwitchState.Moving && _switch.TargetState == _targetState)
                {
                    _switch.State = _targetState;
                    // Update Course/Visuals if needed?
                    // For now, State is the source of truth.
                    
                    // Console.WriteLine($"Time {Time:F1}: Switch {_switch.RailmlSwitch.Id} COMPLETED moving to {_targetState}");
                }
            }
        }
    }
}
