using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.Core.Models
{
    // A fixed placement request: force a specific class/course/teacher into a specific day+slot.
    public sealed class FixedLesson : BaseViewModel
    {
        private ClassGroup _group;
        public ClassGroup Group
        {
            get { return _group; }
            set { Set(ref _group, value); }
        }

        private Day _day;
        public Day Day
        {
            get { return _day; }
            set { Set(ref _day, value); }
        }

        private int _slotIndex;
        public int SlotIndex
        {
            get { return _slotIndex; }
            set { Set(ref _slotIndex, value); }
        }

        private Course _course;
        public Course Course
        {
            get { return _course; }
            set { Set(ref _course, value); }
        }

        private Teacher _teacher;
        public Teacher Teacher
        {
            get { return _teacher; }
            set { Set(ref _teacher, value); }
        }

        private Room _room;
        public Room Room
        {
            get { return _room; }
            set { Set(ref _room, value); }
        }

        private int _blockSize = 1;
        public int BlockSize
        {
            get { return _blockSize; }
            set { Set(ref _blockSize, value); }
        }

        public override string ToString()
        {
            var g = Group != null ? Group.Name : "";
            var d = Day != null ? Day.Name : "";
            var c = Course != null ? Course.Name : "";
            return g + " | " + d + " " + SlotIndex + " | " + c;
        }
    }
}
