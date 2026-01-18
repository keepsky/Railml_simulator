using Railml.Sim.Core.Events;
using Railml.Sim.Core.Models;

namespace Railml.Sim.Core
{
    // Forward declaration or Interface to allow Event execution
    public interface SimulationContext
    {
        EventQueue EventQueue { get; }
        SimulationSettings Settings { get; }
        
        // Methods to access simulation objects
        // void UpdateTrainState(Train train);
        // ...
        
        double CurrentTime { get; }
    }
}
