using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class WelcomeTutorialWindow : Window
    {
        public bool OpenWizardRequested { get; private set; }
        public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

        public WelcomeTutorialWindow()
        {
            InitializeComponent();
        }

        private void OpenWizard_Click(object sender, RoutedEventArgs e)
        {
            OpenWizardRequested = true;
            DialogResult = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
