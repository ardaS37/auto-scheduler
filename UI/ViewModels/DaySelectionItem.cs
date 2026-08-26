using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class DaySelectionItem : BaseViewModel
    {
        private readonly TeacherEditViewModel _owner;

        public Day Day { get; }

        public string Name => Day != null ? Day.Name : string.Empty;

        public bool IsSelected
        {
            get => _owner.IsDayUnavailable(Day);
            set
            {
                _owner.SetDayUnavailable(Day, value);
                OnPropertyChanged();
            }
        }

        public DaySelectionItem(TeacherEditViewModel owner, Day day)
        {
            _owner = owner;
            Day = day;
        }
    }
}
