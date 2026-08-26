using AutoScheduler.Core.Mvvm;
using System;
using System.Linq;

namespace AutoScheduler.Core.Models
{
    public sealed class Student : BaseViewModel
    {
        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (Set(ref _firstName, value))
                    OnPropertyChanged(nameof(FullName));
            }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set
            {
                if (Set(ref _lastName, value))
                    OnPropertyChanged(nameof(FullName));
            }
        }

        // Eski proje dosyalarındaki tek alanlı ad bilgisini geriye uyumlu tutar.
        public string FullName
        {
            get => string.Join(" ", new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            set
            {
                var parts = (value ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
            }
        }

        private string _studentNumber;
        public string StudentNumber
        {
            get => _studentNumber;
            set => Set(ref _studentNumber, value);
        }

        public override string ToString() => FullName;
    }
}
