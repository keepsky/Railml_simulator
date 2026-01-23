using System.Collections.Generic;
using System; // Added for Action delegate

namespace Railml.Sim.Core.Events
{
    public class EventQueue
    {
        private readonly PriorityQueue<DESEvent, DESEvent> _queue = new PriorityQueue<DESEvent, DESEvent>();

        // Custom event for logging: Time, Type, Message, LogInfo
        public event Action<double, string, string, string> OnLog;

        public void Enqueue(DESEvent evt)
        {
            _queue.Enqueue(evt, evt);
            // Log Enqueue
            OnLog?.Invoke(evt.ExecutionTime, "Enqueue", evt.ToString(), evt.GetLogInfo());
        }

        public DESEvent Dequeue()
        {
            var evt = _queue.Dequeue();
            // Log Dequeue - Note: Dequeue time is usually CurrentTime, but strictly speaking it's the event time
            OnLog?.Invoke(evt.ExecutionTime, "Dequeue", evt.ToString(), evt.GetLogInfo()); // Using ExecutionTime as the time for logging
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
