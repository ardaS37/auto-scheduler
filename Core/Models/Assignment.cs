using AutoScheduler.Core.Mvvm;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace AutoScheduler.Core.Models
{
    public sealed class Assignment : BaseViewModel
    {
        private ClassGroup _group;
        public ClassGroup Group
        {
            get => _group;
            set => Set(ref _group, value);
        }

        private Course _course;
        public Course Course
        {
            get => _course;
            set
            {
                if (Set(ref _course, value))
                {
                    Teacher = null; // ders değişince öğretmeni sıfırla
                    RefreshAvailableTeachers();
                }
            }
        }

        private Room _room;
        public Room Room
        {
            get => _room;
            set => Set(ref _room, value);
        }

        private Teacher _teacher;
        public Teacher Teacher
        {
            get => _teacher;
            set => Set(ref _teacher, value);
        }

        private int _weeklyHours;
        public int WeeklyHours
        {
            get => _weeklyHours;
            set => Set(ref _weeklyHours, value);
        }

        // K12: distribution rule. If true, try to spread this course across days.
        private bool _spreadAcrossDays;
        public bool SpreadAcrossDays
        {
            get => _spreadAcrossDays;
            set => Set(ref _spreadAcrossDays, value);
        }

        // K12: maximum hours for this course in a single day for this class.
        // 0 or less => unlimited.
        private int _maxPerDay;
        public int MaxPerDay
        {
            get => _maxPerDay;
            set => Set(ref _maxPerDay, value);
        }

        private int _blockSize = 1;
        public int BlockSize
        {
            get => _blockSize;
            set => Set(ref _blockSize, value);
        }

        // DIŞARIDAN SET EDİLECEK
        private ObservableCollection<Teacher> _teacherPool;
        public ObservableCollection<Teacher> TeacherPool
        {
            get => _teacherPool;
            set
            {
                if (ReferenceEquals(_teacherPool, value)) return;

                DetachTeacherPool(_teacherPool);
                _teacherPool = value;
                AttachTeacherPool(_teacherPool);
                OnPropertyChanged();
                RefreshAvailableTeachers();
            }
        }

        // Teacher combobox ItemsSource için STABLE koleksiyon
        public ObservableCollection<Teacher> AvailableTeachers { get; } =
            new ObservableCollection<Teacher>();

        private void RefreshAvailableTeachers()
        {
            AvailableTeachers.Clear();

            if (Course == null || TeacherPool == null) return;

            foreach (var t in TeacherPool.Where(t => t.CanTeachCourses.Contains(Course)))
                AvailableTeachers.Add(t);
        }

        private void AttachTeacherPool(ObservableCollection<Teacher> teacherPool)
        {
            if (teacherPool == null) return;

            teacherPool.CollectionChanged += TeacherPool_CollectionChanged;
            foreach (var teacher in teacherPool)
                teacher.CanTeachCourses.CollectionChanged += TeacherCourses_CollectionChanged;
        }

        private void DetachTeacherPool(ObservableCollection<Teacher> teacherPool)
        {
            if (teacherPool == null) return;

            teacherPool.CollectionChanged -= TeacherPool_CollectionChanged;
            foreach (var teacher in teacherPool)
                teacher.CanTeachCourses.CollectionChanged -= TeacherCourses_CollectionChanged;
        }

        private void TeacherPool_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Teacher teacher in e.OldItems)
                    teacher.CanTeachCourses.CollectionChanged -= TeacherCourses_CollectionChanged;
            }

            if (e.NewItems != null)
            {
                foreach (Teacher teacher in e.NewItems)
                    teacher.CanTeachCourses.CollectionChanged += TeacherCourses_CollectionChanged;
            }

            RefreshAvailableTeachers();
        }

        private void TeacherCourses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshAvailableTeachers();
        }
    }
}
