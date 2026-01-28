using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Railml.Sim.UI.ViewModels
{
    public abstract class SimElementViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public abstract string DisplayName { get; }
        public abstract string Status { get; }
        
        // Called by Main Loop to refresh state
        public abstract void Update();
    }
}
