using AutoScheduler.Core.Models;
using AutoScheduler.UI.Services;
using AutoScheduler.UI.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AutoScheduler.UI.Views
{
    public partial class BlanketScheduleWindow : Window
    {
        private const double HeaderWidth = 150;
        private const double CellWidth = 58;
        private const double CellHeight = 34;
        private readonly SchedulerViewModel _vm;
        private readonly List<CellVisual> _cells = new List<CellVisual>();
        private readonly List<CellVisual> _matches = new List<CellVisual>();
        private int _currentMatchIndex = -1;
        private Dictionary<string, int> _colorIndexMap = new Dictionary<string, int>();

        public BlanketScheduleWindow(SchedulerViewModel vm, bool openExportOnLoad = false)
        {
            _vm = vm;
            InitializeComponent();
            BuildAll();
            RefreshAlternativeStatus();

            if (openExportOnLoad)
                Loaded += (sender, args) => OpenExportDialog();
        }

        private void BuildAll()
        {
            _colorIndexMap = SchedulePaletteService.BuildIndexMap(
                _vm.Store.Courses.Select(c => c.Name)
                    .Concat(_vm.Store.Groups.Select(g => g.Name))
                    .Concat(_vm.Store.Teachers.Select(t => t.Name)));

            _cells.Clear();
            BuildGrid(TeacherGrid, BuildTeacherRows());
            BuildGrid(ClassGrid, BuildClassRows());
            ApplySearch();
            RefreshAlternativeStatus();
        }

        private void PreviousAlternative_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.PreviousAlternativeCommand == null || !_vm.PreviousAlternativeCommand.CanExecute(null)) return;
            _vm.PreviousAlternativeCommand.Execute(null);
            BuildAll();
        }

        private void NextAlternative_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.NextAlternativeCommand == null || !_vm.NextAlternativeCommand.CanExecute(null)) return;
            _vm.NextAlternativeCommand.Execute(null);
            BuildAll();
        }

        private void RefreshAlternativeStatus()
        {
            if (AlternativeStatusText == null) return;
            AlternativeStatusText.Text = _vm.AlternativeStatus ?? string.Empty;
        }

        private List<BlanketRow> BuildTeacherRows()
        {
            return _vm.Store.Teachers
                .OrderBy(t => t.Name)
                .Select(t => new BlanketRow
                {
                    Header = t.Name,
                    Cells = BuildCells((day, slot) =>
                    {
                        var entry = _vm.Schedule.FirstOrDefault(e => e.Teacher == t && e.Day == day && e.SlotIndex == slot.Index);
                        if (entry == null) return null;
                        return new BlanketCell
                        {
                            Text = Short(entry.Group) + "\n" + Short(entry.Course),
                            SearchText = JoinSearch(t.Name, entry.Group, entry.Course, entry.Teacher),
                            ColorKey = entry.Course != null ? entry.Course.Name : entry.Group != null ? entry.Group.Name : t.Name
                        };
                    })
                })
                .ToList();
        }

        private List<BlanketRow> BuildClassRows()
        {
            return _vm.Store.Groups
                .OrderBy(g => g.Name)
                .Select(g => new BlanketRow
                {
                    Header = g.Name,
                    Cells = BuildCells((day, slot) =>
                    {
                        var entry = _vm.Schedule.FirstOrDefault(e => e.Group == g && e.Day == day && e.SlotIndex == slot.Index);
                        if (entry == null) return null;
                        return new BlanketCell
                        {
                            Text = Short(entry.Course) + "\n" + Short(entry.Teacher),
                            SearchText = JoinSearch(g.Name, entry.Group, entry.Course, entry.Teacher),
                            ColorKey = entry.Course != null ? entry.Course.Name : g.Name
                        };
                    })
                })
                .ToList();
        }

        private List<BlanketCell> BuildCells(Func<Day, TimeSlot, BlanketCell> factory)
        {
            var cells = new List<BlanketCell>();
            foreach (var day in _vm.Store.Days)
            {
                foreach (var slot in day.Slots.OrderBy(s => s.Index))
                    cells.Add(factory(day, slot) ?? new BlanketCell());
            }
            return cells;
        }

        private void BuildGrid(Grid grid, List<BlanketRow> rows, bool trackCells = true)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            var slots = _vm.Store.Days.SelectMany(d => d.Slots.OrderBy(s => s.Index).Select(s => new SlotColumn { Day = d, Slot = s })).ToList();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            foreach (var _ in rows)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellHeight) });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderWidth) });
            foreach (var _ in slots)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });

            AddHeaderCell(grid, "Ad", 0, 0, HeaderWidth);
            for (int i = 0; i < slots.Count; i++)
            {
                var text = slots[i].Day.Name + "\n" + slots[i].Slot.Index;
                AddHeaderCell(grid, text, 0, i + 1, CellWidth);
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                AddRowHeader(grid, row.Header, rowIndex + 1);

                for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    var cell = row.Cells[columnIndex];
                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(218, 224, 232)),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = string.IsNullOrWhiteSpace(cell.Text)
                            ? Brushes.White
                            : BuildBrush(cell.ColorKey),
                        Padding = new Thickness(2)
                    };

                    var text = new TextBlock
                    {
                        Text = cell.Text,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    border.Child = text;
                    Grid.SetRow(border, rowIndex + 1);
                    Grid.SetColumn(border, columnIndex + 1);
                    grid.Children.Add(border);

                    if (trackCells)
                    {
                        _cells.Add(new CellVisual
                        {
                            Border = border,
                            Text = cell.SearchText ?? string.Empty,
                            HasValue = !string.IsNullOrWhiteSpace(cell.Text),
                            OriginalBrush = border.Background,
                            OwnerGrid = grid
                        });
                    }
                }
            }

            if (trackCells)
                StatusText.Text = rows.Count + " satır, " + slots.Count + " sütun";
        }

        private void AddHeaderCell(Grid grid, string text, int row, int column, double width)
        {
            AddHeaderCell(grid, text, row, column, width, false);
        }

        private void AddHeaderCell(Grid grid, string text, int row, int column, double width, bool grayscale)
        {
            var border = new Border
            {
                Width = width,
                BorderBrush = grayscale ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(29, 120, 112)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = grayscale ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 118, 110)),
                Padding = new Thickness(3)
            };
            border.Child = new TextBlock
            {
                Text = text,
                Foreground = grayscale ? Brushes.Black : Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            grid.Children.Add(border);
        }

        private void AddRowHeader(Grid grid, string text, int row)
        {
            AddRowHeader(grid, text, row, false);
        }

        private void AddRowHeader(Grid grid, string text, int row, bool grayscale)
        {
            var border = new Border
            {
                BorderBrush = grayscale ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(29, 120, 112)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = grayscale ? Brushes.White : new SolidColorBrush(Color.FromRgb(7, 94, 89)),
                Padding = new Thickness(6, 3, 4, 3)
            };
            border.Child = new TextBlock
            {
                Text = text,
                Foreground = grayscale ? Brushes.Black : Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, 0);
            grid.Children.Add(border);
        }

        private Brush BuildBrush(string key)
        {
            var rgb = SchedulePaletteService.GetRgb(key, _colorIndexMap);
            return new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void ModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == ModeTabs)
                ApplySearch();
        }

        private void ApplySearch()
        {
            var query = SearchBox == null ? string.Empty : SearchBox.Text;
            var hasQuery = !string.IsNullOrWhiteSpace(query);
            int matches = 0;
            _matches.Clear();
            _currentMatchIndex = -1;
            var activeGrid = GetActiveGrid();

            foreach (var cell in _cells)
            {
                cell.Border.Background = cell.OriginalBrush;
                cell.Border.BorderBrush = new SolidColorBrush(Color.FromRgb(218, 224, 232));
                cell.Border.BorderThickness = new Thickness(0, 0, 1, 1);

                if (!hasQuery || !cell.HasValue) continue;
                if (cell.OwnerGrid != activeGrid) continue;

                if (cell.Text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    matches++;
                    _matches.Add(cell);
                    cell.Border.BorderBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192));
                    cell.Border.BorderThickness = new Thickness(2);
                }
                else
                {
                    cell.Border.Background = new SolidColorBrush(Color.FromRgb(246, 248, 251));
                }
            }

            if (StatusText != null && hasQuery)
                StatusText.Text = matches + " eşleşme";

            if (_matches.Count > 0)
                SelectMatch(0);
        }

        private Grid GetActiveGrid()
        {
            return ModeTabs != null && ModeTabs.SelectedIndex == 1 ? ClassGrid : TeacherGrid;
        }

        private void PreviousMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            var next = _currentMatchIndex <= 0 ? _matches.Count - 1 : _currentMatchIndex - 1;
            SelectMatch(next);
        }

        private void NextMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            var next = _currentMatchIndex >= _matches.Count - 1 ? 0 : _currentMatchIndex + 1;
            SelectMatch(next);
        }

        private void SelectMatch(int index)
        {
            if (index < 0 || index >= _matches.Count) return;

            for (int i = 0; i < _matches.Count; i++)
            {
                _matches[i].Border.BorderBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192));
                _matches[i].Border.BorderThickness = new Thickness(2);
            }

            _currentMatchIndex = index;
            var current = _matches[index];
            current.Border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 111, 0));
            current.Border.BorderThickness = new Thickness(3);
            current.Border.BringIntoView();

            if (StatusText != null)
                StatusText.Text = (index + 1) + " / " + _matches.Count + " eşleşme";
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var isTeacherMode = ModeTabs.SelectedIndex == 0;
            var dlg = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = isTeacherMode ? "ogretmen-carsaf.csv" : "sinif-carsaf.csv"
            };

            if (dlg.ShowDialog(this) != true) return;
            var rows = isTeacherMode ? BuildTeacherRows() : BuildClassRows();
            System.IO.File.WriteAllText(dlg.FileName, BuildCsv(rows), new UTF8Encoding(true));
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;

            var activeGrid = GetActiveGrid();
            activeGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            activeGrid.Arrange(new Rect(activeGrid.DesiredSize));
            activeGrid.UpdateLayout();
            printDialog.PrintVisual(activeGrid, ModeTabs.SelectedIndex == 0 ? "Öğretmen Çarşafı" : "Sınıf Çarşafı");
        }

        private void OpenExport_Click(object sender, RoutedEventArgs e)
        {
            OpenExportDialog();
        }

        private void OpenExportDialog()
        {
            var dialog = new ExportOptionsWindow(_vm.Store.Teachers, ModeTabs.SelectedIndex == 0)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true) return;
            ExecuteExport(dialog);
        }

        private void ExecuteExport(ExportOptionsWindow options)
        {
            if (options.Target == ExportTarget.ActiveBlanket)
            {
                if (options.Package != ExportPackage.SingleFile)
                {
                    MessageBox.Show(this, "Çarşaf görünümü için sadece tek dosya export kullanılır.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (options.Format == ExportFormat.Csv)
                    ExportActiveBlanketCsv();
                else
                    ExportActiveBlanketPdf();
                return;
            }

            if (options.Target == ExportTarget.SelectedTeacher)
            {
                if (options.Package != ExportPackage.SingleFile)
                {
                    MessageBox.Show(this, "Seçili öğretmen için sadece tek dosya export kullanılır.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (options.Format == ExportFormat.Csv)
                    ExportSingleTeacherCsv(options.SelectedTeacher);
                else
                    ExportSingleTeacherPdf(options.SelectedTeacher, options.UseColor);
                return;
            }

            if (options.Format == ExportFormat.Csv)
                ExportAllTeachersCsv(options.Package);
            else
                ExportAllTeachersPdf(options.Package, options.UseColor);
        }

        private void ExportActiveBlanketCsv()
        {
            ExportCsv_Click(this, new RoutedEventArgs());
        }

        private void ExportActiveBlanketPdf()
        {
            ExportPdf_Click(this, new RoutedEventArgs());
        }

        private void ExportSingleTeacherCsv(Teacher teacher)
        {
            if (teacher == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = SafeFileName(teacher.Name) + "-program.csv"
            };

            if (dlg.ShowDialog(this) != true) return;
            File.WriteAllText(dlg.FileName, BuildTeacherWeeklyCsv(teacher), new UTF8Encoding(true));
        }

        private void ExportSingleTeacherPdf(Teacher teacher, bool useColor)
        {
            if (teacher == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "PDF dosyası (*.pdf)|*.pdf",
                FileName = SafeFileName(teacher.Name) + "-program.pdf"
            };

            if (dlg.ShowDialog(this) != true) return;
            SimplePdfExporter.WriteTeacherPdf(dlg.FileName, new[] { BuildTeacherPdfPage(teacher, useColor) }, _colorIndexMap);
        }

        private void ExportAllTeachersCsv(ExportPackage package)
        {
            if (package == ExportPackage.SingleFile)
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Excel CSV dosyası (*.csv)|*.csv",
                    FileName = "tum-ogretmen-programlari.csv"
                };

                if (dlg.ShowDialog(this) != true) return;

                var sb = new StringBuilder();
                foreach (var teacher in _vm.Store.Teachers.OrderBy(t => t.Name))
                {
                    sb.AppendLine(EscapeCsv("Öğretmen") + ";" + EscapeCsv(teacher.Name));
                    sb.Append(BuildTeacherWeeklyCsv(teacher));
                    sb.AppendLine();
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                return;
            }

            if (package == ExportPackage.Folder)
            {
                ExportAllTeacherCsvFilesToFolder();
                return;
            }

            ExportAllTeacherCsvFilesToZip();
        }

        private void ExportAllTeachersPdf(ExportPackage package, bool useColor)
        {
            var teachers = _vm.Store.Teachers.OrderBy(t => t.Name).ToList();

            if (package == ExportPackage.SingleFile)
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "PDF dosyası (*.pdf)|*.pdf",
                    FileName = "tum-ogretmen-programlari.pdf"
                };

                if (dlg.ShowDialog(this) != true) return;
                SimplePdfExporter.WriteTeacherPdf(dlg.FileName, teachers.Select(t => BuildTeacherPdfPage(t, useColor)), _colorIndexMap);
                return;
            }

            if (package == ExportPackage.Folder)
            {
                using (var dialog = new Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Tüm öğretmen PDF programlarının kaydedileceği klasörü seç";
                    if (dialog.ShowDialog() != Forms.DialogResult.OK) return;

                    foreach (var teacher in teachers)
                    {
                        var path = Path.Combine(dialog.SelectedPath, SafeFileName(teacher.Name) + "-program.pdf");
                        SimplePdfExporter.WriteTeacherPdf(path, new[] { BuildTeacherPdfPage(teacher, useColor) }, _colorIndexMap);
                    }
                }
                return;
            }

            var zipDialog = new SaveFileDialog
            {
                Filter = "ZIP dosyası (*.zip)|*.zip",
                FileName = "tum-ogretmen-programlari-pdf.zip"
            };

            if (zipDialog.ShowDialog(this) != true) return;
            if (File.Exists(zipDialog.FileName))
                File.Delete(zipDialog.FileName);

            using (var archive = ZipFile.Open(zipDialog.FileName, ZipArchiveMode.Create))
            {
                foreach (var teacher in teachers)
                {
                    var entry = archive.CreateEntry(SafeFileName(teacher.Name) + "-program.pdf");
                    using (var stream = entry.Open())
                    {
                        var pdfBytes = SimplePdfExporter.BuildTeacherPdfBytes(new[] { BuildTeacherPdfPage(teacher, useColor) }, _colorIndexMap);
                        stream.Write(pdfBytes, 0, pdfBytes.Length);
                    }
                }
            }
        }

        private void ExportAllTeacherCsvFilesToFolder()
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "Tüm öğretmen CSV programlarının kaydedileceği klasörü seç";
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;

                foreach (var teacher in _vm.Store.Teachers.OrderBy(t => t.Name))
                {
                    var path = Path.Combine(dialog.SelectedPath, SafeFileName(teacher.Name) + "-program.csv");
                    File.WriteAllText(path, BuildTeacherWeeklyCsv(teacher), new UTF8Encoding(true));
                }
            }
        }

        private void ExportAllTeacherCsvFilesToZip()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "ZIP dosyası (*.zip)|*.zip",
                FileName = "tum-ogretmen-programlari.zip"
            };

            if (dlg.ShowDialog(this) != true) return;
            if (File.Exists(dlg.FileName))
                File.Delete(dlg.FileName);

            using (var archive = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create))
            {
                foreach (var teacher in _vm.Store.Teachers.OrderBy(t => t.Name))
                {
                    var entry = archive.CreateEntry(SafeFileName(teacher.Name) + "-program.csv");
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
                    {
                        writer.Write(BuildTeacherWeeklyCsv(teacher));
                    }
                }
            }
        }

        private List<BlanketRow> BuildSingleTeacherRows(Teacher teacher)
        {
            return new List<BlanketRow>
            {
                new BlanketRow
                {
                    Header = teacher.Name,
                    Cells = BuildCells((day, slot) =>
                    {
                        var entry = _vm.Schedule.FirstOrDefault(e => e.Teacher == teacher && e.Day == day && e.SlotIndex == slot.Index);
                        if (entry == null) return null;
                        return new BlanketCell
                        {
                            Text = Short(entry.Group) + "\n" + Short(entry.Course),
                            SearchText = JoinSearch(teacher.Name, entry.Group, entry.Course, entry.Teacher),
                            ColorKey = entry.Course != null ? entry.Course.Name : teacher.Name
                        };
                    })
                }
            };
        }

        private void BuildTeacherWeeklyGrid(Grid grid, Teacher teacher, bool grayscale)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            var slotHeaders = BuildSlotHeaders();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            foreach (var _ in _vm.Store.Days)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellHeight) });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderWidth) });
            foreach (var _ in slotHeaders)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });

            AddHeaderCell(grid, "Gün", 0, 0, HeaderWidth, grayscale);
            for (int i = 0; i < slotHeaders.Count; i++)
                AddHeaderCell(grid, slotHeaders[i].Index.ToString(), 0, i + 1, CellWidth, grayscale);

            for (int dayIndex = 0; dayIndex < _vm.Store.Days.Count; dayIndex++)
            {
                var day = _vm.Store.Days[dayIndex];
                AddRowHeader(grid, day.Name, dayIndex + 1, grayscale);

                for (int slotIndex = 0; slotIndex < slotHeaders.Count; slotIndex++)
                {
                    var slot = slotHeaders[slotIndex];
                    var entry = _vm.Schedule.FirstOrDefault(e => e.Teacher == teacher && e.Day == day && e.SlotIndex == slot.Index);
                    var textValue = entry == null ? string.Empty : Short(entry.Group) + "\n" + Short(entry.Course);
                    var border = new Border
                    {
                        BorderBrush = grayscale ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(218, 224, 232)),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = string.IsNullOrWhiteSpace(textValue)
                            ? Brushes.White
                            : grayscale ? Brushes.White : BuildBrush(entry.Course != null ? entry.Course.Name : teacher.Name),
                        Padding = new Thickness(2)
                    };
                    border.Child = new TextBlock
                    {
                        Text = textValue,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetRow(border, dayIndex + 1);
                    Grid.SetColumn(border, slotIndex + 1);
                    grid.Children.Add(border);
                }
            }
        }

        private string BuildTeacherWeeklyCsv(Teacher teacher)
        {
            var slotHeaders = BuildSlotHeaders();
            var sb = new StringBuilder();
            sb.Append(EscapeCsv("Gün"));
            foreach (var slot in slotHeaders)
                sb.Append(";").Append(EscapeCsv("Ders Saati " + slot.Index + ": " + (slot.Label ?? string.Empty)));
            sb.AppendLine();

            foreach (var day in _vm.Store.Days)
            {
                sb.Append(EscapeCsv(day.Name));
                foreach (var slot in slotHeaders)
                {
                    var entry = _vm.Schedule.FirstOrDefault(e => e.Teacher == teacher && e.Day == day && e.SlotIndex == slot.Index);
                    var text = entry == null ? string.Empty : (Short(entry.Group) + " " + Short(entry.Course)).Trim();
                    sb.Append(";").Append(EscapeCsv(text));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private TeacherPdfPage BuildTeacherPdfPage(Teacher teacher, bool useColor)
        {
            var slotHeaders = BuildSlotHeaders();
            var rows = new List<TeacherPdfRow>();

            foreach (var day in _vm.Store.Days)
            {
                var values = new List<TeacherPdfCell>();
                foreach (var slot in slotHeaders)
                {
                    var entry = _vm.Schedule.FirstOrDefault(e => e.Teacher == teacher && e.Day == day && e.SlotIndex == slot.Index);
                    values.Add(new TeacherPdfCell
                    {
                        Text = entry == null ? string.Empty : Short(entry.Group) + "\n" + Short(entry.Course),
                        ColorKey = entry == null || entry.Course == null ? teacher.Name : entry.Course.Name
                    });
                }

                rows.Add(new TeacherPdfRow { DayName = day.Name, Cells = values });
            }

            return new TeacherPdfPage
            {
                Title = teacher.Name + " Ders Programı",
                SlotHeaders = slotHeaders.Select(s => s.Index.ToString()).ToList(),
                Rows = rows,
                UseColor = useColor
            };
        }

        private List<TimeSlot> BuildSlotHeaders()
        {
            return _vm.Store.Days
                .SelectMany(d => d.Slots)
                .GroupBy(s => s.Index)
                .OrderBy(g => g.Key)
                .Select(g => g.First())
                .ToList();
        }

        private string BuildCsv(List<BlanketRow> rows)
        {
            var slots = _vm.Store.Days.SelectMany(d => d.Slots.OrderBy(s => s.Index).Select(s => d.Name + " " + s.Index)).ToList();
            var sb = new StringBuilder();
            sb.Append(EscapeCsv("Ad"));
            foreach (var slot in slots)
                sb.Append(";").Append(EscapeCsv(slot));
            sb.AppendLine();

            foreach (var row in rows)
            {
                sb.Append(EscapeCsv(row.Header));
                foreach (var cell in row.Cells)
                    sb.Append(";").Append(EscapeCsv((cell.Text ?? string.Empty).Replace("\n", " ")));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string JoinSearch(string first, params object[] values)
        {
            var parts = new List<string> { first ?? string.Empty };
            foreach (var value in values)
            {
                var group = value as ClassGroup;
                if (group != null) parts.Add(group.Name);

                var course = value as Course;
                if (course != null) parts.Add(course.Name);

                var teacher = value as Teacher;
                if (teacher != null) parts.Add(teacher.Name);
            }
            return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string Short(object value)
        {
            var named = value as Course;
            if (named != null) return ShortText(string.IsNullOrWhiteSpace(named.Code) ? named.Name : named.Code);

            var group = value as ClassGroup;
            if (group != null) return ShortText(group.Name);

            var teacher = value as Teacher;
            if (teacher != null) return ShortText(teacher.Name);

            return string.Empty;
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ogretmen";
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(ch, '-');
            return value;
        }

        private static string ShortText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var pipeIndex = value.IndexOf('|');
            if (pipeIndex > 0) return value.Substring(0, pipeIndex).Trim();
            if (value.Length <= 10) return value;
            return value.Substring(0, 10);
        }

        private sealed class SlotColumn
        {
            public Day Day { get; set; }
            public TimeSlot Slot { get; set; }
        }

        private sealed class BlanketRow
        {
            public string Header { get; set; }
            public List<BlanketCell> Cells { get; set; }
        }

        private sealed class BlanketCell
        {
            public string Text { get; set; }
            public string SearchText { get; set; }
            public string ColorKey { get; set; }
        }

        private sealed class CellVisual
        {
            public Border Border { get; set; }
            public string Text { get; set; }
            public bool HasValue { get; set; }
            public Brush OriginalBrush { get; set; }
            public Grid OwnerGrid { get; set; }
        }

        private sealed class TeacherPdfPage
        {
            public string Title { get; set; }
            public List<string> SlotHeaders { get; set; }
            public List<TeacherPdfRow> Rows { get; set; }
            public bool UseColor { get; set; }
        }

        private sealed class TeacherPdfRow
        {
            public string DayName { get; set; }
            public List<TeacherPdfCell> Cells { get; set; }
        }

        private sealed class TeacherPdfCell
        {
            public string Text { get; set; }
            public string ColorKey { get; set; }
        }

        private static class SimplePdfExporter
        {
            public static void WriteTeacherPdf(string path, IEnumerable<TeacherPdfPage> pages, IReadOnlyDictionary<string, int> colorIndexMap)
            {
                File.WriteAllBytes(path, BuildTeacherPdfBytes(pages, colorIndexMap));
            }

            public static byte[] BuildTeacherPdfBytes(IEnumerable<TeacherPdfPage> pages, IReadOnlyDictionary<string, int> colorIndexMap)
            {
                var pageList = pages.ToList();
                var objects = new List<string>();

                objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                objects.Add(string.Empty);
                objects.Add("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

                var kids = new List<int>();
                foreach (var page in pageList)
                {
                    var pageId = objects.Count + 1;
                    var contentId = pageId + 1;
                    kids.Add(pageId);

                    var content = BuildPageContent(page, colorIndexMap);
                    var contentBytes = Encoding.ASCII.GetBytes(content);
                    objects.Add(pageId + " 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R >> >> /Contents " + contentId + " 0 R >>\nendobj\n");
                    objects.Add(contentId + " 0 obj\n<< /Length " + contentBytes.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");
                }

                objects[1] = "2 0 obj\n<< /Type /Pages /Kids [" + string.Join(" ", kids.Select(k => k + " 0 R")) + "] /Count " + kids.Count + " >>\nendobj\n";

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

            private static string BuildPageContent(TeacherPdfPage page, IReadOnlyDictionary<string, int> colorIndexMap)
            {
                var sb = new StringBuilder();
                const double pageWidth = 842;
                const double pageHeight = 595;
                const double margin = 28;
                const double leftWidth = 92;
                const double headerHeight = 30;
                const double rowHeight = 54;
                var columnCount = Math.Max(1, page.SlotHeaders.Count);
                var cellWidth = (pageWidth - (margin * 2) - leftWidth) / columnCount;
                var top = pageHeight - margin - 42;

                AddText(sb, page.Title, margin, pageHeight - margin - 8, 18, true);
                AddText(sb, "Guncel aktif alternatif uzerinden uretilmistir.", margin, pageHeight - margin - 28, 9, false);

                AddRect(sb, margin, top, leftWidth, headerHeight, page.UseColor ? "0.06 0.46 0.43" : "1 1 1", "0 0 0");
                AddText(sb, "Gun", margin + 8, top + 11, 10, true, page.UseColor ? "1 1 1" : "0 0 0");

                for (int i = 0; i < page.SlotHeaders.Count; i++)
                {
                    var x = margin + leftWidth + (i * cellWidth);
                    AddRect(sb, x, top, cellWidth, headerHeight, page.UseColor ? "0.06 0.46 0.43" : "1 1 1", "0 0 0");
                    AddText(sb, page.SlotHeaders[i], x + 5, top + 11, 10, true, page.UseColor ? "1 1 1" : "0 0 0");
                }

                for (int r = 0; r < page.Rows.Count; r++)
                {
                    var row = page.Rows[r];
                    var y = top - ((r + 1) * rowHeight);
                    AddRect(sb, margin, y, leftWidth, rowHeight, page.UseColor ? "0.90 0.97 0.94" : "1 1 1", "0 0 0");
                    AddText(sb, row.DayName, margin + 8, y + (rowHeight / 2) - 3, 10, true);

                    for (int c = 0; c < page.SlotHeaders.Count; c++)
                    {
                        var cell = c < row.Cells.Count ? row.Cells[c] : new TeacherPdfCell();
                        var x = margin + leftWidth + (c * cellWidth);
                        var fill = string.IsNullOrWhiteSpace(cell.Text)
                            ? "1 1 1"
                            : page.UseColor ? SchedulePaletteService.GetPdfColor(cell.ColorKey, colorIndexMap) : "1 1 1";
                        AddRect(sb, x, y, cellWidth, rowHeight, fill, "0.75 0.75 0.75");
                        AddMultilineText(sb, cell.Text, x + 4, y + rowHeight - 18, 8, cellWidth - 8);
                    }
                }

                return sb.ToString();
            }

            private static void AddRect(StringBuilder sb, double x, double y, double width, double height, string fillRgb, string strokeRgb)
            {
                sb.Append(fillRgb).Append(" rg ").Append(strokeRgb).Append(" RG ");
                sb.Append(Num(x)).Append(" ").Append(Num(y)).Append(" ").Append(Num(width)).Append(" ").Append(Num(height)).Append(" re B\n");
            }

            private static void AddMultilineText(StringBuilder sb, string text, double x, double y, double fontSize, double maxWidth)
            {
                var lines = (text ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                    AddText(sb, Truncate(lines[i], maxWidth, fontSize), x, y - (i * (fontSize + 3)), fontSize, true);
            }

            private static void AddText(StringBuilder sb, string text, double x, double y, double fontSize, bool bold, string rgb = "0 0 0")
            {
                sb.Append(rgb).Append(" rg BT /F1 ").Append(Num(fontSize)).Append(" Tf ");
                sb.Append(Num(x)).Append(" ").Append(Num(y)).Append(" Td (").Append(EscapePdfText(text)).Append(") Tj ET\n");
            }

            private static string EscapePdfText(string value)
            {
                return ToPdfSafeText(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            }

            private static string ToPdfSafeText(string value)
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

            private static string Truncate(string value, double maxWidth, double fontSize)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                var safe = ToPdfSafeText(value);
                var maxChars = Math.Max(3, (int)(maxWidth / (fontSize * 0.55)));
                return safe.Length <= maxChars ? safe : safe.Substring(0, maxChars - 1) + ".";
            }

            private static string Num(double value)
            {
                return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
