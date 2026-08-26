using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using System.Collections.ObjectModel;
using System.Linq;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class MoveLessonViewModel : BaseViewModel
    {
        public ScheduleEntry Entry { get; }
        public ProjectStore Store { get; }
        public ObservableCollection<Day> Days { get { return Store.Days; } }
        public ObservableCollection<TimeSlot> Slots { get; } = new ObservableCollection<TimeSlot>();

        private Day _selectedDay;
        public Day SelectedDay
        {
            get { return _selectedDay; }
            set
            {
                if (Set(ref _selectedDay, value))
                    RefreshSlots();
            }
        }

        private TimeSlot _selectedSlot;
        public TimeSlot SelectedSlot
        {
            get { return _selectedSlot; }
            set { Set(ref _selectedSlot, value); }
        }

        public string LessonText
        {
            get
            {
                var group = Entry != null && Entry.Group != null ? Entry.Group.Name : "";
                var course = Entry != null && Entry.Course != null ? Entry.Course.Name : "";
                var teacher = Entry != null && Entry.Teacher != null ? Entry.Teacher.Name : "";
                return group + " - " + course + " - " + teacher;
            }
        }

        public MoveLessonViewModel(ProjectStore store, ScheduleEntry entry)
        {
            Store = store;
            Entry = entry;
            SelectedDay = entry != null ? entry.Day : store.Days.FirstOrDefault();
            SelectedSlot = Slots.FirstOrDefault(s => entry != null && s.Index == entry.SlotIndex) ?? Slots.FirstOrDefault();
        }

        private void RefreshSlots()
        {
            Slots.Clear();
            if (SelectedDay != null)
            {
                foreach (var slot in SelectedDay.Slots.OrderBy(s => s.Index))
                    Slots.Add(slot);
            }

            SelectedSlot = Slots.FirstOrDefault();
        }
    }
}
