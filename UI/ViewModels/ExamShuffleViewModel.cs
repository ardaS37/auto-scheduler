using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class ExamShuffleViewModel : BaseViewModel
    {
        private ClassGroup _selectedGroup;
        private string _generationSummary = "Katılacak sınıfları seçip Karıştır düğmesine basın.";

        public ExamShuffleViewModel(ProjectStore store)
        {
            Store = store;
            ApplyDefaultLayoutToAllCommand = new RelayCommand(ApplyDefaultLayoutToAll, () => !Store.IsBusy && Store.Groups.Count > 0);
            GenerateCommand = new RelayCommand(Generate, () => !Store.IsBusy && Store.Groups.Count(group => group.IncludeInExamShuffle) >= 2);
            Store.PropertyChanged += Store_PropertyChanged;
            Store.Groups.CollectionChanged += (sender, e) =>
            {
                if (SelectedGroup == null || !Store.Groups.Contains(SelectedGroup))
                    SelectedGroup = Store.Groups.FirstOrDefault();
                OnPropertyChanged(nameof(Groups));
            };
            SelectedGroup = Store.Groups.FirstOrDefault();
        }

        public ProjectStore Store { get; }
        public ObservableCollection<ClassGroup> Groups => Store.Groups;
        public ObservableCollection<ExamNeighborRuleMode> NeighborRuleModes { get; } =
            new ObservableCollection<ExamNeighborRuleMode>((ExamNeighborRuleMode[])Enum.GetValues(typeof(ExamNeighborRuleMode)));
        public RelayCommand ApplyDefaultLayoutToAllCommand { get; }
        public RelayCommand GenerateCommand { get; }
        public ObservableCollection<ExamSeatAssignment> Assignments { get; } = new ObservableCollection<ExamSeatAssignment>();
        public ObservableCollection<string> Warnings { get; } = new ObservableCollection<string>();

        public string GenerationSummary
        {
            get => _generationSummary;
            private set => Set(ref _generationSummary, value);
        }

        public ClassGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != null)
                    _selectedGroup.PropertyChanged -= SelectedGroup_PropertyChanged;

                if (Set(ref _selectedGroup, value))
                {
                    if (_selectedGroup != null)
                        _selectedGroup.PropertyChanged += SelectedGroup_PropertyChanged;
                    RefreshPreview();
                }
            }
        }

        public int EffectiveRows => GetRows(SelectedGroup);
        public int EffectiveColumns => GetColumns(SelectedGroup);
        public int EffectiveStudentsPerDesk => GetStudentsPerDesk(SelectedGroup);
        public int EffectiveDeskCount => EffectiveRows * EffectiveColumns;
        public int EffectiveCapacity => EffectiveDeskCount * EffectiveStudentsPerDesk;
        public ObservableCollection<DeskPreviewItem> DeskPreview { get; } = new ObservableCollection<DeskPreviewItem>();

        private void ApplyDefaultLayoutToAll()
        {
            foreach (var group in Store.Groups)
            {
                group.UseCustomSeatingLayout = true;
                group.SeatingRows = Store.DefaultSeatingRows;
                group.SeatingColumns = Store.DefaultSeatingColumns;
                group.StudentsPerDesk = Store.DefaultStudentsPerDesk;
            }
            RefreshPreview();
        }

        private void Generate()
        {
            Assignments.Clear();
            Warnings.Clear();

            var participatingGroups = Store.Groups.Where(group => group.IncludeInExamShuffle).ToList();
            var students = participatingGroups
                .SelectMany(group => group.Students.Select(student => new StudentSource { Student = student, HomeGroup = group }))
                .Where(source => source.Student != null && !string.IsNullOrWhiteSpace(source.Student.FirstName))
                .ToList();
            var seats = CreateSeats(participatingGroups);

            if (students.Count == 0)
            {
                GenerationSummary = "Katılan sınıflarda öğrenci bulunamadı.";
                return;
            }

            if (seats.Count < students.Count)
                Warnings.Add("Toplam kapasite yetersiz: " + students.Count + " öğrenci için " + seats.Count + " yer var.");

            var best = new List<PlacedStudent>();
            var bestUnplaced = students;
            for (var attempt = 0; attempt < 160; attempt++)
            {
                var random = new Random(Environment.TickCount + attempt * 7919);
                var placed = PlaceStudents(students, seats, random);
                if (placed.Count > best.Count)
                {
                    best = placed;
                    var placedStudents = new HashSet<Student>(placed.Select(item => item.Source.Student));
                    bestUnplaced = students.Where(source => !placedStudents.Contains(source.Student)).ToList();
                    if (best.Count == students.Count)
                        break;
                }
            }

            foreach (var placed in best.OrderBy(item => item.Seat.HostGroup.Name).ThenBy(item => item.Seat.Row).ThenBy(item => item.Seat.Column).ThenBy(item => item.Seat.PlaceIndex))
            {
                Assignments.Add(new ExamSeatAssignment
                {
                    Room = string.IsNullOrWhiteSpace(placed.Seat.HostGroup.RoomName) ? placed.Seat.HostGroup.Name : placed.Seat.HostGroup.RoomName,
                    Seat = (placed.Seat.Row + 1) + ". satır / " + (placed.Seat.Column + 1) + ". sütun / " + (placed.Seat.PlaceIndex + 1) + ". yer",
                    FirstName = placed.Source.Student.FirstName,
                    LastName = placed.Source.Student.LastName,
                    StudentNumber = placed.Source.Student.StudentNumber,
                    ClassName = placed.Source.HomeGroup.Name,
                    GradeLevel = placed.Source.HomeGroup.GradeLevel
                });
            }

            if (bestUnplaced.Count > 0)
                Warnings.Add(bestUnplaced.Count + " öğrenci seçili kurallar ve kapasiteyle yerleştirilemedi.");

            GenerationSummary = best.Count + " / " + students.Count + " öğrenci yerleştirildi. " + participatingGroups.Count + " sınıf/salon kullanıldı.";
        }

        private List<PlacedStudent> PlaceStudents(List<StudentSource> students, List<SeatPosition> seats, Random random)
        {
            var placements = new List<PlacedStudent>();
            var order = students.OrderBy(source => random.Next()).ToList();
            foreach (var source in order)
            {
                var candidates = seats
                    .Where(seat => !placements.Any(placed => placed.Seat == seat))
                    .Where(seat => CanPlace(source, seat, placements))
                    .OrderBy(seat => random.Next())
                    .ToList();
                if (candidates.Count > 0)
                    placements.Add(new PlacedStudent { Source = source, Seat = candidates[0] });
            }
            return placements;
        }

        private bool CanPlace(StudentSource source, SeatPosition seat, List<PlacedStudent> placements)
        {
            if (Store.ExamPreventOwnClassRoom && seat.HostGroup == source.HomeGroup)
                return false;

            if (!Store.ExamPreventSameGradeNeighbors || string.IsNullOrWhiteSpace(source.HomeGroup.GradeLevel))
                return true;

            foreach (var placed in placements)
            {
                if (placed.Seat.HostGroup != seat.HostGroup || !SameGrade(source.HomeGroup, placed.Source.HomeGroup))
                    continue;
                if (AreNeighbors(seat, placed.Seat))
                    return false;
            }
            return true;
        }

        private bool AreNeighbors(SeatPosition first, SeatPosition second)
        {
            var rowDistance = Math.Abs(first.Row - second.Row);
            var columnDistance = Math.Abs(first.Column - second.Column);
            if (rowDistance == 0 && columnDistance == 0)
                return first.PlaceIndex != second.PlaceIndex;

            switch (Store.ExamNeighborRuleMode)
            {
                case ExamNeighborRuleMode.SadeceYan:
                    return rowDistance == 0 && columnDistance == 1;
                case ExamNeighborRuleMode.YanOnArka:
                    return rowDistance + columnDistance == 1;
                default:
                    return rowDistance <= 1 && columnDistance <= 1;
            }
        }

        private static bool SameGrade(ClassGroup first, ClassGroup second)
        {
            return !string.IsNullOrWhiteSpace(first.GradeLevel) &&
                   string.Equals(first.GradeLevel.Trim(), second.GradeLevel?.Trim(), StringComparison.CurrentCultureIgnoreCase);
        }

        private List<SeatPosition> CreateSeats(IEnumerable<ClassGroup> groups)
        {
            var seats = new List<SeatPosition>();
            foreach (var group in groups)
            {
                for (var row = 0; row < GetRows(group); row++)
                for (var column = 0; column < GetColumns(group); column++)
                for (var place = 0; place < GetStudentsPerDesk(group); place++)
                    seats.Add(new SeatPosition { HostGroup = group, Row = row, Column = column, PlaceIndex = place });
            }
            return seats;
        }

        private int GetRows(ClassGroup group) => group != null && group.UseCustomSeatingLayout ? group.SeatingRows : Store.DefaultSeatingRows;
        private int GetColumns(ClassGroup group) => group != null && group.UseCustomSeatingLayout ? group.SeatingColumns : Store.DefaultSeatingColumns;
        private int GetStudentsPerDesk(ClassGroup group) => group != null && group.UseCustomSeatingLayout ? group.StudentsPerDesk : Store.DefaultStudentsPerDesk;

        private void Store_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectStore.DefaultSeatingRows) || e.PropertyName == nameof(ProjectStore.DefaultSeatingColumns) || e.PropertyName == nameof(ProjectStore.DefaultStudentsPerDesk))
                RefreshPreview();
        }

        private void SelectedGroup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClassGroup.UseCustomSeatingLayout) || e.PropertyName == nameof(ClassGroup.SeatingRows) || e.PropertyName == nameof(ClassGroup.SeatingColumns) || e.PropertyName == nameof(ClassGroup.StudentsPerDesk))
                RefreshPreview();
        }

        private void RefreshPreview()
        {
            OnPropertyChanged(nameof(EffectiveRows));
            OnPropertyChanged(nameof(EffectiveColumns));
            OnPropertyChanged(nameof(EffectiveStudentsPerDesk));
            OnPropertyChanged(nameof(EffectiveDeskCount));
            OnPropertyChanged(nameof(EffectiveCapacity));
            DeskPreview.Clear();
            for (var i = 1; i <= EffectiveDeskCount; i++)
                DeskPreview.Add(new DeskPreviewItem { DeskNumber = i, StudentsPerDesk = EffectiveStudentsPerDesk });
        }

        private sealed class StudentSource { public Student Student { get; set; } public ClassGroup HomeGroup { get; set; } }
        private sealed class SeatPosition { public ClassGroup HostGroup { get; set; } public int Row { get; set; } public int Column { get; set; } public int PlaceIndex { get; set; } }
        private sealed class PlacedStudent { public StudentSource Source { get; set; } public SeatPosition Seat { get; set; } }
        public sealed class DeskPreviewItem { public int DeskNumber { get; set; } public int StudentsPerDesk { get; set; } }
        public sealed class ExamSeatAssignment { public string Room { get; set; } public string Seat { get; set; } public string FirstName { get; set; } public string LastName { get; set; } public string StudentNumber { get; set; } public string ClassName { get; set; } public string GradeLevel { get; set; } }
    }
}
