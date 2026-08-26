using AutoScheduler.Core.Store;
using System;
using System.Globalization;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class LunchBreakWindow : Window
    {
        private readonly ProjectStore _store;

        public LunchBreakWindow(ProjectStore store)
        {
            InitializeComponent();
            _store = store;

            // HH:mm format
            StartBox.Text = _store.LunchBreakStart.ToString(@"hh\:mm");
            EndBox.Text = _store.LunchBreakEnd.ToString(@"hh\:mm");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!TimeSpan.TryParseExact(StartBox.Text?.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var start) &&
                !TimeSpan.TryParseExact(StartBox.Text?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out start))
            {
                MessageBox.Show(this, "Başlangıç saati formatı geçersiz. Örn: 12:00", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!TimeSpan.TryParseExact(EndBox.Text?.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var end) &&
                !TimeSpan.TryParseExact(EndBox.Text?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out end))
            {
                MessageBox.Show(this, "Bitiş saati formatı geçersiz. Örn: 13:00", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (end <= start)
            {
                MessageBox.Show(this, "Bitiş saati başlangıçtan sonra olmalı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _store.LunchBreakStart = start;
            _store.LunchBreakEnd = end;

            DialogResult = true;
            Close();
        }
    }
}
