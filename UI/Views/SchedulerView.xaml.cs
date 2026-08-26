using AutoScheduler.UI.Services;
using AutoScheduler.Core.Models;
using AutoScheduler.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AutoScheduler.UI.ViewModels;

namespace AutoScheduler.UI.Views
{
    public partial class SchedulerView : UserControl
    {
        private bool _syncingScroll;

        public SchedulerView()
        {
            InitializeComponent();
        }

        private void BodyScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll) return;
            if (HeaderScroll == null) return;

            try
            {
                _syncingScroll = true;
                if (e.HorizontalChange != 0)
                    HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
            finally
            {
                _syncingScroll = false;
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel;
            if (vm == null) return;
            if (vm.Groups == null || vm.Groups.Count == 0) return;

            var dlg = new PrintDialog();
            bool? ok = dlg.ShowDialog();
            if (ok != true) return;

            var doc = ScheduleExportService.BuildPrintableDocument(
                vm.Groups,
                vm.Store.Days,
                vm.SlotHeaders,
                vm.GetCellText);

            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            dlg.PrintDocument(paginator, "AutoScheduler - Tüm Sınıflar Haftalık Program");
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel;
            if (vm == null) return;
            if (vm.Groups == null || vm.Groups.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = "schedule-all-classes.csv"
            };

            bool? ok = dlg.ShowDialog();
            if (ok != true) return;

            string path = dlg.FileName;
            var csv = ScheduleExportService.BuildCsv(
                vm.Groups,
                vm.Store.Days,
                vm.SlotHeaders,
                vm.GetCellText);

            System.IO.File.WriteAllText(path, csv, new UTF8Encoding(true));
        }

        private void ExportOfficialPdf_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel;
            if (vm == null) return;
            if (vm.SelectedGroup == null) return;

            var safeGroupName = SafeFileName(vm.SelectedGroup.Name);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF dosyası (*.pdf)|*.pdf",
                FileName = safeGroupName + "-resmi-ders-programi.pdf"
            };

            if (dlg.ShowDialog() != true) return;

            OfficialSchedulePdfExporter.WriteClassSchedulePdf(
                dlg.FileName,
                vm.Store,
                vm.SelectedGroup,
                vm.Schedule,
                vm.SlotHeaders,
                vm.GetCellText);

            MessageBox.Show(
                Window.GetWindow(this),
                "Resmi ders programı PDF çıktısı oluşturuldu.",
                "Resmi PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OpenBlanket_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel;
            if (vm == null) return;

            var window = new BlanketScheduleWindow(vm)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }

        private async void MoveLesson_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var cell = button != null ? button.DataContext as SchedulerViewModel.WeeklyCell : null;
            var vm = DataContext as SchedulerViewModel;
            if (vm == null || cell == null || cell.Entry == null) return;

