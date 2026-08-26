using AutoScheduler.Core.Mvvm;
using System.Collections.ObjectModel;

namespace AutoScheduler.Core.Models
{
    public sealed class Day : BaseViewModel
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        public ObservableCollection<TimeSlot> Slots { get; } =
            new ObservableCollection<TimeSlot>();
    }
}
