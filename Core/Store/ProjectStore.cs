using System.Collections.ObjectModel;
using AutoScheduler.Core.Models;





namespace AutoScheduler.Core.Store
{
    public enum EducationMode
    {
        HigherEducation = 0,
        PrimarySecondary = 1
    }

    public sealed class ProjectStore : AutoScheduler.Core.Mvvm.BaseViewModel
    {
        public ObservableCollection<Teacher> Teachers { get; } =
            new ObservableCollection<Teacher>();

        public System.Collections.ObjectModel.ObservableCollection<Room> Rooms { get; } =
            new System.Collections.ObjectModel.ObservableCollection<Room>();

        public ObservableCollection<Day> Days { get; } =
            new ObservableCollection<Day>();

        public ObservableCollection<Course> Courses { get; } =
            new ObservableCollection<Course>();

        public ObservableCollection<Assignment> Assignments { get; } =
            new ObservableCollection<Assignment>();

        public ObservableCollection<CourseConflictPair> CourseConflictPairs { get; } =
            new ObservableCollection<CourseConflictPair>();

        public ObservableCollection<GroupSlotRule> GroupSlotRules { get; } =
            new ObservableCollection<GroupSlotRule>();

        public ObservableCollection<CourseKindSlotRule> CourseKindSlotRules { get; } =
            new ObservableCollection<CourseKindSlotRule>();

        public ObservableCollection<AutoScheduler.Core.Models.FixedLesson> FixedLessons { get; } =
            new ObservableCollection<AutoScheduler.Core.Models.FixedLesson>();

        // Son üretilen/düzenlenen ders programı - proje dosyasıyla birlikte kaydedilir,
        // böylece yeniden açılışta veya taşıma/düzenleme sonrası kaybolmaz.
        public ObservableCollection<ScheduleEntry> Schedule { get; } =
            new ObservableCollection<ScheduleEntry>();

        private string _projectName = "Yeni Proje";
        public string ProjectName
        {
            get => _projectName;
            set => Set(ref _projectName, value);
        }

        public ObservableCollection<ClassGroup> Groups { get; } =
            new ObservableCollection<ClassGroup>();

        private EducationMode _educationMode = EducationMode.HigherEducation;
        public EducationMode EducationMode
        {
            get => _educationMode;
            set => Set(ref _educationMode, value);
        }

        // K12 only: Lunch break window used for teacher half-day availability constraints
        private System.TimeSpan _lunchBreakStart = new System.TimeSpan(12, 0, 0);
        public System.TimeSpan LunchBreakStart
        {
            get => _lunchBreakStart;
            set => Set(ref _lunchBreakStart, value);
        }

        private System.TimeSpan _lunchBreakEnd = new System.TimeSpan(13, 0, 0);
        public System.TimeSpan LunchBreakEnd
        {
            get => _lunchBreakEnd;
            set => Set(ref _lunchBreakEnd, value);
        }

        private bool _randomizeRooms;
        public bool RandomizeRooms
        {
            get { return _randomizeRooms; }
            set { Set(ref _randomizeRooms, value); }
        }

        private int _defaultRoomCapacity = 30;
        public int DefaultRoomCapacity
        {
            get { return _defaultRoomCapacity; }
            set
            {
                var normalized = value;
                if (normalized < 0) normalized = 0;
                if (normalized > 10000) normalized = 10000;
                Set(ref _defaultRoomCapacity, normalized);
            }
        }

        private int _defaultSeatingRows = 5;
        public int DefaultSeatingRows
        {
            get { return _defaultSeatingRows; }
            set { Set(ref _defaultSeatingRows, NormalizeSeatingDimension(value)); }
        }

        private int _defaultSeatingColumns = 3;
        public int DefaultSeatingColumns
        {
            get { return _defaultSeatingColumns; }
            set { Set(ref _defaultSeatingColumns, NormalizeSeatingDimension(value)); }
        }

        private int _defaultStudentsPerDesk = 1;
        public int DefaultStudentsPerDesk
        {
            get { return _defaultStudentsPerDesk; }
            set
            {
                var normalized = value;
                if (normalized < 1) normalized = 1;
                if (normalized > 10) normalized = 10;
                Set(ref _defaultStudentsPerDesk, normalized);
            }
        }

        private bool _examPreventOwnClassRoom = true;
        public bool ExamPreventOwnClassRoom
        {
            get { return _examPreventOwnClassRoom; }
            set { Set(ref _examPreventOwnClassRoom, value); }
        }

        private bool _examPreventSameGradeNeighbors = true;
        public bool ExamPreventSameGradeNeighbors
        {
            get { return _examPreventSameGradeNeighbors; }
            set { Set(ref _examPreventSameGradeNeighbors, value); }
        }

        private ExamNeighborRuleMode _examNeighborRuleMode = ExamNeighborRuleMode.YanOnArka;
        public ExamNeighborRuleMode ExamNeighborRuleMode
        {
            get { return _examNeighborRuleMode; }
            set { Set(ref _examNeighborRuleMode, value); }
        }

        private static int NormalizeSeatingDimension(int value)
        {
            if (value < 1) return 1;
            if (value > 50) return 50;
            return value;
        }

        private bool _preferMorning;
        public bool PreferMorning
        {
            get { return _preferMorning; }
            set { Set(ref _preferMorning, value); }
        }

        private bool _respectTeacherUnavailableDays = true;
        public bool RespectTeacherUnavailableDays
        {
            get { return _respectTeacherUnavailableDays; }
            set { Set(ref _respectTeacherUnavailableDays, value); }
        }

