using AutoScheduler.UI.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoScheduler.UI.Views
{
    public partial class TemplateView : UserControl
    {
        public TemplateView()
        {
            InitializeComponent();
        }

        private void GroupsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as TemplateViewModel;
            if (vm == null || vm.SelectedGroup == null) return;

            var win = new ClassGroupEditWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                DataContext = new ClassGroupEditViewModel(vm.Store, vm.SelectedGroup)
            };

            win.ShowDialog();
        }
    }
}
