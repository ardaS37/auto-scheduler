using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class TeacherSurveyImportWindow : Window
    {
        private readonly ProjectStore _store;

        public TeacherSurveyImportWindow(ProjectStore store)
        {
            _store = store;
            InitializeComponent();

            foreach (var question in TeacherSurveyImportService.BuildTemplateQuestions())
                TemplateQuestionsListBox.Items.Add(question);
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv",
                FileName = "ogretmen-anketi-sablon.csv"
            };

            if (dlg.ShowDialog(this) != true) return;

            File.WriteAllText(dlg.FileName, TeacherSurveyImportService.BuildTemplateCsv(), new UTF8Encoding(true));
            StatusTextBlock.Text = "Şablon kaydedildi: " + Path.GetFileName(dlg.FileName);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV dosyası (*.csv)|*.csv|Tüm dosyalar (*.*)|*.*"
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                CsvTextBox.Text = File.ReadAllText(dlg.FileName);
                StatusTextBlock.Text = "Dosya yüklendi: " + Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Dosya okunamadı: " + ex.Message;
            }
        }

        private void ClearBox_Click(object sender, RoutedEventArgs e)
        {
            CsvTextBox.Clear();
            ResultListBox.Items.Clear();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var result = TeacherSurveyImportService.Import(
                _store,
                CsvTextBox.Text,
                AutoCreateCoursesCheckBox.IsChecked == true,
                UpdateExistingCheckBox.IsChecked == true);

            ResultListBox.Items.Clear();
            foreach (var error in result.Errors)
                ResultListBox.Items.Add("Hata: " + error);
            foreach (var warning in result.Warnings)
                ResultListBox.Items.Add("Uyarı: " + warning);

            StatusTextBlock.Text = string.Format(
                "{0} yeni öğretmen eklendi, {1} öğretmen güncellendi, {2} hata, {3} uyarı.",
                result.AddedCount, result.UpdatedCount, result.Errors.Count, result.Warnings.Count);
        }
    }
}
