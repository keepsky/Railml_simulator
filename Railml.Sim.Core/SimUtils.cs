using System;

namespace Railml.Sim.Core
{
    public static class SimUtils
    {
        public static string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds)) return "--:--:--.---";
            
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            // TimeSpan handles H:M:S but we want strictly HH:MM:SS.mmm
            // Total hours might exceed 24, but usually not in sim.
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";
        }
    }
}
