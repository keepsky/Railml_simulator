using Railml.Simulation.Core.Events;
using Railml.Simulation.Core.Models;

namespace Railml.Simulation.Core
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
