using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Railml.Simulation.Core;
using Railml.Simulation.Core.Models;

namespace Railml.Simulator.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private SimulationManager _simulationManager;
        private DispatcherTimer _timer;

        public SimulationManager SimulationManager => _simulationManager;
        public double CurrentTime => _simulationManager.CurrentTime;

        public MainViewModel()
        {
            // Initialize with dummy or load logic (to be added)
            // For now, empty or null manager until loaded
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33); // ~30 FPS
            _timer.Tick += OnTimerTick;
        }

        public void LoadSimulation(Railml.Simulation.Core.Models.Railml model, SimulationSettings settings)
        {
            _simulationManager = new SimulationManager(model.Infrastructure, settings);
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
                double dt = 0.033; // 33ms
                _simulationManager.RunUntil(_simulationManager.CurrentTime + dt);
                
                OnPropertyChanged(nameof(CurrentTime));
                // Trigger Redraw (View will handle this via InvalidateVisual or Binding)
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
