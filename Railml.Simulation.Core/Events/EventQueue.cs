using System.Collections.Generic;

namespace Railml.Simulation.Core.Events
{
    public class EventQueue
    {
        private readonly PriorityQueue<DESEvent, DESEvent> _queue = new PriorityQueue<DESEvent, DESEvent>();

        public void Enqueue(DESEvent evt)
        {
            _queue.Enqueue(evt, evt);
        }

        public DESEvent Dequeue()
        {
            return _queue.Dequeue();
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
