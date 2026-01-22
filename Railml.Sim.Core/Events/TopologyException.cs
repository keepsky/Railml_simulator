using System;

namespace Railml.Sim.Core.Events
{
    public class TopologyException : Exception
    {
        public TopologyException(string message) : base(message)
        {
        }

        public TopologyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
