using System.Windows;

namespace AutoScheduler.UI.Views
{
    public partial class MoveLessonWindow : Window
    {
        public MoveLessonWindow()
        {
            InitializeComponent();
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
