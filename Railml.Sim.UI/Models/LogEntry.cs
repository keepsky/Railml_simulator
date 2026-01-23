using System;

namespace Railml.Sim.UI.Models
{
    public class LogEntry
    {
        public string Time { get; set; }
        public string Type { get; set; } // Enqueue or Dequeue
        public string Message { get; set; }
        public string Information { get; set; }

        public LogEntry(string time, string type, string message, string information)
        {
            Time = time;
            Type = type;
            Message = message;
            Information = information;
        }
    }
}
