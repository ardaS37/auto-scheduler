using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using System;
using System.Collections.ObjectModel;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class ClassGroupEditViewModel : BaseViewModel
    {
        public ClassGroupEditViewModel(ProjectStore store, ClassGroup group)
        {
            Store = store;
            Group = group;
            Tracks = new ObservableCollection<ClassTrack>((ClassTrack[])Enum.GetValues(typeof(ClassTrack)));
            AddStudentCommand = new RelayCommand(AddStudent);
            RemoveStudentCommand = new RelayCommand(RemoveStudent, () => SelectedStudent != null);
        }

        public ProjectStore Store { get; }
        public ClassGroup Group { get; }
        public ObservableCollection<ClassTrack> Tracks { get; }
        public ObservableCollection<Student> Students => Group.Students;
        public RelayCommand AddStudentCommand { get; }
        public RelayCommand RemoveStudentCommand { get; }

        private Student _selectedStudent;
        public Student SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (Set(ref _selectedStudent, value))
                    RemoveStudentCommand.RaiseCanExecuteChanged();
            }
        }

        private void AddStudent()
        {
            var student = new Student { FirstName = "Yeni", LastName = "Öğrenci" };
            Students.Add(student);
            SelectedStudent = student;
        }

        private void RemoveStudent()
        {
            if (SelectedStudent == null)
                return;

            Students.Remove(SelectedStudent);
            SelectedStudent = null;
        }
    }
}
