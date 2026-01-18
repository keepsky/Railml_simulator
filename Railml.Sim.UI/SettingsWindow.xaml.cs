using System.Windows;
using Railml.Sim.Core;

namespace Railml.Sim.UI
{
    public partial class SettingsWindow : Window
    {
        public SimulationSettings Settings { get; private set; }

        public SettingsWindow(SimulationSettings settings)
        {
            InitializeComponent();
            // Clone settings to support cancel? 
            // For simplicity, we bind directly. To support cancel properly, we would need to clone.
            // Let's rely on the caller to pass a copy or reference. 
            // Given the user request, direct editing is likely fine, but standard is clone.
            // We'll create a simple copy logic here or just bind to the passed object.
            // Let's explicitly set DataContext to the passed object.
            Settings = settings;
            DataContext = Settings;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
