using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoScheduler.Core.Services
{
    public enum ProjectReadinessLevel
    {
        HazirDegil = 0,
        KismenHazir = 1,
        Hazir = 2
    }

    public sealed class ProjectHealthItem
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public int NavigateTabIndex { get; set; }
        public string ActionLabel { get; set; }
        public bool IsBlocking { get; set; }
    }

    public sealed class ProjectHealthReport
    {
        public ProjectReadinessLevel ReadinessLevel { get; set; }
        public string ReadinessLabel { get; set; }
        public string Summary { get; set; }
        public int Score { get; set; }
        public int BlockingCount { get; set; }
        public List<ProjectHealthItem> Items { get; } = new List<ProjectHealthItem>();
    }

    public static class ProjectHealthService
    {
        public static ProjectHealthReport Analyze(ProjectStore store)
        {
            var report = new ProjectHealthReport();
            if (store == null)
            {
                report.ReadinessLevel = ProjectReadinessLevel.HazirDegil;
                report.ReadinessLabel = "Hazır değil";
                report.Summary = "Proje verisi bulunamadı.";
                return report;
            }

            AddCollectionChecks(store, report);
            AddScheduleChecks(store, report);
            AddAssignmentChecks(store, report);
            AddTeacherChecks(store, report);

            var validationIssues = ProjectValidationService.Validate(store);
            foreach (var issue in validationIssues.Take(6))
            {
                report.Items.Add(new ProjectHealthItem
                {
                    Title = "Düzeltilecek veri sorunu",
                    Detail = issue,
                    NavigateTabIndex = 1,
                    ActionLabel = "Şablona git",
                    IsBlocking = true
                });
            }

            report.BlockingCount = report.Items.Count(x => x.IsBlocking);

            var baseScore = 100;
            baseScore -= report.Items.Count(x => x.IsBlocking) * 18;
            baseScore -= report.Items.Count(x => !x.IsBlocking) * 7;
            report.Score = Math.Max(0, Math.Min(100, baseScore));

            if (report.BlockingCount > 0)
            {
                report.ReadinessLevel = ProjectReadinessLevel.HazirDegil;
                report.ReadinessLabel = "Hazır değil";
            }
            else if (report.Items.Count > 0)
            {
                report.ReadinessLevel = ProjectReadinessLevel.KismenHazir;
                report.ReadinessLabel = "Kısmen hazır";
            }
            else
            {
                report.ReadinessLevel = ProjectReadinessLevel.Hazir;
                report.ReadinessLabel = "Program üretmeye hazır";
            }

            report.Summary = BuildSummary(store, report);
            return report;
        }

        private static void AddCollectionChecks(ProjectStore store, ProjectHealthReport report)
        {
            if (store.Days.Count == 0)
                report.Items.Add(MakeBlocking("Gün tanımı yok", "Önce haftanın günlerini ekleyin.", 1, "Şablona git"));

            if (store.Groups.Count == 0)
                report.Items.Add(MakeBlocking("Sınıf tanımı yok", "Program üretmeden önce en az bir sınıf ekleyin.", 1, "Şablona git"));

            if (store.Courses.Count == 0)
                report.Items.Add(MakeBlocking("Ders tanımı yok", "Önce ders listesini oluşturun.", 2, "Hoca-Ders'e git"));

            if (store.Teachers.Count == 0)
                report.Items.Add(MakeBlocking("Öğretmen tanımı yok", "En az bir öğretmen ekleyin.", 2, "Hoca-Ders'e git"));

            if (store.Assignments.Count == 0)
                report.Items.Add(MakeBlocking("Atama bulunmuyor", "Sınıf, ders ve öğretmen eşleştirmeleri olmadan program üretilemez.", 2, "Atamalara git"));
        }

        private static void AddScheduleChecks(ProjectStore store, ProjectHealthReport report)
        {
            if (store.Days.Count == 0)
                return;

            var daysWithoutSlots = store.Days.Where(d => d == null || d.Slots.Count == 0).ToList();
            if (daysWithoutSlots.Count > 0)
            {
                report.Items.Add(MakeBlocking(
                    "Boş günler var",
                    string.Join(", ", daysWithoutSlots.Select(d => d != null ? d.Name : "Adsız gün")) + " için ders saati ekleyin.",
                    1,
                    "Saatleri düzenle"));
            }

            var usesOnlyEightSlots = store.Days
                .Where(d => d != null)
                .All(d => d.Slots.Count == 0 || d.Slots.Max(s => s.Index) <= 8);
            if (!usesOnlyEightSlots)
            {
                report.Items.Add(new ProjectHealthItem
                {
                    Title = "8 ders saati sınırı aşılıyor",
                    Detail = "Bu projede varsayılan beklenti 8 ders saatidir. 9. ve 10. saatleri gözden geçirin.",
                    NavigateTabIndex = 1,
                    ActionLabel = "Şablona git",
                    IsBlocking = false
                });
            }

            var inconsistentSlotCounts = store.Days
                .Where(d => d != null)
                .Select(d => d.Slots.Count)
                .Distinct()
                .Count() > 1;
            if (inconsistentSlotCounts)
            {
                report.Items.Add(new ProjectHealthItem
                {
                    Title = "Günlerde saat sayısı farklı",
                    Detail = "Bazı günlerde daha az veya daha fazla ders saati var. Bu bilinçli değilse günleri eşitlemeniz iyi olur.",
                    NavigateTabIndex = 1,
                    ActionLabel = "Şablona git",
                    IsBlocking = false
                });
            }
        }

        private static void AddAssignmentChecks(ProjectStore store, ProjectHealthReport report)
        {
            if (store.Groups.Count == 0 || store.Assignments.Count == 0)
                return;

            var groupsWithoutAssignments = store.Groups
                .Where(g => store.Assignments.All(a => a.Group != g))
                .Select(g => g.Name)
                .ToList();

            if (groupsWithoutAssignments.Count > 0)
            {
                report.Items.Add(MakeBlocking(
                    "Ataması eksik sınıflar var",
                    string.Join(", ", groupsWithoutAssignments.Take(4)) +
                    (groupsWithoutAssignments.Count > 4 ? " ve diğerleri" : string.Empty),
                    2,
                    "Atama gir"));
            }

            var incompleteAssignments = store.Assignments
                .Where(a => a.Group == null || a.Course == null || a.Teacher == null || a.WeeklyHours <= 0)
                .Take(5)
                .ToList();

            if (incompleteAssignments.Count > 0)
            {
                report.Items.Add(MakeBlocking(
                    "Eksik atamalar bulunuyor",
                    "Bazı satırlarda ders, öğretmen veya haftalık saat bilgisi eksik.",
                    2,
                    "Atamaları düzelt"));
            }
        }

        private static void AddTeacherChecks(ProjectStore store, ProjectHealthReport report)
        {
            var teachersWithoutCourse = store.Teachers
                .Where(t => t.CanTeachCourses.Count == 0)
                .Select(t => t.Name)
                .ToList();

            if (teachersWithoutCourse.Count > 0)
            {
                report.Items.Add(new ProjectHealthItem
                {
                    Title = "Ders veremeyen öğretmenler var",
                    Detail = string.Join(", ", teachersWithoutCourse.Take(4)) +
                             (teachersWithoutCourse.Count > 4 ? " ve diğerleri" : string.Empty),
                    NavigateTabIndex = 3,
                    ActionLabel = "Öğretmenlere git",
                    IsBlocking = false
                });
            }
        }

        private static ProjectHealthItem MakeBlocking(string title, string detail, int tabIndex, string actionLabel)
        {
            return new ProjectHealthItem
            {
                Title = title,
                Detail = detail,
                NavigateTabIndex = tabIndex,
                ActionLabel = actionLabel,
                IsBlocking = true
            };
        }

        private static string BuildSummary(ProjectStore store, ProjectHealthReport report)
        {
            if (report.ReadinessLevel == ProjectReadinessLevel.Hazir)
                return "Temel veri seti tamam görünüyor. Program üretmeye geçebilirsiniz.";

            if (report.BlockingCount > 0)
                return string.Format("{0} kritik konu çözülmeden sağlıklı program üretimi beklenmez.", report.BlockingCount);

            return string.Format("{0} iyileştirme önerisi var. İsterseniz mevcut veriyle yine de program üretebilirsiniz.", report.Items.Count);
        }
    }
}
