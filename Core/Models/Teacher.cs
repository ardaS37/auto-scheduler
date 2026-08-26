using AutoScheduler.Core.Mvvm;
using System.Collections.ObjectModel;

namespace AutoScheduler.Core.Models
{
    public enum HalfDayAvailability
    {
        Any = 0,
        Morning = 1,
        Afternoon = 2
    }

    public sealed class Teacher : BaseViewModel
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set => Set(ref _phone, value);
        }

        private string _photoPath;
        public string PhotoPath
        {
            get => _photoPath;
            set => Set(ref _photoPath, value);
        }

        private AcademicTitle _title = AcademicTitle.None;
        public AcademicTitle Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private HalfDayAvailability _halfDayAvailability = HalfDayAvailability.Any;
        public HalfDayAvailability HalfDayAvailability
        {
            get => _halfDayAvailability;
            set => Set(ref _halfDayAvailability, value);
        }

        // Hocanın verebileceği dersler (popup'ta seçilecek)
        public ObservableCollection<Course> CanTeachCourses { get; } =
            new ObservableCollection<Course>();

        // Hocanın özellikle istediği dersler (Course.Name listesi olarak tutulur)
        public ObservableCollection<string> PreferredCourseNames { get; } =
            new ObservableCollection<string>();

        // Hocanın mümkünse verilmesini istemediği dersler (Course.Name listesi olarak tutulur)
        public ObservableCollection<string> UnwantedCourseNames { get; } =
            new ObservableCollection<string>();

        // Hocanın müsait olmadığı günler (Day.Name listesi olarak tutulur)
        public ObservableCollection<string> UnavailableDayNames { get; } =
            new ObservableCollection<string>();

        // Hocanın müsait olmadığı detaylı ders saatleri ("Gün|SlotIndex" olarak tutulur)
        public ObservableCollection<string> UnavailableSlotKeys { get; } =
            new ObservableCollection<string>();

        // K12: Hocanın nöbetçi olduğu günler (Day.Name listesi olarak tutulur)
        public ObservableCollection<string> DutyDayNames { get; } =
            new ObservableCollection<string>();

        public override string ToString() => Name;
    }
}
