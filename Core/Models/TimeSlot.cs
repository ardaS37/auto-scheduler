using AutoScheduler.Core.Mvvm;
using System;

namespace AutoScheduler.Core.Models
{
    public sealed class TimeSlot : BaseViewModel
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => Set(ref _index, value);
        }

        private TimeSpan _start;
        public TimeSpan Start
        {
            get => _start;
            set => Set(ref _start, value);
        }

        private TimeSpan _end;
        public TimeSpan End
        {
            get => _end;
            set => Set(ref _end, value);
        }

        private string _label;
        public string Label
        {
            get => _label;
            set => Set(ref _label, value);
        }
    }
}
