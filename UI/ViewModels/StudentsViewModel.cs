using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using System.Collections.ObjectModel;
using System.Linq;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class StudentsViewModel : BaseViewModel
    {
        private ClassGroup _selectedGroup;
        private Student _selectedStudent;

        public StudentsViewModel(ProjectStore store)
        {
            Store = store;
            AddStudentCommand = new RelayCommand(AddStudent, () => !Store.IsBusy && SelectedGroup != null);
            RemoveStudentCommand = new RelayCommand(RemoveStudent, () => !Store.IsBusy && SelectedStudent != null);
            Store.Groups.CollectionChanged += (sender, e) =>
            {
                if (SelectedGroup == null || !Store.Groups.Contains(SelectedGroup))
                    SelectedGroup = Store.Groups.FirstOrDefault();
                OnPropertyChanged(nameof(Groups));
            };
            SelectedGroup = Store.Groups.FirstOrDefault();
        }

        public ProjectStore Store { get; }
        public ObservableCollection<ClassGroup> Groups => Store.Groups;
        public ObservableCollection<Student> Students => SelectedGroup?.Students;
        public RelayCommand AddStudentCommand { get; }
        public RelayCommand RemoveStudentCommand { get; }

        public ClassGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                {
                    SelectedStudent = null;
                    OnPropertyChanged(nameof(Students));
                    OnPropertyChanged(nameof(StudentCount));
                    AddStudentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public Student SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (Set(ref _selectedStudent, value))
                    RemoveStudentCommand.RaiseCanExecuteChanged();
            }
        }

        public int StudentCount => Students?.Count ?? 0;

        private void AddStudent()
        {
            if (SelectedGroup == null)
                return;

            var student = new Student { FirstName = "Yeni", LastName = "Öğrenci" };
            SelectedGroup.Students.Add(student);
            SelectedStudent = student;
            OnPropertyChanged(nameof(StudentCount));
        }

        private void RemoveStudent()
        {
            if (SelectedGroup == null || SelectedStudent == null)
                return;

            SelectedGroup.Students.Remove(SelectedStudent);
            SelectedStudent = null;
            OnPropertyChanged(nameof(StudentCount));
        }
    }
}
