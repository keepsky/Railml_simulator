using System.Collections.Generic;
using SkiaSharp;
using Railml.Sim.Core;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.UI.Rendering
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

        private SKPaint _trainRectPaint = new SKPaint
        {
            Color = SKColors.Pink.WithAlpha(204), // 20% Transparency = 80% Opacity
            StrokeWidth = 8, // Track(4) + 4
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        private void RenderTrains(SKCanvas canvas, SimulationManager manager)
        {
            foreach (var train in manager.Trains)
            {
                var track = train.CurrentTrack;
                if (track != null)
                {
                     float x1 = (float)track.StartScreenPos.X;
                     float y1 = (float)track.StartScreenPos.Y;
                     float x2 = (float)track.EndScreenPos.X;
                     float y2 = (float)track.EndScreenPos.Y;
                     
                     // Track connection length in meters
                     // Avoid div by zero.
                     if (track.Length <= 0.001) continue;

                     double headPos = train.PositionOnTrack;
                     double tailPos = headPos;

                     if (train.MoveDirection == TrainDirection.Up)
                     {
                         // Moving towards End (Pos increasing). Tail is behind (smaller pos).
                         tailPos = headPos - train.Length;
                     }
                     else
                     {
                         // Moving towards Start (Pos decreasing). Tail is behind (larger pos).
                         tailPos = headPos + train.Length;
                     }

                     // Clamp to track bounds to draw only what's on this track
                     if (headPos < 0) headPos = 0;
                     if (headPos > track.Length) headPos = track.Length;
                     
                     if (tailPos < 0) tailPos = 0;
                     if (tailPos > track.Length) tailPos = track.Length;

                     // Convert to t [0..1]
                     float tHead = (float)(headPos / track.Length);
                     float tTail = (float)(tailPos / track.Length);

                     // Map to Screen
                     float hx = x1 + (x2 - x1) * tHead;
                     float hy = y1 + (y2 - y1) * tHead;
                     
                     float tx = x1 + (x2 - x1) * tTail;
                     float ty = y1 + (y2 - y1) * tTail;

                     // Draw Train Segment
                     // Use specific paint with width = TrackWidth + 4
                     canvas.DrawLine(hx, hy, tx, ty, _trainRectPaint);
                }
            }
        }
        
        private void RenderSignals(SKCanvas canvas, SimulationManager manager)
        {
             // New Logic based on user request:
             // (1) dir="down" -> Above track (Offset -Normal)
             // (2) dir="up" -> Below track (Offset +Normal)
             // (3) pos -> Proportional to track length
             
             foreach(var track in manager.Tracks.Values)
             {
                 if (track.RailmlTrack.OcsElements?.Signals?.SignalList != null)
                 {
                     float x1 = (float)track.StartScreenPos.X;
                     float y1 = (float)track.StartScreenPos.Y;
                     float x2 = (float)track.EndScreenPos.X;
                     float y2 = (float)track.EndScreenPos.Y;
                     
                     // Calculate Track Vector
                     float dx = x2 - x1;
                     float dy = y2 - y1;
                     float length = (float)System.Math.Sqrt(dx*dx + dy*dy);
                     if (length < 0.001f) continue;
                     
                     // Normalized Vector
                     float nx = dx / length;
                     float ny = dy / length;
                     
                     // Perpendicular Vector (Upward in screen coords is -Y, but "Normal" usually R to L relative to direction?)
                     // Left Normal: (-ny, nx)
                     // Right Normal: (ny, -nx)
                     // Let's assume standard mathematics:
                     // Vector (dx, dy). Rotate 90 CCW = (-dy, dx).
                     
                     foreach(var sig in track.RailmlTrack.OcsElements.Signals.SignalList)
                     {
                         // Find SimSignal to get state
                         SimSignal simSig = null;
                         if (manager.Signals.TryGetValue(sig.Id, out simSig))
                         {
                             // Calculate t
                             // Need absolute pos range of track
                             double trackBegin = track.RailmlTrack.TrackTopology.TrackBegin.Pos;
                             double trackEnd = track.RailmlTrack.TrackTopology.TrackEnd.Pos;
                             double trackLen = System.Math.Abs(trackEnd - trackBegin);
                             
                             double sigPos = sig.Pos; // relative to what? usually absolute on line?
                             // Assuming sig.Pos is in same coordinate space as trackBegin/End
                             
                             float t = 0;
                             if (trackLen > 0)
                                 t = (float)((sigPos - trackBegin) / (trackEnd - trackBegin));
                                 
                             // Clamp t?
                             if (t < 0) t = 0; else if (t > 1) t = 1;

                             float bx = x1 + dx * t;
                             float by = y1 + dy * t;
                             
                             float renderX = bx;
                             float renderY = by;
                             
                             float offset = 15.0f; // 10px radius or offset? User said "10px circle", "above". Offset > radius.
                             
                             // Offset logic
                             // dir="down" -> "Above" -> Y decreases?
                             // dir="up" -> "Below" -> Y increases?
                             
                             // NOTE: Visual "Above" means lower Y.
                             // Visual "Below" means higher Y.
                             // This applies if track is roughly horizontal.
                             // If we use normal vectors:
                             // "Above" implies Normal pointing "Up" (-Y).
                             // (-dy, dx) is 90 deg rotation.
                             // If line is Left->Right (1,0), Normal is (0,1) which is DOWN. 
                             // So (-dy, dx) is 'Right Normal' or 'Down Normal'.
                             // We want 'Up Normal' i.e. (dy, -dx).
                             
                             float px = dy / length; // (dy, -dx) normalized? No. (nx, ny) -> perp (-ny, nx) is (-dy/L, dx/L)
                             float py = -dx / length;
                             
                             // (px, py) is (-ny, nx) -> (-0, 1) for L->R. That is DOWN.
                             // Wait. Left->Right is (1,0). 
                             // (-0, 1) is (0,1). +Y is Down in screen coords.
                             // So (-ny, nx) is "Down/Right" side.
                             
                             // User: "down" -> Above (-Y).
                             // User: "up" -> Below (+Y).
                             
                             if (sig.Dir == "down")
                             {
                                 // Draw Above. We found (-ny, nx) points Down.
                                 // So we subtract it? Or use opposiste.
                                 // Let's use subtraction of the "Down" vector to go "Up".
                                 renderX -= px * offset;
                                 renderY -= py * offset;
                             }
                             else if (sig.Dir == "up")
                             {
                                 // Draw Below (Down).
                                 renderX += px * offset;
                                 renderY += py * offset;
                             }
                             
                             canvas.DrawCircle(renderX, renderY, 5, simSig.Aspect == SignalAspect.Stop ? _signalRedPaint : _signalGreenPaint);
                         }
                     }
                 }
             }
        }
    }
}
