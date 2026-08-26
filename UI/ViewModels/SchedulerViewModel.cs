using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class SchedulerViewModel : BaseViewModel
    {
        public ProjectStore Store { get; }

        private readonly Dictionary<string, string> _cellTextOverrides = new Dictionary<string, string>();

        public event EventHandler ScheduleChanged;

        public ObservableCollection<ScheduleEntry> Schedule => Store.Schedule;

        public ObservableCollection<string> Warnings { get; } =
            new ObservableCollection<string>();

        public ObservableCollection<TimeSlot> SlotHeaders { get; } =
            new ObservableCollection<TimeSlot>();

        public ObservableCollection<WeeklyDayRow> WeeklyRows { get; } =
            new ObservableCollection<WeeklyDayRow>();

        public ObservableCollection<RelaxationRuleItem> RelaxationRules { get; } =
            new ObservableCollection<RelaxationRuleItem>();

        public RelayCommand GenerateCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand GenerateAlternativeCommand { get; }
        public RelayCommand PreviousAlternativeCommand { get; }
        public RelayCommand NextAlternativeCommand { get; }
        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }

        private readonly List<AlternativeSchedule> _alternatives = new List<AlternativeSchedule>();
        private int _currentAlternativeIndex = -1;

        private const int MaxUndoDepth = 30;
        private readonly List<List<ScheduleEntry>> _undoStack = new List<List<ScheduleEntry>>();
        private readonly List<List<ScheduleEntry>> _redoStack = new List<List<ScheduleEntry>>();

        public ObservableCollection<ClassGroup> Groups => Store.Groups;

        private ClassGroup _selectedGroup;
        public ClassGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                    RefreshWeeklyGrid();
            }
        }

        public SchedulerViewModel(ProjectStore store)
        {
            Store = store;

            GenerateCommand = new RelayCommand(Generate, () => !IsGenerating);
            ClearCommand = new RelayCommand(Clear, () => !IsGenerating);
            GenerateAlternativeCommand = new RelayCommand(GenerateAlternative, () => !IsGenerating);
            PreviousAlternativeCommand = new RelayCommand(PreviousAlternative, () => !IsGenerating && _alternatives.Count > 1);
            NextAlternativeCommand = new RelayCommand(NextAlternative, () => !IsGenerating && _alternatives.Count > 1);
            UndoCommand = new RelayCommand(Undo, () => !IsGenerating && _undoStack.Count > 0);
            RedoCommand = new RelayCommand(Redo, () => !IsGenerating && _redoStack.Count > 0);

            LoadRelaxationRules();
            SelectedGroup = Store.Groups.FirstOrDefault();
            RefreshWeeklyGrid();
            FriendlyWarningSummary = "Henüz program üretilmedi.";
            GenerationRecommendation = "Önce veri girişlerini tamamlayıp sonra Program Oluştur butonunu kullanın.";
        }

        private void LoadRelaxationRules()
        {
            RelaxationRules.Clear();

            var labels = new Dictionary<string, string>
            {
                { "TeacherPreferences", "Öğretmen ders tercihleri" },
                { "ConsecutiveTeacher", "Aynı öğretmenin dersleri art arda gelmesin" },
                { "BalanceTeacher", "Öğretmen dersleri günlere dengeli dağıtılsın" },
                { "SpreadAcrossDays", "Ders günlere yayılsın" },
                { "MaxPerDay", "Günlük ders sınırı" },
                { "DetailedAvailability", "Ayrıntılı öğretmen uygunluk listesi" },
                { "CoursePriority", "Ders öncelik derecesi" },
                { "Blocks", "Blok dersler bölünmesin" },
                { "GroupSlotRules", "Sınıf saat kuralları" },
                { "TeacherHalfDay", "Öğretmen yarım gün uygunluğu" },
                { "LunchBreak", "Öğle arası yasağı" },
                { "TeacherUnavailableDays", "Öğretmenin uygun olmadığı günler" }
            };

            var orderedKeys = (Store.RelaxationOrder ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => labels.ContainsKey(x))
                .Distinct()
                .ToList();

            foreach (var key in labels.Keys)
            {
                if (!orderedKeys.Contains(key))
                    orderedKeys.Add(key);
            }

            for (int i = 0; i < orderedKeys.Count; i++)
                RelaxationRules.Add(new RelaxationRuleItem(this, orderedKeys[i], labels[orderedKeys[i]], i + 1));

            SaveRelaxationOrder();
        }

        internal void SaveRelaxationOrder()
        {
            Store.RelaxationOrder = string.Join(",", RelaxationRules
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Label)
                .Select(x => x.Key));
        }

        public void MoveRelaxationRule(RelaxationRuleItem source, RelaxationRuleItem target)
        {
            if (source == null || target == null || source == target) return;

            var oldIndex = RelaxationRules.IndexOf(source);
            var newIndex = RelaxationRules.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0) return;

            RelaxationRules.Move(oldIndex, newIndex);
            for (int i = 0; i < RelaxationRules.Count; i++)
                RelaxationRules[i].SetOrderSilently(i + 1);

            SaveRelaxationOrder();
        }

        private string MakeCellKey(ClassGroup g, Day d, int slotIndex)
        {
            var groupName = g != null ? g.Name : "";
            var dayName = d != null ? d.Name : "";
            return groupName + "|" + dayName + "|" + slotIndex;
        }

        internal string GetCellTextOverride(ClassGroup g, Day d, int slotIndex)
        {
            string value;
            return _cellTextOverrides.TryGetValue(MakeCellKey(g, d, slotIndex), out value) ? value : null;
        }

        internal void SetCellTextOverride(ClassGroup g, Day d, int slotIndex, string value)
        {
            var key = MakeCellKey(g, d, slotIndex);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (_cellTextOverrides.ContainsKey(key))
                    _cellTextOverrides.Remove(key);
            }
            else
            {
                _cellTextOverrides[key] = value;
            }
        }

        public string GetCellText(ClassGroup group, Day day, int slotIndex)
        {
            var overrideText = GetCellTextOverride(group, day, slotIndex);
            if (overrideText != null)
                return overrideText;

            var entry = Schedule.FirstOrDefault(e =>
                e.Group == group &&
                e.Day == day &&
                e.SlotIndex == slotIndex);

            return FormatEntryText(entry);
        }

        private void Clear()
        {
            PushUndoSnapshotIfNeeded();

            Schedule.Clear();
            Warnings.Clear();
            _cellTextOverrides.Clear();
            _alternatives.Clear();
            _currentAlternativeIndex = -1;
            AlternativeStatus = string.Empty;
            FriendlyWarningSummary = "Henüz program üretilmedi.";
            GenerationRecommendation = "Önce veri girişlerini tamamlayıp sonra Program Oluştur butonunu kullanın.";
            RefreshAlternativeCommands();
            RefreshWeeklyGrid();
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PushUndoSnapshotIfNeeded()
        {
            if (Schedule.Count == 0) return;

            _undoStack.Add(Schedule.ToList());
            if (_undoStack.Count > MaxUndoDepth)
                _undoStack.RemoveAt(0);

            _redoStack.Clear();
            RefreshUndoRedoCommands();
        }

        private void RefreshUndoRedoCommands()
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;

            var previous = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(Schedule.ToList());

            ApplyScheduleDirect(previous, "Son değişiklik geri alındı.");
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            var next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(Schedule.ToList());

            ApplyScheduleDirect(next, "Geri alınan değişiklik yeniden uygulandı.");
        }

        private void ApplyScheduleDirect(List<ScheduleEntry> schedule, string statusMessage)
        {
            Schedule.Clear();
            foreach (var entry in schedule)
                Schedule.Add(entry);

            _cellTextOverrides.Clear();
            RefreshWeeklyGrid();
            FriendlyWarningSummary = statusMessage;
            RefreshUndoRedoCommands();
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void Generate()
        {
            if (IsGenerating) return;

            Clear();
            IsGenerating = true;
            GenerationStatus = "Program hesaplanıyor...";
            GenerationProgressPercent = 0;

            var generationOptions = CreateGenerationOptions(RandomizePlacement, 0);

            ScheduleGenerationResult result;
            IProgress<string> progress = new Progress<string>(UpdateGenerationProgress);
            try
            {
                result = await Task.Run(() => ScheduleGenerationService.Generate(Store, generationOptions, message => progress.Report(message)));
            }
            finally
            {
                IsGenerating = false;
                GenerationStatus = string.Empty;
            }

            ApplyResult(result);
            _alternatives.Clear();
            _currentAlternativeIndex = -1;
            AlternativeStatus = string.Empty;
            RefreshAlternativeCommands();
        }

        private async void GenerateAlternative()
        {
            if (IsGenerating) return;

            PushUndoSnapshotIfNeeded();

            IsGenerating = true;
            GenerationStatus = "Yeni alternatif hesaplanıyor...";
            GenerationProgressPercent = 0;

            var seed = Environment.TickCount + (_alternatives.Count * 10000);
            var generationOptions = CreateGenerationOptions(true, seed);

            ScheduleGenerationResult result;
            IProgress<string> progress = new Progress<string>(UpdateGenerationProgress);
            try
            {
                result = await Task.Run(() => ScheduleGenerationService.Generate(Store, generationOptions, message => progress.Report(message)));
            }
            finally
            {
                IsGenerating = false;
                GenerationStatus = string.Empty;
            }

            _alternatives.Add(new AlternativeSchedule(result.Schedule.ToList(), result.Warnings.ToList()));
            _currentAlternativeIndex = _alternatives.Count - 1;
            ApplyAlternative(_currentAlternativeIndex);
            RefreshAlternativeCommands();
        }

        private ScheduleGenerationOptions CreateGenerationOptions(bool randomize, int seed)
        {
            return new ScheduleGenerationOptions
            {
                AvoidConsecutiveTeacherLessons = AvoidConsecutiveTeacherLessons,
                BalanceTeacherAcrossDays = BalanceTeacherAcrossDays,
                RandomizePlacement = randomize,
                RandomSeedOffset = seed,
                RespectTeacherUnavailableDays = Store.RespectTeacherUnavailableDays,
                RespectGroupSlotRules = Store.RespectGroupSlotRules,
                RespectLunchBreak = Store.RespectLunchBreak,
                RespectTeacherHalfDay = Store.RespectTeacherHalfDay,
                UseDutyDayPriority = Store.UseDutyDayPriority,
                UseCoursePriorityLevel = Store.UseCoursePriorityLevel,
                UseTeacherCoursePreferences = Store.UseTeacherCoursePreferences,
                UseSpreadAcrossDays = Store.UseSpreadAcrossDays,
                UseMaxPerDay = Store.UseMaxPerDay,
                UseDetailedTeacherAvailability = Store.UseDetailedTeacherAvailability,
                UseIntensiveRepairSearch = Store.UseIntensiveRepairSearch,
                UseClassByClassPlacement = Store.UseClassByClassPlacement,
                UseProgressiveImprovement = Store.UseProgressiveImprovement,
                UseParallelSearch = Store.UseParallelSearch,
                KeepBlocksStrict = Store.KeepBlocksStrict,
                DeepSearchEnabled = Store.DeepSearchEnabled,
                MaxGenerationAttempts = Store.MaxGenerationAttempts,
                RelaxationOrder = RelaxationRules.OrderBy(x => x.Order).Select(x => x.Key).ToList(),
                UseRelaxationOrder = Store.UseRelaxationOrder,
                SearchStrategy = Store.SearchStrategy
            };
        }

        public async Task<bool> MoveLessonAndRegenerateAsync(ScheduleEntry entry, Day targetDay, TimeSlot targetSlot)
        {
            if (entry == null || targetDay == null || targetSlot == null || IsGenerating) return false;

            IsGenerating = true;
            GenerationStatus = "Ders taşınıyor ve program onarılıyor...";
            GenerationProgressPercent = 0;

            var expectedEntryCount = Schedule.Count;
            var originalFixedLessons = Store.FixedLessons.ToList();
            var temporaryFixedLessons = BuildTemporaryFixedLessonsForMove(entry, targetDay, targetSlot);
            ScheduleGenerationResult result;
            IProgress<string> progress = new Progress<string>(message => UpdateGenerationProgress("Taşıma: " + message));

            try
            {
                Store.FixedLessons.Clear();
                foreach (var lesson in originalFixedLessons.Where(l => !ShouldSkipFixedLessonForMove(l, entry, targetDay, targetSlot)))
                    Store.FixedLessons.Add(lesson);
                foreach (var lesson in temporaryFixedLessons)
                    Store.FixedLessons.Add(lesson);

                var options = CreateGenerationOptions(true, Environment.TickCount);
                result = await Task.Run(() => ScheduleGenerationService.Generate(Store, options, message => progress.Report(message)));
            }
            finally
            {
                Store.FixedLessons.Clear();
                foreach (var lesson in originalFixedLessons)
                    Store.FixedLessons.Add(lesson);

                IsGenerating = false;
                GenerationStatus = string.Empty;
            }

            if (!MoveSucceeded(result, entry, targetDay, targetSlot, expectedEntryCount))
            {
                Warnings.Insert(0, "Taşıma uygulanamadı: " + FormatEntryText(entry) + " için " + targetDay.Name + " " + targetSlot.Index +
                    ". saatte geçerli bir yerleşim bulunamadı. Program değiştirilmedi, başka bir saat deneyin.");
                FriendlyWarningSummary = "Taşıma başarısız oldu, program eski haliyle korundu.";
                return false;
            }

            PushUndoSnapshotIfNeeded();
            ApplyResult(result);
            Warnings.Insert(0, "Taşıma uygulandı: " + FormatEntryText(entry) + " -> " + targetDay.Name + " " + targetSlot.Index + ". Etkilenen sınıflar boşluk bırakmamaya öncelik verilerek yeniden düzenlendi.");
            return true;
        }

        private static bool MoveSucceeded(ScheduleGenerationResult result, ScheduleEntry movingEntry, Day targetDay, TimeSlot targetSlot, int expectedEntryCount)
        {
            if (result == null || result.Schedule == null) return false;
            if (result.Schedule.Count < expectedEntryCount) return false;

            var block = Math.Max(1, movingEntry.BlockSize);
            return result.Schedule.Any(e =>
                e.Group == movingEntry.Group &&
                e.Course == movingEntry.Course &&
                e.Teacher == movingEntry.Teacher &&
                e.Day == targetDay &&
                e.SlotIndex == targetSlot.Index &&
                Math.Max(1, e.BlockSize) == block);
        }

        private List<FixedLesson> BuildTemporaryFixedLessonsForMove(ScheduleEntry movingEntry, Day targetDay, TimeSlot targetSlot)
        {
            var lessons = new List<FixedLesson>();
            var groupsToRebuild = new HashSet<ClassGroup> { movingEntry.Group };
            var movingBlockStart = movingEntry.SlotIndex - Math.Max(0, movingEntry.BlockPos - 1);
            var movingBlockEnd = movingBlockStart + Math.Max(1, movingEntry.BlockSize) - 1;

            bool IsMovingBlock(ScheduleEntry e)
            {
                return e.Group == movingEntry.Group &&
                       e.Course == movingEntry.Course &&
                       e.Teacher == movingEntry.Teacher &&
                       e.Day == movingEntry.Day &&
                       e.SlotIndex >= movingBlockStart &&
                       e.SlotIndex <= movingBlockEnd;
            }

            bool ConflictsWithTarget(ScheduleEntry e)
            {
                if (e.Day != targetDay) return false;

                var movingBlock = Math.Max(1, movingEntry.BlockSize);
                var targetStart = targetSlot.Index;
                var targetEnd = targetStart + movingBlock - 1;

                // e.SlotIndex is the block-start slot for BlockPos==1 entries (the only
                // ones passed in here), so its own BlockSize must be considered too -
                // otherwise a multi-slot block only overlapping via its later slots was
                // missed, leaving two lessons pinned onto the same slot and one silently
                // dropped by the generator (see ScheduleGenerationService fixed-lesson loop).
                var eBlock = Math.Max(1, e.BlockSize);
                var eStart = e.SlotIndex;
                var eEnd = eStart + eBlock - 1;

                if (eStart > targetEnd || eEnd < targetStart) return false;

                return e.Group == movingEntry.Group ||
                       e.Teacher == movingEntry.Teacher ||
                       (movingEntry.Room != null && e.Room == movingEntry.Room);
            }

            lessons.Add(new FixedLesson
            {
                Group = movingEntry.Group,
                Day = targetDay,
                SlotIndex = targetSlot.Index,
                Course = movingEntry.Course,
                Teacher = movingEntry.Teacher,
                Room = movingEntry.Room,
                BlockSize = Math.Max(1, movingEntry.BlockSize)
            });

            // Eski programdaki her dersi sabitlemek, taşınan dersin bıraktığı
            // sınıf içi boşluğun kapanmasını imkansız hale getiriyordu.
            foreach (var entry in Schedule)
            {
                if (entry != null && ConflictsWithTarget(entry) && entry.Group != null)
                    groupsToRebuild.Add(entry.Group);
            }

            foreach (var entry in Schedule.OrderBy(e => e.Group != null ? e.Group.Name : "").ThenBy(e => e.Day != null ? e.Day.Name : "").ThenBy(e => e.SlotIndex))
            {
                if (entry == null || entry.BlockPos != 1) continue;
                if (IsMovingBlock(entry)) continue;
                if (entry.Group != null && groupsToRebuild.Contains(entry.Group)) continue;
                if (ConflictsWithTarget(entry)) continue;

                lessons.Add(new FixedLesson
                {
                    Group = entry.Group,
                    Day = entry.Day,
                    SlotIndex = entry.SlotIndex,
                    Course = entry.Course,
                    Teacher = entry.Teacher,
                    Room = entry.Room,
                    BlockSize = Math.Max(1, entry.BlockSize)
                });
            }

            return lessons;
        }

        private static bool ShouldSkipFixedLessonForMove(FixedLesson lesson, ScheduleEntry movingEntry, Day targetDay, TimeSlot targetSlot)
        {
            if (lesson == null || movingEntry == null || targetDay == null || targetSlot == null) return false;

            var movingBlockStart = movingEntry.SlotIndex - Math.Max(0, movingEntry.BlockPos - 1);
            var movingBlockEnd = movingBlockStart + Math.Max(1, movingEntry.BlockSize) - 1;
            if (lesson.Group == movingEntry.Group &&
                lesson.Course == movingEntry.Course &&
                lesson.Teacher == movingEntry.Teacher &&
                lesson.Day == movingEntry.Day &&
                lesson.SlotIndex <= movingBlockEnd &&
                lesson.SlotIndex + Math.Max(1, lesson.BlockSize) - 1 >= movingBlockStart)
            {
                return true;
            }

            var targetEnd = targetSlot.Index + Math.Max(1, movingEntry.BlockSize) - 1;
            if (lesson.Day == targetDay &&
                lesson.SlotIndex <= targetEnd &&
                lesson.SlotIndex + Math.Max(1, lesson.BlockSize) - 1 >= targetSlot.Index &&
                (lesson.Group == movingEntry.Group ||
                 lesson.Teacher == movingEntry.Teacher ||
                 (movingEntry.Room != null && lesson.Room == movingEntry.Room)))
            {
                return true;
            }

            return false;
        }

        private void PreviousAlternative()
        {
            if (_alternatives.Count == 0) return;
            _currentAlternativeIndex = _currentAlternativeIndex <= 0 ? _alternatives.Count - 1 : _currentAlternativeIndex - 1;
            ApplyAlternative(_currentAlternativeIndex);
            RefreshAlternativeCommands();
        }

        private void NextAlternative()
        {
            if (_alternatives.Count == 0) return;
            _currentAlternativeIndex = _currentAlternativeIndex >= _alternatives.Count - 1 ? 0 : _currentAlternativeIndex + 1;
            ApplyAlternative(_currentAlternativeIndex);
            RefreshAlternativeCommands();
        }

        private void ApplyAlternative(int index)
        {
            if (index < 0 || index >= _alternatives.Count) return;

            var alternative = _alternatives[index];
            ApplySchedule(alternative.Schedule, alternative.Warnings);
            AlternativeStatus = "Alternatif " + (index + 1) + " / " + _alternatives.Count;
        }

        private void ApplyResult(ScheduleGenerationResult result)
        {
            ApplySchedule(result.Schedule, result.Warnings);
        }

        private void ApplySchedule(IEnumerable<ScheduleEntry> schedule, IEnumerable<string> warnings)
        {
            Schedule.Clear();
            Warnings.Clear();
            _cellTextOverrides.Clear();

            foreach (var entry in schedule)
                Schedule.Add(entry);

            var warningList = (warnings ?? Enumerable.Empty<string>()).ToList();
            foreach (var warning in warningList)
                Warnings.Add(MakeUserFriendlyWarning(warning));

            RefreshWeeklyGrid();
            FriendlyWarningSummary = BuildWarningSummary(warningList.Count);
            GenerationRecommendation = BuildGenerationRecommendation(warningList);
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshAlternativeCommands()
        {
            GenerateAlternativeCommand.RaiseCanExecuteChanged();
            PreviousAlternativeCommand.RaiseCanExecuteChanged();
            NextAlternativeCommand.RaiseCanExecuteChanged();
        }

        private bool _avoidConsecutiveTeacherLessons = true;
        public bool AvoidConsecutiveTeacherLessons
        {
            get => _avoidConsecutiveTeacherLessons;
            set => Set(ref _avoidConsecutiveTeacherLessons, value);
        }

        private bool _balanceTeacherAcrossDays;
        public bool BalanceTeacherAcrossDays
        {
            get => _balanceTeacherAcrossDays;
            set => Set(ref _balanceTeacherAcrossDays, value);
        }

        private bool _randomizePlacement = true;
        public bool RandomizePlacement
        {
            get => _randomizePlacement;
            set => Set(ref _randomizePlacement, value);
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            private set
            {
                if (Set(ref _isGenerating, value))
                {
                    Store.IsBusy = value;
                    GenerateCommand.RaiseCanExecuteChanged();
                    ClearCommand.RaiseCanExecuteChanged();
                    RefreshAlternativeCommands();
                    RefreshUndoRedoCommands();
                }
            }
        }

        private string _generationStatus;
        public string GenerationStatus
        {
            get => _generationStatus;
            private set => Set(ref _generationStatus, value);
        }

        private int _generationProgressPercent;
        public int GenerationProgressPercent
        {
            get => _generationProgressPercent;
            private set => Set(ref _generationProgressPercent, value);
        }

        private string _alternativeStatus;
        public string AlternativeStatus
        {
            get => _alternativeStatus;
            private set => Set(ref _alternativeStatus, value);
        }

        private string _friendlyWarningSummary;
        public string FriendlyWarningSummary
        {
            get => _friendlyWarningSummary;
            private set => Set(ref _friendlyWarningSummary, value);
        }

        private string _generationRecommendation;
        public string GenerationRecommendation
        {
            get => _generationRecommendation;
            private set => Set(ref _generationRecommendation, value);
        }

        public void AfterProjectLoaded()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            RefreshUndoRedoCommands();

            PurgeOrphanedScheduleEntries();

            Warnings.Clear();
            _cellTextOverrides.Clear();
            _alternatives.Clear();
            _currentAlternativeIndex = -1;
            AlternativeStatus = string.Empty;
            GenerationProgressPercent = 0;

            if (Schedule.Count > 0)
            {
                FriendlyWarningSummary = "Kaydedilmiş bir program yüklendi.";
                GenerationRecommendation = "Programda değişiklik yaparsanız Dosya > Kaydet ile kaydetmeyi unutmayın.";
            }
            else
            {
                FriendlyWarningSummary = "Henüz program üretilmedi.";
                GenerationRecommendation = "Önce veri girişlerini tamamlayıp sonra Program Oluştur butonunu kullanın.";
            }

            RefreshAlternativeCommands();
            LoadRelaxationRules();
            SelectedGroup = Store.Groups.FirstOrDefault();
            RefreshWeeklyGrid();
        }

        private void PurgeOrphanedScheduleEntries()
        {
            var groups = new HashSet<ClassGroup>(Store.Groups);
            var days = new HashSet<Day>(Store.Days);

            var stale = Schedule.Where(e => e == null || !groups.Contains(e.Group) || !days.Contains(e.Day)).ToList();
            foreach (var entry in stale)
                Schedule.Remove(entry);
        }

        private void RefreshWeeklyGrid()
        {
            SlotHeaders.Clear();
            WeeklyRows.Clear();

            if (SelectedGroup == null) return;
            if (Store.Days == null || Store.Days.Count == 0) return;

            var slotIndexToSlot = new Dictionary<int, TimeSlot>();
            foreach (var day in Store.Days)
            {
                if (day == null || day.Slots == null) continue;

                foreach (var slot in day.Slots)
                {
                    if (slot == null) continue;
                    if (!slotIndexToSlot.ContainsKey(slot.Index))
                        slotIndexToSlot[slot.Index] = slot;
                }
            }

            foreach (var slot in slotIndexToSlot.OrderBy(kv => kv.Key).Select(kv => kv.Value))
                SlotHeaders.Add(slot);

            foreach (var day in Store.Days)
            {
                var row = new WeeklyDayRow(day);

                foreach (var slot in SlotHeaders)
                {
                    var entry = Schedule.FirstOrDefault(e =>
                        e.Group == SelectedGroup &&
                        e.Day == day &&
                        e.SlotIndex == slot.Index);

                    row.Cells.Add(new WeeklyCell(this, day, slot.Index, entry));
                }

                WeeklyRows.Add(row);
            }
        }

        private static string FormatEntryText(ScheduleEntry entry)
        {
            if (entry == null) return string.Empty;

            var course = entry.Course != null
                ? (string.IsNullOrWhiteSpace(entry.Course.Code) ? entry.Course.Name : entry.Course.Code)
                : string.Empty;
            var teacher = entry.Teacher != null ? entry.Teacher.Name : string.Empty;
            var room = entry.Room != null ? entry.Room.Name : string.Empty;

            var block = entry.BlockSize > 1
                ? " (" + entry.BlockPos + "/" + entry.BlockSize + ")"
                : string.Empty;

            var line1 = course + block;
            var line2 = string.Empty;

            if (!string.IsNullOrWhiteSpace(teacher) && !string.IsNullOrWhiteSpace(room))
                line2 = teacher + " / " + room;
            else if (!string.IsNullOrWhiteSpace(teacher))
                line2 = teacher;
            else if (!string.IsNullOrWhiteSpace(room))
                line2 = room;

            if (string.IsNullOrWhiteSpace(line2))
                return line1;

            return line1 + "\n" + line2;
        }

        private static string BuildWarningSummary(int count)
        {
            if (count <= 0)
                return "Harika görünüyor. Bu üretimde ek uyarı oluşmadı.";

            if (count == 1)
                return "1 uyarı var. Program kullanılabilir olabilir ama kısa bir kontrol önerilir.";

            return count + " uyarı var. Export almadan önce kritik sınıf ve öğretmenleri gözden geçirmeniz iyi olur.";
        }

        private static string BuildGenerationRecommendation(List<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return "İsterseniz alternatif üretip daha dengeli bir yerleşim arayabilirsiniz.";

            var combined = string.Join(" | ", warnings);
            if (combined.IndexOf("yerleştir", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "Yerleşmeyen dersler için önce eksik atamaları ve saat kurallarını kontrol edin. Gerekirse Arama Stratejisi'ni Yoğun Arama veya Derin Arama yapın.";

            if (combined.IndexOf("öğretmen", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "Öğretmen kaynaklı uyarılar için müsaitlik, yarım gün ve ders tercihlerini tekrar gözden geçirmeniz faydalı olur.";

            return "Uyarılar varsa alternatif üretmeyi, sabit dersleri azaltmayı veya kuralları kademeli gevşetmeyi deneyebilirsiniz.";
        }

        private static string MakeUserFriendlyWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
                return "Ayrıntı verilmeyen bir uyarı oluştu. İlgili sınıf ve öğretmenleri kontrol edin.";

            if (warning.IndexOf("pending", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                warning.IndexOf("yerleştir", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "Yerleşmeyen ders uyarısı: " + warning + " Gerekirse daha yoğun arama deneyin.";

            if (warning.IndexOf("teacher", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                warning.IndexOf("hoca", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                warning.IndexOf("öğretmen", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "Öğretmen kuralı uyarısı: " + warning + " Müsaitlik veya ders yükünü kontrol edin.";

            if (warning.IndexOf("room", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                warning.IndexOf("salon", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "Salon çakışması veya kısıtı: " + warning + " Gerekirse salon atamasını değiştirin.";

            return warning;
        }

        private void UpdateGenerationProgress(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var displayMessage = message;
            var taggedMatch = Regex.Match(message, @"^\[(\d{1,3})\]\s*(.*)$");
            if (taggedMatch.Success)
            {
                int taggedPercent;
                if (int.TryParse(taggedMatch.Groups[1].Value, out taggedPercent))
                    GenerationProgressPercent = Math.Max(0, Math.Min(100, taggedPercent));

                displayMessage = taggedMatch.Groups[2].Value;
            }
            else
            {
                var attemptMatch = Regex.Match(message, @"Deneme\s+(\d+)\s*/\s*(\d+)");
                if (attemptMatch.Success)
                {
                    int currentAttempt;
                    int totalAttempts;
                    if (int.TryParse(attemptMatch.Groups[1].Value, out currentAttempt) &&
                        int.TryParse(attemptMatch.Groups[2].Value, out totalAttempts) &&
                        totalAttempts > 0)
                    {
                        GenerationProgressPercent = Math.Max(0, Math.Min(99, (int)Math.Round((currentAttempt / (double)totalAttempts) * 100d)));
                    }
                }
            }

            GenerationStatus = displayMessage;
        }

        public sealed class WeeklyDayRow
        {
            public Day Day { get; }
            public string DayName => Day != null ? Day.Name : string.Empty;

            public ObservableCollection<WeeklyCell> Cells { get; } =
                new ObservableCollection<WeeklyCell>();

            public WeeklyDayRow(Day day)
            {
                Day = day;
            }
        }

        public sealed class WeeklyCell : BaseViewModel
        {
            private readonly SchedulerViewModel _vm;

            public int SlotIndex { get; }
            public ScheduleEntry Entry { get; }
            public Day Day { get; }
            public bool HasEntry { get { return Entry != null; } }

            public string DefaultText => FormatEntryText(Entry);

            public string EditableText
            {
                get
                {
                    if (_vm == null) return DefaultText;
                    var overrideText = _vm.GetCellTextOverride(_vm.SelectedGroup, Day, SlotIndex);
                    return overrideText ?? DefaultText;
                }
                set
                {
                    if (_vm == null) return;
                    _vm.SetCellTextOverride(_vm.SelectedGroup, Day, SlotIndex, value);
                    OnPropertyChanged(nameof(EditableText));
                }
            }

            public WeeklyCell(SchedulerViewModel vm, Day day, int slotIndex, ScheduleEntry entry)
            {
                _vm = vm;
                Day = day;
                SlotIndex = slotIndex;
                Entry = entry;
            }
        }

        private sealed class AlternativeSchedule
        {
            public List<ScheduleEntry> Schedule { get; }
            public List<string> Warnings { get; }

            public AlternativeSchedule(List<ScheduleEntry> schedule, List<string> warnings)
            {
                Schedule = schedule ?? new List<ScheduleEntry>();
                Warnings = warnings ?? new List<string>();
            }
        }

        public sealed class RelaxationRuleItem : BaseViewModel
        {
            private readonly SchedulerViewModel _owner;
            private int _order;

            public string Key { get; }
            public string Label { get; }

            public int Order
            {
                get { return _order; }
                set
                {
                    var normalized = value;
                    if (normalized < 1) normalized = 1;
                    if (normalized > 99) normalized = 99;
                    if (Set(ref _order, normalized) && _owner != null)
                        _owner.SaveRelaxationOrder();
                }
            }

            public RelaxationRuleItem(SchedulerViewModel owner, string key, string label, int order)
            {
                _owner = owner;
                Key = key;
                Label = label;
                _order = order;
            }

            internal void SetOrderSilently(int order)
            {
                _order = order;
                OnPropertyChanged(nameof(Order));
            }
        }
    }
}
