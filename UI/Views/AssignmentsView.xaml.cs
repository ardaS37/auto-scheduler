using AutoScheduler.UI.ViewModels;
using AutoScheduler.UI.Views;
using System.Windows;
using System.Windows.Controls;

namespace AutoScheduler.UI.Views
{
    public partial class AssignmentsView : UserControl
    {
        private bool _quickAssigning;

        public AssignmentsView()
        {
            InitializeComponent();
        }
        private void TeachersList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Boş alana double click olursa açmasın
            var vm = DataContext as AssignmentsViewModel;
            if (vm?.SelectedTeacher == null) return;

            // Var olan buton handler’ını kullan
            TeacherDetails_Click(sender, new RoutedEventArgs());
        }

        private void TeacherDetails_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AssignmentsViewModel;
            if (vm?.SelectedTeacher == null) return;

            var win = new TeacherEditWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = new TeacherEditViewModel(vm.SelectedTeacher, vm.Store, vm.Courses, vm.Store.Days)
            };

            win.ShowDialog();
        }

        private bool _committing;

        private void CommitGridEdits(DataGrid dg)
        {
            if (dg == null) return;
            if (_committing) return;

            try
            {
                _committing = true;

                // Commit any in-progress edits so values don't revert when switching selection/tabs/buttons.
                dg.CommitEdit(DataGridEditingUnit.Cell, true);
                dg.CommitEdit(DataGridEditingUnit.Row, true);
            }
            finally
            {
                _committing = false;
            }
        }

        private void CommitAssignmentsGridEdits()
        {
            CommitGridEdits(AssignmentsGrid);
        }

        private void QueueCommitAssignmentsGridEdits()
        {
            // Avoid re-entrancy (CellEditEnding/RowEditEnding can be triggered by CommitEdit).
            Dispatcher.BeginInvoke(new System.Action(() => CommitAssignmentsGridEdits()), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyQuickAssign()
        {
            if (_quickAssigning) return;

            var vm = DataContext as AssignmentsViewModel;
            if (vm == null) return;
            if (!vm.QuickAssignMode) return;
            if (vm.SelectedAssignment == null) return;

            try
            {
                _quickAssigning = true;

                // Apply last-selected items from the left panels to the selected assignment.
                // Course first (this clears Teacher and refreshes available teachers).
                if (vm.SelectedCourse != null)
                    vm.SelectedAssignment.Course = vm.SelectedCourse;

                if (vm.SelectedTeacher != null)
                    vm.SelectedAssignment.Teacher = vm.SelectedTeacher;

                if (vm.IsRoomEditingEnabled && vm.SelectedRoom != null)
                    vm.SelectedAssignment.Room = vm.SelectedRoom;

                CommitAssignmentsGridEdits();
            }
            finally
            {
                _quickAssigning = false;
            }
        }

        private static bool HasAncestorOfType(DependencyObject obj, System.Type t)
        {
            var cur = obj;
            while (cur != null)
            {
                if (t.IsInstanceOfType(cur)) return true;
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            }
            return false;
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Don't force-commit here; it can cancel entering edit mode.

            // Only apply when selection actually changes.
            if (e == null || e.AddedItems == null || e.AddedItems.Count == 0) return;
            ApplyQuickAssign();
        }

        private void AssignmentsGrid_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // SelectionChanged doesn't always fire (e.g., clicking inside the already-selected row).
            // So on mouse up, try to apply quick assign to the currently selected row.
            // BUT: don't auto-assign when the user is clicking inside an editor control (ComboBox/TextBox).
            var src = e != null ? e.OriginalSource as DependencyObject : null;
            if (src != null)
            {
                if (HasAncestorOfType(src, typeof(ComboBox)) ||
                    HasAncestorOfType(src, typeof(TextBox)) ||
                    HasAncestorOfType(src, typeof(Button)) ||
                    HasAncestorOfType(src, typeof(CheckBox)))
                {
                    return;
                }
            }

            QueueCommitAssignmentsGridEdits();
            ApplyQuickAssign();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            QueueCommitAssignmentsGridEdits();
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            QueueCommitAssignmentsGridEdits();
        }

        private void AddAssignment_Click(object sender, RoutedEventArgs e)
        {
            CommitAssignmentsGridEdits();

            var vm = DataContext as AssignmentsViewModel;
            if (vm == null) return;

            if (vm.AddAssignmentCommand != null && vm.AddAssignmentCommand.CanExecute(null))
                vm.AddAssignmentCommand.Execute(null);

            CommitAssignmentsGridEdits();
        }

        private void RemoveAssignment_Click(object sender, RoutedEventArgs e)
        {
            CommitAssignmentsGridEdits();

            var vm = DataContext as AssignmentsViewModel;
            if (vm == null) return;

            if (vm.RemoveAssignmentCommand != null && vm.RemoveAssignmentCommand.CanExecute(null))
                vm.RemoveAssignmentCommand.Execute(null);

            CommitAssignmentsGridEdits();
        }

        private void OpenBulkAssignments_Click(object sender, RoutedEventArgs e)
        {
            CommitAssignmentsGridEdits();

            var vm = DataContext as AssignmentsViewModel;
            if (vm == null) return;

            var win = new BulkAssignmentsWindow(vm)
            {
                Owner = Window.GetWindow(this)
            };

            win.ShowDialog();
            CommitAssignmentsGridEdits();
        }
    }
}
