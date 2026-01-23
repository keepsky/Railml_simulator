using System.Collections.Generic;
using System; // Added for Action delegate

namespace Railml.Sim.Core.Events
{
    public class EventQueue
    {
        private readonly PriorityQueue<DESEvent, DESEvent> _queue = new PriorityQueue<DESEvent, DESEvent>();

        // Custom event for logging: ProcessTime, ExecutionTime, Type, Message, LogInfo
        public event Action<double, double, string, string, string> OnLog;

        public double CurrentTime { get; set; } = 0.0;

        public void Enqueue(DESEvent evt)
        {
            _queue.Enqueue(evt, evt);
            // Log Enqueue: ProcessTime = CurrentTime, ExecutionTime = evt.ExecutionTime
            OnLog?.Invoke(CurrentTime, evt.ExecutionTime, "Enqueue", evt.ToString(), evt.GetLogInfo());
        }

        public DESEvent Dequeue()
        {
            var evt = _queue.Dequeue();
            // Log Dequeue - CurrentTime is updated by Manager to evt.ExecutionTime during processing
            OnLog?.Invoke(evt.ExecutionTime, evt.ExecutionTime, "Dequeue", evt.ToString(), evt.GetLogInfo());
            return evt;
        }

        public bool IsEmpty => _queue.Count == 0;
        
        public double NextEventTime => _queue.Count > 0 ? _queue.Peek().ExecutionTime : double.PositiveInfinity;
        
        public int Count => _queue.Count;

        public void Clear()
        {
            _queue.Clear();
        }
    }
}