            var moveVm = new MoveLessonViewModel(vm.Store, cell.Entry);
            var window = new MoveLessonWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = moveVm
            };

            if (window.ShowDialog() != true) return;
            if (moveVm.SelectedDay == null || moveVm.SelectedSlot == null) return;

            var moved = await vm.MoveLessonAndRegenerateAsync(cell.Entry, moveVm.SelectedDay, moveVm.SelectedSlot);
            if (!moved)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    "Ders taşınamadı: hedef saatte geçerli bir yerleşim bulunamadı. Program değiştirilmedi, başka bir saat deneyin.",
                    "Taşıma Başarısız",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void SaveAlternatives_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel;
            if (vm == null) return;
            if (vm.Groups == null || vm.Groups.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = "program-alternatif.csv"
            };

            if (dlg.ShowDialog() != true) return;

            var directory = Path.GetDirectoryName(dlg.FileName);
            var fileName = Path.GetFileNameWithoutExtension(dlg.FileName);
            var extension = Path.GetExtension(dlg.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".csv";

            for (int i = 1; i <= 5; i++)
            {
                var options = new ScheduleGenerationOptions
                {
                    AvoidConsecutiveTeacherLessons = vm.AvoidConsecutiveTeacherLessons,
                    BalanceTeacherAcrossDays = vm.BalanceTeacherAcrossDays,
                    RandomizePlacement = true,
                    RandomSeedOffset = Environment.TickCount + (i * 10000),
                    RespectTeacherUnavailableDays = vm.Store.RespectTeacherUnavailableDays,
                    RespectGroupSlotRules = vm.Store.RespectGroupSlotRules,
                    RespectLunchBreak = vm.Store.RespectLunchBreak,
                    RespectTeacherHalfDay = vm.Store.RespectTeacherHalfDay,
                    UseDutyDayPriority = vm.Store.UseDutyDayPriority,
                    UseCoursePriorityLevel = vm.Store.UseCoursePriorityLevel,
                    UseTeacherCoursePreferences = vm.Store.UseTeacherCoursePreferences,
                    UseSpreadAcrossDays = vm.Store.UseSpreadAcrossDays,
                    UseMaxPerDay = vm.Store.UseMaxPerDay,
                    UseDetailedTeacherAvailability = vm.Store.UseDetailedTeacherAvailability,
                    UseIntensiveRepairSearch = vm.Store.UseIntensiveRepairSearch,
                    UseClassByClassPlacement = vm.Store.UseClassByClassPlacement,
                    UseProgressiveImprovement = vm.Store.UseProgressiveImprovement,
                    UseParallelSearch = vm.Store.UseParallelSearch,
                    KeepBlocksStrict = vm.Store.KeepBlocksStrict,
                    DeepSearchEnabled = vm.Store.DeepSearchEnabled,
                    MaxGenerationAttempts = vm.Store.MaxGenerationAttempts,
                    RelaxationOrder = vm.RelaxationRules.OrderBy(x => x.Order).Select(x => x.Key).ToList()
                };

                var result = ScheduleGenerationService.Generate(vm.Store, options);
                var schedule = result.Schedule.ToList();
                string GetText(ClassGroup group, Day day, int slotIndex)
                {
                    var entry = schedule.FirstOrDefault(x => x.Group == group && x.Day == day && x.SlotIndex == slotIndex);
                    return FormatEntryText(entry);
                }

                var path = Path.Combine(directory ?? string.Empty, fileName + "-" + i.ToString("00") + extension);
                var csv = ScheduleExportService.BuildCsv(vm.Groups, vm.Store.Days, vm.SlotHeaders, GetText);
                File.WriteAllText(path, csv, new UTF8Encoding(true));
            }

            MessageBox.Show("5 alternatif program sırasıyla kaydedildi.", "Alternatifler", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void RelaxationRulesGrid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            var grid = sender as DataGrid;
            if (grid == null || grid.SelectedItem == null) return;

            DragDrop.DoDragDrop(grid, grid.SelectedItem, DragDropEffects.Move);
        }

        public static void RelaxationRulesGrid_Drop(object sender, DragEventArgs e)
        {
            var source = e.Data.GetData(typeof(AutoScheduler.UI.ViewModels.SchedulerViewModel.RelaxationRuleItem)) as AutoScheduler.UI.ViewModels.SchedulerViewModel.RelaxationRuleItem;
            if (source == null) return;

            var targetElement = e.OriginalSource as DependencyObject;
            while (targetElement != null && !(targetElement is DataGridRow))
                targetElement = System.Windows.Media.VisualTreeHelper.GetParent(targetElement);

            var row = targetElement as DataGridRow;
            var target = row != null ? row.Item as AutoScheduler.UI.ViewModels.SchedulerViewModel.RelaxationRuleItem : null;
            if (target == null) return;

            var grid = sender as DataGrid;
            var schedulerVm = grid != null ? grid.DataContext as AutoScheduler.UI.ViewModels.SchedulerViewModel : null;
            if (schedulerVm == null) return;

            schedulerVm.MoveRelaxationRule(source, target);
        }

        private static string FormatEntryText(ScheduleEntry entry)
        {
            if (entry == null) return string.Empty;

            var course = entry.Course != null
                ? (string.IsNullOrWhiteSpace(entry.Course.Code) ? entry.Course.Name : entry.Course.Code)
                : string.Empty;
            var teacher = entry.Teacher != null ? entry.Teacher.Name : string.Empty;
            var room = entry.Room != null ? entry.Room.Name : string.Empty;
            var block = entry.BlockSize > 1 ? " (" + entry.BlockPos + "/" + entry.BlockSize + ")" : string.Empty;
            var line1 = course + block;

            if (!string.IsNullOrWhiteSpace(teacher) && !string.IsNullOrWhiteSpace(room))
                return line1 + "\n" + teacher + " / " + room;
            if (!string.IsNullOrWhiteSpace(teacher))
                return line1 + "\n" + teacher;
            if (!string.IsNullOrWhiteSpace(room))
                return line1 + "\n" + room;

            return line1;
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "sinif";
            foreach (var ch in Path.GetInvalidFileNameChars())
                value = value.Replace(ch, '-');
            return value;
        }
    }
}
