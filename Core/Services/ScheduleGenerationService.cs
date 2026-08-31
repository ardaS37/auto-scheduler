using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AutoScheduler.Core.Services
{
    public sealed class ScheduleGenerationOptions
    {
        public bool AvoidConsecutiveTeacherLessons { get; set; }
        public bool BalanceTeacherAcrossDays { get; set; }
        public bool RandomizePlacement { get; set; }
        public int RandomSeedOffset { get; set; }
        public bool RespectTeacherUnavailableDays { get; set; } = true;
        public bool RespectGroupSlotRules { get; set; } = true;
        public bool RespectLunchBreak { get; set; } = true;
        public bool RespectTeacherHalfDay { get; set; } = true;
        public bool UseDutyDayPriority { get; set; } = true;
        public bool UseCoursePriorityLevel { get; set; } = true;
        public bool UseTeacherCoursePreferences { get; set; } = true;
        public bool UseSpreadAcrossDays { get; set; } = true;
        public bool UseMaxPerDay { get; set; } = true;
        public bool UseDetailedTeacherAvailability { get; set; } = true;
        public bool UseIntensiveRepairSearch { get; set; } = true;
        public bool UseClassByClassPlacement { get; set; } = true;
        public bool UseProgressiveImprovement { get; set; } = true;
        public bool UseParallelSearch { get; set; } = true;
        public bool KeepBlocksStrict { get; set; } = true;
        public bool DeepSearchEnabled { get; set; } = true;
        public int MaxGenerationAttempts { get; set; } = 5000;
        public List<string> RelaxationOrder { get; set; } = new List<string>();
        public bool UseRelaxationOrder { get; set; } = true;
        public GenerationSearchStrategy SearchStrategy { get; set; } = GenerationSearchStrategy.Standart;

        // Hizli stratejisinde NormalizeOptions() bunu true yapar; kalite tercihlerine bakan
        // store.PreferMinimumVerbalPerDay/PreferMinimumNumericPerDay okumaları bununla kapatılır.
        public bool IgnoreSoftPreferences { get; set; }

        // Aşağıdaki dört alan NormalizeOptions() içinde SearchStrategy'ye göre otomatik doldurulur.
        public int BacktrackingPendingCap { get; set; }
        public int BacktrackingNodeBudget { get; set; }
        public int BacktrackingMillisecondBudget { get; set; }
        public int BacktrackingMaxCandidateSlots { get; set; }
        public int BacktrackingSwapBudget { get; set; }
        public int OverallTimeBudgetMs { get; set; }
        public int StrategyAttemptCap { get; set; }
        public int RelaxationCadence { get; set; } = 18;
    }

    public sealed class ScheduleGenerationResult
    {
        public List<ScheduleEntry> Schedule { get; } = new List<ScheduleEntry>();
        public List<string> Warnings { get; } = new List<string>();
        public int QualityScore { get; set; }
    }

    public static class ScheduleGenerationService
    {
        public static ScheduleGenerationResult Generate(ProjectStore store, ScheduleGenerationOptions options, Action<string> progress = null)
        {
            if (store == null) return new ScheduleGenerationResult();
            if (options == null) options = new ScheduleGenerationOptions();
            NormalizeOptions(options);

            var validationIssues = ProjectValidationService.Validate(store).ToList();
            if (store.Days.Count == 0)
            {
                var result = new ScheduleGenerationResult();
                result.Warnings.AddRange(validationIssues);
                result.Warnings.Add("Gün listesi boş. Önce günleri ekleyin.");
                return result;
            }

            if (store.Days.Where(d => d != null).Any(d => d.Slots.GroupBy(s => s.Index).Any(g => g.Count() > 1)))
            {
                var result = new ScheduleGenerationResult();
                result.Warnings.AddRange(validationIssues);
                result.Warnings.Add("Bazı günlerde aynı ders saati birden fazla kez tanımlı. Program oluşturmadan önce Şablon sekmesinden düzeltin.");
                return result;
            }

            if (store.Assignments.Count == 0)
            {
                var result = new ScheduleGenerationResult();
                result.Warnings.AddRange(validationIssues);
                result.Warnings.Add("Atama yok. Önce sınıf-ders-hoca ekleyin.");
                return result;
            }

            var attempts = options.DeepSearchEnabled
                ? Math.Max(1, options.MaxGenerationAttempts)
                : (options.RandomizePlacement ? 120 : 40);

            if (options.StrategyAttemptCap > 0)
                attempts = Math.Min(attempts, options.StrategyAttemptCap);

            var totalAttemptCount = attempts + 1;

            progress?.Invoke("[1] Deneme 1 / " + totalAttemptCount + " hazırlanıyor...");
            var best = GenerateSingle(
                store,
                options,
                validationIssues,
                randomizeOrder: options.RandomizePlacement,
                seed: options.RandomSeedOffset,
                progress: progress,
                attemptNumber: 1);
            best.QualityScore = EvaluateQuality(store, options, best);

            if (!options.RandomizePlacement && !options.DeepSearchEnabled && !HasHardPlacementWarnings(best))
            {
                progress?.Invoke("[100] Program oluşturma tamamlandı.");
                return best;
            }

            var overallStopwatch = Stopwatch.StartNew();
            var timeBudgetExceeded = false;

            if (options.UseParallelSearch && attempts > 1)
            {
                var maxDegree = Math.Max(1, Math.Min(Environment.ProcessorCount, attempts));
                var completed = 0;
                var sync = new object();

                progress?.Invoke("[2] Çok çekirdekli arama başlıyor: " + attempts + " ek deneme, " + maxDegree + " iş parçacığı...");
                Parallel.For(0, attempts, new ParallelOptions { MaxDegreeOfParallelism = maxDegree }, (i, loopState) =>
                {
                    if (options.OverallTimeBudgetMs > 0 && overallStopwatch.ElapsedMilliseconds >= options.OverallTimeBudgetMs)
                    {
                        loopState.Stop();
                        return;
                    }

                    ScheduleGenerationResult currentBest;
                    lock (sync)
                        currentBest = best;

                    var attemptOptions = BuildAttemptOptions(options, currentBest, i);
                    var candidate = GenerateSingle(store, attemptOptions, validationIssues, randomizeOrder: true, seed: options.RandomSeedOffset + 1000 + i, progress: progress, attemptNumber: i + 2);
                    candidate.QualityScore = EvaluateQuality(store, attemptOptions, candidate);

                    var done = System.Threading.Interlocked.Increment(ref completed);
                    lock (sync)
                    {
                        if (IsBetter(candidate, best))
                            best = candidate;
                    }

                    if (done == attempts || done % Math.Max(1, attempts / 100) == 0)
                    {
                        var percent = Math.Min(99, (int)Math.Round(((done + 1d) / totalAttemptCount) * 100d));
                        progress?.Invoke("[" + percent + "] Çok çekirdekli arama: " + done + " / " + attempts + " ek deneme tamamlandı...");
                    }
                });

                progress?.Invoke("[100] Program oluşturma tamamlandı.");
                return best;
            }

            for (int i = 0; i < attempts; i++)
            {
                if (options.OverallTimeBudgetMs > 0 && overallStopwatch.ElapsedMilliseconds >= options.OverallTimeBudgetMs)
                {
                    timeBudgetExceeded = true;
                    break;
                }

                var currentAttempt = i + 2;
                var percent = Math.Min(99, (int)Math.Round((currentAttempt / (double)totalAttemptCount) * 100d));
                progress?.Invoke("[" + percent + "] Deneme " + currentAttempt + " / " + totalAttemptCount + " çalışıyor...");
                var attemptOptions = BuildAttemptOptions(options, best, i);
                var candidate = GenerateSingle(store, attemptOptions, validationIssues, randomizeOrder: true, seed: options.RandomSeedOffset + 1000 + i, progress: progress, attemptNumber: i + 2);
                candidate.QualityScore = EvaluateQuality(store, attemptOptions, candidate);
                if (IsBetter(candidate, best))
                    best = candidate;

                if (!options.DeepSearchEnabled && !HasHardPlacementWarnings(best))
                    break;
            }

            if (timeBudgetExceeded)
                progress?.Invoke("[99] Zaman sınırına ulaşıldı, en iyi sonuç kullanılıyor.");

            progress?.Invoke("[100] Program oluşturma tamamlandı.");
            return best;
        }

        private static void NormalizeOptions(ScheduleGenerationOptions options)
        {
            if (options.MaxGenerationAttempts < 1) options.MaxGenerationAttempts = 1;
            if (options.RelaxationOrder == null)
                options.RelaxationOrder = new List<string>();

            switch (options.SearchStrategy)
            {
                case GenerationSearchStrategy.Yogun:
                    options.BacktrackingPendingCap = 50;
                    options.BacktrackingNodeBudget = 30000;
                    options.BacktrackingMillisecondBudget = 3000;
                    options.BacktrackingMaxCandidateSlots = 40;
                    options.BacktrackingSwapBudget = 30;
                    options.StrategyAttemptCap = 150;
                    options.OverallTimeBudgetMs = 20000;
                    options.RelaxationCadence = Math.Max(1, options.StrategyAttemptCap / (options.RelaxationOrder.Count + 1));
                    break;
                case GenerationSearchStrategy.Maksimum:
                    options.BacktrackingPendingCap = 120;
                    options.BacktrackingNodeBudget = 90000;
                    options.BacktrackingMillisecondBudget = 7000;
                    options.BacktrackingMaxCandidateSlots = 60;
                    options.BacktrackingSwapBudget = 60;
                    options.StrategyAttemptCap = 60;
                    options.OverallTimeBudgetMs = 45000;
                    options.RelaxationCadence = Math.Max(1, options.StrategyAttemptCap / (options.RelaxationOrder.Count + 1));
                    break;
                case GenerationSearchStrategy.SonCare:
                    options.BacktrackingPendingCap = 400;
                    options.BacktrackingNodeBudget = 500000;
                    options.BacktrackingMillisecondBudget = 30000;
                    options.BacktrackingMaxCandidateSlots = 80;
                    options.BacktrackingSwapBudget = 150;
                    options.StrategyAttemptCap = 10;
                    options.OverallTimeBudgetMs = 180000;
                    options.RelaxationCadence = Math.Max(1, options.StrategyAttemptCap / (options.RelaxationOrder.Count + 1));
                    break;
                case GenerationSearchStrategy.Hizli:
                    // Kalite tercihlerini (yandaki tikleri) yok sayıp tek geçişte, en hızlı
                    // şekilde geçerli bir program üretmeyi hedefler; onarım/deneme yapmaz.
                    options.IgnoreSoftPreferences = true;
                    options.AvoidConsecutiveTeacherLessons = false;
                    options.BalanceTeacherAcrossDays = false;
                    options.UseDutyDayPriority = false;
                    options.UseCoursePriorityLevel = false;
                    options.UseTeacherCoursePreferences = false;
                    options.UseSpreadAcrossDays = false;
                    options.KeepBlocksStrict = false;
                    options.UseRelaxationOrder = false;
                    options.DeepSearchEnabled = false;
                    options.BacktrackingPendingCap = 0;
                    options.BacktrackingNodeBudget = 0;
                    options.BacktrackingMillisecondBudget = 0;
                    options.BacktrackingMaxCandidateSlots = 0;
                    options.BacktrackingSwapBudget = 0;
                    options.StrategyAttemptCap = 1;
                    options.OverallTimeBudgetMs = 8000;
                    options.RelaxationCadence = 40;
                    break;
                default:
                    options.BacktrackingPendingCap = options.UseIntensiveRepairSearch ? 18 : 10;
                    options.BacktrackingNodeBudget = options.UseIntensiveRepairSearch ? 9000 : 1500;
                    options.BacktrackingMillisecondBudget = options.UseIntensiveRepairSearch ? 1800 : 400;
                    options.BacktrackingMaxCandidateSlots = options.UseIntensiveRepairSearch ? 28 : 14;
                    options.BacktrackingSwapBudget = options.UseIntensiveRepairSearch ? 14 : 8;
                    options.StrategyAttemptCap = 0;
                    options.OverallTimeBudgetMs = 0;
                    options.RelaxationCadence = 40;
                    break;
            }
        }

        private static ScheduleGenerationOptions BuildAttemptOptions(ScheduleGenerationOptions source, ScheduleGenerationResult currentBest, int attemptIndex)
        {
            var copy = new ScheduleGenerationOptions
            {
                AvoidConsecutiveTeacherLessons = source.AvoidConsecutiveTeacherLessons,
                BalanceTeacherAcrossDays = source.BalanceTeacherAcrossDays,
                RandomizePlacement = source.RandomizePlacement,
                RandomSeedOffset = source.RandomSeedOffset,
                RespectTeacherUnavailableDays = source.RespectTeacherUnavailableDays,
                RespectGroupSlotRules = source.RespectGroupSlotRules,
                RespectLunchBreak = source.RespectLunchBreak,
                RespectTeacherHalfDay = source.RespectTeacherHalfDay,
                UseDutyDayPriority = source.UseDutyDayPriority,
                UseCoursePriorityLevel = source.UseCoursePriorityLevel,
                UseTeacherCoursePreferences = source.UseTeacherCoursePreferences,
                UseSpreadAcrossDays = source.UseSpreadAcrossDays,
                UseMaxPerDay = source.UseMaxPerDay,
                UseDetailedTeacherAvailability = source.UseDetailedTeacherAvailability,
                UseIntensiveRepairSearch = source.UseIntensiveRepairSearch,
                UseClassByClassPlacement = source.UseClassByClassPlacement,
                UseProgressiveImprovement = source.UseProgressiveImprovement,
                UseParallelSearch = source.UseParallelSearch,
                KeepBlocksStrict = source.KeepBlocksStrict,
                DeepSearchEnabled = source.DeepSearchEnabled,
                MaxGenerationAttempts = source.MaxGenerationAttempts,
                RelaxationOrder = source.RelaxationOrder.ToList(),
                UseRelaxationOrder = source.UseRelaxationOrder,
                SearchStrategy = source.SearchStrategy,
                IgnoreSoftPreferences = source.IgnoreSoftPreferences,
                BacktrackingPendingCap = source.BacktrackingPendingCap,
                BacktrackingNodeBudget = source.BacktrackingNodeBudget,
                BacktrackingMillisecondBudget = source.BacktrackingMillisecondBudget,
                BacktrackingMaxCandidateSlots = source.BacktrackingMaxCandidateSlots,
                BacktrackingSwapBudget = source.BacktrackingSwapBudget,
                OverallTimeBudgetMs = source.OverallTimeBudgetMs,
                StrategyAttemptCap = source.StrategyAttemptCap,
                RelaxationCadence = source.RelaxationCadence
            };

            if (!copy.UseRelaxationOrder || !HasHardPlacementWarnings(currentBest) || copy.RelaxationOrder.Count == 0)
                return copy;

            var relaxCount = Math.Min(copy.RelaxationOrder.Count, attemptIndex / Math.Max(1, copy.RelaxationCadence));
            for (int i = 0; i < relaxCount; i++)
                DisableRule(copy, copy.RelaxationOrder[i]);

            return copy;
        }

        private static void DisableRule(ScheduleGenerationOptions options, string key)
        {
            if (key == "TeacherPreferences") options.UseTeacherCoursePreferences = false;
            else if (key == "ConsecutiveTeacher") options.AvoidConsecutiveTeacherLessons = false;
            else if (key == "BalanceTeacher") options.BalanceTeacherAcrossDays = false;
            else if (key == "SpreadAcrossDays") options.UseSpreadAcrossDays = false;
            else if (key == "MaxPerDay") options.UseMaxPerDay = false;
            else if (key == "DetailedAvailability") options.UseDetailedTeacherAvailability = false;
            else if (key == "CoursePriority") options.UseCoursePriorityLevel = false;
            else if (key == "Blocks") options.KeepBlocksStrict = false;
            else if (key == "GroupSlotRules") options.RespectGroupSlotRules = false;
            else if (key == "TeacherHalfDay") options.RespectTeacherHalfDay = false;
            else if (key == "LunchBreak") options.RespectLunchBreak = false;
            else if (key == "TeacherUnavailableDays") options.RespectTeacherUnavailableDays = false;
        }

        private static ScheduleGenerationResult GenerateSingle(
            ProjectStore store,
            ScheduleGenerationOptions options,
            IReadOnlyCollection<string> validationIssues,
            bool randomizeOrder,
            int seed,
            Action<string> progress = null,
            int attemptNumber = 1)
        {
            var result = new ScheduleGenerationResult();
            foreach (var issue in validationIssues)
                result.Warnings.Add(issue);

            var rng = new Random(seed);
            var teacherDayCount = new Dictionary<string, Dictionary<string, int>>();
            var occGroup = new HashSet<string>();
            var occTeacher = new HashSet<string>();
            var occRoom = new HashSet<string>();
            var groupCourseDayCount = new Dictionary<string, Dictionary<string, int>>();
            var groupKindDayCount = new Dictionary<string, Dictionary<string, int>>();

            var teacherWeeklyLoad = store.Assignments
                .Where(a => a.Teacher != null)
                .GroupBy(a => a.Teacher.Name)
                .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.WeeklyHours)));

            var groupWeeklyLoad = store.Assignments
                .Where(a => a.Group != null)
                .GroupBy(a => a.Group.Name)
                .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.WeeklyHours)));

            var pendingSinglePlacements = new List<PendingPlacement>();
            var pendingBlockPlacements = new List<PendingPlacement>();
            var backtrackingStopwatch = Stopwatch.StartNew();
            int backtrackingVisitedNodeCount = 0;
            long lastBacktrackingProgressMs = 0;

            void IncTeacherDay(Teacher t, Day d)
            {
                if (t == null || d == null) return;

                if (!teacherDayCount.TryGetValue(t.Name, out var dayMap))
                {
                    dayMap = new Dictionary<string, int>();
                    teacherDayCount[t.Name] = dayMap;
                }

                dayMap.TryGetValue(d.Name, out var count);
                dayMap[d.Name] = count + 1;
            }

            int GetTeacherDayCount(Teacher t, Day d)
            {
                if (t == null || d == null) return 0;
                if (!teacherDayCount.TryGetValue(t.Name, out var dayMap)) return 0;
                return dayMap.TryGetValue(d.Name, out var count) ? count : 0;
            }

            int GetGroupCourseDayCount(ClassGroup g, Course c, Day d)
            {
                if (g == null || c == null || d == null) return 0;
                var key = g.Name + "|" + c.Name;
                if (!groupCourseDayCount.TryGetValue(key, out var map)) return 0;
                return map.TryGetValue(d.Name, out var count) ? count : 0;
            }

            void IncGroupCourseDay(ClassGroup g, Course c, Day d)
            {
                if (g == null || c == null || d == null) return;
                var key = g.Name + "|" + c.Name;
                if (!groupCourseDayCount.TryGetValue(key, out var map))
                {
                    map = new Dictionary<string, int>();
                    groupCourseDayCount[key] = map;
                }

                map.TryGetValue(d.Name, out var count);
                map[d.Name] = count + 1;
            }

            int GetGroupKindDayCount(ClassGroup g, CourseKind kind, Day d)
            {
                if (g == null || d == null) return 0;
                var key = g.Name + "|" + ((int)kind).ToString();
                if (!groupKindDayCount.TryGetValue(key, out var map)) return 0;
                return map.TryGetValue(d.Name, out var count) ? count : 0;
            }

            void IncGroupKindDay(ClassGroup g, Course c, Day d)
            {
                if (g == null || c == null || d == null || c.Kind == CourseKind.Genel) return;
                var key = g.Name + "|" + ((int)c.Kind).ToString();
                if (!groupKindDayCount.TryGetValue(key, out var map))
                {
                    map = new Dictionary<string, int>();
                    groupKindDayCount[key] = map;
                }

                map.TryGetValue(d.Name, out var count);
                map[d.Name] = count + 1;
            }

            bool IsCourseKindDiscouraged(ClassGroup g, Course c, Day d, int slotIndex)
            {
                if (g == null || c == null || d == null || c.Kind == CourseKind.Genel) return false;
                return store.CourseKindSlotRules.Any(r =>
                    r.Group == g &&
                    r.Day == d &&
                    r.SlotIndex == slotIndex &&
                    r.Kind == c.Kind);
            }

            int GetAdjacentTeacherCount(Teacher t, Day d, int slotIndex)
            {
                if (!options.AvoidConsecutiveTeacherLessons || t == null || d == null) return 0;

                int count = 0;
                if (occTeacher.Contains(t.Name + "|" + d.Name + "|" + (slotIndex - 1))) count++;
                if (occTeacher.Contains(t.Name + "|" + d.Name + "|" + (slotIndex + 1))) count++;
                return count;
            }

            bool IsAllowed(ClassGroup g, Day d, int slotIndex)
            {
                if (!options.RespectGroupSlotRules) return true;
                var rule = store.GroupSlotRules.FirstOrDefault(r => r.Group == g && r.Day == d && r.SlotIndex == slotIndex);
                return rule == null || rule.IsAllowed;
            }

            bool IsTeacherDutyDay(Teacher t, Day d)
            {
                return options.UseDutyDayPriority &&
                    t != null &&
                    d != null &&
                    t.DutyDayNames.Contains(d.Name);
            }

            bool OverlapsLunchBreak(TimeSlot slot)
            {
                if (!options.RespectLunchBreak || slot == null) return false;
                return slot.Start < store.LunchBreakEnd && slot.End > store.LunchBreakStart;
            }

            bool TeacherHalfDayOk(Teacher t, TimeSlot slot)
            {
                if (!options.RespectTeacherHalfDay || t == null || slot == null) return true;

                if (t.HalfDayAvailability == HalfDayAvailability.Morning)
                    return slot.End <= store.LunchBreakStart;

                if (t.HalfDayAvailability == HalfDayAvailability.Afternoon)
                    return slot.Start >= store.LunchBreakEnd;

                return true;
            }

            bool TeacherDetailedAvailabilityOk(Teacher t, Day day, int slotIndex)
            {
                if (!options.UseDetailedTeacherAvailability || t == null || day == null) return true;
                return !t.UnavailableSlotKeys.Contains(day.Name + "|" + slotIndex);
            }

            bool IsFree(ClassGroup g, Teacher t, Room r, Day d, int slotIndex)
            {
                var groupKey = (g != null ? g.Name : "") + "|" + (d != null ? d.Name : "") + "|" + slotIndex;
                var teacherKey = (t != null ? t.Name : "") + "|" + (d != null ? d.Name : "") + "|" + slotIndex;
                var roomKey = (r != null ? r.Name : "") + "|" + (d != null ? d.Name : "") + "|" + slotIndex;

                if (g != null && occGroup.Contains(groupKey)) return false;
                if (t != null && occTeacher.Contains(teacherKey)) return false;
                if (r != null && occRoom.Contains(roomKey)) return false;
                return true;
            }

            bool AreCoursesPaired(Course firstCourse, Course secondCourse)
            {
                if (firstCourse == null || secondCourse == null || firstCourse == secondCourse)
                    return false;

                return store.CourseConflictPairs.Any(pair => pair != null &&
                    ((pair.FirstCourse == firstCourse && pair.SecondCourse == secondCourse) ||
                     (pair.FirstCourse == secondCourse && pair.SecondCourse == firstCourse)));
            }

            bool HasPairedCourseAtSlot(Course course, Day day, int slotIndex)
            {
                if (course == null || day == null)
                    return false;

                return result.Schedule.Any(entry =>
                    entry.Day == day &&
                    entry.SlotIndex == slotIndex &&
                    AreCoursesPaired(course, entry.Course));
            }

            int GetGroupGapCount(ClassGroup group, Day day, int candidateStartIndex, int candidateBlockSize)
            {
                if (group == null || day == null) return 0;

                var occupiedSlots = new HashSet<int>(result.Schedule
                    .Where(e => e.Group == group && e.Day == day)
                    .Select(e => e.SlotIndex));

                for (int k = 0; k < candidateBlockSize; k++)
                    occupiedSlots.Add(candidateStartIndex + k);

                var usableSlots = day.Slots
                    .OrderBy(s => s.Index)
                    .Where(s => IsAllowed(group, day, s.Index) && !OverlapsLunchBreak(s))
                    .Select(s => s.Index)
                    .ToList();

                var occupiedUsableSlots = usableSlots.Where(occupiedSlots.Contains).ToList();
                if (occupiedUsableSlots.Count < 2) return 0;

                var first = occupiedUsableSlots.First();
                var last = occupiedUsableSlots.Last();
                return usableSlots.Count(slotIndex =>
                    slotIndex > first && slotIndex < last && !occupiedSlots.Contains(slotIndex));
            }

            void MarkUsed(ClassGroup g, Teacher t, Room r, Day d, int slotIndex, Course c)
            {
                if (g != null) occGroup.Add(g.Name + "|" + d.Name + "|" + slotIndex);
                if (t != null) occTeacher.Add(t.Name + "|" + d.Name + "|" + slotIndex);
                if (r != null) occRoom.Add(r.Name + "|" + d.Name + "|" + slotIndex);
                IncGroupCourseDay(g, c, d);
                IncGroupKindDay(g, c, d);
            }

            void DecTeacherDay(Teacher t, Day d)
            {
                if (t == null || d == null) return;
                if (!teacherDayCount.TryGetValue(t.Name, out var dayMap)) return;
                if (!dayMap.TryGetValue(d.Name, out var count)) return;

                if (count <= 1)
                    dayMap.Remove(d.Name);
                else
                    dayMap[d.Name] = count - 1;

                if (dayMap.Count == 0)
                    teacherDayCount.Remove(t.Name);
            }

            void DecGroupCourseDay(ClassGroup g, Course c, Day d)
            {
                if (g == null || c == null || d == null) return;
                var key = g.Name + "|" + c.Name;
                if (!groupCourseDayCount.TryGetValue(key, out var map)) return;
                if (!map.TryGetValue(d.Name, out var count)) return;

                if (count <= 1)
                    map.Remove(d.Name);
                else
                    map[d.Name] = count - 1;

                if (map.Count == 0)
                    groupCourseDayCount.Remove(key);
            }

            void DecGroupKindDay(ClassGroup g, Course c, Day d)
            {
                if (g == null || c == null || d == null || c.Kind == CourseKind.Genel) return;
                var key = g.Name + "|" + ((int)c.Kind).ToString();
                if (!groupKindDayCount.TryGetValue(key, out var map)) return;
                if (!map.TryGetValue(d.Name, out var count)) return;

                if (count <= 1)
                    map.Remove(d.Name);
                else
                    map[d.Name] = count - 1;

                if (map.Count == 0)
                    groupKindDayCount.Remove(key);
            }

            void MarkUnused(ScheduleEntry entry)
            {
                if (entry == null) return;

                if (entry.Group != null)
                    occGroup.Remove(entry.Group.Name + "|" + entry.Day.Name + "|" + entry.SlotIndex);
                if (entry.Teacher != null)
                    occTeacher.Remove(entry.Teacher.Name + "|" + entry.Day.Name + "|" + entry.SlotIndex);
                if (entry.Room != null)
                    occRoom.Remove(entry.Room.Name + "|" + entry.Day.Name + "|" + entry.SlotIndex);

                DecGroupCourseDay(entry.Group, entry.Course, entry.Day);
                DecGroupKindDay(entry.Group, entry.Course, entry.Day);
                DecTeacherDay(entry.Teacher, entry.Day);
                result.Schedule.Remove(entry);
            }

            bool IsFixedEntry(ScheduleEntry entry)
            {
                return store.FixedLessons.Any(f =>
                    f.Group == entry.Group &&
                    f.Day == entry.Day &&
                    f.Course == entry.Course &&
                    f.Teacher == entry.Teacher &&
                    entry.SlotIndex >= f.SlotIndex &&
                    entry.SlotIndex < f.SlotIndex + Math.Max(1, f.BlockSize));
            }

            StateSnapshot SnapshotState()
            {
                return new StateSnapshot
                {
                    Schedule = result.Schedule.Select(e => new ScheduleEntry
                    {
                        Group = e.Group,
                        Day = e.Day,
                        SlotIndex = e.SlotIndex,
                        Course = e.Course,
                        Teacher = e.Teacher,
                        Room = e.Room,
                        BlockSize = e.BlockSize,
                        BlockPos = e.BlockPos
                    }).ToList(),
                    OccGroup = new HashSet<string>(occGroup),
                    OccTeacher = new HashSet<string>(occTeacher),
                    OccRoom = new HashSet<string>(occRoom),
                    TeacherDayCount = teacherDayCount.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)),
                    GroupCourseDayCount = groupCourseDayCount.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)),
                    GroupKindDayCount = groupKindDayCount.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value))
                };
            }

            void RestoreState(StateSnapshot snapshot)
            {
                result.Schedule.Clear();
                result.Schedule.AddRange(snapshot.Schedule.Select(e => new ScheduleEntry
                {
                    Group = e.Group,
                    Day = e.Day,
                    SlotIndex = e.SlotIndex,
                    Course = e.Course,
                    Teacher = e.Teacher,
                    Room = e.Room,
                    BlockSize = e.BlockSize,
                    BlockPos = e.BlockPos
                }));

                occGroup.Clear();
                foreach (var key in snapshot.OccGroup)
                    occGroup.Add(key);

                occTeacher.Clear();
                foreach (var key in snapshot.OccTeacher)
                    occTeacher.Add(key);

                occRoom.Clear();
                foreach (var key in snapshot.OccRoom)
                    occRoom.Add(key);

                teacherDayCount.Clear();
                foreach (var kv in snapshot.TeacherDayCount)
                    teacherDayCount[kv.Key] = kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value);

                groupCourseDayCount.Clear();
                foreach (var kv in snapshot.GroupCourseDayCount)
                    groupCourseDayCount[kv.Key] = kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value);

                groupKindDayCount.Clear();
                foreach (var kv in snapshot.GroupKindDayCount)
                    groupKindDayCount[kv.Key] = kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value);
            }

            int EstimateCandidateStarts(Assignment a)
            {
                if (a == null || a.Group == null || a.Teacher == null) return 0;
                int total = 0;
                foreach (var day in store.Days)
                {
                    if (options.RespectTeacherUnavailableDays && a.Teacher.UnavailableDayNames.Contains(day.Name)) continue;
                    var slots = day.Slots.OrderBy(s => s.Index).ToList();
                    var slotIndexSet = new HashSet<int>(slots.Select(s => s.Index));
                    foreach (var slot in slots)
                    {
                        bool hasAll = true;
                        for (int k = 0; k < Math.Max(1, a.BlockSize); k++)
                        {
                            if (!slotIndexSet.Contains(slot.Index + k))
                            {
                                hasAll = false;
                                break;
                            }
                        }

                        if (hasAll)
                            total++;
                    }
                }

                return total;
            }

            var groupOrder = store.Groups
                .Select((g, i) => new { Group = g, Index = i })
                .Where(x => x.Group != null)
                .ToDictionary(x => x.Group, x => x.Index);

            var orderedAssignments = store.Assignments
                .Where(a => a.Group != null && a.Course != null && a.Teacher != null)
                .Select(a => new
                {
                    Assignment = a,
                    GroupOrder = groupOrder.TryGetValue(a.Group, out var order) ? order : int.MaxValue,
                    TeacherLoad = teacherWeeklyLoad.TryGetValue(a.Teacher.Name, out var tLoad) ? tLoad : 0,
                    GroupLoad = groupWeeklyLoad.TryGetValue(a.Group.Name, out var gLoad) ? gLoad : 0,
                    CandidateStarts = EstimateCandidateStarts(a),
                    Tie = rng.Next()
                })
                .OrderBy(x => options.UseClassByClassPlacement ? x.GroupOrder : 0)
                .ThenBy(x => x.CandidateStarts)
                .ThenByDescending(x => x.TeacherLoad)
                .ThenByDescending(x => x.GroupLoad)
                .ThenByDescending(x => x.Assignment.BlockSize)
                .ThenByDescending(x => x.Assignment.WeeklyHours)
                .ThenByDescending(x => x.Assignment.Group != null && x.Assignment.Group.IsPriority)
                .ThenByDescending(x => x.Assignment.Course != null && x.Assignment.Course.IsPriority)
                .ToList();

            if (randomizeOrder)
                orderedAssignments = orderedAssignments
                    .OrderBy(x => options.UseClassByClassPlacement ? x.GroupOrder : 0)
                    .ThenBy(x => x.CandidateStarts)
                    .ThenBy(x => x.Tie)
                    .ToList();

            bool TryPlaceBlock(Assignment a, ClassGroup group, Teacher teacher, Room fixedRoom, int blockSize)
            {
                const int AdjacentTeacherWeight = 50;
                const int CandidateRoomNoiseRange = 1000000;
                int bestPenalty = int.MaxValue;
                int bestTie = int.MaxValue;
                Day bestDay = null;
                int bestStartIndex = -1;
                Room bestRoom = null;

                foreach (var day in store.Days)
                {
                    if (day.Slots.Count == 0) continue;
                    if (options.RespectTeacherUnavailableDays && teacher != null && teacher.UnavailableDayNames.Contains(day.Name))
                        continue;

                    var slots = day.Slots.OrderBy(s => s.Index).ToList();
                    var slotIndexSet = new HashSet<int>(slots.Select(s => s.Index));
                    var slotByIndex = slots.ToDictionary(s => s.Index, s => s);

                    for (int i = 0; i < slots.Count; i++)
                    {
                        int startIndex = slots[i].Index;
                        bool hasAll = true;
                        for (int k = 0; k < blockSize; k++)
                        {
                            if (!slotIndexSet.Contains(startIndex + k))
                            {
                                hasAll = false;
                                break;
                            }
                        }

                        if (!hasAll) continue;

                        IEnumerable<Room> roomCandidates;
                        if (store.RandomizeRooms)
                        {
                            roomCandidates = store.Rooms.Count > 0
                                ? store.Rooms.OrderBy(_ => rng.Next()).ToList()
                                : new List<Room> { null };
                        }
                        else if (fixedRoom != null)
                        {
                            roomCandidates = new List<Room> { fixedRoom };
                        }
                        else if (store.Rooms.Count > 0)
                        {
                            roomCandidates = store.Rooms;
                        }
                        else
                        {
                            roomCandidates = new List<Room> { null };
                        }

                        foreach (var candidateRoom in roomCandidates)
                        {
                            bool ok = true;
                            int adjacentCountSum = 0;
                            int discouragedKindCount = 0;

                            for (int k = 0; k < blockSize; k++)
                            {
                                int idx = startIndex + k;
                                if (!slotByIndex.TryGetValue(idx, out var ts)) { ok = false; break; }
                                if (!IsAllowed(group, day, idx)) { ok = false; break; }
                                if (!IsFree(group, teacher, candidateRoom, day, idx)) { ok = false; break; }
                                if (HasPairedCourseAtSlot(a.Course, day, idx)) { ok = false; break; }
                                if (OverlapsLunchBreak(ts)) { ok = false; break; }
                                if (!TeacherHalfDayOk(teacher, ts)) { ok = false; break; }
                                if (!TeacherDetailedAvailabilityOk(teacher, day, idx)) { ok = false; break; }

                                if (options.UseMaxPerDay && a != null && a.MaxPerDay > 0 && GetGroupCourseDayCount(group, a.Course, day) >= a.MaxPerDay)
                                {
                                    ok = false;
                                    break;
                                }

                                adjacentCountSum += GetAdjacentTeacherCount(teacher, day, idx);
                                if (IsCourseKindDiscouraged(group, a != null ? a.Course : null, day, idx))
                                    discouragedKindCount++;
                            }

                            if (!ok) continue;

                            int penalty = 0;
                            // Aynı sınıfın gün içindeki derslerini mümkün olduğunca bitişik
                            // tut. Özellikle elle taşıma sonrasında oluşan ara boşlukları
                            // yeniden üretim sırasında kapatır.
                            penalty += GetGroupGapCount(group, day, startIndex, blockSize) * 140;
                            if (store.PreferMorning)
                            {
                                const int MorningWeight = 4;
                                penalty += startIndex * MorningWeight;
                            }

                            if (options.UseCoursePriorityLevel && group != null && group.IsPriority && a.Course != null && a.Course.IsPriority)
                            {
                                const int EarlySlotWeight = 25;
                                penalty += startIndex * EarlySlotWeight;
                            }

                            if (options.UseCoursePriorityLevel && a != null && a.Course != null)
                            {
                                var level = Math.Max(1, Math.Min(5, a.Course.PriorityLevel));
                                penalty += startIndex * level * 7;
                            }

                            if (options.AvoidConsecutiveTeacherLessons)
                                penalty += adjacentCountSum * AdjacentTeacherWeight;

                            if (options.BalanceTeacherAcrossDays)
                                penalty += GetTeacherDayCount(teacher, day);

                            if (options.UseSpreadAcrossDays && a != null && a.SpreadAcrossDays)
                            {
                                const int SpreadWeight = 30;
                                penalty += GetGroupCourseDayCount(group, a.Course, day) * SpreadWeight;
                            }

                            if (discouragedKindCount > 0)
                                penalty += discouragedKindCount * 420;

                            if (!options.IgnoreSoftPreferences && store.PreferMinimumVerbalPerDay && a != null && a.Course != null && group != null)
                            {
                                var verbalCount = GetGroupKindDayCount(group, CourseKind.Sozel, day);
                                if (verbalCount < store.MinimumVerbalPerDay)
                                {
                                    if (a.Course.Kind == CourseKind.Sozel)
                                        penalty -= 120;
                                    else
                                        penalty += 35;
                                }
                            }

                            if (!options.IgnoreSoftPreferences && store.PreferMinimumNumericPerDay && a != null && a.Course != null && group != null)
                            {
                                var numericCount = GetGroupKindDayCount(group, CourseKind.Sayisal, day);
                                if (numericCount < store.MinimumNumericPerDay)
                                {
                                    if (a.Course.Kind == CourseKind.Sayisal)
                                        penalty -= 120;
                                    else
                                        penalty += 35;
                                }
                            }

                            if (options.UseTeacherCoursePreferences && teacher != null && a != null && a.Course != null)
                            {
                                if (teacher.PreferredCourseNames.Contains(a.Course.Name))
                                    penalty -= 35;
                                if (teacher.UnwantedCourseNames.Contains(a.Course.Name))
                                    penalty += 220;
                            }

                            if (IsTeacherDutyDay(teacher, day))
                            {
                                const int DutyDayWeight = 90;
                                penalty -= DutyDayWeight;
                                penalty -= GetTeacherDayCount(teacher, day) * 6;
                            }

                            penalty += GetTeacherDayCount(teacher, day) * 3;
                            penalty += GetGroupCourseDayCount(group, a.Course, day) * 10;

                            int tie = randomizeOrder ? rng.Next(0, CandidateRoomNoiseRange) : startIndex;
                            if (penalty < bestPenalty || (penalty == bestPenalty && tie < bestTie))
                            {
                                bestPenalty = penalty;
                                bestTie = tie;
                                bestDay = day;
                                bestStartIndex = startIndex;
                                bestRoom = candidateRoom;
                            }
                        }
                    }
                }

                if (bestDay == null) return false;

                for (int k = 0; k < blockSize; k++)
                {
                    int idx = bestStartIndex + k;
                    result.Schedule.Add(new ScheduleEntry
                    {
                        Group = group,
                        Day = bestDay,
                        SlotIndex = idx,
                        Course = a.Course,
                        Teacher = teacher,
                        Room = bestRoom,
                        BlockSize = blockSize,
                        BlockPos = k + 1
                    });

                    MarkUsed(group, teacher, bestRoom, bestDay, idx, a.Course);
                    IncTeacherDay(teacher, bestDay);
                }

                return true;
            }

            bool CanUseSpecificSlot(Assignment a, ClassGroup group, Teacher teacher, Room room, Day day, int slotIndex)
            {
                var slot = day.Slots.FirstOrDefault(s => s.Index == slotIndex);
                if (slot == null) return false;
                if (options.RespectTeacherUnavailableDays && teacher != null && teacher.UnavailableDayNames.Contains(day.Name)) return false;
                if (!IsAllowed(group, day, slotIndex)) return false;
                if (HasPairedCourseAtSlot(a != null ? a.Course : null, day, slotIndex)) return false;
                if (!TeacherHalfDayOk(teacher, slot)) return false;
                if (!TeacherDetailedAvailabilityOk(teacher, day, slotIndex)) return false;
                if (OverlapsLunchBreak(slot)) return false;
                if (options.UseMaxPerDay && a != null && a.MaxPerDay > 0 && GetGroupCourseDayCount(group, a.Course, day) >= a.MaxPerDay)
                    return false;
                return true;
            }

            bool TryPlaceSingleAt(Assignment a, ClassGroup group, Teacher teacher, Room room, Day day, int slotIndex)
            {
                if (!CanUseSpecificSlot(a, group, teacher, room, day, slotIndex)) return false;
                if (!IsFree(group, teacher, room, day, slotIndex)) return false;

                result.Schedule.Add(new ScheduleEntry
                {
                    Group = group,
                    Day = day,
                    SlotIndex = slotIndex,
                    Course = a.Course,
                    Teacher = teacher,
                    Room = room,
                    BlockSize = 1,
                    BlockPos = 1
                });

                MarkUsed(group, teacher, room, day, slotIndex, a.Course);
                IncTeacherDay(teacher, day);
                return true;
            }

            Assignment FindAssignmentForEntry(ScheduleEntry entry)
            {
                if (entry == null) return null;
                return store.Assignments.FirstOrDefault(a =>
                    a.Group == entry.Group &&
                    a.Course == entry.Course &&
                    a.Teacher == entry.Teacher);
            }

            IEnumerable<(Day Day, int SlotIndex, int Penalty)> EnumerateSingleSlotCandidates(
                Assignment a,
                ClassGroup group,
                Teacher teacher,
                Room room,
                Day excludedDay,
                int excludedSlotIndex)
            {
                foreach (var day in store.Days)
                {
                    foreach (var slot in day.Slots.OrderBy(s => s.Index))
                    {
                        if (day == excludedDay && slot.Index == excludedSlotIndex)
                            continue;
                        if (!CanUseSpecificSlot(a, group, teacher, room, day, slot.Index))
                            continue;

                        int penalty = 0;
                        if (store.PreferMorning)
                            penalty += slot.Index * 4;

                        if (options.UseCoursePriorityLevel && group != null && group.IsPriority && a != null && a.Course != null && a.Course.IsPriority)
                            penalty += slot.Index * 25;

                        if (options.UseCoursePriorityLevel && a != null && a.Course != null)
                        {
                            var level = Math.Max(1, Math.Min(5, a.Course.PriorityLevel));
                            penalty += slot.Index * level * 7;
                        }

                        if (options.AvoidConsecutiveTeacherLessons)
                            penalty += GetAdjacentTeacherCount(teacher, day, slot.Index) * 50;

                        if (options.BalanceTeacherAcrossDays)
                            penalty += GetTeacherDayCount(teacher, day);

                        if (options.UseSpreadAcrossDays && a != null && a.SpreadAcrossDays)
                            penalty += GetGroupCourseDayCount(group, a.Course, day) * 30;

                        if (IsCourseKindDiscouraged(group, a != null ? a.Course : null, day, slot.Index))
                            penalty += 420;

                        if (!options.IgnoreSoftPreferences && store.PreferMinimumVerbalPerDay && a != null && a.Course != null && group != null)
                        {
                            var verbalCount = GetGroupKindDayCount(group, CourseKind.Sozel, day);
                            if (verbalCount < store.MinimumVerbalPerDay)
                            {
                                if (a.Course.Kind == CourseKind.Sozel)
                                    penalty -= 120;
                                else
                                    penalty += 35;
                            }
                        }

                        if (!options.IgnoreSoftPreferences && store.PreferMinimumNumericPerDay && a != null && a.Course != null && group != null)
                        {
                            var numericCount = GetGroupKindDayCount(group, CourseKind.Sayisal, day);
                            if (numericCount < store.MinimumNumericPerDay)
                            {
                                if (a.Course.Kind == CourseKind.Sayisal)
                                    penalty -= 120;
                                else
                                    penalty += 35;
                            }
                        }

                        if (options.UseTeacherCoursePreferences && teacher != null && a != null && a.Course != null)
                        {
                            if (teacher.PreferredCourseNames.Contains(a.Course.Name))
                                penalty -= 35;
                            if (teacher.UnwantedCourseNames.Contains(a.Course.Name))
                                penalty += 220;
                        }

                        if (IsTeacherDutyDay(teacher, day))
                        {
                            penalty -= 90;
                            penalty -= GetTeacherDayCount(teacher, day) * 6;
                        }

                        penalty += GetTeacherDayCount(teacher, day) * 3;
                        penalty += GetGroupCourseDayCount(group, a.Course, day) * 10;

                        var conflicts = result.Schedule
                            .Where(e => e.Day == day &&
                                        e.SlotIndex == slot.Index &&
                                        (e.Group == group || e.Teacher == teacher || (room != null && e.Room == room)))
                            .Distinct()
                            .ToList();

                        penalty += conflicts.Count * 100;
                        penalty += conflicts.Count(e => e.BlockSize != 1 || IsFixedEntry(e)) * 1000;
                        yield return (day, slot.Index, penalty);
                    }
                }
            }

            bool TryMoveEntryWithRepair(
                ScheduleEntry entry,
                Day forbiddenDay,
                int forbiddenSlotIndex,
                int remainingDepth,
                HashSet<string> relocationStack)
            {
                if (entry == null || entry.BlockSize != 1) return false;
                if (IsFixedEntry(entry)) return false;

                var assignment = FindAssignmentForEntry(entry);
                if (assignment == null) return false;

                var entryKey = (entry.Group != null ? entry.Group.Name : "") + "|" +
                               (entry.Course != null ? entry.Course.Name : "") + "|" +
                               (entry.Teacher != null ? entry.Teacher.Name : "") + "|" +
                               entry.Day.Name + "|" + entry.SlotIndex;

                if (!relocationStack.Add(entryKey))
                    return false;

                foreach (var candidate in EnumerateSingleSlotCandidates(assignment, entry.Group, entry.Teacher, entry.Room, forbiddenDay, forbiddenSlotIndex)
                    .OrderBy(x => x.Penalty)
                    .ThenBy(x => x.Day.Name)
                    .ThenBy(x => x.SlotIndex))
                {
                    if (candidate.Day == entry.Day && candidate.SlotIndex == entry.SlotIndex)
                        continue;

                    if (TryPlaceSingleAt(assignment, entry.Group, entry.Teacher, entry.Room, candidate.Day, candidate.SlotIndex))
                    {
                        relocationStack.Remove(entryKey);
                        return true;
                    }

                    if (remainingDepth <= 0)
                        continue;

                    var conflicts = result.Schedule
                        .Where(e => e.Day == candidate.Day &&
                                    e.SlotIndex == candidate.SlotIndex &&
                                    (e.Group == entry.Group ||
                                     e.Teacher == entry.Teacher ||
                                     (entry.Room != null && e.Room == entry.Room)))
                        .Distinct()
                        .ToList();

                    if (conflicts.Count == 0 || conflicts.Any(e => e.BlockSize != 1 || IsFixedEntry(e)))
                        continue;

                    if (conflicts.Any(conflict =>
                    {
                        var key = (conflict.Group != null ? conflict.Group.Name : "") + "|" +
                                  (conflict.Course != null ? conflict.Course.Name : "") + "|" +
                                  (conflict.Teacher != null ? conflict.Teacher.Name : "") + "|" +
                                  conflict.Day.Name + "|" + conflict.SlotIndex;
                        return relocationStack.Contains(key);
                    }))
                    {
                        continue;
                    }

                    var snapshot = SnapshotState();
                    bool ok = true;

                    foreach (var conflict in conflicts)
                        MarkUnused(conflict);

                    foreach (var conflict in conflicts.OrderBy(c => c.Group != null ? c.Group.Name : "").ThenBy(c => c.Course != null ? c.Course.Name : ""))
                    {
                        if (!TryMoveEntryWithRepair(conflict, candidate.Day, candidate.SlotIndex, remainingDepth - 1, relocationStack))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok && TryPlaceSingleAt(assignment, entry.Group, entry.Teacher, entry.Room, candidate.Day, candidate.SlotIndex))
                    {
                        relocationStack.Remove(entryKey);
                        return true;
                    }

                    RestoreState(snapshot);
                }

                relocationStack.Remove(entryKey);
                return false;
            }

            bool TryRepairAndPlaceSingleSlot(Assignment a, ClassGroup group, Teacher teacher, Room room)
            {
                foreach (var candidate in EnumerateSingleSlotCandidates(a, group, teacher, room, excludedDay: null, excludedSlotIndex: -1)
                    .OrderBy(x => x.Penalty)
                    .ThenBy(x => x.Day.Name)
                    .ThenBy(x => x.SlotIndex))
                {
                    var conflicts = result.Schedule
                        .Where(e => e.Day == candidate.Day &&
                                    e.SlotIndex == candidate.SlotIndex &&
                                    (e.Group == group || e.Teacher == teacher || (room != null && e.Room == room)))
                        .Distinct()
                        .ToList();

                    if (conflicts.Count == 0)
                    {
                        if (TryPlaceSingleAt(a, group, teacher, room, candidate.Day, candidate.SlotIndex))
                            return true;
                        continue;
                    }

                    if (conflicts.Any(e => e.BlockSize != 1 || IsFixedEntry(e)))
                        continue;

                    var snapshot = SnapshotState();
                    bool ok = true;
                    var relocationStack = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var conflict in conflicts)
                        MarkUnused(conflict);

                    foreach (var conflict in conflicts.OrderBy(c => c.Group != null ? c.Group.Name : "").ThenBy(c => c.Course != null ? c.Course.Name : ""))
                    {
                        if (!TryMoveEntryWithRepair(conflict, candidate.Day, candidate.SlotIndex, remainingDepth: 3, relocationStack))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok && TryPlaceSingleAt(a, group, teacher, room, candidate.Day, candidate.SlotIndex))
                        return true;

                    RestoreState(snapshot);
                }

                return false;
            }

            bool TryProgressivelyPlaceBlock(PendingPlacement pending)
            {
                if (pending == null || pending.Assignment == null) return false;
                var block = Math.Max(1, pending.BlockSize);
                if (TryPlaceBlock(pending.Assignment, pending.Group, pending.Teacher, pending.Room, block))
                    return true;

                return false;
            }

            PendingPlacement CreatePendingPlacement(Assignment a, ClassGroup group, Teacher teacher, Room room, int blockSize = 1)
            {
                if (a == null || group == null || teacher == null || a.Course == null)
                    return null;

                return new PendingPlacement
                {
                    Assignment = a,
                    Group = group,
                    Teacher = teacher,
                    Room = room,
                    BlockSize = blockSize
                };
            }

            PendingPlacement CreatePendingPlacementFromEntry(ScheduleEntry entry)
            {
                var assignment = FindAssignmentForEntry(entry);
                if (assignment == null) return null;
                return CreatePendingPlacement(assignment, entry.Group, entry.Teacher, entry.Room);
            }

            int EstimateSingleCandidateCount(PendingPlacement pending)
            {
                if (pending == null) return int.MaxValue;
                return EnumerateSingleSlotCandidates(
                        pending.Assignment,
                        pending.Group,
                        pending.Teacher,
                        pending.Room,
                        excludedDay: null,
                        excludedSlotIndex: -1)
                    .Count();
            }

            bool TryBacktrackingPendingSingles(List<PendingPlacement> pending, int index, int remainingBudget)
            {
                var maxBacktrackingNodes = options.BacktrackingNodeBudget;
                var maxBacktrackingMilliseconds = options.BacktrackingMillisecondBudget;
                var maxCandidateSlots = options.BacktrackingMaxCandidateSlots;

                if (progress != null)
                {
                    var elapsedForReport = backtrackingStopwatch.ElapsedMilliseconds;
                    if (elapsedForReport - lastBacktrackingProgressMs >= 200)
                    {
                        lastBacktrackingProgressMs = elapsedForReport;
                        progress.Invoke("Deneme " + attemptNumber + ": onarım taraması " + backtrackingVisitedNodeCount +
                            " / " + maxBacktrackingNodes + " adım (" + elapsedForReport + " ms)...");
                    }
                }

                if (index >= pending.Count)
                    return true;
                if (remainingBudget < 0)
                    return false;
                if (backtrackingVisitedNodeCount++ >= maxBacktrackingNodes)
                    return false;
                if (backtrackingStopwatch.ElapsedMilliseconds >= maxBacktrackingMilliseconds)
                    return false;

                var current = pending[index];
                if (current == null)
                    return TryBacktrackingPendingSingles(pending, index + 1, remainingBudget);

                var candidateSlots = EnumerateSingleSlotCandidates(current.Assignment, current.Group, current.Teacher, current.Room, excludedDay: null, excludedSlotIndex: -1)
                    .OrderBy(x => x.Penalty)
                    .ThenBy(x => x.Day.Name)
                    .ThenBy(x => x.SlotIndex)
                    .Take(maxCandidateSlots)
                    .ToList();

                foreach (var candidate in candidateSlots)
                {
                    var snapshot = SnapshotState();

                    if (TryPlaceSingleAt(current.Assignment, current.Group, current.Teacher, current.Room, candidate.Day, candidate.SlotIndex))
                    {
                        if (TryBacktrackingPendingSingles(pending, index + 1, remainingBudget))
                            return true;

                        RestoreState(snapshot);
                        continue;
                    }

                    if (remainingBudget == 0)
                        continue;

                    var conflicts = result.Schedule
                        .Where(e => e.Day == candidate.Day &&
                                    e.SlotIndex == candidate.SlotIndex &&
                                    (e.Group == current.Group ||
                                     e.Teacher == current.Teacher ||
                                     (current.Room != null && e.Room == current.Room)))
                        .Distinct()
                        .ToList();

                    if (conflicts.Count == 0)
                        continue;

                    if (conflicts.Any(e => e.BlockSize != 1 || IsFixedEntry(e)))
                        continue;

                    var displaced = conflicts
                        .Select(conflict => CreatePendingPlacementFromEntry(conflict))
                        .Where(p => p != null)
                        .ToList();

                    if (displaced.Count != conflicts.Count)
                        continue;

                    foreach (var conflict in conflicts)
                        MarkUnused(conflict);

                    if (!TryPlaceSingleAt(current.Assignment, current.Group, current.Teacher, current.Room, candidate.Day, candidate.SlotIndex))
                    {
                        RestoreState(snapshot);
                        continue;
                    }

                    pending.InsertRange(index + 1, displaced);
                    if (TryBacktrackingPendingSingles(pending, index + 1, remainingBudget - 1))
                        return true;

                    pending.RemoveRange(index + 1, displaced.Count);
                    RestoreState(snapshot);
                }

                return false;
            }

            foreach (var fixedLesson in store.FixedLessons)
            {
                if (fixedLesson == null) continue;
                if (fixedLesson.Group == null || fixedLesson.Day == null || fixedLesson.Course == null || fixedLesson.Teacher == null) continue;

                int block = Math.Max(1, fixedLesson.BlockSize);
                var daySlots = fixedLesson.Day.Slots.OrderBy(s => s.Index).ToList();
                var slotByIndex = daySlots.ToDictionary(s => s.Index, s => s);

                bool ok = true;
                for (int k = 0; k < block; k++)
                {
                    int idx = fixedLesson.SlotIndex + k;
                    if (!slotByIndex.TryGetValue(idx, out var ts)) { ok = false; break; }
                    if (!IsAllowed(fixedLesson.Group, fixedLesson.Day, idx)) { ok = false; break; }
                    if (!IsFree(fixedLesson.Group, fixedLesson.Teacher, fixedLesson.Room, fixedLesson.Day, idx)) { ok = false; break; }
                    if (HasPairedCourseAtSlot(fixedLesson.Course, fixedLesson.Day, idx)) { ok = false; break; }
                    if (OverlapsLunchBreak(ts)) { ok = false; break; }
                    if (!TeacherHalfDayOk(fixedLesson.Teacher, ts)) { ok = false; break; }
                    if (!TeacherDetailedAvailabilityOk(fixedLesson.Teacher, fixedLesson.Day, idx)) { ok = false; break; }
                }

                if (!ok)
                {
                    result.Warnings.Add("Sabit ders yerleştirilemedi: " + fixedLesson);
                    continue;
                }

                for (int k = 0; k < block; k++)
                {
                    int idx = fixedLesson.SlotIndex + k;
                    result.Schedule.Add(new ScheduleEntry
                    {
                        Group = fixedLesson.Group,
                        Day = fixedLesson.Day,
                        SlotIndex = idx,
                        Course = fixedLesson.Course,
                        Teacher = fixedLesson.Teacher,
                        Room = fixedLesson.Room,
                        BlockSize = block,
                        BlockPos = k + 1
                    });

                    MarkUsed(fixedLesson.Group, fixedLesson.Teacher, fixedLesson.Room, fixedLesson.Day, idx, fixedLesson.Course);
                    IncTeacherDay(fixedLesson.Teacher, fixedLesson.Day);
                }
            }

            foreach (var item in orderedAssignments)
            {
                var assignment = item.Assignment;
                var group = assignment.Group;
                var teacher = assignment.Teacher;
                var room = assignment.Room;
                int weekly = Math.Max(0, assignment.WeeklyHours);
                int block = Math.Max(1, assignment.BlockSize);

                if (weekly == 0)
                {
                    result.Warnings.Add((group != null ? group.Name : "") + " - " + (assignment.Course != null ? assignment.Course.Name : "") + ": WeeklyHours 0, atlandı.");
                    continue;
                }

                if (weekly % block != 0)
                {
                    result.Warnings.Add((group != null ? group.Name : "") + " - " + (assignment.Course != null ? assignment.Course.Name : "") + ": WeeklyHours (" + weekly + ") BlockSize (" + block + ") ile tam bölünmüyor. Kalan saatler tekli yerleştirilecek.");
                }

                int fixedCount = result.Schedule.Count(e => e.Group == group && e.Course == assignment.Course && e.Teacher == teacher);
                int remaining = weekly - fixedCount;
                if (remaining < 0) remaining = 0;
                bool usedFallbackSplit = false;

                while (remaining > 0)
                {
                    int desiredBlock = Math.Min(block, remaining);
                    int placedBlock = 0;

                    var smallestCandidateBlock = options.KeepBlocksStrict ? desiredBlock : 1;
                    for (int candidateBlock = desiredBlock; candidateBlock >= smallestCandidateBlock; candidateBlock--)
                    {
                        if (TryPlaceBlock(assignment, group, teacher, room, candidateBlock))
                        {
                            placedBlock = candidateBlock;
                            if (candidateBlock != desiredBlock)
                                usedFallbackSplit = true;
                            break;
                        }
                    }

                    if (placedBlock == 0)
                    {
                        if (desiredBlock == 1 && TryRepairAndPlaceSingleSlot(assignment, group, teacher, room))
                        {
                            placedBlock = 1;
                            usedFallbackSplit = true;
                            remaining -= placedBlock;
                            continue;
                        }

                        if (desiredBlock > 1 && options.KeepBlocksStrict && options.UseProgressiveImprovement)
                        {
                            var pendingBlock = CreatePendingPlacement(assignment, group, teacher, room, desiredBlock);
                            if (pendingBlock != null)
                            {
                                pendingBlockPlacements.Add(pendingBlock);
                                remaining -= desiredBlock;
                                continue;
                            }
                        }

                        if (desiredBlock == 1)
                        {
                            var pending = CreatePendingPlacement(assignment, group, teacher, room);
                            if (pending != null)
                            {
                                pendingSinglePlacements.Add(pending);
                                usedFallbackSplit = true;
                                remaining -= 1;
                                continue;
                            }
                        }

                        result.Warnings.Add("Yerleştirilemedi: " + (group != null ? group.Name : "") + " - " + (assignment.Course != null ? assignment.Course.Name : "") + " (" + desiredBlock + " saat blok)");
                        break;
                    }

                    remaining -= placedBlock;
                }

                if (usedFallbackSplit)
                {
                    result.Warnings.Add("Blok parçalanarak yerleştirildi: " + (group != null ? group.Name : "") + " - " + (assignment.Course != null ? assignment.Course.Name : ""));
                }
            }

            if (pendingBlockPlacements.Count > 0)
            {
                var orderedBlocks = pendingBlockPlacements
                    .OrderBy(p => EstimateSingleCandidateCount(p))
                    .ThenByDescending(p => p.BlockSize)
                    .ThenBy(p => p.Group.Name)
                    .ThenBy(p => p.CourseName)
                    .ToList();

                foreach (var pending in orderedBlocks)
                {
                    if (!TryProgressivelyPlaceBlock(pending))
                        result.Warnings.Add("Yerleştirilemedi: " + pending.Group.Name + " - " + pending.Assignment.Course.Name + " (" + pending.BlockSize + " saat blok)");
                }
            }

            if (pendingSinglePlacements.Count > 0)
            {
                var orderedPending = pendingSinglePlacements
                    .OrderBy(p => EstimateSingleCandidateCount(p))
                    .ThenByDescending(p => teacherWeeklyLoad.TryGetValue(p.Teacher.Name, out var load) ? load : 0)
                    .ThenBy(p => p.Group.Name)
                    .ThenBy(p => p.CourseName)
                    .ToList();

                bool canRunBacktracking = orderedPending.Count <= options.BacktrackingPendingCap;
                var backtrackingBudget = options.BacktrackingSwapBudget;

                if (!canRunBacktracking || !TryBacktrackingPendingSingles(orderedPending, 0, remainingBudget: backtrackingBudget))
                {
                    foreach (var pending in orderedPending)
                    {
                        var targetHours = result.Schedule.Count(e =>
                            e.Group == pending.Group &&
                            e.Teacher == pending.Teacher &&
                            e.Course == pending.Assignment.Course);

                        if (targetHours < pending.Assignment.WeeklyHours)
                        {
                            result.Warnings.Add("Yerleştirilemedi: " + pending.Group.Name + " - " + pending.Assignment.Course.Name + " (1 saat blok)");
                        }
                    }
                }
            }

            return result;
        }

        private static bool HasHardPlacementWarnings(ScheduleGenerationResult result)
        {
            return result.Warnings.Any(IsHardPlacementWarning);
        }

        private static bool IsBetter(ScheduleGenerationResult candidate, ScheduleGenerationResult current)
        {
            int candidatePlaced = candidate.Schedule.Count;
            int currentPlaced = current.Schedule.Count;
            if (candidatePlaced != currentPlaced)
                return candidatePlaced > currentPlaced;

            int candidateHardWarnings = candidate.Warnings.Count(IsHardPlacementWarning);
            int currentHardWarnings = current.Warnings.Count(IsHardPlacementWarning);
            if (candidateHardWarnings != currentHardWarnings)
                return candidateHardWarnings < currentHardWarnings;

            if (candidate.QualityScore != current.QualityScore)
                return candidate.QualityScore < current.QualityScore;

            return candidate.Warnings.Count < current.Warnings.Count;
        }

        private static int EvaluateQuality(ProjectStore store, ScheduleGenerationOptions options, ScheduleGenerationResult result)
        {
            if (store == null || options == null || result == null) return int.MaxValue;

            int score = 0;
            score += result.Warnings.Count(IsHardPlacementWarning) * 100000;
            score += result.Warnings.Count * 1000;

            var schedule = result.Schedule ?? new List<ScheduleEntry>();
            foreach (var group in store.Groups)
            {
                foreach (var day in store.Days)
                {
                    var dayEntries = schedule
                        .Where(e => e.Group == group && e.Day == day)
                        .ToList();

                    var occupiedSlots = new HashSet<int>(dayEntries.Select(e => e.SlotIndex));
                    var usableSlots = day.Slots
                        .OrderBy(s => s.Index)
                        .Where(s =>
                            (!options.RespectGroupSlotRules || !store.GroupSlotRules.Any(r => r.Group == group && r.Day == day && r.SlotIndex == s.Index && !r.IsAllowed)) &&
                            (!options.RespectLunchBreak || !(s.Start < store.LunchBreakEnd && s.End > store.LunchBreakStart)))
                        .Select(s => s.Index)
                        .ToList();
                    var occupiedUsableSlots = usableSlots.Where(occupiedSlots.Contains).ToList();
                    if (occupiedUsableSlots.Count > 1)
                    {
                        var firstOccupiedSlot = occupiedUsableSlots.First();
                        var lastOccupiedSlot = occupiedUsableSlots.Last();
                        score += usableSlots.Count(slotIndex =>
                            slotIndex > firstOccupiedSlot &&
                            slotIndex < lastOccupiedSlot &&
                            !occupiedSlots.Contains(slotIndex)) * 140;
                    }

                    if (!options.IgnoreSoftPreferences && store.PreferMinimumVerbalPerDay)
                    {
                        var verbal = dayEntries.Count(e => e.Course != null && e.Course.Kind == CourseKind.Sozel);
                        if (verbal < store.MinimumVerbalPerDay)
                            score += (store.MinimumVerbalPerDay - verbal) * 90;
                    }

                    if (!options.IgnoreSoftPreferences && store.PreferMinimumNumericPerDay)
                    {
                        var numeric = dayEntries.Count(e => e.Course != null && e.Course.Kind == CourseKind.Sayisal);
                        if (numeric < store.MinimumNumericPerDay)
                            score += (store.MinimumNumericPerDay - numeric) * 90;
                    }
                }
            }

            foreach (var entry in schedule)
            {
                if (entry == null) continue;

                if (store.PreferMorning && entry.Course != null)
                    score += Math.Max(0, entry.SlotIndex - 1) * 2;

                if (options.UseCoursePriorityLevel && entry.Course != null)
                {
                    var level = Math.Max(1, Math.Min(5, entry.Course.PriorityLevel));
                    score += Math.Max(0, entry.SlotIndex - 1) * level;

                    if (entry.Group != null &&
                        entry.Group.IsPriority &&
                        entry.Course.IsPriority)
                    {
                        score += Math.Max(0, entry.SlotIndex - 1) * 6;
                    }
                }

                if (entry.Group != null && entry.Course != null && entry.Day != null)
                {
                    if (store.CourseKindSlotRules.Any(r =>
                        r.Group == entry.Group &&
                        r.Day == entry.Day &&
                        r.SlotIndex == entry.SlotIndex &&
                        r.Kind == entry.Course.Kind))
                    {
                        score += 500;
                    }
                }

                if (options.UseTeacherCoursePreferences && entry.Teacher != null && entry.Course != null)
                {
                    if (entry.Teacher.UnwantedCourseNames.Contains(entry.Course.Name))
                        score += 240;
                    if (entry.Teacher.PreferredCourseNames.Contains(entry.Course.Name))
                        score -= 45;
                }
            }

            if (options.AvoidConsecutiveTeacherLessons)
            {
                var teacherDayGroups = schedule
                    .Where(e => e.Teacher != null && e.Day != null)
                    .GroupBy(e => e.Teacher.Name + "|" + e.Day.Name);

                foreach (var group in teacherDayGroups)
                {
                    var slots = new HashSet<int>(group.Select(e => e.SlotIndex));
                    foreach (var slot in slots)
                    {
                        if (slots.Contains(slot + 1))
                            score += 70;
                    }
                }
            }

            if (options.BalanceTeacherAcrossDays)
            {
                var teacherGroups = schedule
                    .Where(e => e.Teacher != null && e.Day != null)
                    .GroupBy(e => e.Teacher.Name);

                foreach (var teacher in teacherGroups)
                {
                    var counts = teacher
                        .GroupBy(e => e.Day.Name)
                        .Select(g => g.Count())
                        .ToList();

                    if (counts.Count > 1)
                        score += (counts.Max() - counts.Min()) * 30;
                }
            }

            if (options.UseSpreadAcrossDays || options.UseMaxPerDay)
            {
                foreach (var assignment in store.Assignments.Where(a => a.Group != null && a.Course != null))
                {
                    var counts = schedule
                        .Where(e => e.Group == assignment.Group && e.Course == assignment.Course)
                        .GroupBy(e => e.Day)
                        .Select(g => g.Count())
                        .ToList();

                    if (options.UseSpreadAcrossDays && assignment.SpreadAcrossDays)
                        score += counts.Where(c => c > 1).Sum(c => (c - 1) * 55);

                    if (options.UseMaxPerDay && assignment.MaxPerDay > 0)
                        score += counts.Where(c => c > assignment.MaxPerDay).Sum(c => (c - assignment.MaxPerDay) * 120);
                }
            }

            return score;
        }

        private static bool IsHardPlacementWarning(string warning)
        {
            return !string.IsNullOrWhiteSpace(warning) &&
                (warning.StartsWith("Yerleştirilemedi:", StringComparison.Ordinal) ||
                 warning.StartsWith("Sabit ders yerleştirilemedi:", StringComparison.Ordinal));
        }

        private sealed class StateSnapshot
        {
            public List<ScheduleEntry> Schedule { get; set; }
            public HashSet<string> OccGroup { get; set; }
            public HashSet<string> OccTeacher { get; set; }
            public HashSet<string> OccRoom { get; set; }
            public Dictionary<string, Dictionary<string, int>> TeacherDayCount { get; set; }
            public Dictionary<string, Dictionary<string, int>> GroupCourseDayCount { get; set; }
            public Dictionary<string, Dictionary<string, int>> GroupKindDayCount { get; set; }
        }

            private sealed class PendingPlacement
            {
                public Assignment Assignment { get; set; }
                public ClassGroup Group { get; set; }
                public Teacher Teacher { get; set; }
                public Room Room { get; set; }
                public int BlockSize { get; set; }
                public string CourseName => Assignment != null && Assignment.Course != null ? Assignment.Course.Name : string.Empty;
            }
    }
}
