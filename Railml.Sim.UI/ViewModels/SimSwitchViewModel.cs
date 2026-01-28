using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.UI.ViewModels
{
    public class SimSwitchViewModel : SimElementViewModel
    {
        private readonly SimSwitch _switch;



        public override string DisplayName
        {
            get
            {
                if (_switch == null) return "NullSwitch";
                Railml.Sim.Core.Models.Switch? sw = _switch.RailmlSwitch;
                if (sw == null) return "NullRailmlSwitch";
                
                // Debug step: Check if AdditionalName definition exists
                var an = sw.AdditionalName; 
                if (an == null) return sw.Id;

                // Explicitly access Name on the variable 'an'
                string? name = an.Name;
                return name ?? sw.Id;
            }
        }

        private string _status;
        public override string Status => _status;

        public SimSwitchViewModel(SimSwitch sw)
        {
            _switch = sw;
            _status = "Unknown";
        }

        public override void Update()
        {
            string newStatus = _switch.State.ToString();
            if (_status != newStatus)
            {
                _status = newStatus;
                OnPropertyChanged(nameof(Status));
            }
        }
    }
}
