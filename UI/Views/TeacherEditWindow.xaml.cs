using AutoScheduler.UI.ViewModels;
using Microsoft.Win32;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class TeacherEditWindow : Window
    {
        public TeacherEditWindow()
        {
            InitializeComponent();
        }

        private void OpenCoursePreferences_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeacherEditViewModel;
            if (vm == null) return;

            var win = new TeacherCoursePreferencesWindow
            {
                Owner = this,
                DataContext = vm
            };

            win.ShowDialog();
        }

        private void OpenDetailedAvailability_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeacherEditViewModel;
            if (vm == null) return;

            var win = new TeacherDetailedAvailabilityWindow
            {
                Owner = this,
                DataContext = vm
            };

            win.ShowDialog();
        }

        private void AddPhoto_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeacherEditViewModel;
            if (vm == null || vm.Teacher == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Resim Dosyaları|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tüm Dosyalar|*.*"
            };

            if (dlg.ShowDialog() == true)
                vm.Teacher.PhotoPath = dlg.FileName;
        }

        private void ClearPhoto_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as TeacherEditViewModel;
            if (vm == null || vm.Teacher == null) return;
            vm.Teacher.PhotoPath = null;
        }
    }
}
