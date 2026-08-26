using System;
using System.Globalization;
using System.Windows.Data;
using AutoScheduler.Core.Models;

namespace AutoScheduler.UI.Converters
{
    public sealed class EnumDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HalfDayAvailability h)
            {
                switch (h)
                {
                    case HalfDayAvailability.Any:
                        return "Farketmez";
                    case HalfDayAvailability.Morning:
                        return "Öğleden önce";
                    case HalfDayAvailability.Afternoon:
                        return "Öğleden sonra";
                }
            }

            if (value is ClassTrack track)
            {
                switch (track)
                {
                    case ClassTrack.Yok:
                        return "Yok";
                    case ClassTrack.EsitAgirlik:
                        return "EA";
                    case ClassTrack.Sayisal:
                        return "Sayısal";
                    case ClassTrack.Sozel:
                        return "Sözel";
                    case ClassTrack.Dil:
                        return "Dil";
                }
            }

            if (value is CourseKind kind)
            {
                switch (kind)
                {
                    case CourseKind.Genel:
                        return "Genel";
                    case CourseKind.Sayisal:
                        return "Sayısal";
                    case CourseKind.Sozel:
                        return "Sözel";
                }
            }

            if (value is GenerationSearchStrategy strategy)
            {
                switch (strategy)
                {
                    case GenerationSearchStrategy.Standart:
                        return "Standart";
                    case GenerationSearchStrategy.Yogun:
                        return "Yoğun Arama (daha uzun sürebilir)";
                    case GenerationSearchStrategy.Maksimum:
                        return "Derin Arama (daha da uzun sürebilir, zor durumlar için)";
                    case GenerationSearchStrategy.SonCare:
                        return "Ayrıntılı Arama (en uzun sürebilir, dakikalarca sürebilir; çok detaylı arar)";
                    case GenerationSearchStrategy.Hizli:
                        return "Hızlı Arama (kalite tercihlerini önemsemez, en hızlı üretir)";
                }
            }

            if (value is ExamNeighborRuleMode neighborMode)
            {
                switch (neighborMode)
                {
                    case ExamNeighborRuleMode.SadeceYan:
                        return "Sadece sağ-sol yan sıra";
                    case ExamNeighborRuleMode.YanOnArka:
                        return "Sağ-sol ve ön-arka";
                    case ExamNeighborRuleMode.TumCevre:
                        return "Tüm çevre (çaprazlar dahil)";
                }
            }

            return value != null ? value.ToString() : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not used; SelectedItem binds to enum values directly.
            return Binding.DoNothing;
        }
    }
}
