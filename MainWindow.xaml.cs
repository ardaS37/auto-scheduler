using AutoScheduler.UI.Views;
using AutoScheduler.UI.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AutoScheduler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow
            {
                Owner = this
            };

            win.ShowDialog();
        }

        private void LunchBreak_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.MainViewModel;
            if (vm == null) return;

            var win = new AutoScheduler.UI.Views.LunchBreakWindow(vm.Store)
            {
                Owner = this
            };

            win.ShowDialog();
        }

        private void FixedLessons_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.MainViewModel;
            if (vm == null) return;

            var win = new AutoScheduler.UI.Views.FixedLessonsWindow(vm.Store)
            {
                Owner = this
            };

            win.ShowDialog();
        }

        private void DashboardNavigate_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.MainViewModel;
            var button = sender as Button;
            if (vm == null || button == null) return;

            if (int.TryParse(Convert.ToString(button.Tag), out var index))
                vm.SelectedTabIndex = index;
        }

        private void CheckProjectHealth_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var report = vm.RefreshProjectHealth();

            var message = report.Items.Count == 0
                ? "Kritik veya önerilen bir sorun bulunamadı. Program üretmeye hazırsınız."
                : string.Format(
                    "{0} ({1} kritik) konu bulundu:\n\n- {2}{3}",
                    report.Items.Count,
                    report.BlockingCount,
                    string.Join("\n- ", report.Items.Take(10).Select(i => i.Title + ": " + i.Detail)),
                    report.Items.Count > 10 ? "\n- ..." : string.Empty);

            MessageBox.Show(
                this,
                message,
                "Program Sağlığı: " + report.ReadinessLabel,
                MessageBoxButton.OK,
                report.BlockingCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void OpenBlanket_Click(object sender, RoutedEventArgs e)
        {
            OpenBlanketWindow(false);
        }

        private void OpenBlanketExport_Click(object sender, RoutedEventArgs e)
        {
            OpenBlanketWindow(true);
        }

        private void OpenBlanketWindow(bool openExport)
        {
            var vm = DataContext as AutoScheduler.UI.ViewModels.MainViewModel;
            if (vm == null) return;

            var window = new BlanketScheduleWindow(vm.SchedulerVm, openExport)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void OpenSettingsTab_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;
            vm.SelectedTabIndex = 6;
        }

        private void TeacherSurveyImport_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var win = new TeacherSurveyImportWindow(vm.Store)
            {
                Owner = this
            };

            win.ShowDialog();
        }

        private void CreateDesktopShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = Path.Combine(desktop, "Sapsoft Ders Programı Hazırlayıcı.lnk");
                var exePath = Process.GetCurrentProcess().MainModule.FileName;

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    throw new InvalidOperationException("Windows kısayol servisi bulunamadı.");

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.IconLocation = exePath + ",0";
                shortcut.Description = "Sapsoft Ders Programı Hazırlayıcı";
                shortcut.Save();

                MessageBox.Show(this, "Masaüstü kısayolu oluşturuldu.", "Kısayol", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Kısayol oluşturulamadı: " + ex.Message, "Kısayol", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartNewProjectWizard_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var win = new SetupWizardWindow
            {
                Owner = this
            };

            vm.StartNewProjectWizard(store => win.ApplyTo(store));
        }

        private void OpenRecentProject_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var menuItem = sender as MenuItem;
            if (vm == null || menuItem == null) return;

            vm.OpenRecentProject(Convert.ToString(menuItem.Tag));
        }

        private void RecentProjects_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var menuItem = sender as MenuItem;
            if (vm == null || menuItem == null) return;

            menuItem.Items.Clear();

            if (!vm.RecentProjects.Any())
            {
                menuItem.Items.Add(new MenuItem
                {
                    Header = "Henüz kayıt yok",
                    IsEnabled = false
                });
                return;
            }

            foreach (var item in vm.RecentProjects)
            {
                var child = new MenuItem
                {
                    Header = item.DisplayName,
                    Tag = item.Path
                };
                child.Click += OpenRecentProject_Click;
                menuItem.Items.Add(child);
            }
        }

        private void ProjectHealthNavigate_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var frameworkElement = sender as FrameworkElement;
            var item = frameworkElement != null ? frameworkElement.DataContext as AutoScheduler.Core.Services.ProjectHealthItem : null;
            if (vm == null || item == null) return;

            vm.OpenProjectHealthTask(item);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            if (!vm.HandleWindowClosing())
                e.Cancel = true;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null)
                return;

            var feedbackWindow = new StartupFeedbackWindow
            {
                Owner = this
            };
            feedbackWindow.ShowDialog();

            if (vm.ShouldShowWelcomeTutorial())
                ShowWelcomeTutorial(vm);
        }

        private void ShowWelcomeTutorial_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null)
                return;

            ShowWelcomeTutorial(vm);
        }

        private void ShowWelcomeTutorial(MainViewModel vm)
        {
            var tutorial = new WelcomeTutorialWindow
            {
                Owner = this
            };

            tutorial.ShowDialog();
            vm.RecordWelcomeTutorialShown(tutorial.DontShowAgain);

            if (tutorial.OpenWizardRequested)
                StartNewProjectWizard_Click(this, new RoutedEventArgs());
        }

        private void RelaxationRulesGrid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            var grid = sender as DataGrid;
            if (grid == null || grid.SelectedItem == null) return;

            DragDrop.DoDragDrop(grid, grid.SelectedItem, DragDropEffects.Move);
        }

        private void RelaxationRulesGrid_Drop(object sender, DragEventArgs e)
        {
            var source = e.Data.GetData(typeof(SchedulerViewModel.RelaxationRuleItem)) as SchedulerViewModel.RelaxationRuleItem;
            if (source == null) return;

            var targetElement = e.OriginalSource as DependencyObject;
            while (targetElement != null && !(targetElement is DataGridRow))
                targetElement = System.Windows.Media.VisualTreeHelper.GetParent(targetElement);

            var row = targetElement as DataGridRow;
            var target = row != null ? row.Item as SchedulerViewModel.RelaxationRuleItem : null;
            if (target == null) return;

            var vm = DataContext as MainViewModel;
            if (vm == null || vm.SchedulerVm == null) return;

            vm.SchedulerVm.MoveRelaxationRule(source, target);
        }
    }
}
