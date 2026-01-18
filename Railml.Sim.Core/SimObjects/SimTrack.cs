using System.Collections.Generic;
using Railml.Sim.Core.Models;

namespace Railml.Sim.Core.SimObjects
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

        // Visualization Coordinates
        public Point StartScreenPos { get; set; }
        public Point EndScreenPos { get; set; }
    }

    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

    }
}
