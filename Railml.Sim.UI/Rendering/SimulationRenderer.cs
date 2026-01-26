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

        private SKPaint _switchNormalPaint = new SKPaint
        {
            Color = SKColors.Blue,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        private SKPaint _switchReversePaint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        private SKPaint _switchMovingPaint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        public ViewportController Viewport { get; } = new ViewportController();
        private SKPicture? _staticTrackLayer;
        private bool _isStaticLayerDirty = true;

        public void Render(SKCanvas canvas, SimulationManager manager, int width, int height)
        {
            if (manager == null) return;
            
            // 2. Prepare Static Layer
            if (_isStaticLayerDirty || _staticTrackLayer == null)
            {
                UpdateStaticLayer(manager);
                _isStaticLayerDirty = false;
            }

            canvas.Clear(SKColors.White);

            // 3. Apply Viewport Transform
            canvas.Save();
            canvas.Concat(Viewport.Matrix.Value);

            // 4. Draw Static Layer (Tracks)
            if (_staticTrackLayer != null)
            {
                canvas.DrawPicture(_staticTrackLayer);
            }

            // 5. Draw Dynamic Layer (Occupancy, Signals, Trains)
            RenderOccupancyOverlay(canvas, manager);
            RenderSwitches(canvas, manager);
            RenderSignals(canvas, manager); 
            RenderTrains(canvas, manager);

            canvas.Restore();
        }

        public void InvalidateStaticLayer()
        {
            _isStaticLayerDirty = true;
        }

        private SKPaint _trackTextPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 2f, 
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        private void UpdateStaticLayer(SimulationManager manager)
        {
            using (var recorder = new SKPictureRecorder())
            {
                var canvas = recorder.BeginRecording(SKRect.Create(-10000, -10000, 20000, 20000)); // Large bounds
                
                RenderTracks(canvas, manager);
                
                _staticTrackLayer = recorder.EndRecording();
            }
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
                
                // Draw Rect (0, -4, Len, 4) -> 8px width
                SKRect rect = new SKRect(0, -4, len, 4);
                
                _trackFillPaint.StrokeWidth = 0;
                _trackStrokePaint.StrokeWidth = 1.0f; // Border

                // Always draw "Unoccupied" state in Static Layer
                // Draw White Fill
                canvas.DrawRoundRect(rect, 2.0f, 2.0f, _trackFillPaint);
                // Draw Black Border
                canvas.DrawRoundRect(rect, 2.0f, 2.0f, _trackStrokePaint);

                // Draw Track Name
                string name = track.RailmlTrack.Name ?? track.RailmlTrack.Id;
                if (!string.IsNullOrEmpty(name))
                {
                    // Center in track
                    _trackTextPaint.TextSize = 6.0f;
                    // Vertical center adjustment
                    canvas.DrawText(name, len / 2, 2.5f, _trackTextPaint);
                }
                
                canvas.Restore();
            }
        }

        private void RenderOccupancyOverlay(SKCanvas canvas, SimulationManager manager)
        {
             // Identify all occupied Track Names
             var occupiedNames = new HashSet<string>();
             foreach(var t in manager.Tracks.Values)
             {
                 if (t.IsOccupied && !string.IsNullOrEmpty(t.RailmlTrack.Name))
                 {
                     occupiedNames.Add(t.RailmlTrack.Name);
                 }
             }

             using (var p = new SKPaint { Color = SKColors.Gray.WithAlpha(178), Style = SKPaintStyle.Fill })
             {
                 foreach (var track in manager.Tracks.Values)
                 {
                     bool showGray = track.IsOccupied || 
                                     (!string.IsNullOrEmpty(track.RailmlTrack.Name) && occupiedNames.Contains(track.RailmlTrack.Name));

                     if (showGray)
                     {
                        float x1 = (float)track.StartScreenPos.X;
                        float y1 = (float)track.StartScreenPos.Y;
                        float x2 = (float)track.EndScreenPos.X;
                        float y2 = (float)track.EndScreenPos.Y;
                        
                        float dx = x2 - x1;
                        float dy = y2 - y1;
                        float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        
                        if (len < 0.001f) continue;
                        
                        float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);
                        
                        canvas.Save();
                        canvas.Translate(x1, y1);
                        canvas.RotateDegrees(angle);
                        
                        SKRect rect = new SKRect(0, -4, len, 4);
                        canvas.DrawRect(rect, p);
                        
                        canvas.Restore();
                     }
                 }
             }
        }

        private SKPaint _trainRectPaint = new SKPaint
        {
            Color = new SKColor(SKColors.Pink.Red, SKColors.Pink.Green, SKColors.Pink.Blue, 204), // 20% Transparency
            StrokeWidth = 2.0f, // 2m
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

                int iterations = 0;
                while (remainingLen > 0 && currentTrack != null && iterations < 10) // Safety break
                {
                    iterations++; 
                    
                    double drawLen = 0;
                    double segmentStart = 0;
                    double segmentEnd = 0;

                    if (currentDir == TrainDirection.Up)
                    {
                        double tailPos = currentHeadPos - remainingLen;
                        segmentStart = System.Math.Max(0, tailPos);
                        segmentEnd = System.Math.Min(currentTrack.Length, currentHeadPos);
                    }
                    else
                    {
                        double tailPos = currentHeadPos + remainingLen;
                        segmentStart = System.Math.Max(0, currentHeadPos);
                        segmentEnd = System.Math.Min(currentTrack.Length, tailPos);
                    }
                    
                    drawLen = segmentEnd - segmentStart;
                    if (drawLen < 0) drawLen = 0;

                    if (drawLen > 0.001)
                    {
                        float tx1 = (float)currentTrack.StartScreenPos.X;
                        float ty1 = (float)currentTrack.StartScreenPos.Y;
                        float tx2 = (float)currentTrack.EndScreenPos.X;
                        float ty2 = (float)currentTrack.EndScreenPos.Y;

                        float tStart = (float)(segmentStart / currentTrack.Length);
                        float tEnd = (float)(segmentEnd / currentTrack.Length);

                        float hx = tx1 + (tx2 - tx1) * tEnd;
                        float hy = ty1 + (ty2 - ty1) * tEnd;
                        
                        float tx = tx1 + (tx2 - tx1) * tStart;
                        float ty = ty1 + (ty2 - ty1) * tStart;

                        canvas.DrawLine(hx, hy, tx, ty, _trainRectPaint);
                    }

                    // Account for both the drawn part and the off-track part behind the head
                    double usedLen;
                    if (currentDir == TrainDirection.Up)
                        usedLen = System.Math.Min(remainingLen, currentHeadPos);
                    else
                        usedLen = System.Math.Min(remainingLen, currentTrack.Length - currentHeadPos);

                    remainingLen -= usedLen;

                    if (remainingLen <= 0.001) break;

                    var checkDir = (currentDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                    
                    if (manager.FindNextTrack(currentTrack, checkDir, out var prevTrack, out var entryPos, out var entryDir))
                    {
                        currentTrack = prevTrack;
                        currentHeadPos = entryPos;
                        currentDir = (entryDir == TrainDirection.Up) ? TrainDirection.Down : TrainDirection.Up;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        
        private void RenderSignals(SKCanvas canvas, SimulationManager manager)
        {
             foreach(var track in manager.Tracks.Values)
             {
                 if (track.RailmlTrack.OcsElements?.Signals?.SignalList != null)
                 {
                     float x1 = (float)track.StartScreenPos.X;
                     float y1 = (float)track.StartScreenPos.Y;
                     float x2 = (float)track.EndScreenPos.X;
                     float y2 = (float)track.EndScreenPos.Y;
                     
                     float dx = x2 - x1;
                     float dy = y2 - y1;
                     
                     if (System.Math.Abs(dx) < 0.001f && System.Math.Abs(dy) < 0.001f) continue;
                     
                     foreach(var sig in track.RailmlTrack.OcsElements.Signals.SignalList)
                     {
                         SimSignal simSig = null;
                         if (manager.Signals.TryGetValue(sig.Id, out simSig))
                         {
                              float renderX = 0;
                              float renderY = 0;
                              float offset = 12.0f; // Visual offset

                              if (sig.Dir == "down")
                              {
                                  renderX = x2;
                                  renderY = y2 + offset;
                              }
                              else if (sig.Dir == "up")
                              {
                                  renderX = x1;
                                  renderY = y1 - offset;
                              }
                              else
                              {
                                  continue;
                              }
                              
                              canvas.DrawCircle(renderX, renderY, 3.0f, simSig.Aspect == SignalAspect.Stop ? _signalRedPaint : _signalGreenPaint);
                              var sigName = sig.AdditionalName?.Name ?? sig.Id;
                              canvas.DrawText(sigName, renderX, renderY - 5, _trackTextPaint);
                         }
                     }
                 }
             }
        }

        private void RenderSwitches(SKCanvas canvas, SimulationManager manager)
        {
            foreach (var sw in manager.Switches.Values)
            {
                float x = (float)sw.ScreenPos.X;
                float y = (float)sw.ScreenPos.Y;

                if (x == 0 && y == 0) continue;

                SKPaint paint;
                if (sw.State == SimSwitch.SwitchState.Moving)
                {
                    paint = _switchMovingPaint;
                }
                else if (sw.CurrentCourse == sw.RailmlSwitch.NormalPosition)
                {
                    paint = _switchNormalPaint;
                }
                else
                {
                    paint = _switchReversePaint;
                }

                // Draw a triangle pointing up, located slightly below the point
                // Base at y+12, top at y+4
                using (var path = new SKPath())
                {
                    path.MoveTo(x, y + 4); // Top
                    path.LineTo(x - 4, y + 12); // Bottom Left
                    path.LineTo(x + 4, y + 12); // Bottom Right
                    path.Close();
                    
                    canvas.DrawPath(path, paint);
                    // Optional: Border
                    canvas.DrawPath(path, _trackStrokePaint);
                }

                // Draw Switch Name (ID)
                string swName = sw.RailmlSwitch.AdditionalName?.Name ?? "";
                string swLabel = string.IsNullOrEmpty(swName) ? sw.RailmlSwitch.Id : $"{swName}({sw.RailmlSwitch.Id})";
                canvas.DrawText(swLabel, x, y + 20, _trackTextPaint);
            }
        }
    }
}
