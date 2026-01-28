using Railml.Sim.Core.SimObjects;

namespace Railml.Sim.UI.ViewModels
{
    public class SimTrackViewModel : SimElementViewModel
    {
        private readonly SimTrack _track;

        public SimTrackViewModel(SimTrack track)
        {
            _track = track;
            _status = "Unknown";
        }

        public string Id => _track.RailmlTrack.Id;
        public override string DisplayName => _track.RailmlTrack.Name ?? Id;
        public double Length => _track.Length;

        private string _status;
        public override string Status => _status;

        public override void Update()
        {
            // Logic: if OccupyingTrains > 0 -> Occupied
            string newStatus = (_track.OccupyingTrains.Count > 0) ? "Occupied" : "Unoccupied";
            if (_status != newStatus)
            {
                _status = newStatus;
                OnPropertyChanged(nameof(Status));
            }
        }
    }
}
