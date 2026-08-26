using AutoScheduler.Core.Mvvm;

namespace AutoScheduler.Core.Models
{
    public sealed class Room : BaseViewModel
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private int _capacity;
        public int Capacity
        {
            get => _capacity;
            set => Set(ref _capacity, value < 0 ? 0 : value);
        }

        private string _type;
        public string Type
        {
            get => _type;
            set => Set(ref _type, value);
        }

        public override string ToString() => Name;
    }
}
