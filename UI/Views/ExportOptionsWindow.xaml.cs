using AutoScheduler.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class ExportOptionsWindow : Window
    {
        public ExportOptionsWindow(IEnumerable<Teacher> teachers, bool teacherBlanketActive)
        {
            InitializeComponent();

            TeacherBox.ItemsSource = teachers.OrderBy(t => t.Name).ToList();
            TeacherBox.SelectedIndex = TeacherBox.Items.Count > 0 ? 0 : -1;

            ActiveBlanketRadio.IsChecked = true;
            CsvRadio.IsChecked = true;
            SingleFileRadio.IsChecked = true;
            Title = teacherBlanketActive ? "Öğretmen Çarşafı Export" : "Sınıf Çarşafı Export";
        }

        public ExportTarget Target
        {
            get
            {
                if (SelectedTeacherRadio.IsChecked == true) return ExportTarget.SelectedTeacher;
                if (AllTeachersRadio.IsChecked == true) return ExportTarget.AllTeachers;
                return ExportTarget.ActiveBlanket;
            }
        }

        public ExportFormat Format
        {
            get { return PdfRadio.IsChecked == true ? ExportFormat.Pdf : ExportFormat.Csv; }
        }

        public ExportPackage Package
        {
            get
            {
                if (FolderRadio.IsChecked == true) return ExportPackage.Folder;
                if (ZipRadio.IsChecked == true) return ExportPackage.Zip;
                return ExportPackage.SingleFile;
            }
        }

        public bool UseColor
        {
            get { return ColorCheck.IsChecked == true; }
        }

        public Teacher SelectedTeacher
        {
            get { return TeacherBox.SelectedItem as Teacher; }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (Target == ExportTarget.SelectedTeacher && SelectedTeacher == null)
            {
                MessageBox.Show(this, "Öğretmen seçmelisin.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }

    public enum ExportTarget
    {
        ActiveBlanket,
        SelectedTeacher,
        AllTeachers
    }

    public enum ExportFormat
    {
        Csv,
        Pdf
    }

    public enum ExportPackage
    {
        SingleFile,
        Folder,
        Zip
    }
}
