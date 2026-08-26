using AutoScheduler.UI.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace AutoScheduler.UI.Views
{
    public partial class TeachersView : UserControl
    {
        public TeachersView()
        {
            InitializeComponent();
        }

        private void TeachersList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenTeacherDetails();
        }

        private void OpenTeacher_Click(object sender, RoutedEventArgs e)
        {
            OpenTeacherDetails();
        }

        private void OpenTeacherDetails()
        {
            var vm = DataContext as TeachersViewModel;
            if (vm == null || vm.SelectedTeacher == null) return;

            var win = new TeacherEditWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = new TeacherEditViewModel(vm.SelectedTeacher, vm.Store, vm.Store.Courses, vm.Store.Days)
            };

            win.ShowDialog();
        }

        private void AddPhoto_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeachersViewModel;
            if (vm == null || vm.SelectedTeacher == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Resim Dosyaları|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tüm Dosyalar|*.*"
            };

            if (dlg.ShowDialog() == true)
                vm.SelectedTeacher.PhotoPath = dlg.FileName;
        }

        private void ClearPhoto_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeachersViewModel;
            if (vm == null || vm.SelectedTeacher == null) return;
            vm.SelectedTeacher.PhotoPath = null;
        }
    }
}
