using AutoScheduler.UI.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class BulkAssignmentsWindow : Window
    {
        private readonly AssignmentsViewModel _vm;

        public BulkAssignmentsWindow(AssignmentsViewModel vm)
        {
            _vm = vm;
            InitializeComponent();

            CourseComboBox.ItemsSource = _vm.Courses;
            TeacherComboBox.ItemsSource = _vm.Teachers;
            RoomComboBox.ItemsSource = _vm.Rooms;
            GroupsListBox.ItemsSource = _vm.Groups;

            CourseComboBox.SelectedItem = _vm.SelectedCourse;
            TeacherComboBox.SelectedItem = _vm.SelectedTeacher;
            RoomComboBox.SelectedItem = _vm.SelectedRoom;
        }

        private void ApplyBatch_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroups = GroupsListBox.SelectedItems.Cast<object>()
                .OfType<AutoScheduler.Core.Models.ClassGroup>()
                .ToList();

            int weeklyHours;
            int blockSize;
            int maxPerDay;

            if (!int.TryParse(WeeklyHoursTextBox.Text, out weeklyHours) || weeklyHours <= 0)
            {
                StatusTextBlock.Text = "Haftalık saat pozitif bir sayı olmalı.";
                return;
            }

            if (!int.TryParse(BlockSizeTextBox.Text, out blockSize) || blockSize <= 0)
            {
                StatusTextBlock.Text = "Blok en az 1 olmalı.";
                return;
            }

            if (!int.TryParse(MaxPerDayTextBox.Text, out maxPerDay) || maxPerDay < 0)
            {
                StatusTextBlock.Text = "Günlük max 0 veya pozitif olmalı.";
                return;
            }

            var message = _vm.ApplyBatchAssignment(
                selectedGroups,
                CourseComboBox.SelectedItem as AutoScheduler.Core.Models.Course,
                TeacherComboBox.SelectedItem as AutoScheduler.Core.Models.Teacher,
                RoomComboBox.SelectedItem as AutoScheduler.Core.Models.Room,
                weeklyHours,
                blockSize,
                SpreadAcrossDaysCheckBox.IsChecked == true,
                maxPerDay);

            StatusTextBlock.Text = message;
        }

        private void ImportLines_Click(object sender, RoutedEventArgs e)
        {
            var result = _vm.ImportBulkAssignments(
                BulkTextBox.Text ?? string.Empty,
                AutoCreateGroupsCheckBox.IsChecked == true,
                AutoCreateCoursesCheckBox.IsChecked == true,
                AutoCreateTeachersCheckBox.IsChecked == true,
                AutoCreateRoomsCheckBox.IsChecked == true);

            BindErrors(result.Errors);
            StatusTextBlock.Text = result.Summary;
        }

        private void FillSample_Click(object sender, RoutedEventArgs e)
        {
            BulkTextBox.Text =
                "9-A;Matematik;Ayşe Yılmaz;4;1;2;Salon 101\n" +
                "9-B;Matematik;Ayşe Yılmaz;4;1;2;Salon 101\n" +
                "10-A, Fizik, Mehmet Kaya, 2, 1, 1, Lab 1";
        }

        private void BindErrors(List<string> errors)
        {
            ErrorsListBox.ItemsSource = null;
            ErrorsListBox.ItemsSource = errors ?? new List<string>();
        }
    }
}
