using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class DutyDaySelectionItem : BaseViewModel
    {
        private readonly TeacherEditViewModel _owner;

        public Day Day { get; }

        public string Name => Day != null ? Day.Name : string.Empty;

        public bool IsSelected
        {
            get => _owner.IsDutyDay(Day);
            set
            {
                _owner.SetDutyDay(Day, value);
                OnPropertyChanged();
            }
        }

        public DutyDaySelectionItem(TeacherEditViewModel owner, Day day)
        {
            _owner = owner;
            Day = day;
        }
    }
}
