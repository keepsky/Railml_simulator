using System.Collections.Generic;
using SkiaSharp;
using Railml.Simulation.Core;
using Railml.Simulation.Core.SimObjects;

namespace Railml.Simulator.UI.Rendering
{
    public class SimulationRenderer
    {
        private SKPaint _trackPaint = new SKPaint
        {
            Color = SKColors.Gray,
            StrokeWidth = 4,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        
        private SKPaint _trackActivePaint = new SKPaint
        {
            Color = SKColors.Blue,
            StrokeWidth = 4,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        private SKPaint _trainPaint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        private SKPaint _signalRedPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill, IsAntialias = true };
        private SKPaint _signalGreenPaint = new SKPaint { Color = SKColors.Green, Style = SKPaintStyle.Fill, IsAntialias = true };

        public void Render(SKCanvas canvas, SimulationManager manager, float width, float height)
        {
            canvas.Clear(SKColors.White);

            if (manager == null) return;
            
            // Transform? Scale/Pan logic needed?
            // RailML coordinates could be large. 
            // We need a Viewport calc. For now, simplistic scaling.
            canvas.Translate(100, 100); // Offset
            
            RenderTracks(canvas, manager);
            RenderTrains(canvas, manager);
            RenderSignals(canvas, manager);
        }

        private void RenderTracks(SKCanvas canvas, SimulationManager manager)
        {
            foreach (var track in manager.Tracks.Values)
            {
                // Use Parsed ScreenPos
                float x1 = (float)track.StartScreenPos.X;
                float y1 = (float)track.StartScreenPos.Y;
                float x2 = (float)track.EndScreenPos.X;
                float y2 = (float)track.EndScreenPos.Y;
                
                // If 0,0 maybe fallback to legacy or skip?
                // sim.railml has valid coordinates.
                if (x1 != 0 || y1 != 0 || x2 != 0 || y2 != 0)
                {
                    canvas.DrawLine(x1, y1, x2, y2, track.IsOccupied ? _trackActivePaint : _trackPaint);
                }
            }
        }

        private void RenderTrains(SKCanvas canvas, SimulationManager manager)
        {
            foreach (var train in manager.Trains)
            {
                var track = train.CurrentTrack;
                if (track != null)
                {
                     // interpolated position
                     float x1 = (float)track.StartScreenPos.X;
                     float y1 = (float)track.StartScreenPos.Y;
                     float x2 = (float)track.EndScreenPos.X;
                     float y2 = (float)track.EndScreenPos.Y;
                        
                     float t = (float)(train.PositionOnTrack / track.Length);
                     float cx = x1 + (x2 - x1) * t;
                     float cy = y1 + (y2 - y1) * t;
                        
                     canvas.DrawCircle(cx, cy, 5, _trainPaint);
                }
            }
        }
        
        private void RenderSignals(SKCanvas canvas, SimulationManager manager)
        {
             foreach(var sig in manager.Signals.Values)
             {
                 if (sig.RailmlSignal.ScreenPos != null)
                 {
                     float x = (float)sig.RailmlSignal.ScreenPos.X;
                     float y = (float)sig.RailmlSignal.ScreenPos.Y;
                     
                     canvas.DrawCircle(x, y, 4, sig.Aspect == SignalAspect.Stop ? _signalRedPaint : _signalGreenPaint);
                 }
             }
        }
    }
}
