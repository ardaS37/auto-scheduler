using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using System;
using System.Linq;
using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class SetupWizardWindow : Window
    {
        private SchoolPreset _selectedPreset = SchoolPreset.Lise;

        public SetupWizardWindow()
        {
            InitializeComponent();
            UpdatePresetSummary();
        }

        public bool ApplyTo(ProjectStore store)
        {
            if (ShowDialog() != true)
                return false;

            var groups = (GroupsTextBox.Text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            ProjectPresetService.ApplySchoolPreset(store, _selectedPreset, ProjectNameTextBox.Text, groups);
            return true;
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button == null || button.Tag == null)
                return;

            SchoolPreset parsed;
            if (!Enum.TryParse(Convert.ToString(button.Tag), out parsed))
                return;

            _selectedPreset = parsed;
            if (string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) || ProjectNameTextBox.Text == "Yeni Proje")
                ProjectNameTextBox.Text = GetPresetProjectName(parsed);

            if (string.IsNullOrWhiteSpace(GroupsTextBox.Text))
                GroupsTextBox.Text = GetPresetGroupSuggestion(parsed);

            UpdatePresetSummary();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void UpdatePresetSummary()
        {
            var slotsPerDay = _selectedPreset == SchoolPreset.Ilkokul ? 6 : 8;
            PresetSummaryText.Text =
                string.Format("{0} şablonu seçildi. 5 gün, {1} ders saati ve birleşik özellik ayarları uygulanacak.", GetPresetProjectName(_selectedPreset), slotsPerDay);
        }

        private static string GetPresetProjectName(SchoolPreset preset)
        {
            switch (preset)
            {
                case SchoolPreset.Ilkokul:
                    return "İlkokul";
                case SchoolPreset.Ortaokul:
                    return "Ortaokul";
                case SchoolPreset.Lise:
                    return "Lise";
                case SchoolPreset.Universite:
                    return "Üniversite";
                default:
                    return "Yeni Proje";
            }
        }

        private static string GetPresetGroupSuggestion(SchoolPreset preset)
        {
            switch (preset)
            {
                case SchoolPreset.Ilkokul:
                    return "1-A\n1-B\n2-A";
                case SchoolPreset.Ortaokul:
                    return "5-A\n5-B\n6-A";
                case SchoolPreset.Lise:
                    return "9-A\n9-B\n10-A";
                case SchoolPreset.Universite:
                    return "1. Sınıf A\n1. Sınıf B";
                default:
                    return string.Empty;
            }
        }
    }
}
