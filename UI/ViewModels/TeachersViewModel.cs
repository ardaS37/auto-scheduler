using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class TeachersViewModel : BaseViewModel
    {
        public TeachersViewModel(ProjectStore store)
        {
            Store = store;
            AddTeacherCommand = new RelayCommand(AddTeacher, () => !Store.IsBusy);
            RemoveTeacherCommand = new RelayCommand(RemoveTeacher, () => !Store.IsBusy && SelectedTeacher != null);
            SelectedTeacher = Teachers.FirstOrDefault();
        }

        public ProjectStore Store { get; }
        public ObservableCollection<Teacher> Teachers => Store.Teachers;

        public RelayCommand AddTeacherCommand { get; }
        public RelayCommand RemoveTeacherCommand { get; }

        private Teacher _selectedTeacher;
        public Teacher SelectedTeacher
        {
            get => _selectedTeacher;
            set
            {
                if (Set(ref _selectedTeacher, value))
                    RemoveTeacherCommand.RaiseCanExecuteChanged();
            }
        }

        public void AfterProjectLoaded()
        {
            SelectedTeacher = Teachers.FirstOrDefault();
        }

        private void AddTeacher()
        {
            var teacher = new Teacher { Name = "Yeni Hoca" };
            Store.Teachers.Add(teacher);
            SelectedTeacher = teacher;
        }

        private void RemoveTeacher()
        {
            if (SelectedTeacher == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedTeacher.Name}\" silinsin mi? Bu hocaya ait tüm atamalar da silinecek.",
                "Öğretmeni Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveTeacher(Store, SelectedTeacher);
            SelectedTeacher = Store.Teachers.FirstOrDefault();
        }
    }
}
