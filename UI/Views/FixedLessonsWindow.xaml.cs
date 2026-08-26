using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class FixedLessonsWindow : Window
    {
        public ProjectStore Store { get; }

        // Use DataGrid.SelectedItem instead of binding to avoid INotifyPropertyChanged plumbing in this window.
        public FixedLesson SelectedFixedLesson { get; set; }

        public FixedLessonsWindow(ProjectStore store)
        {
            InitializeComponent();
            Store = store;
            DataContext = this;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var f = new FixedLesson
            {
                Group = Store.Groups.Count > 0 ? Store.Groups[0] : null,
                Day = Store.Days.Count > 0 ? Store.Days[0] : null,
                SlotIndex = 1,
                Course = Store.Courses.Count > 0 ? Store.Courses[0] : null,
                Teacher = Store.Teachers.Count > 0 ? Store.Teachers[0] : null,
                Room = Store.Rooms.Count > 0 ? Store.Rooms[0] : null,
                BlockSize = 1
            };

            Store.FixedLessons.Add(f);
            SelectedFixedLesson = f;
            FixedGrid.SelectedItem = f;
            FixedGrid.ScrollIntoView(f);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var selected = FixedGrid.SelectedItem as FixedLesson;
            if (selected == null) return;

            var confirm = MessageBox.Show(
                "Seçili sabit ders silinsin mi?",
                "Sabit Dersi Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            Store.FixedLessons.Remove(selected);
            SelectedFixedLesson = null;
        }
    }
}
