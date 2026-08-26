using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoScheduler.Core.Services
{
    public enum SchoolPreset
    {
        Ilkokul = 0,
        Ortaokul = 1,
        Lise = 2,
        Universite = 3
    }

    public static class ProjectPresetService
    {
        public static void ResetProject(ProjectStore store)
        {
            if (store == null) return;

            store.ProjectName = "Yeni Proje";
            store.EducationMode = EducationMode.HigherEducation;
            store.LunchBreakStart = new TimeSpan(12, 0, 0);
            store.LunchBreakEnd = new TimeSpan(13, 0, 0);
            store.RandomizeRooms = false;
            store.PreferMorning = false;
            store.RespectTeacherUnavailableDays = true;
            store.RespectGroupSlotRules = true;
            store.RespectLunchBreak = true;
            store.RespectTeacherHalfDay = true;
            store.UseDutyDayPriority = true;
            store.UseCoursePriorityLevel = true;
            store.UseTeacherCoursePreferences = true;
            store.UseSpreadAcrossDays = true;
            store.UseMaxPerDay = true;
            store.UseDetailedTeacherAvailability = true;
            store.UseIntensiveRepairSearch = true;
            store.UseClassByClassPlacement = true;
            store.UseProgressiveImprovement = true;
            store.UseParallelSearch = true;
            store.PreferMinimumVerbalPerDay = false;
            store.MinimumVerbalPerDay = 1;
            store.PreferMinimumNumericPerDay = false;
            store.MinimumNumericPerDay = 1;
            store.KeepBlocksStrict = true;
            store.DeepSearchEnabled = true;
            store.MaxGenerationAttempts = 5000;
            store.UseRelaxationOrder = true;
            store.SearchStrategy = GenerationSearchStrategy.Standart;
            store.Teachers.Clear();
            store.Rooms.Clear();
            store.Days.Clear();
            store.Courses.Clear();
            store.Assignments.Clear();
            store.GroupSlotRules.Clear();
            store.CourseKindSlotRules.Clear();
            store.FixedLessons.Clear();
            store.Groups.Clear();
        }

        public static void ApplySchoolPreset(ProjectStore store, SchoolPreset preset, string projectName, IEnumerable<string> groupNames)
        {
            if (store == null) return;

            ResetProject(store);

            store.ProjectName = string.IsNullOrWhiteSpace(projectName)
                ? GetDefaultProjectName(preset)
                : projectName.Trim();

            store.EducationMode = EducationMode.HigherEducation;

            // İlkokulda günlük ders saati sayısı diğer seviyelerden daha az; geri kalan
            // seviyeler (Ortaokul/Lise/Üniversite) standart 8 ders saatinde kalır.
            var slotsPerDay = preset == SchoolPreset.Ilkokul ? 6 : 8;
            ApplyStandardWeek(store, slotsPerDay);
            ApplyGroups(store, groupNames);
        }

        public static void ApplyStandardWeek(ProjectStore store, int slotsPerDay = 8)
        {
            if (store == null) return;

            store.Days.Clear();
            store.GroupSlotRules.Clear();
            store.CourseKindSlotRules.Clear();
            store.FixedLessons.Clear();
            foreach (var name in new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma" })
            {
                var day = new Day { Name = name };
                foreach (var slot in CreateStandardSlots(slotsPerDay))
                    day.Slots.Add(slot);
                store.Days.Add(day);
            }
        }

        public static void ApplyGroups(ProjectStore store, IEnumerable<string> groupNames)
        {
            if (store == null) return;

            store.Groups.Clear();
            foreach (var name in (groupNames ?? Enumerable.Empty<string>())
                .Select(x => x != null ? x.Trim() : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase))
            {
                store.Groups.Add(new ClassGroup { Name = name });
            }
        }

        private static IEnumerable<TimeSlot> CreateStandardSlots(int count = 8)
        {
            var current = new TimeSpan(8, 30, 0);
            for (int i = 1; i <= count; i++)
            {
                var next = current.Add(new TimeSpan(0, 40, 0));
                yield return new TimeSlot
                {
                    Index = i,
                    Start = current,
                    End = next,
                    Label = string.Format("{0}. Ders", i)
                };
                current = next.Add(new TimeSpan(0, 10, 0));
            }
        }

        private static string GetDefaultProjectName(SchoolPreset preset)
        {
            switch (preset)
            {
                case SchoolPreset.Ilkokul:
                    return "İlkokul Programı";
                case SchoolPreset.Ortaokul:
                    return "Ortaokul Programı";
                case SchoolPreset.Lise:
                    return "Lise Programı";
                case SchoolPreset.Universite:
                    return "Üniversite Programı";
                default:
                    return "Yeni Proje";
            }
        }
    }
}
