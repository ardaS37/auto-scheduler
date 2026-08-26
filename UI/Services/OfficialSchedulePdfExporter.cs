using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoScheduler.UI.Services
{
    public static class OfficialSchedulePdfExporter
    {
        public static void WriteClassSchedulePdf(
            string path,
            ProjectStore store,
            ClassGroup group,
            IEnumerable<ScheduleEntry> schedule,
            IEnumerable<TimeSlot> slotHeaders,
            Func<ClassGroup, Day, int, string> getCellText)
        {
            File.WriteAllBytes(path, BuildClassSchedulePdfBytes(store, group, schedule, slotHeaders, getCellText));
        }

        private static byte[] BuildClassSchedulePdfBytes(
            ProjectStore store,
            ClassGroup group,
            IEnumerable<ScheduleEntry> schedule,
            IEnumerable<TimeSlot> slotHeaders,
            Func<ClassGroup, Day, int, string> getCellText)
        {
            var content = BuildPageContent(store, group, schedule != null ? schedule.ToList() : new List<ScheduleEntry>(), slotHeaders, getCellText);
            var contentBytes = Encoding.ASCII.GetBytes(content);

            var objects = new List<string>
            {
                "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
                "2 0 obj\n<< /Type /Pages /Kids [4 0 R] /Count 1 >>\nendobj\n",
                "3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n",
                "4 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents 5 0 R >>\nendobj\n",
                "5 0 obj\n<< /Length " + contentBytes.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n"
            };

            var output = new StringBuilder();
            var offsets = new List<int> { 0 };
            output.Append("%PDF-1.4\n");
            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
                output.Append(obj);
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
            output.Append("xref\n0 ").Append(objects.Count + 1).Append("\n");
            output.Append("0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++)
                output.Append(offsets[i].ToString("0000000000")).Append(" 00000 n \n");
            output.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
            output.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF");

            return Encoding.ASCII.GetBytes(output.ToString());
        }

        private static string BuildPageContent(
            ProjectStore store,
            ClassGroup group,
            List<ScheduleEntry> schedule,
            IEnumerable<TimeSlot> slotHeaders,
            Func<ClassGroup, Day, int, string> getCellText)
        {
            var sb = new StringBuilder();
            const double pageWidth = 595;
            const double margin = 36;
            const double top = 760;
            const double dayWidth = 48;
            const double headerHeight = 38;
            const double rowHeight = 38;
            const int officialSlotCount = 12;
            var tableWidth = pageWidth - (margin * 2);
            var slotWidth = (tableWidth - dayWidth) / officialSlotCount;

            var slots = BuildOfficialSlots(slotHeaders);
            var days = BuildOfficialDays(store);

            AddCenteredText(sb, "T.C.", pageWidth / 2, 806, 7, true);
            AddCenteredText(sb, "MILLI EGITIM BAKANLIGI", pageWidth / 2, 796, 7, true);
            AddCenteredText(sb, Safe(store != null ? store.ProjectName : null), pageWidth / 2, 786, 7, true);
            AddText(sb, DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), pageWidth - margin - 48, 806, 7, true);

            var totalHours = store != null && group != null
                ? store.Assignments.Where(a => a.Group == group).Sum(a => a.WeeklyHours)
                : 0;

            AddText(sb, "Sinif : " + Safe(group != null ? group.Name : ""), margin + 36, 770, 7, true);
            AddText(sb, "Toplam Ders Saati : " + totalHours.ToString(CultureInfo.InvariantCulture), margin + 190, 770, 7, true);
            AddText(sb, "Sinif Ogretmeni :", margin + 340, 770, 7, true);

            var x0 = margin;
            var y0 = top - headerHeight;
            AddRect(sb, x0, y0, dayWidth, headerHeight, "1 1 1", "0 0 0", 0.8);
            AddText(sb, "Ders", x0 + 24, y0 + 24, 6, true);
            AddText(sb, "Gun", x0 + 5, y0 + 8, 6, true);
            AddLine(sb, x0, y0 + headerHeight, x0 + dayWidth, y0, "0 0 0", 0.8);

            for (int i = 0; i < officialSlotCount; i++)
            {
                var slot = slots[i];
                var x = x0 + dayWidth + (i * slotWidth);
                AddRect(sb, x, y0, slotWidth, headerHeight, "1 1 1", "0 0 0", 0.8);
                AddCenteredText(sb, "(" + slot.Index.ToString(CultureInfo.InvariantCulture) + ")", x + (slotWidth / 2), y0 + 26, 5.8, true);
                AddSlotTimeText(sb, slot.StartEnd, x + (slotWidth / 2), y0 + 17, 5.4);
            }

            for (int r = 0; r < days.Count; r++)
            {
                var day = days[r];
                var y = y0 - ((r + 1) * rowHeight);
                AddRect(sb, x0, y, dayWidth, rowHeight, "1 1 1", "0 0 0", 0.8);
                AddText(sb, Safe(day.Name), x0 + 5, y + 16, 6, true);

                for (int c = 0; c < officialSlotCount; c++)
                {
                    var x = x0 + dayWidth + (c * slotWidth);
                    AddRect(sb, x, y, slotWidth, rowHeight, "1 1 1", "0 0 0", 0.8);

                    var text = string.Empty;
                    if (day.Day != null && getCellText != null && group != null)
                        text = getCellText(group, day.Day, slots[c].Index);

                    AddOfficialCellText(sb, text, x + 2, y + rowHeight - 12, slotWidth - 4);
                }
            }

            var signatureY = y0 - (days.Count * rowHeight) - 28;
            AddCenteredText(sb, DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), pageWidth - 130, signatureY, 6, true);
            AddCenteredText(sb, "Okul Muduru", pageWidth - 130, signatureY - 20, 6, true);

            DrawCourseTable(sb, store, group, schedule, margin + 12, signatureY - 275, tableWidth - 24);

            return sb.ToString();
        }

        private static void DrawCourseTable(StringBuilder sb, ProjectStore store, ClassGroup group, List<ScheduleEntry> schedule, double x, double y, double width)
        {
            var rows = BuildCourseRows(store, group, schedule);
            var headers = new[] { "S.No", "Ders", "Dersin Adi", "Hs", "Yer", "Dersin Ogretmeni", "", "" };
            var weights = new[] { 0.55, 0.75, 2.35, 0.55, 0.7, 2.0, 1.3, 1.3 };
            var totalWeight = weights.Sum();
            var colWidths = weights.Select(w => width * w / totalWeight).ToList();
            const double rowHeight = 17;
            var headerY = y + (rows.Count + 1) * rowHeight;

            var currentX = x;
            for (int c = 0; c < headers.Length; c++)
            {
                AddRect(sb, currentX, headerY, colWidths[c], rowHeight, "1 1 1", "0 0 0", 0.7);
                AddCenteredText(sb, headers[c], currentX + (colWidths[c] / 2), headerY + 6, 5.7, true);
                currentX += colWidths[c];
            }

            for (int r = 0; r < rows.Count; r++)
            {
                currentX = x;
                var rowY = headerY - ((r + 1) * rowHeight);
                var values = rows[r];
                for (int c = 0; c < headers.Length; c++)
                {
                    AddRect(sb, currentX, rowY, colWidths[c], rowHeight, "1 1 1", "0 0 0", 0.7);
                    var value = c < values.Count ? values[c] : string.Empty;
                    AddText(sb, Truncate(value, colWidths[c] - 4, 5.3), currentX + 2, rowY + 6, 5.3, c == 0);
                    currentX += colWidths[c];
                }
            }
        }

        private static List<List<string>> BuildCourseRows(ProjectStore store, ClassGroup group, List<ScheduleEntry> schedule)
        {
            var assignments = store != null && group != null
                ? store.Assignments.Where(a => a.Group == group).ToList()
                : new List<Assignment>();

            var rows = assignments
                .GroupBy(a => new
                {
                    Course = a.Course,
                    Teacher = a.Teacher,
                    Room = a.Room
                })
                .Select((g, index) => new List<string>
                {
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    ShortCourseCode(g.Key.Course),
                    g.Key.Course != null ? g.Key.Course.Name : "",
                    g.Sum(a => a.WeeklyHours).ToString(CultureInfo.InvariantCulture),
                    g.Key.Room != null ? g.Key.Room.Name : (group != null ? group.RoomName : ""),
                    g.Key.Teacher != null ? g.Key.Teacher.Name : ""
                })
                .ToList();

            if (rows.Count == 0 && schedule != null)
            {
                rows = schedule
                    .Where(e => e.Group == group && e.Course != null)
                    .GroupBy(e => new { e.Course, e.Teacher, e.Room })
                    .Select((g, index) => new List<string>
                    {
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                        ShortCourseCode(g.Key.Course),
                        g.Key.Course != null ? g.Key.Course.Name : "",
                        g.Count().ToString(CultureInfo.InvariantCulture),
                        g.Key.Room != null ? g.Key.Room.Name : (group != null ? group.RoomName : ""),
                        g.Key.Teacher != null ? g.Key.Teacher.Name : ""
                    })
                    .ToList();
            }

            return rows.Take(14).ToList();
        }

        private static List<OfficialDay> BuildOfficialDays(ProjectStore store)
        {
            var standard = new[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" };
            var result = new List<OfficialDay>();
            var days = store != null ? store.Days.ToList() : new List<Day>();

            foreach (var day in days)
                result.Add(new OfficialDay { Name = day.Name, Day = day });

            foreach (var name in standard)
            {
                if (!result.Any(d => Normalize(d.Name) == Normalize(name)))
                    result.Add(new OfficialDay { Name = name, Day = null });
            }

            return result.Take(7).ToList();
        }

        private static List<OfficialSlot> BuildOfficialSlots(IEnumerable<TimeSlot> slotHeaders)
        {
            var slots = slotHeaders != null ? slotHeaders.OrderBy(s => s.Index).ToList() : new List<TimeSlot>();
            var result = new List<OfficialSlot>();
            for (int i = 1; i <= 12; i++)
            {
                var slot = slots.FirstOrDefault(s => s.Index == i);
                result.Add(new OfficialSlot
                {
                    Index = i,
                    StartEnd = slot == null ? "" : slot.Start.ToString(@"hh\:mm") + "\n" + slot.End.ToString(@"hh\:mm")
                });
            }

            return result;
        }

        private static void AddOfficialCellText(StringBuilder sb, string text, double x, double y, double maxWidth)
        {
            var lines = (text ?? string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Safe)
                .Take(2)
                .ToList();

            if (lines.Count == 0) return;
            AddCenteredText(sb, Truncate(lines[0], maxWidth, 5.8), x + (maxWidth / 2), y, 5.8, true);
            if (lines.Count > 1)
                AddCenteredText(sb, Truncate(ShortTeacher(lines[1]), maxWidth, 5.2), x + (maxWidth / 2), y - 9, 5.2, true);
        }

        private static void AddSlotTimeText(StringBuilder sb, string text, double centerX, double y, double fontSize)
        {
            var lines = (text ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length && i < 2; i++)
                AddCenteredText(sb, lines[i], centerX, y - (i * 8), fontSize, false);
        }

        private static string ShortCourseCode(Course course)
        {
            if (course == null) return string.Empty;
            return string.IsNullOrWhiteSpace(course.Code) ? ShortText(course.Name, 8) : ShortText(course.Code, 8);
        }

        private static string ShortTeacher(string teacher)
        {
            if (string.IsNullOrWhiteSpace(teacher)) return string.Empty;
            var parts = teacher.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return ShortText(teacher, 12);
            return ShortText(parts[0].Substring(0, 1) + "." + parts[parts.Length - 1], 12);
        }

        private static string ShortText(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var safe = Safe(value);
            return safe.Length <= max ? safe : safe.Substring(0, max);
        }

        private static string Truncate(string value, double maxWidth, double fontSize)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var safe = Safe(value);
            var maxChars = Math.Max(2, (int)(maxWidth / (fontSize * 0.52)));
            return safe.Length <= maxChars ? safe : safe.Substring(0, maxChars - 1) + ".";
        }

        private static string Normalize(string value)
        {
            return Safe(value).Replace(" ", "").ToUpperInvariant();
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value
                .Replace("Ç", "C").Replace("ç", "c")
                .Replace("Ğ", "G").Replace("ğ", "g")
                .Replace("İ", "I").Replace("ı", "i")
                .Replace("Ö", "O").Replace("ö", "o")
                .Replace("Ş", "S").Replace("ş", "s")
                .Replace("Ü", "U").Replace("ü", "u");
        }

        private static void AddRect(StringBuilder sb, double x, double y, double width, double height, string fillRgb, string strokeRgb, double lineWidth)
        {
            sb.Append(Num(lineWidth)).Append(" w ");
            sb.Append(fillRgb).Append(" rg ").Append(strokeRgb).Append(" RG ");
            sb.Append(Num(x)).Append(" ").Append(Num(y)).Append(" ").Append(Num(width)).Append(" ").Append(Num(height)).Append(" re B\n");
        }

        private static void AddLine(StringBuilder sb, double x1, double y1, double x2, double y2, string strokeRgb, double lineWidth)
        {
            sb.Append(Num(lineWidth)).Append(" w ").Append(strokeRgb).Append(" RG ");
            sb.Append(Num(x1)).Append(" ").Append(Num(y1)).Append(" m ");
            sb.Append(Num(x2)).Append(" ").Append(Num(y2)).Append(" l S\n");
        }

        private static void AddText(StringBuilder sb, string text, double x, double y, double fontSize, bool bold)
        {
            sb.Append("0 0 0 rg BT /F1 ").Append(Num(fontSize)).Append(" Tf ");
            sb.Append(Num(x)).Append(" ").Append(Num(y)).Append(" Td (").Append(EscapePdfText(text)).Append(") Tj ET\n");
        }

        private static void AddCenteredText(StringBuilder sb, string text, double centerX, double y, double fontSize, bool bold)
        {
            var safe = Safe(text);
            var width = safe.Length * fontSize * 0.52;
            AddText(sb, safe, centerX - (width / 2), y, fontSize, bold);
        }

        private static string EscapePdfText(string value)
        {
            return Safe(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static string Num(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private sealed class OfficialDay
        {
            public string Name { get; set; }
            public Day Day { get; set; }
        }

        private sealed class OfficialSlot
        {
            public int Index { get; set; }
            public string StartEnd { get; set; }
        }
    }
}
