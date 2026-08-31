using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace AutoScheduler.UI.Views
{
    public partial class StartupFeedbackWindow : Window
    {
        public StartupFeedbackWindow()
        {
            InitializeComponent();
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
