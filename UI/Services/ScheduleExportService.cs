using AutoScheduler.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AutoScheduler.UI.Services
{
    public static class ScheduleExportService
    {
        public static FlowDocument BuildPrintableDocument(
            IEnumerable<ClassGroup> groups,
            IEnumerable<Day> days,
            IEnumerable<TimeSlot> slotHeaders,
            System.Func<ClassGroup, Day, int, string> getCellText)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(30),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            var groupList = groups != null ? groups.ToList() : new List<ClassGroup>();
            var dayList = days != null ? days.ToList() : new List<Day>();
            var slotList = slotHeaders != null ? slotHeaders.ToList() : new List<TimeSlot>();

            for (int gi = 0; gi < groupList.Count; gi++)
            {
                var group = groupList[gi];
                var title = new Paragraph(new Run("Sınıf: " + (group != null ? group.Name : "")))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                doc.Blocks.Add(title);

                var table = new Table { CellSpacing = 0 };
                for (int i = 0; i < 1 + slotList.Count; i++)
                    table.Columns.Add(new TableColumn());

                var rowGroup = new TableRowGroup();
                table.RowGroups.Add(rowGroup);

                var headerRow = new TableRow { Background = Brushes.Gainsboro };
                headerRow.Cells.Add(MakeCell("Gün", true));
                foreach (var slot in slotList)
                {
                    var header = slot != null ? ("Ders Saati " + slot.Index + ": " + (slot.Label ?? "")) : "";
                    headerRow.Cells.Add(MakeCell(header, true));
                }
                rowGroup.Rows.Add(headerRow);

                foreach (var day in dayList)
                {
                    var row = new TableRow();
                    row.Cells.Add(MakeCell(day != null ? day.Name : "", false));

                    foreach (var slot in slotList)
                    {
                        var text = getCellText != null && day != null && slot != null
                            ? getCellText(group, day, slot.Index)
                            : string.Empty;
                        row.Cells.Add(MakeCell(text, false));
                    }

                    rowGroup.Rows.Add(row);
                }

                doc.Blocks.Add(table);
                if (gi < groupList.Count - 1)
                    doc.Blocks.Add(new Paragraph { BreakPageBefore = true });
            }

            return doc;
        }

        public static string BuildCsv(
            IEnumerable<ClassGroup> groups,
            IEnumerable<Day> days,
            IEnumerable<TimeSlot> slotHeaders,
            System.Func<ClassGroup, Day, int, string> getCellText)
        {
            const string sep = ";";
            var sb = new StringBuilder();
            var groupList = groups != null ? groups.ToList() : new List<ClassGroup>();
            var dayList = days != null ? days.ToList() : new List<Day>();
            var slotList = slotHeaders != null ? slotHeaders.ToList() : new List<TimeSlot>();

            for (int gi = 0; gi < groupList.Count; gi++)
            {
                var group = groupList[gi];
                sb.AppendLine(EscapeCsv("Sınıf") + sep + EscapeCsv(group != null ? group.Name : ""));
                sb.Append(EscapeCsv("Gün"));

                foreach (var slot in slotList)
                {
                    var header = slot != null ? ("Ders Saati " + slot.Index + ": " + (slot.Label ?? "")) : "";
                    sb.Append(sep);
                    sb.Append(EscapeCsv(header));
                }

                sb.AppendLine();

                foreach (var day in dayList)
                {
                    sb.Append(EscapeCsv(day != null ? day.Name : ""));
                    foreach (var slot in slotList)
                    {
                        sb.Append(sep);
                        var text = getCellText != null && day != null && slot != null
                            ? getCellText(group, day, slot.Index)
                            : string.Empty;
                        sb.Append(EscapeCsv(text));
                    }
                    sb.AppendLine();
                }

                if (gi < groupList.Count - 1)
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        private static TableCell MakeCell(string text, bool isHeader)
        {
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(4);
            paragraph.Inlines.Add(new Run(text ?? string.Empty));

            var cell = new TableCell(paragraph)
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5)
            };

            if (isHeader)
                cell.FontWeight = FontWeights.SemiBold;

            return cell;
        }

        private static string EscapeCsv(string value)
        {
            if (value == null) value = string.Empty;
            value = value.Replace("\"", "\"\"");
            return "\"" + value + "\"";
        }
    }
}
