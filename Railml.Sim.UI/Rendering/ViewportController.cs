using SkiaSharp;
using System;

namespace Railml.Sim.UI.Rendering
{
    public class ViewportController
    {
        public ObservableMatrix Matrix { get; private set; } = new ObservableMatrix();
        
        public float Zoom { get; private set; } = 1.0f;
        public float PanX { get; private set; } = 0.0f;
        public float PanY { get; private set; } = 0.0f;
        
        // Settings
        public bool InvertY { get; set; } = false;

        public ViewportController()
        {
            Reset();
        }

        public void Reset()
        {
            Zoom = 1.0f;
            PanX = 0.0f;
            PanY = 0.0f;
            UpdateMatrix();
        }

        public void SetZoom(float zoom, float centerX, float centerY)
        {
            // Limit Zoom
            if (zoom < 0.1f) zoom = 0.1f;
            if (zoom > 50.0f) zoom = 50.0f;
            
            // Calculate scale ratio
            float ratio = zoom / Zoom;
            
            // Adjust Pan so that (centerX, centerY) remains stationary
            // NewPan = OldPan + (Center - OldPan) * (1 - ratio) ??
            // Standard Formula: Translate(-C) * Scale(s) * Translate(C)
            // But we store Pan and Zoom separately for "Simple" Matrix reconstruction.
            // If we simply reconstruct matrix: Pan then Scale.
            // Matrix = Scale * Translate.
            // Let's rely on standard logic:
            // World = (Screen - Pan) / Zoom
            // NewWorld = World
            // (Screen - NewPan) / NewZoom = (Screen - Pan) / Zoom
            // Screen - NewPan = NewZoom/Zoom * (Screen - Pan)
            // Screen - NewPan = ratio * (Screen - Pan)
            // NewPan = Screen - ratio * (Screen - Pan)
            
            PanX = centerX - ratio * (centerX - PanX);
            PanY = centerY - ratio * (centerY - PanY);
            
            Zoom = zoom;
            UpdateMatrix();
        }
        
        public void Pan(float dx, float dy)
        {
            PanX += dx;
            PanY += dy;
            UpdateMatrix();
        }

        private void UpdateMatrix()
        {
            var mat = SKMatrix.CreateIdentity();
            
            // Apply Pan
            mat = mat.PostConcat(SKMatrix.CreateTranslation(PanX, PanY));
            
            // Apply Zoom
            // Invert Y if needed (RailML Y is usually Up, Screen Y is Down)
            float scaleY = InvertY ? -Zoom : Zoom;
            mat = mat.PostConcat(SKMatrix.CreateScale(Zoom, scaleY));

            Matrix.Value = mat;
        }

        public SKPoint WorldToScreen(SKPoint worldPos)
        {
            return Matrix.Value.MapPoint(worldPos);
        }
    }

    // Simple wrapper to notify changes if needed
    public class ObservableMatrix
    {
        public SKMatrix Value { get; set; } = SKMatrix.CreateIdentity();
    }
}
