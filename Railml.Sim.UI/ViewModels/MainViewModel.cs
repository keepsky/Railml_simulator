using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;

namespace Railml.Sim.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private SimulationManager _simulationManager;
        private DispatcherTimer _timer;

        public SimulationManager SimulationManager => _simulationManager;
        public double CurrentTime => _simulationManager?.CurrentTime ?? 0.0;
        public SimulationSettings CurrentSettings { get; set; }
        
        private double _timeScale = 1.0;
        public double TimeScale
        {
            get => _timeScale;
            set
            {
                if (_timeScale != value)
                {
                    _timeScale = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainViewModel()
        {
            // Load settings on startup
            CurrentSettings = SimulationSettings.Load("settings.json");

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33); // ~30 FPS
            _timer.Tick += OnTimerTick;
        }

        public void LoadSimulation(Railml.Sim.Core.Models.Railml model)
        {
            _simulationManager = new SimulationManager(model, CurrentSettings);
            OnPropertyChanged(nameof(SimulationManager));
        }

        public void Start()
        {
            if (_simulationManager != null)
            {
                _simulationManager.Start();
                _timer.Start();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _simulationManager?.Stop();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_simulationManager != null && _simulationManager.IsRunning)
            {
                // Sync simulation time to wall clock or run as fast as possible?
                // Request said: "Usage defined unit time ... continuous movement".
                // If we want real-time visualization, we advance by DeltaTime.
                double dt = 0.033 * TimeScale; // Apply TimeScale
                _simulationManager.RunUntil(_simulationManager.CurrentTime + dt);
                
                OnPropertyChanged(nameof(CurrentTime));
                
                // Update Event Count
                PendingEventCount = _simulationManager.EventQueue.Count;
                OnPropertyChanged(nameof(PendingEventCount));

                // Trigger Redraw (View will handle this via InvalidateVisual or Binding)
            }
        }

        public int PendingEventCount { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
