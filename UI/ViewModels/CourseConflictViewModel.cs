using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using System.Collections.ObjectModel;
using System.Linq;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class CourseConflictViewModel : BaseViewModel
    {
        private CourseConflictPair _selectedPair;

        public CourseConflictViewModel(ProjectStore store)
        {
            Store = store;
            AddPairCommand = new RelayCommand(AddPair, () => Store.Courses.Count >= 2 && !Store.IsBusy);
            RemovePairCommand = new RelayCommand(RemovePair, () => SelectedPair != null && !Store.IsBusy);
        }

        public ProjectStore Store { get; }
        public ObservableCollection<Course> Courses => Store.Courses;
        public ObservableCollection<CourseConflictPair> Pairs => Store.CourseConflictPairs;
        public RelayCommand AddPairCommand { get; }
        public RelayCommand RemovePairCommand { get; }

        public CourseConflictPair SelectedPair
        {
            get => _selectedPair;
            set
            {
                if (Set(ref _selectedPair, value))
                    RemovePairCommand.RaiseCanExecuteChanged();
            }
        }

        private void AddPair()
        {
            var firstCourse = Courses.FirstOrDefault();
            var secondCourse = Courses.Skip(1).FirstOrDefault();
            if (firstCourse == null || secondCourse == null)
                return;

            var pair = new CourseConflictPair
            {
                FirstCourse = firstCourse,
                SecondCourse = secondCourse
            };
            Pairs.Add(pair);
            SelectedPair = pair;
        }

        private void RemovePair()
        {
            if (SelectedPair == null)
                return;

            Pairs.Remove(SelectedPair);
            SelectedPair = null;
        }
    }
}