        private bool _respectGroupSlotRules = true;
        public bool RespectGroupSlotRules
        {
            get { return _respectGroupSlotRules; }
            set { Set(ref _respectGroupSlotRules, value); }
        }

        private bool _respectLunchBreak = true;
        public bool RespectLunchBreak
        {
            get { return _respectLunchBreak; }
            set { Set(ref _respectLunchBreak, value); }
        }

        private bool _respectTeacherHalfDay = true;
        public bool RespectTeacherHalfDay
        {
            get { return _respectTeacherHalfDay; }
            set { Set(ref _respectTeacherHalfDay, value); }
        }

        private bool _useDutyDayPriority = true;
        public bool UseDutyDayPriority
        {
            get { return _useDutyDayPriority; }
            set { Set(ref _useDutyDayPriority, value); }
        }

        private bool _useCoursePriorityLevel = true;
        public bool UseCoursePriorityLevel
        {
            get { return _useCoursePriorityLevel; }
            set { Set(ref _useCoursePriorityLevel, value); }
        }

        private bool _useTeacherCoursePreferences = true;
        public bool UseTeacherCoursePreferences
        {
            get { return _useTeacherCoursePreferences; }
            set { Set(ref _useTeacherCoursePreferences, value); }
        }

        private bool _useSpreadAcrossDays = true;
        public bool UseSpreadAcrossDays
        {
            get { return _useSpreadAcrossDays; }
            set { Set(ref _useSpreadAcrossDays, value); }
        }

        private bool _useMaxPerDay = true;
        public bool UseMaxPerDay
        {
            get { return _useMaxPerDay; }
            set { Set(ref _useMaxPerDay, value); }
        }

        private bool _keepBlocksStrict = true;
        public bool KeepBlocksStrict
        {
            get { return _keepBlocksStrict; }
            set { Set(ref _keepBlocksStrict, value); }
        }

        private bool _deepSearchEnabled = true;
        public bool DeepSearchEnabled
        {
            get { return _deepSearchEnabled; }
            set { Set(ref _deepSearchEnabled, value); }
        }

        private bool _useDetailedTeacherAvailability = true;
        public bool UseDetailedTeacherAvailability
        {
            get { return _useDetailedTeacherAvailability; }
            set { Set(ref _useDetailedTeacherAvailability, value); }
        }

        private bool _useIntensiveRepairSearch = true;
        public bool UseIntensiveRepairSearch
        {
            get { return _useIntensiveRepairSearch; }
            set { Set(ref _useIntensiveRepairSearch, value); }
        }

        private bool _useClassByClassPlacement = true;
        public bool UseClassByClassPlacement
        {
            get { return _useClassByClassPlacement; }
            set { Set(ref _useClassByClassPlacement, value); }
        }

        private bool _useProgressiveImprovement = true;
        public bool UseProgressiveImprovement
        {
            get { return _useProgressiveImprovement; }
            set { Set(ref _useProgressiveImprovement, value); }
        }

        private bool _useParallelSearch = true;
        public bool UseParallelSearch
        {
            get { return _useParallelSearch; }
            set { Set(ref _useParallelSearch, value); }
        }

        private bool _preferMinimumVerbalPerDay;
        public bool PreferMinimumVerbalPerDay
        {
            get { return _preferMinimumVerbalPerDay; }
            set { Set(ref _preferMinimumVerbalPerDay, value); }
        }

        private int _minimumVerbalPerDay = 1;
        public int MinimumVerbalPerDay
        {
            get { return _minimumVerbalPerDay; }
            set
            {
                var normalized = value;
                if (normalized < 0) normalized = 0;
                if (normalized > 8) normalized = 8;
                Set(ref _minimumVerbalPerDay, normalized);
            }
        }

        private bool _preferMinimumNumericPerDay;
        public bool PreferMinimumNumericPerDay
        {
            get { return _preferMinimumNumericPerDay; }
            set { Set(ref _preferMinimumNumericPerDay, value); }
        }

        private int _minimumNumericPerDay = 1;
        public int MinimumNumericPerDay
        {
            get { return _minimumNumericPerDay; }
            set
            {
                var normalized = value;
                if (normalized < 0) normalized = 0;
                if (normalized > 8) normalized = 8;
                Set(ref _minimumNumericPerDay, normalized);
            }
        }

        private int _maxGenerationAttempts = 5000;
        public int MaxGenerationAttempts
        {
            get { return _maxGenerationAttempts; }
            set
            {
                var normalized = value;
                if (normalized < 1) normalized = 1;
                Set(ref _maxGenerationAttempts, normalized);
            }
        }

        private string _relaxationOrder = "TeacherPreferences,ConsecutiveTeacher,BalanceTeacher,SpreadAcrossDays,MaxPerDay,CoursePriority,Blocks,GroupSlotRules,TeacherHalfDay,LunchBreak,TeacherUnavailableDays";
        public string RelaxationOrder
        {
            get { return _relaxationOrder; }
            set { Set(ref _relaxationOrder, value); }
        }

        private GenerationSearchStrategy _searchStrategy = GenerationSearchStrategy.Standart;
        public GenerationSearchStrategy SearchStrategy
        {
            get { return _searchStrategy; }
            set { Set(ref _searchStrategy, value); }
        }

        private bool _useRelaxationOrder = true;
        public bool UseRelaxationOrder
        {
            get { return _useRelaxationOrder; }
            set { Set(ref _useRelaxationOrder, value); }
        }

        // Program üretimi sürerken true olur; diğer sekmelerdeki ekleme/silme komutları
        // bu bayrağı kontrol ederek arka planda okunan koleksiyonların değişmesini engeller.
        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { Set(ref _isBusy, value); }
        }
    }
}
