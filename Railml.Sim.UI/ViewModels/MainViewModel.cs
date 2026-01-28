using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows.Data;
using Railml.Sim.Core;
using Railml.Sim.Core.Models;

namespace Railml.Sim.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private SimulationManager _simulationManager;
        private DispatcherTimer _timer;

        public System.Collections.ObjectModel.ObservableCollection<Railml.Sim.UI.Models.LogEntry> LogEntries { get; } = new System.Collections.ObjectModel.ObservableCollection<Railml.Sim.UI.Models.LogEntry>();
        public System.Collections.ObjectModel.ObservableCollection<FilterItem> MessageFilters { get; } = new System.Collections.ObjectModel.ObservableCollection<FilterItem>();
        public ICollectionView LogView { get; private set; }



        private bool _isLogVisible = true;
        public bool IsLogVisible
        {
            get => _isLogVisible;
            set
            {
                if (_isLogVisible != value)
                {
                    _isLogVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public ExplorerViewModel Explorer { get; private set; }
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

            // Initialize CollectionView
            LogView = CollectionViewSource.GetDefaultView(LogEntries);
            LogView.Filter = FilterLog;
            LogView = CollectionViewSource.GetDefaultView(LogEntries);
            LogView.Filter = FilterLog;

            ToggleSimulationCommand = new RelayCommand(ToggleSimulation);
        }

        private bool FilterLog(object item)
        {
            if (item is Railml.Sim.UI.Models.LogEntry entry)
            {
               foreach(var filter in MessageFilters)
               {
                   if (filter.Name == entry.Message)
                   {
                       return filter.IsChecked;
                   }
               }
               // If new message type appears before filter is added (async race), show it by default
               return true;
            }
            return true;
        }

        public void LoadSimulation(Railml.Sim.Core.Models.Railml model)
        {
            _simulationManager = new SimulationManager(model, CurrentSettings);
            
            // Subscribe to Log Events: processTime, executionTime, type, msg, info
            _simulationManager.EventQueue.OnLog += (pTime, eTime, type, msg, info) =>
            {
                // UI update on dispatcher
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    string timeStr = SimUtils.FormatTime(pTime);
                    string finalInfo = info;

                    if (type == "Enqueue")
                    {
                        // Prefix info with execution time in (hh:mm:ss.msec)
                        finalInfo = $"({SimUtils.FormatTime(eTime)}) {info}";
                    }

                    // Dynamic Filter Management
                    bool found = false;
                    foreach(var f in MessageFilters) { if (f.Name == msg) { found = true; break; } }
                    
                    if (!found)
                    {
                        var newItem = new FilterItem(msg);
                        newItem.CheckedChanged += (s, e) => LogView.Refresh();
                        MessageFilters.Add(newItem);
                    }

                    LogEntries.Add(new Railml.Sim.UI.Models.LogEntry(timeStr, type, msg, finalInfo));
                    
                    // Optional: Limit log size
                    if (LogEntries.Count > 1000) LogEntries.RemoveAt(0);
                });
            };

            _simulationManager.OnAccident += (info) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Stop(); // Stop UI timer and Manager
                    System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow, info, "Simulation Accident", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            };
            
            Explorer = new ExplorerViewModel(_simulationManager);
            OnPropertyChanged(nameof(Explorer));

            OnPropertyChanged(nameof(SimulationManager));
        }

        public void ClearLog()
        {
            LogEntries.Clear();
        }

        public void SaveLog(string filename)
        {
            try 
            {
                using (var writer = new System.IO.StreamWriter(filename))
                {
                    writer.WriteLine("Time\tType\tMessage\tInformation");
                    foreach(var entry in LogEntries)
                    {
                        writer.WriteLine($"{entry.Time}\t{entry.Type}\t{entry.Message}\t{entry.Information}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Can't show msgbox from VM easily without service, but ignoring or handling in View is better.
                // We'll throw or assume valid path.
                Console.WriteLine($"Error saving log: {ex.Message}");
            }
        }

        public void Start()
        {
            if (_simulationManager != null)
            {
                _simulationManager.Start();
                _timer.Start();
                OnPropertyChanged(nameof(IsSimulationRunning));
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _simulationManager?.Stop();
            OnPropertyChanged(nameof(IsSimulationRunning));
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

                // Update Explorer UI
                Explorer?.Update();

                // Trigger Redraw (View will handle this via InvalidateVisual or Binding)
            }
        }

        public int PendingEventCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public System.Windows.Input.ICommand ToggleSimulationCommand { get; }
        public bool IsSimulationRunning => _simulationManager != null && _simulationManager.IsRunning;

        private void ToggleSimulation()
        {
            if (IsSimulationRunning) Stop();
            else Start();
        }
    }
}
