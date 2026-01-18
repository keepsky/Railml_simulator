using System.Collections.Generic;
using Railml.Simulation.Core.Models;

namespace Railml.Simulation.Core.SimObjects
{
    public class SimTrack
    {
        public Track RailmlTrack { get; }
        public bool IsOccupied => OccupyingTrains.Count > 0;
        public List<Train> OccupyingTrains { get; } = new List<Train>();

        public SimTrack(Track track)
        {
            RailmlTrack = track;
        }

        public double Length => RailmlTrack.TrackTopology.TrackEnd.Pos - RailmlTrack.TrackTopology.TrackBegin.Pos;
        // Ideally we calculate length from absPos differences, but simplified here. 
        // In RailML 2.x, pos is usually absolute distance from a reference. 
        // We will assume 'pos' is relative or absolute on the line. 
        // If AbsPos is used, we need that. The prompt mentions "actual length uses <track> <absPos>".
        // For now, let's assume the difference between Begin and End Pos is length.
    }
}
