using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.Core.Models
{
    public sealed class CourseKindSlotRule : BaseViewModel
    {
        private ClassGroup _group;
        public ClassGroup Group
        {
            get => _group;
            set => Set(ref _group, value);
        }

        private Day _day;
        public Day Day
        {
            get => _day;
            set => Set(ref _day, value);
        }

        private int _slotIndex;
        public int SlotIndex
        {
            get => _slotIndex;
            set => Set(ref _slotIndex, value);
        }

        private CourseKind _kind;
        public CourseKind Kind
        {
            get => _kind;
            set => Set(ref _kind, value);
        }
    }
}
