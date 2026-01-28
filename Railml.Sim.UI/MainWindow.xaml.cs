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
using Railml.Sim.UI.Configuration;

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

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var state = UIState.Load("ui_state.json");
            Top = state.WindowTop;
            Left = state.WindowLeft;
            Width = state.WindowWidth;
            Height = state.WindowHeight;
            _viewModel.IsLogVisible = state.IsLogVisible;
            _viewModel.TimeScale = state.TimeScale;
            LogRow.Height = new GridLength(state.LogHeight);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            var state = new UIState
            {
                WindowTop = Top,
                WindowLeft = Left,
                WindowWidth = Width,
                WindowHeight = Height,
                IsLogVisible = _viewModel.IsLogVisible,
                TimeScale = _viewModel.TimeScale,
                LogHeight = LogRow.Height.Value
            };
            state.Save("ui_state.json");
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

        private void OnClearLogClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLog();
        }

        private void OnSaveLogClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Log Files (*.txt)|*.txt", FileName = $"sim_log_{DateTime.Now:yyyyMMdd_HHmm}.txt" };
            if (dlg.ShowDialog() == true)
            {
                _viewModel.SaveLog(dlg.FileName);
            }
        }

        private void OnCopyLog(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (LogListView.SelectedItems.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var item in LogListView.SelectedItems)
                {
                    if (item is Railml.Sim.UI.Models.LogEntry entry)
                    {
                        sb.AppendLine($"{entry.Time}\t{entry.Type}\t{entry.Message}\t{entry.Information}");
                    }
                }
                if (sb.Length > 0)
                {
                    Clipboard.SetText(sb.ToString());
                }
            }
        }
    }
}