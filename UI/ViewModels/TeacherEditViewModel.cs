using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class TeacherEditViewModel : BaseViewModel
    {
        public Teacher Teacher { get; }
        public AutoScheduler.Core.Store.ProjectStore Store { get; }

        public ObservableCollection<Course> AllCourses { get; }
        public ObservableCollection<Day> AllDays { get; }

        public ObservableCollection<TeacherCoursePreferenceItem> DetailedCourseChoices { get; }
        public ObservableCollection<DaySelectionItem> DayChoices { get; }
        public ObservableCollection<DutyDaySelectionItem> DutyDayChoices { get; }

        public ObservableCollection<AcademicTitle> Titles { get; }
        public ObservableCollection<HalfDayAvailability> HalfDayChoices { get; }

        public TeacherEditViewModel(Teacher teacher, AutoScheduler.Core.Store.ProjectStore store, ObservableCollection<Course> allCourses, ObservableCollection<Day> allDays)
        {
            Teacher = teacher;
            Store = store;
            AllCourses = allCourses;
            AllDays = allDays;

            Titles = new ObservableCollection<AcademicTitle>(
                ((AcademicTitle[])Enum.GetValues(typeof(AcademicTitle))));

            HalfDayChoices = new ObservableCollection<HalfDayAvailability>(
                ((HalfDayAvailability[])Enum.GetValues(typeof(HalfDayAvailability))));

            DetailedCourseChoices = new ObservableCollection<TeacherCoursePreferenceItem>(
                allCourses.Select(c => new TeacherCoursePreferenceItem(this, c)));

            DayChoices = new ObservableCollection<DaySelectionItem>(
                (allDays ?? new ObservableCollection<Day>()).Select(d => new DaySelectionItem(this, d)));

            DutyDayChoices = new ObservableCollection<DutyDaySelectionItem>(
                (allDays ?? new ObservableCollection<Day>()).Select(d => new DutyDaySelectionItem(this, d)));
        }

        public bool IsCourseSelected(Course c) => Teacher.CanTeachCourses.Contains(c);

        public void SetCourseSelected(Course c, bool selected)
        {
            if (c == null) return;
            if (selected)
            {
                if (!Teacher.CanTeachCourses.Contains(c))
                    Teacher.CanTeachCourses.Add(c);
            }
            else
            {
                var existing = Teacher.CanTeachCourses.FirstOrDefault(x => x == c);
                if (existing != null)
                    Teacher.CanTeachCourses.Remove(existing);

                SetCoursePreferred(c, false);
                SetCourseUnwanted(c, false);
            }
        }

        public bool IsCoursePreferred(Course c)
        {
            if (c == null) return false;
            return Teacher.PreferredCourseNames.Contains(c.Name);
        }

        public void SetCoursePreferred(Course c, bool preferred)
        {
            if (c == null) return;

            if (preferred)
            {
                if (!Teacher.PreferredCourseNames.Contains(c.Name))
                    Teacher.PreferredCourseNames.Add(c.Name);
            }
            else
            {
                if (Teacher.PreferredCourseNames.Contains(c.Name))
                    Teacher.PreferredCourseNames.Remove(c.Name);
            }
        }

        public bool IsCourseUnwanted(Course c)
        {
            if (c == null) return false;
            return Teacher.UnwantedCourseNames.Contains(c.Name);
        }

        public void SetCourseUnwanted(Course c, bool unwanted)
        {
            if (c == null) return;

            if (unwanted)
            {
                if (!Teacher.UnwantedCourseNames.Contains(c.Name))
                    Teacher.UnwantedCourseNames.Add(c.Name);
            }
            else
            {
                if (Teacher.UnwantedCourseNames.Contains(c.Name))
                    Teacher.UnwantedCourseNames.Remove(c.Name);
            }
        }

        public bool IsDayUnavailable(Day d)
        {
            if (d == null) return false;
            return Teacher.UnavailableDayNames.Contains(d.Name);
        }

        public void SetDayUnavailable(Day d, bool unavailable)
        {
            if (d == null) return;

            if (unavailable)
            {
                if (!Teacher.UnavailableDayNames.Contains(d.Name))
                    Teacher.UnavailableDayNames.Add(d.Name);
            }
            else
            {
                if (Teacher.UnavailableDayNames.Contains(d.Name))
                    Teacher.UnavailableDayNames.Remove(d.Name);
            }
        }

        private static string MakeSlotKey(Day day, TimeSlot slot)
        {
            if (day == null || slot == null) return string.Empty;
            return day.Name + "|" + slot.Index;
        }

        public bool IsSlotUnavailable(Day day, TimeSlot slot)
        {
            var key = MakeSlotKey(day, slot);
            if (string.IsNullOrWhiteSpace(key)) return false;
            return Teacher.UnavailableSlotKeys.Contains(key);
        }

        public void SetSlotUnavailable(Day day, TimeSlot slot, bool unavailable)
        {
            var key = MakeSlotKey(day, slot);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (unavailable)
            {
                if (!Teacher.UnavailableSlotKeys.Contains(key))
                    Teacher.UnavailableSlotKeys.Add(key);
            }
            else
            {
                if (Teacher.UnavailableSlotKeys.Contains(key))
                    Teacher.UnavailableSlotKeys.Remove(key);
            }
        }

        public bool IsDutyDay(Day d)
        {
            if (d == null) return false;
            return Teacher.DutyDayNames.Contains(d.Name);
        }

        public void SetDutyDay(Day d, bool duty)
        {
            if (d == null) return;

            if (duty)
            {
                if (!Teacher.DutyDayNames.Contains(d.Name))
                    Teacher.DutyDayNames.Add(d.Name);
            }
            else
            {
                if (Teacher.DutyDayNames.Contains(d.Name))
                    Teacher.DutyDayNames.Remove(d.Name);
            }
        }
    }
}
