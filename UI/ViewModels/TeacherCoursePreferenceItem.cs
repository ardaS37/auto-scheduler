using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class TeacherCoursePreferenceItem : BaseViewModel
    {
        private readonly TeacherEditViewModel _owner;

        public Course Course { get; }

        public string Code
        {
            get { return Course != null ? Course.Code : string.Empty; }
        }

        public string Name
        {
            get { return Course != null ? Course.Name : string.Empty; }
        }

        public TeacherCoursePreferenceItem(TeacherEditViewModel owner, Course course)
        {
            _owner = owner;
            Course = course;
        }

        public bool CanTeach
        {
            get { return _owner != null && _owner.IsCourseSelected(Course); }
            set
            {
                if (_owner == null) return;
                _owner.SetCourseSelected(Course, value);
                if (!value)
                {
                    _owner.SetCoursePreferred(Course, false);
                    _owner.SetCourseUnwanted(Course, false);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPreferred));
                OnPropertyChanged(nameof(IsUnwanted));
            }
        }

        public bool IsPreferred
        {
            get { return _owner != null && _owner.IsCoursePreferred(Course); }
            set
            {
                if (_owner == null) return;
                if (value && !CanTeach) CanTeach = true;
                _owner.SetCoursePreferred(Course, value);
                if (value) _owner.SetCourseUnwanted(Course, false);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsUnwanted));
            }
        }

        public bool IsUnwanted
        {
            get { return _owner != null && _owner.IsCourseUnwanted(Course); }
            set
            {
                if (_owner == null) return;
                if (value && !CanTeach) CanTeach = true;
                _owner.SetCourseUnwanted(Course, value);
                if (value) _owner.SetCoursePreferred(Course, false);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPreferred));
            }
        }
    }
}
