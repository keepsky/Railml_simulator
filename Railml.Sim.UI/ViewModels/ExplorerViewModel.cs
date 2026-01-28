using System.Collections.ObjectModel;
using System.Linq;
using Railml.Sim.Core;

namespace Railml.Sim.UI.ViewModels
{
    public class ExplorerViewModel : SimElementViewModel
    {
        private readonly SimulationManager _manager;

        public ObservableCollection<SimElementViewModel> Tracks { get; } = new ObservableCollection<SimElementViewModel>();
        public ObservableCollection<SimElementViewModel> Signals { get; } = new ObservableCollection<SimElementViewModel>();
        public ObservableCollection<SimElementViewModel> Switches { get; } = new ObservableCollection<SimElementViewModel>();

        // Tree Root Items
        public ObservableCollection<ExplorerCategory> Roots { get; } = new ObservableCollection<ExplorerCategory>();

        private SimElementViewModel _selectedElement;
        public SimElementViewModel SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (_selectedElement != value)
                {
                    _selectedElement = value;
                    OnPropertyChanged();
                }
            }
        }

        public override string DisplayName => "Explorer";
        public override string Status => "Ready";

        public ExplorerViewModel(SimulationManager manager)
        {
            _manager = manager;
            Initialize();
        }

        private void Initialize()
        {
            // Populate Tracks
            foreach (var track in _manager.Tracks.Values.OrderBy(t => t.RailmlTrack.Id))
            {
                Tracks.Add(new SimTrackViewModel(track));
            }
            Roots.Add(new ExplorerCategory("Tracks", Tracks));

            // Populate Signals
            foreach (var signal in _manager.Signals.Values.OrderBy(s => s.RailmlSignal.Id))
            {
                Signals.Add(new SimSignalViewModel(signal));
            }
            Roots.Add(new ExplorerCategory("Signals", Signals));

            // Populate Switches
            foreach (var sw in _manager.Switches.Values.OrderBy(s => s.RailmlSwitch.Id))
            {
                Switches.Add(new SimSwitchViewModel(sw));
            }
            Roots.Add(new ExplorerCategory("Switches", Switches));
        }

        public override void Update()
        {
            // Update all children
            foreach (var track in Tracks) track.Update();
            foreach (var signal in Signals) signal.Update();
            foreach (var sw in Switches) sw.Update();
        }
    }

    public class ExplorerCategory
    {
        public string Name { get; }
        public ObservableCollection<SimElementViewModel> Items { get; }

        public ExplorerCategory(string name, ObservableCollection<SimElementViewModel> items)
        {
            Name = name;
            Items = items;
        }
    }
}
