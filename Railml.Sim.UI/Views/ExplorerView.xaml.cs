using System.Windows;
using System.Windows.Controls;
using Railml.Sim.UI.ViewModels;

namespace Railml.Sim.UI.Views
{
    public partial class ExplorerView : UserControl
    {
        public ExplorerView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ExplorerViewModel vm && e.NewValue is SimElementViewModel element)
            {
                vm.SelectedElement = element;
            }
        }
    }
}
