using System.Collections.Generic;
using SkiaSharp;
using Railml.Sim.Core;
using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.UI.Rendering
{
    public class SimulationRenderer
    {
        private SKPaint _trackFillPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        private SKPaint _trackStrokePaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        
        private SKPaint _trackActivePaint = new SKPaint
        {
            Color = SKColors.Gray,
            StrokeWidth = 8, 
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        private SKPaint _signalRedPaint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        private SKPaint _signalGreenPaint = new SKPaint
        {
            Color = SKColors.Green,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };





        public void Render(SKCanvas canvas, SimulationManager manager, int width, int height)
        {
            if (manager == null) return;
            
            // Optional: Draw background or handle scaling using width/height
            // canvas.Clear(SKColors.White);

            RenderTracks(canvas, manager);
            RenderSignals(canvas, manager); 
            RenderTrains(canvas, manager);
        }

        private void RenderTracks(SKCanvas canvas, SimulationManager manager)
        {
            foreach (var track in manager.Tracks.Values)
            {
                float x1 = (float)track.StartScreenPos.X;
                float y1 = (float)track.StartScreenPos.Y;
                float x2 = (float)track.EndScreenPos.X;
                float y2 = (float)track.EndScreenPos.Y;
                
                if (x1 == 0 && y1 == 0 && x2 == 0 && y2 == 0) continue;

                float dx = x2 - x1;
                float dy = y2 - y1;
                float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.1f) continue;
                
                float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);

                canvas.Save();
                canvas.Translate(x1, y1);
                canvas.RotateDegrees(angle);
                
                // Draw Rect (0, -4, Len, 4) -> 8px height centered locally
                SKRect rect = new SKRect(0, -4, len, 4);
                
                if (track.IsOccupied)
                {
                    // Draw Blue Occupied Line
                    canvas.DrawLine(0, 0, len, 0, _trackActivePaint); 
                }
                else
                {
                    // Draw White Fill (One shape per track)
                    canvas.DrawRoundRect(rect, 3, 3, _trackFillPaint);
                    // Draw Black Border
                    canvas.DrawRoundRect(rect, 3, 3, _trackStrokePaint);
                }
                
                canvas.Restore();
            }
        }

        private SKPaint _trainRectPaint = new SKPaint
        {
            Color = new SKColor(SKColors.Pink.Red, SKColors.Pink.Green, SKColors.Pink.Blue, 204), // 20% Transparency
            StrokeWidth = 8, // Track(4) + 4
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        private void RenderTrains(SKCanvas canvas, SimulationManager manager)
        {
            if (manager.Trains == null) return;

            foreach (var train in manager.Trains)
            {
                double remainingLen = train.Length;
                var currentTrack = train.CurrentTrack;
                double currentHeadPos = train.PositionOnTrack;
                var currentDir = train.MoveDirection;

                // Loop to draw train across multiple tracks
                int iterations = 0;
                while (remainingLen > 0 && currentTrack != null && iterations < 10) // Safety break
                {
                    iterations++; // Safety limit
                    
                    double drawLen = 0;
                    double segmentStart = 0;
                    double segmentEnd = 0;

                    // Calculate segment on current track
                    if (currentDir == TrainDirection.Up)
                    {
                        // Moving Up (Start -> End). Head is at currentHeadPos.
                        // Body extends towards Start (decreasing pos).
                        // Segment is [Max(0, Head - Remaining), Head]
                        double tailPos = currentHeadPos - remainingLen;
                        double actualTail = System.Math.Max(0, tailPos);
                        
                        segmentStart = actualTail;
                        segmentEnd = currentHeadPos;
                        drawLen = segmentEnd - segmentStart;
                    }
                    else // Down
                    {
                        // Moving Down (End -> Start). Head is at currentHeadPos.
                        // Body extends towards End (increasing pos).
                        // Segment is [Head, Min(Length, Head + Remaining)]
                        double tailPos = currentHeadPos + remainingLen;
                        double actualTail = System.Math.Min(currentTrack.Length, tailPos);

                        segmentStart = currentHeadPos;
                        segmentEnd = actualTail;
                        drawLen = segmentEnd - segmentStart;
                    }

                    // Draw this segment
                    if (drawLen > 0.001)
                    {
                        float x1, y1, x2, y2;
                        // Map pos to screen coords
                        // Track screen pos: Start -> End.
                        float tx1 = (float)currentTrack.StartScreenPos.X;
                        float ty1 = (float)currentTrack.StartScreenPos.Y;
                        float tx2 = (float)currentTrack.EndScreenPos.X;
                        float ty2 = (float)currentTrack.EndScreenPos.Y;

                        // Normalize t [0..1]
                        float tStart = (float)(segmentStart / currentTrack.Length);
                        float tEnd = (float)(segmentEnd / currentTrack.Length);

                        float hx = tx1 + (tx2 - tx1) * tEnd;
                        float hy = ty1 + (ty2 - ty1) * tEnd;
                        
                        float tx = tx1 + (tx2 - tx1) * tStart;
                        float ty = ty1 + (ty2 - ty1) * tStart;

                        canvas.DrawLine(hx, hy, tx, ty, _trainRectPaint);
                    }

                    remainingLen -= drawLen;

                    if (remainingLen <= 0.001) break;

                    // Find previous track to continue drawing
                    // We look "behind" the train.
                    // If moving Up, look for connection at Start (Move Down check).
                    // If moving Down, look for connection at End (Move Up check).
                    var checkDir = (currentDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                    
                    if (manager.FindNextTrack(currentTrack, checkDir, out var prevTrack, out var entryPos, out var entryDir))
                    {
                        currentTrack = prevTrack;
                        currentHeadPos = entryPos;
                        // Important: entryDir is the direction we 'enter' the new track moving in the 'checkDir'.
                        // But we want the Train's logical direction.
                        // If we trace 'Down' and enter a track moving 'Up' (relative to that track),
                        // then the TRAIN is effectively moving 'Down' relative to that track (Opposite of tracer).
                        currentDir = (entryDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                    }
                    else
                    {
                        // No connection found, stop drawing
                        break;
                    }
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
