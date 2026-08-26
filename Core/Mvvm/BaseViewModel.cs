using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoScheduler.Core.Mvvm
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propName);
            return true;
        }
    }
}
