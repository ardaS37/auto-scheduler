using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class TemplateViewModel : BaseViewModel
    {
        public ProjectStore Store { get; }

        public ObservableCollection<Day> Days => Store.Days;


        private Day _selectedDay;
        public Day SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (Set(ref _selectedDay, value))
                {
                    // Day changed -> slot list changes; clear slot selection so commands refresh correctly
                    SelectedSlot = null;
                    OnPropertyChanged(nameof(SelectedGroupSlotRules));

                    (RemoveDayCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (AddSlotCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RemoveSlotCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddDayCommand { get; }
        public ICommand RemoveDayCommand { get; }
        public ICommand CopyDayCommand { get; }
        public ICommand AddSlotCommand { get; }
        public ICommand RemoveSlotCommand { get; }
        public ICommand ApplyStandardWeekTemplateCommand { get; }

        private TimeSlot _selectedSlot;
        public TimeSlot SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (Set(ref _selectedSlot, value))
                    (RemoveSlotCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Sınıf/Şube
        public ObservableCollection<ClassGroup> Groups => Store.Groups;
        public ObservableCollection<ClassTrack> ClassTracks { get; } =
            new ObservableCollection<ClassTrack>((ClassTrack[])Enum.GetValues(typeof(ClassTrack)));

        private ClassGroup _selectedGroup;
        public ClassGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                {
                    OnPropertyChanged(nameof(SelectedGroupSlotRules));
                    (RemoveGroupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddGroupCommand { get; }
        public ICommand RemoveGroupCommand { get; }

        // Seçili sınıf + seçili gün için slot izinleri (UI buraya bağlanacak)
        public IEnumerable<GroupSlotRule> SelectedGroupSlotRules
        {
            get
            {
                if (SelectedGroup == null || SelectedDay == null)
                    yield break;


                foreach (var slot in SelectedDay.Slots)
                {
                    var rule = Store.GroupSlotRules.FirstOrDefault(r =>
                        r.Group == SelectedGroup &&
                        r.Day == SelectedDay &&
                        r.SlotIndex == slot.Index);

                    if (rule == null)
                    {
                        rule = new GroupSlotRule
                        {
                            Group = SelectedGroup,
                            Day = SelectedDay,
                            SlotIndex = slot.Index,
                            IsAllowed = true
                        };
                        Store.GroupSlotRules.Add(rule);
                    }

                    yield return rule;
                }
            }
        }
        public void AfterProjectLoaded()
        {
            SelectedDay = Days.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedGroupSlotRules));
        }

        public TemplateViewModel(ProjectStore store)//constructor
        {
            Store = store;

            AddDayCommand = new RelayCommand(AddDay, () => !Store.IsBusy);
            // Keep buttons clickable; we already guard against nulls inside execute.
            // This also avoids cases where WPF doesn't requery CanExecute as expected.
            RemoveDayCommand = new RelayCommand(RemoveDay, () => !Store.IsBusy);
            CopyDayCommand = new RelayCommand(CopyDay, () => !Store.IsBusy);
            AddSlotCommand = new RelayCommand(AddSlot, () => !Store.IsBusy);
            RemoveSlotCommand = new RelayCommand(RemoveSlot, () => !Store.IsBusy);
            ApplyStandardWeekTemplateCommand = new RelayCommand(ApplyStandardWeekTemplate, () => !Store.IsBusy);

            AddGroupCommand = new RelayCommand(AddGroup, () => !Store.IsBusy);
            RemoveGroupCommand = new RelayCommand(RemoveGroup, () => !Store.IsBusy);



            // başlangıç günleri
            if (Store.Days.Count == 0)
            {
                Store.Days.Add(new Day { Name = "Pazartesi" });
                Store.Days.Add(new Day { Name = "Salı" });
                Store.Days.Add(new Day { Name = "Çarşamba" });
                Store.Days.Add(new Day { Name = "Perşembe" });
                Store.Days.Add(new Day { Name = "Cuma" });
            }

            SelectedDay = Store.Days.FirstOrDefault();

            // başlangıç grupları
            /*
            if (Store.Groups.Count == 0)
            {
                Store.Groups.Add(new ClassGroup { Name = "1A" });
                Store.Groups.Add(new ClassGroup { Name = "1B" });
                Store.Groups.Add(new ClassGroup { Name = "2A" });
            }
            SelectedGroup = Store.Groups.FirstOrDefault();
            */
            // ilk render'da izin tablosu boş kalmasın diye tetikle
            OnPropertyChanged(nameof(SelectedGroupSlotRules));
        }

        private void AddDay()
        {
            Days.Add(new Day { Name = "Yeni Gün" });
            SelectedDay = Days.LastOrDefault();
        }

        private void ApplyStandardWeekTemplate()
        {
            var confirm = MessageBox.Show(
                "Standart 5 gün / 8 ders saati şablonu uygulansın mı? Mevcut gün ve saat tanımları değiştirilecek.",
                "Standart Şablon",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            ProjectPresetService.ApplyStandardWeek(Store);
            SelectedDay = Store.Days.FirstOrDefault();
            SelectedSlot = SelectedDay != null ? SelectedDay.Slots.FirstOrDefault() : null;
            OnPropertyChanged(nameof(SelectedGroupSlotRules));
        }

        private void CopyDay()
        {
            var src = SelectedDay;
            if (src == null) return;

            // Create copy
            var copy = new Day { Name = $"{src.Name} (Kopya)" };
            foreach (var s in src.Slots)
            {
                copy.Slots.Add(new TimeSlot
                {
                    Index = s.Index,
                    Start = s.Start,
                    End = s.End,
                    Label = s.Label
                });
            }

            // Insert right after the source day for convenience
            var insertAt = Days.IndexOf(src);
            if (insertAt < 0) insertAt = Days.Count - 1;
            Days.Insert(insertAt + 1, copy);
            SelectedDay = copy;

            // Copy group-slot rules that reference this day
            var rulesToCopy = Store.GroupSlotRules
                .Where(r => r.Day == src)
                .ToList();

            foreach (var r in rulesToCopy)
            {
                Store.GroupSlotRules.Add(new GroupSlotRule
                {
                    Group = r.Group,
                    Day = copy,
                    SlotIndex = r.SlotIndex,
                    IsAllowed = r.IsAllowed
                });
            }

            OnPropertyChanged(nameof(SelectedGroupSlotRules));
        }

        private void RemoveDay()
        {
            if (SelectedDay == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedDay.Name}\" günü silinsin mi? Bu güne ait ders saatleri ve yerleşmiş dersler de silinecek.",
                "Günü Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveDay(Store, SelectedDay);
            SelectedDay = Days.FirstOrDefault();
        }

        private void AddSlot()
        {
            var day = SelectedDay;
            if (day == null) return;

            int nextIndex = day.Slots.Count + 1;

            // Default timing:
            // - If there is a previous slot, start = prev.Start + 50 minutes, end = start + 50 minutes
            // - Otherwise start at 09:00 for the first slot
            var start = new System.TimeSpan(9, 0, 0);

            var prev = day.Slots.OrderBy(s => s.Index).LastOrDefault();
            if (prev != null)
            {
                start = prev.Start.Add(new System.TimeSpan(0, 50, 0));
            }

            var end = start.Add(new System.TimeSpan(0, 50, 0));

            day.Slots.Add(new TimeSlot
            {
                Index = nextIndex,
                Start = start,
                End = end,
                Label = $"Ders {nextIndex}"
            });

            SelectedSlot = day.Slots.LastOrDefault();
            OnPropertyChanged(nameof(SelectedGroupSlotRules)); // slot listesi değişti
        }

        private void RemoveSlot()
        {
            var day = SelectedDay;
            var slot = SelectedSlot;
            if (day == null || slot == null) return;

            var confirm = MessageBox.Show(
                $"{slot.Index}. ders saati silinsin mi? Bu saate yerleşmiş dersler de silinecek.",
                "Ders Saatini Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var slots = day.Slots;
            var removedIndex = slots.IndexOf(slot);

            ProjectCleanupService.RemoveSlot(Store, day, slot);

            // Auto-select: prefer the previous item; if none, select the new item at the same index (which becomes the next).
            if (slots.Count == 0)
            {
                SelectedSlot = null;
            }
            else
            {
                var nextIndex = removedIndex - 1;
                if (nextIndex < 0) nextIndex = 0;
                if (nextIndex >= slots.Count) nextIndex = slots.Count - 1;

                SelectedSlot = slots[nextIndex];
            }

            OnPropertyChanged(nameof(SelectedGroupSlotRules));
        }

        private void AddGroup()
        {
            Store.Groups.Add(new ClassGroup { Name = "Yeni Sınıf" });
            SelectedGroup = Store.Groups.LastOrDefault();
        }

        private void RemoveGroup()
        {
            if (SelectedGroup == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedGroup.Name}\" silinsin mi? Bu sınıfa ait tüm atamalar da silinecek.",
                "Sınıfı Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveGroup(Store, SelectedGroup);
            SelectedGroup = Store.Groups.FirstOrDefault();
        }
    }
}
