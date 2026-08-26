using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.Core.Models
{
    public enum CourseKind
    {
        Genel = 0,
        Sayisal = 1,
        Sozel = 2
    }

    public sealed class Course : BaseViewModel
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private string _code;
        public string Code
        {
            get => _code;
            set => Set(ref _code, value);
        }

        private bool _isPriority;
        public bool IsPriority
        {
            get => _isPriority;
            set => Set(ref _isPriority, value);
        }

        private int _priorityLevel = 3;
        public int PriorityLevel
        {
            get => _priorityLevel;
            set
            {
                var normalized = value;
                if (normalized < 1) normalized = 1;
                if (normalized > 5) normalized = 5;
                Set(ref _priorityLevel, normalized);
            }
        }

        private CourseKind _kind = CourseKind.Genel;
        public CourseKind Kind
        {
            get => _kind;
            set => Set(ref _kind, value);
        }

        public override string ToString() => string.IsNullOrWhiteSpace(Code) ? Name : Code + " - " + Name;
    }
}
