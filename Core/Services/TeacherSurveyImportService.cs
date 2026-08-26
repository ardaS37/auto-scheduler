using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoScheduler.Core.Services
{
    public sealed class TeacherSurveyImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    // Google Forms (Google E-Tablolar üzerinden "Yanıtlar" CSV olarak dışa aktarılır) veya
    // Excel/Sheets'ten kopyalanan bir öğretmen anketini CSV olarak içeri aktarır. Form
    // sorularının başlık metni serbest bırakılır; sütunlar anahtar kelimelerle (ör. "ad soyad",
    // "telefon", "verebileceği dersler") eşleştirilir, böylece kullanıcı Google Forms'ta
    // BuildTemplateQuestions() önerisine yakın herhangi bir soru metni kullanabilir.
    public static class TeacherSurveyImportService
    {
        public static List<string> BuildTemplateQuestions()
        {
            return new List<string>
            {
                "Ad Soyad",
                "Telefon",
                "Unvan (Öğretim Görevlisi / Doktor Öğretim Üyesi / Doçent / Profesör / Araştırma Görevlisi)",
                "Yarım Gün Uygunluğu (Farketmez / Sabah / Öğleden Sonra)",
                "Verebileceği Dersler (virgülle ayırın)",
                "Tercih Ettiği Dersler (virgülle ayırın)",
                "Vermek İstemediği Dersler (virgülle ayırın)",
                "Uygun Olmadığı Günler (virgülle ayırın)",
                "Nöbetçi Olduğu Günler (varsa, virgülle ayırın)",
                "Uygun Olmadığı Ders Saatleri (Gün:Saat şeklinde, virgülle ayırın - örn: Pazartesi:1, Salı:3)"
            };
        }

        public static string BuildTemplateCsv()
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(";", BuildTemplateQuestions().Select(EscapeCsv)));
            sb.Append("\r\n");
            sb.Append(string.Join(";", new[]
            {
                "Örn: Ayşe Yılmaz", "05xx xxx xx xx", "Öğretim Görevlisi", "Farketmez",
                "Matematik, Fizik", "Matematik", "Beden Eğitimi",
                "Cuma", "", "Pazartesi:1, Salı:3"
            }.Select(EscapeCsv)));
            sb.Append("\r\n");
            return sb.ToString();
        }

        public static TeacherSurveyImportResult Import(ProjectStore store, string csvText, bool autoCreateCourses, bool updateExisting)
        {
            var result = new TeacherSurveyImportResult();
            if (store == null)
            {
                result.Errors.Add("Proje verisi bulunamadı.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(csvText))
            {
                result.Errors.Add("İçe aktarılacak veri bulunamadı. CSV içeriğini yapıştırın veya bir dosya seçin.");
                return result;
            }

            var delimiter = DetectDelimiter(csvText);
            var rows = ParseCsv(csvText, delimiter);
            rows.RemoveAll(r => r.Count == 0 || r.All(string.IsNullOrWhiteSpace));

            if (rows.Count < 2)
            {
                result.Errors.Add("En az bir başlık satırı ve bir veri satırı gerekli.");
                return result;
            }

            var headers = rows[0];

            int nameCol = FindColumn(headers, "ad soyad", "isim", "ogretmen adi", "adi soyadi", "name");
            int phoneCol = FindColumn(headers, "telefon", "phone", "gsm");
            int titleCol = FindColumn(headers, "unvan", "title");
            int halfDayCol = FindColumn(headers, "yarim gun", "yarim gunluk", "half day");
            int canTeachCol = FindColumn(headers, "verebilecegi", "verebilecek", "can teach");
            int preferredCol = FindColumn(headers, "tercih ettigi", "tercih edilen", "preferred");
            int unwantedCol = FindColumn(headers, "istemedigi", "vermek istemedigi", "unwanted");
            int unavailableDaysCol = FindColumn(headers, "uygun olmadigi gun", "musait olmadigi gun", "unavailable day");
            int dutyDaysCol = FindColumn(headers, "nobetci", "duty");
            int unavailableSlotsCol = FindColumn(headers, "uygun olmadigi ders saat", "uygun olmadigi saat", "musait olmadigi saat", "unavailable slot");

            if (nameCol < 0)
            {
                result.Errors.Add("Başlık satırında öğretmen adını içeren bir sütun bulunamadı (\"Ad Soyad\" gibi bir başlık kullanın).");
                return result;
            }

            for (int i = 1; i < rows.Count; i++)
            {
                var cells = rows[i];

                string Get(int col) => col >= 0 && col < cells.Count ? (cells[col] ?? string.Empty).Trim() : string.Empty;

                var name = Get(nameCol);
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Errors.Add((i + 1) + ". satır: öğretmen adı boş, atlandı.");
                    continue;
                }

                var teacher = store.Teachers.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.CurrentCultureIgnoreCase));
                var isNew = teacher == null;

                if (isNew)
                {
                    teacher = new Teacher { Name = name };
                }
                else if (!updateExisting)
                {
                    result.Warnings.Add(name + " zaten kayıtlı, atlandı (mevcut öğretmenleri güncelle seçeneği kapalı).");
                    continue;
                }

                var phone = Get(phoneCol);
                if (!string.IsNullOrWhiteSpace(phone)) teacher.Phone = phone;

                var titleText = Get(titleCol);
                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    var matchedTitle = MatchEnum<AcademicTitle>(titleText);
                    if (matchedTitle.HasValue) teacher.Title = matchedTitle.Value;
                }

                var halfDayText = Get(halfDayCol);
                if (!string.IsNullOrWhiteSpace(halfDayText))
                    teacher.HalfDayAvailability = ParseHalfDay(halfDayText);

                if (isNew)
                    store.Teachers.Add(teacher);

                ApplyCourseList(teacher.CanTeachCourses, Get(canTeachCol), store, autoCreateCourses);
                ApplyNameList(teacher.PreferredCourseNames, Get(preferredCol));
                ApplyNameList(teacher.UnwantedCourseNames, Get(unwantedCol));
                ApplyNameList(teacher.UnavailableDayNames, Get(unavailableDaysCol));
                ApplyNameList(teacher.DutyDayNames, Get(dutyDaysCol));
                ApplySlotList(teacher.UnavailableSlotKeys, Get(unavailableSlotsCol));

                if (isNew) result.AddedCount++;
                else result.UpdatedCount++;
            }

            return result;
        }

        private static void ApplyCourseList(ObservableCollection<Course> target, string raw, ProjectStore store, bool autoCreate)
        {
            foreach (var name in SplitListValue(raw))
            {
                var course = store.Courses.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));
                if (course == null)
                {
                    if (!autoCreate) continue;
                    course = new Course { Name = name };
                    store.Courses.Add(course);
                }

                if (!target.Contains(course))
                    target.Add(course);
            }
        }

        private static void ApplyNameList(ObservableCollection<string> target, string raw)
        {
            foreach (var name in SplitListValue(raw))
            {
                if (!target.Contains(name))
                    target.Add(name);
            }
        }

        private static void ApplySlotList(ObservableCollection<string> target, string raw)
        {
            foreach (var pair in SplitListValue(raw))
            {
                var parts = pair.Split(':');
                if (parts.Length != 2) continue;

                var dayName = parts[0].Trim();
                if (!int.TryParse(parts[1].Trim(), out var slotIndex)) continue;

                var key = dayName + "|" + slotIndex;
                if (!target.Contains(key))
                    target.Add(key);
            }
        }

        private static IEnumerable<string> SplitListValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            var separators = raw.Contains(';') ? new[] { ';' } : new[] { ',' };
            foreach (var part in raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
            }
        }

        private static HalfDayAvailability ParseHalfDay(string text)
        {
            var normalized = Normalize(text);
            if (normalized.Contains("sabah") || normalized.Contains("morning")) return HalfDayAvailability.Morning;
            if (normalized.Contains("ogle") || normalized.Contains("afternoon")) return HalfDayAvailability.Afternoon;
            return HalfDayAvailability.Any;
        }

        private static TEnum? MatchEnum<TEnum>(string text) where TEnum : struct, Enum
        {
            var normalized = Normalize(text);
            foreach (var value in Enum.GetValues(typeof(TEnum)).Cast<TEnum>())
            {
                if (Normalize(value.ToString()) == normalized)
                    return value;
            }
            return null;
        }

        private static int FindColumn(List<string> headers, params string[] keywords)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var normalized = Normalize(headers[i]);
                if (keywords.Any(k => normalized.Contains(Normalize(k))))
                    return i;
            }
            return -1;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var lowered = value.ToLower(CultureInfo.InvariantCulture)
                .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i")
                .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");

            var sb = new StringBuilder();
            foreach (var ch in lowered)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static char DetectDelimiter(string csvText)
        {
            var firstLineEnd = csvText.IndexOfAny(new[] { '\r', '\n' });
            var firstLine = firstLineEnd >= 0 ? csvText.Substring(0, firstLineEnd) : csvText;

            if (firstLine.Contains(';')) return ';';
            if (firstLine.Contains('\t')) return '\t';
            return ',';
        }

        private static List<List<string>> ParseCsv(string text, char delimiter)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == delimiter)
                {
                    row.Add(sb.ToString());
                    sb.Clear();
                }
                else if (ch == '\r')
                {
                    // ignore, satır sonu \n ile ele alınır
                }
                else if (ch == '\n')
                {
                    row.Add(sb.ToString());
                    sb.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOfAny(new[] { ';', ',', '"', '\n', '\r' }) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
