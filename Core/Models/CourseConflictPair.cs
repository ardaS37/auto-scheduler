using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.Core.Models
{
    // Üniversitede aynı öğrencinin alabileceği iki dersin aynı saat dilimine konmasını engeller.
    public sealed class CourseConflictPair : BaseViewModel
    {
        private Course _firstCourse;
        public Course FirstCourse
        {
            get => _firstCourse;
            set
            {
                if (Set(ref _firstCourse, value))
                    OnPropertyChanged(nameof(DisplayName));
            }
        }

        private Course _secondCourse;
        public Course SecondCourse
        {
            get => _secondCourse;
            set
            {
                if (Set(ref _secondCourse, value))
                    OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName => (FirstCourse?.Name ?? "Ders seçin") + " ↔ " + (SecondCourse?.Name ?? "Ders seçin");
    }
}
