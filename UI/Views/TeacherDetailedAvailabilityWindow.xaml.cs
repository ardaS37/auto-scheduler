using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using AutoScheduler.Core.Models;
using AutoScheduler.UI.ViewModels;

namespace AutoScheduler.UI.Views
{
    public partial class TeacherDetailedAvailabilityWindow : Window
    {
        public TeacherDetailedAvailabilityWindow()
        {
            InitializeComponent();
            Loaded += TeacherDetailedAvailabilityWindow_Loaded;
        }

        private void TeacherDetailedAvailabilityWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildGrid();
        }

        private void BuildGrid()
        {
            var vm = DataContext as TeacherEditViewModel;
            if (vm == null) return;

            AvailabilityGrid.Children.Clear();
            AvailabilityGrid.RowDefinitions.Clear();
            AvailabilityGrid.ColumnDefinitions.Clear();

            var days = vm.AllDays.ToList();
            var slots = days
                .SelectMany(d => d.Slots)
                .GroupBy(s => s.Index)
                .OrderBy(g => g.Key)
                .Select(g => g.First())
                .ToList();

            AvailabilityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            foreach (var slot in slots)
                AvailabilityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });

            AvailabilityGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            foreach (var day in days)
                AvailabilityGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            AddHeader("Gün", 0, 0);
            for (int i = 0; i < slots.Count; i++)
                AddHeader(slots[i].Index + "\n" + slots[i].Start.ToString(@"hh\:mm"), 0, i + 1);

            for (int row = 0; row < days.Count; row++)
            {
                var day = days[row];
                AddDayHeader(day.Name, row + 1, 0);

                for (int col = 0; col < slots.Count; col++)
                {
                    var slot = day.Slots.FirstOrDefault(s => s.Index == slots[col].Index);
                    AddSlotCell(vm, day, slot, row + 1, col + 1);
                }
            }
        }

        private void AddHeader(string text, int row, int column)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4)
            };
            border.SetResourceReference(Border.BorderBrushProperty, "AppAccentSoftBorderBrush");
            border.SetResourceReference(Border.BackgroundProperty, "AppAccentSoftBackgroundBrush");

            var textBlock = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "AppAccentStrongBrush");
            border.Child = textBlock;

            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            AvailabilityGrid.Children.Add(border);
        }

        private void AddDayHeader(string text, int row, int column)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 4, 4, 4)
            };
            border.SetResourceReference(Border.BorderBrushProperty, "AppCardBorderBrush");
            border.SetResourceReference(Border.BackgroundProperty, "AppCardBackgroundBrush");

            var textBlock = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "AppAccentStrongBrush");
            border.Child = textBlock;

            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            AvailabilityGrid.Children.Add(border);
        }

        private void AddSlotCell(TeacherEditViewModel vm, Day day, TimeSlot slot, int row, int column)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4)
            };
            border.SetResourceReference(Border.BorderBrushProperty, "AppBorderBrush");
            border.SetResourceReference(Border.BackgroundProperty, "AppCardBackgroundBrush");

            if (slot != null)
            {
                var checkBox = new CheckBox
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = vm.IsSlotUnavailable(day, slot),
                    ToolTip = day.Name + " - " + slot.Index + ". ders: " + slot.Start.ToString(@"hh\:mm") + " - " + slot.End.ToString(@"hh\:mm")
                };

                checkBox.Checked += (s, e) => vm.SetSlotUnavailable(day, slot, true);
                checkBox.Unchecked += (s, e) => vm.SetSlotUnavailable(day, slot, false);
                border.Child = checkBox;
            }

            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            AvailabilityGrid.Children.Add(border);
        }
    }
}
