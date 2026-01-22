using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;
using Microsoft.Win32;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;
using Railml.Sim.UI.Rendering;
using Railml.Sim.UI.ViewModels;
using SkiaSharp.Views.Desktop;

namespace Railml.Sim.UI
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private SimulationRenderer _renderer;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _renderer = new SimulationRenderer();
            
            DataContext = _viewModel;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentTime) || e.PropertyName == nameof(MainViewModel.SimulationManager))
                {
                    SkiaCanvas.InvalidateVisual();
                }
            };
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            _renderer.Render(e.Surface.Canvas, _viewModel.SimulationManager, e.Info.Width, e.Info.Height);
        }

        private void OnLoadClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "RailML Files (*.railml;*.xml)|*.railml;*.xml" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(Railml.Sim.Core.Models.Railml));
                    using (var fs = new FileStream(dlg.FileName, FileMode.Open))
                    {
                        var model = (Railml.Sim.Core.Models.Railml)serializer.Deserialize(fs);
                        _viewModel.LoadSimulation(model);
                        StatusText.Text = $"Loaded: {dlg.FileName}";
                        SkiaCanvas.InvalidateVisual();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}");
                }
            }
        }

        // Pan State
        private bool _isPanning = false;
        private Point _lastMousePos;

        private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(SkiaCanvas);
                SkiaCanvas.CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning)
            {
                var currentPos = e.GetPosition(SkiaCanvas);
                float dx = (float)(currentPos.X - _lastMousePos.X);
                float dy = (float)(currentPos.Y - _lastMousePos.Y);
                
                _renderer.Viewport.Pan(dx, dy);
                SkiaCanvas.InvalidateVisual();
                
                _lastMousePos = currentPos;
            }
        }

        private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                _isPanning = false;
                SkiaCanvas.ReleaseMouseCapture();
            }
        }

        private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                // Zoom
                float zoomFactor = 1.1f;
                if (e.Delta < 0) zoomFactor = 1.0f / 1.1f;

                var position = e.GetPosition(SkiaCanvas);
                float x = (float)position.X;
                float y = (float)position.Y;

                var currentZoom = _renderer.Viewport.Zoom;
                var newZoom = currentZoom * zoomFactor;

                _renderer.Viewport.SetZoom(newZoom, x, y);
                SkiaCanvas.InvalidateVisual();
            }
        }

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Start();
            StatusText.Text = "Simulation Running";
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Stop();
            StatusText.Text = "Simulation Stopped";
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // Edit global settings directly
            var dlg = new SettingsWindow(_viewModel.CurrentSettings);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                // Save settings
                _viewModel.CurrentSettings.Save("settings.json");
            }
        }
    }
}