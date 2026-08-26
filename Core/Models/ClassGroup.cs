using AutoScheduler.Core.Mvvm;
using System.Collections.ObjectModel;

namespace AutoScheduler.Core.Models
{
    public enum ClassTrack
    {
        Yok = 0,
        EsitAgirlik = 1,
        Sayisal = 2,
        Sozel = 3,
        Dil = 4
    }

    public sealed class ClassGroup : BaseViewModel
    {
        public ObservableCollection<Student> Students { get; } = new ObservableCollection<Student>();

        private bool _includeInExamShuffle = true;
        public bool IncludeInExamShuffle
        {
            get => _includeInExamShuffle;
            set => Set(ref _includeInExamShuffle, value);
        }

        private bool _useCustomSeatingLayout;
        public bool UseCustomSeatingLayout
        {
            get => _useCustomSeatingLayout;
            set => Set(ref _useCustomSeatingLayout, value);
        }

        private int _seatingRows = 5;
        public int SeatingRows
        {
            get => _seatingRows;
            set => Set(ref _seatingRows, NormalizeSeatingDimension(value));
        }

        private int _seatingColumns = 3;
        public int SeatingColumns
        {
            get => _seatingColumns;
            set => Set(ref _seatingColumns, NormalizeSeatingDimension(value));
        }

        private int _studentsPerDesk = 1;
        public int StudentsPerDesk
        {
            get => _studentsPerDesk;
            set
            {
                var normalized = value;
                if (normalized < 1) normalized = 1;
                if (normalized > 10) normalized = 10;
                Set(ref _studentsPerDesk, normalized);
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private bool _isPriority;
        public bool IsPriority
        {
            get => _isPriority;
            set => Set(ref _isPriority, value);
        }

        private ClassTrack _track = ClassTrack.Yok;
        public ClassTrack Track
        {
            get => _track;
            set => Set(ref _track, value);
        }

        private string _gradeLevel;
        public string GradeLevel
        {
            get => _gradeLevel;
            set => Set(ref _gradeLevel, value);
        }

        private string _branchCode;
        public string BranchCode
        {
            get => _branchCode;
            set => Set(ref _branchCode, value);
        }

        private string _roomName;
        public string RoomName
        {
            get => _roomName;
            set => Set(ref _roomName, value);
        }

        public override string ToString() => Name;

        private static int NormalizeSeatingDimension(int value)
        {
            if (value < 1) return 1;
            if (value > 50) return 50;
            return value;
        }
    }
}
