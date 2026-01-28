using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.UI.ViewModels
{
    public class SimSignalViewModel : SimElementViewModel
    {
        private readonly SimSignal _signal;



        public override string DisplayName
        {
            get
            {
                Railml.Sim.Core.Models.Signal? sig = _signal?.RailmlSignal;
                if (sig?.AdditionalName != null)
                {
                    return sig.AdditionalName.Name ?? sig.Id;
                }
                return sig?.Id ?? "UnknownId";
            }
        }
        public string Type => _signal.RailmlSignal.Type ?? "Unknown";

        private string _status;
        public override string Status => _status;

        public SimSignalViewModel(SimSignal signal)
        {
            _signal = signal;
            _status = "Unknown";
        }

        public override void Update()
        {
            string newStatus = _signal.Aspect.ToString();
            if (_status != newStatus)
            {
                _status = newStatus;
                OnPropertyChanged(nameof(Status));
            }
        }
    }
}
