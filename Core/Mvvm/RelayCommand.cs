using System;
using System.Windows.Input;

namespace AutoScheduler.Core.Mvvm
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // WPF command sources (Button/MenuItem etc.) only refresh enabled/disabled state
        // when CanExecuteChanged is raised. Hooking into CommandManager.RequerySuggested
        // gives us automatic refresh on common UI events; we can also force-refresh via
        // InvalidateRequerySuggested.
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
