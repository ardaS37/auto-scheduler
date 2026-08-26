using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoScheduler.Core.Services
{
    public static class ProjectValidationService
    {
        public static IReadOnlyList<string> Validate(ProjectStore store)
        {
            var issues = new List<string>();
            if (store == null) return issues;

            AddDuplicateNameIssues(issues, store.Days.Select(d => d != null ? d.Name : null), "gün");
            AddDuplicateNameIssues(issues, store.Groups.Select(g => g != null ? g.Name : null), "sınıf");
            AddDuplicateNameIssues(issues, store.Courses.Select(c => c != null ? c.Name : null), "ders");
            AddDuplicateNameIssues(issues, store.Teachers.Select(t => t != null ? t.Name : null), "hoca");
            AddDuplicateNameIssues(issues, store.Rooms.Select(r => r != null ? r.Name : null), "salon");

            foreach (var day in store.Days.Where(d => d != null))
            {
                var duplicateSlotIndexes = day.Slots
                    .GroupBy(s => s.Index)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .OrderBy(x => x)
                    .ToList();

                foreach (var index in duplicateSlotIndexes)
                    issues.Add(string.Format("'{0}' günü için {1}. ders saati birden fazla kez tanımlı.", day.Name, index));
            }

            return issues;
        }

        private static void AddDuplicateNameIssues(List<string> issues, IEnumerable<string> names, string label)
        {
            var duplicates = names
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(x => x.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var duplicate in duplicates)
                issues.Add(string.Format("Aynı {0} adı birden fazla kez kullanılmış: {1}", label, duplicate));
        }
    }
}
