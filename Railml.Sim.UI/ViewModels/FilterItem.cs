using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Railml.Sim.UI.ViewModels
{
    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string Name { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    CheckedChanged?.Invoke(this, System.EventArgs.Empty);
                }
            }
        }

        public event System.EventHandler CheckedChanged;

        public FilterItem(string name)
        {
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
