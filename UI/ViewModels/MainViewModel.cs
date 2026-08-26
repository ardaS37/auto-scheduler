using AutoScheduler.Core.IO;
using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class MainViewModel : BaseViewModel
    {
        private sealed class TrackedCollection
        {
            public INotifyCollectionChanged Source { get; set; }
            public NotifyCollectionChangedEventHandler Handler { get; set; }
        }

        public sealed class RecentProjectItem
        {
            public string Path { get; set; }
            public string DisplayName
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(Path))
                        return "Adsız proje";

                    var file = System.IO.Path.GetFileName(Path);
                    var folder = System.IO.Path.GetDirectoryName(Path);
                    return string.Format("{0} ({1})", file, folder);
                }
            }

            public override string ToString() => DisplayName;
        }

        public ProjectStore Store { get; }
        public ObservableCollection<CourseKind> CourseKinds { get; } =
            new ObservableCollection<CourseKind>((CourseKind[])Enum.GetValues(typeof(CourseKind)));

        // Enum'un altındaki int değerleri (eski JSON dosyalarında SearchStrategy yoksa
        // Standart'a düşülmesi için) sabit kalmalı; burada sadece görüntüleme sırası
        // hız/derinlik mantığına göre elle veriliyor (Hızlı en hızlı, Ayrıntılı en yavaş).
        public ObservableCollection<GenerationSearchStrategy> SearchStrategyChoices { get; } =
            new ObservableCollection<GenerationSearchStrategy>(new[]
            {
                GenerationSearchStrategy.Hizli,
                GenerationSearchStrategy.Standart,
                GenerationSearchStrategy.Yogun,
                GenerationSearchStrategy.Maksimum,
                GenerationSearchStrategy.SonCare
            });

        public ObservableCollection<ProjectHealthItem> ProjectHealthItems { get; } =
            new ObservableCollection<ProjectHealthItem>();

        public ObservableCollection<RecentProjectItem> RecentProjects { get; } =
            new ObservableCollection<RecentProjectItem>();

        private readonly DispatcherTimer _autoSaveTimer;
        private readonly HashSet<INotifyPropertyChanged> _trackedObjects = new HashSet<INotifyPropertyChanged>();
        private readonly List<TrackedCollection> _trackedCollections = new List<TrackedCollection>();
        private UserSessionState _session;
        private bool _suppressDirtyTracking;
        private string _currentProjectPath;

        public bool RandomizeRooms
        {
            get { return Store.RandomizeRooms; }
            set
            {
                Store.RandomizeRooms = value;
                OnPropertyChanged(nameof(RandomizeRooms));
            }
        }

        public bool PreferMorning
        {
            get { return Store.PreferMorning; }
            set
            {
                Store.PreferMorning = value;
                OnPropertyChanged(nameof(PreferMorning));
            }
        }

        public RelayCommand SaveProjectCommand { get; }
        public RelayCommand LoadProjectCommand { get; }
        public RelayCommand CreateRoomsForGroupsCommand { get; }

        private string _status;
        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (Set(ref _isDirty, value))
                    OnPropertyChanged(nameof(WindowTitle));
            }
        }

        private string _readinessLabel = "Hazır değil";
        public string ReadinessLabel
        {
            get => _readinessLabel;
            private set => Set(ref _readinessLabel, value);
        }

        private string _readinessSummary = "Henüz veri girişi yapılmadı.";
        public string ReadinessSummary
        {
            get => _readinessSummary;
            private set => Set(ref _readinessSummary, value);
        }

        private int _readinessScore;
        public int ReadinessScore
        {
            get => _readinessScore;
            private set => Set(ref _readinessScore, value);
        }

        public string WindowTitle
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(Store.ProjectName) ? "Yeni Proje" : Store.ProjectName;
                return IsDirty ? name + " *" : name;
            }
        }

        public string DashboardSummary
        {
            get
            {
                var savedState = string.IsNullOrWhiteSpace(_currentProjectPath) ? "Henüz kaydedilmedi" : Path.GetFileName(_currentProjectPath);
                return string.Format(
                    "{0} sınıf, {1} öğretmen, {2} ders, {3} atama · Birleşik özellik seti · Dosya: {4}",
                    Store.Groups.Count,
                    Store.Teachers.Count,
                    Store.Courses.Count,
                    Store.Assignments.Count,
                    savedState);
            }
        }

        public string LastSavedLocation
        {
            get => string.IsNullOrWhiteSpace(_currentProjectPath) ? "Henüz proje dosyası seçilmedi." : _currentProjectPath;
        }

        public TemplateViewModel TemplateVm { get; }
        public AssignmentsViewModel AssignmentsVm { get; }
        public TeachersViewModel TeachersVm { get; }
        public StudentsViewModel StudentsVm { get; }
        public ImportViewModel ImportVm { get; }
        public SchedulerViewModel SchedulerVm { get; }
        public ExamShuffleViewModel ExamShuffleVm { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set { Set(ref _selectedTabIndex, value); }
        }

        public MainViewModel()
        {
            Store = new ProjectStore();

            TemplateVm = new TemplateViewModel(Store);
            AssignmentsVm = new AssignmentsViewModel(Store);
            TeachersVm = new TeachersViewModel(Store);
            StudentsVm = new StudentsViewModel(Store);
            ImportVm = new ImportViewModel(Store);
            SchedulerVm = new SchedulerViewModel(Store);
            ExamShuffleVm = new ExamShuffleViewModel(Store);
            SchedulerVm.ScheduleChanged += (sender, e) => MarkDirty("Program güncellendi.");

            SaveProjectCommand = new RelayCommand(SaveProject);
            LoadProjectCommand = new RelayCommand(LoadProject);
            CreateRoomsForGroupsCommand = new RelayCommand(CreateRoomsForGroups, () => !Store.IsBusy);
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(45)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();

            AttachAllTracking();
            _session = UserSessionService.Load();
            RefreshRecentProjects();
            RefreshProjectHealth();
            TryRestoreStartupProject();

            SelectedTabIndex = 0;
            if (string.IsNullOrWhiteSpace(Status))
                Status = "Başlamak için kullanım rehberini açabilir veya mevcut bir projeyi yükleyebilirsiniz.";
        }

        public void StartNewProjectWizard(Func<ProjectStore, bool> wizardAction)
        {
            if (wizardAction == null) return;
            if (!EnsureChangesHandled("Yeni proje rehberi açılırken mevcut değişiklikler kaybolabilir.")) return;

            _suppressDirtyTracking = true;
            try
            {
                if (!wizardAction(Store))
                    return;

                TemplateVm.AfterProjectLoaded();
                AssignmentsVm.AfterProjectLoaded();
                TeachersVm.AfterProjectLoaded();
                SchedulerVm.AfterProjectLoaded();
                _currentProjectPath = null;
                IsDirty = true;
                Status = "Yeni proje rehberi uygulandı. Artık öğretmen ve ders girişine geçebilirsiniz.";
                SelectedTabIndex = 1;
            }
            finally
            {
                _suppressDirtyTracking = false;
                RebuildTrackingSubscriptions();
                RefreshProjectHealth();
                OnPropertyChanged(nameof(DashboardSummary));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(LastSavedLocation));
            }
        }

        public void NavigateToTask(ProjectHealthItem item)
        {
            if (item == null) return;
            SelectedTabIndex = item.NavigateTabIndex;
            Status = item.Title + ": " + item.Detail;
        }

        public void OpenRecentProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!File.Exists(path))
            {
                Status = "Son projeler listesindeki dosya bulunamadı.";
                if (_session != null)
                {
                    _session.RecentProjectPaths = _session.RecentProjectPaths.Where(File.Exists).ToList();
                    UserSessionService.Save(_session);
                    RefreshRecentProjects();
                }
                return;
            }

            if (!EnsureChangesHandled("Başka bir proje açarsanız kaydedilmemiş değişiklikler kaybolabilir."))
                return;

            LoadProjectFromPath(path);
        }

        public bool HandleWindowClosing()
        {
            return EnsureChangesHandled("Uygulamayı kapatmadan önce değişiklikleri kaydetmek ister misiniz?");
        }

        private void CreateRoomsForGroups()
        {
            var groups = Store.Groups
                .Where(group => group != null && !string.IsNullOrWhiteSpace(group.Name))
                .ToList();

            if (groups.Count == 0)
            {
                Status = "Önce Şablon sekmesinden en az bir sınıf/şube ekleyin.";
                return;
            }

            var existingNames = new HashSet<string>(
                Store.Rooms
                    .Where(room => room != null && !string.IsNullOrWhiteSpace(room.Name))
                    .Select(room => room.Name.Trim()),
                StringComparer.CurrentCultureIgnoreCase);

            var createdCount = 0;
            foreach (var group in groups)
            {
                var roomName = group.Name.Trim();
                if (existingNames.Add(roomName))
                {
                    Store.Rooms.Add(new Room
                    {
                        Name = roomName,
                        Capacity = Store.DefaultRoomCapacity,
                        Type = "Normal"
                    });
                    createdCount++;
                }

                // Sınıfın salon alanını, oluşturulan/var olan aynı adlı salonla eşleştir.
                group.RoomName = roomName;
            }

            if (createdCount == 0)
            {
                Status = "Tüm sınıf/şubeler için aynı adlı salon zaten var.";
                return;
            }

            MarkDirty(createdCount + " sınıf/şube için salon oluşturuldu ve sınıflara bağlandı.");
        }

        public void OpenProjectHealthTask(ProjectHealthItem item)
        {
            NavigateToTask(item);
        }

        public bool ShouldShowWelcomeTutorial()
        {
            if (_session == null)
                _session = new UserSessionState();

            if (_session.SkipWelcomeTutorial)
                return false;

            return true;
        }

        public void RecordWelcomeTutorialShown(bool dontShowAgain)
        {
            if (_session == null)
                _session = new UserSessionState();

            _session.HasShownWelcomeTutorial = true;
            if (dontShowAgain)
                _session.SkipWelcomeTutorial = true;

            UserSessionService.Save(_session);
        }

        private void TryRestoreStartupProject()
        {
            if (_session == null)
                _session = new UserSessionState();

            if (!string.IsNullOrWhiteSpace(_session.LastProjectPath) && File.Exists(_session.LastProjectPath))
            {
                LoadProjectFromPath(_session.LastProjectPath, true);
                Status = "Son kullanılan proje otomatik açıldı.";
                return;
            }

            if (UserSessionService.TryRestoreAutoBackup(Store))
            {
                TemplateVm.AfterProjectLoaded();
                AssignmentsVm.AfterProjectLoaded();
                TeachersVm.AfterProjectLoaded();
                SchedulerVm.AfterProjectLoaded();
                RebuildTrackingSubscriptions();
                RefreshProjectHealth();
                IsDirty = true;
                Status = "Otomatik yedekten son çalışma geri yüklendi.";
            }
        }

        private void SaveProject()
        {
            SaveProjectInternal(false, false);
        }

        private void LoadProject()
        {
            if (!EnsureChangesHandled("Yeni proje yüklenirse kaydedilmemiş değişiklikler kaybolabilir."))
                return;

            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "AutoScheduler Project (*.autosched.json)|*.autosched.json|JSON (*.json)|*.json",
                    InitialDirectory = ResolveInitialDirectory()
                };

                if (dlg.ShowDialog() != true)
                    return;

                UserSessionService.RememberFolder(_session, Path.GetDirectoryName(dlg.FileName));
                LoadProjectFromPath(dlg.FileName);
            }
            catch (Exception ex)
            {
                Status = "Yükleme hatası: " + ex.Message;
            }
        }

        private bool SaveProjectInternal(bool saveAs, bool silent)
        {
            try
            {
                var targetPath = _currentProjectPath;
                if (saveAs || string.IsNullOrWhiteSpace(targetPath))
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = "AutoScheduler Project (*.autosched.json)|*.autosched.json|JSON (*.json)|*.json",
                        InitialDirectory = ResolveInitialDirectory(),
                        FileName = string.IsNullOrWhiteSpace(Store.ProjectName)
                            ? "project.autosched.json"
                            : Store.ProjectName + ".autosched.json"
                    };

                    if (dlg.ShowDialog() != true)
                        return false;

                    targetPath = dlg.FileName;
                    UserSessionService.RememberFolder(_session, Path.GetDirectoryName(targetPath));
                }

                var validationIssues = ProjectValidationService.Validate(Store);
                if (validationIssues.Count > 0)
                {
                    var message = "Projede kaydetmeden önce düzeltilmesi gereken sorunlar var:\n\n- "
                        + string.Join("\n- ", validationIssues.Take(10));

                    if (validationIssues.Count > 10)
                        message += "\n- ...";

                    if (!silent)
                        MessageBox.Show(message, "Geçersiz Proje", MessageBoxButton.OK, MessageBoxImage.Warning);

                    Status = "Kaydetme iptal edildi: doğrulama hataları var.";
                    return false;
                }

                ProjectSerializer.Save(Store, targetPath);
                _currentProjectPath = targetPath;
                IsDirty = false;
                UserSessionService.RememberProject(_session, targetPath);
                UserSessionService.Save(_session);
                RefreshRecentProjects();
                OnPropertyChanged(nameof(DashboardSummary));
                OnPropertyChanged(nameof(LastSavedLocation));
                Status = silent
                    ? "Değişiklikler kaydedildi."
                    : "Kaydedildi: " + Path.GetFileName(targetPath);
                return true;
            }
            catch (Exception ex)
            {
                Status = "Kaydetme hatası: " + ex.Message;
                return false;
            }
        }

        private void LoadProjectFromPath(string filePath, bool startupLoad = false)
        {
            try
            {
                _suppressDirtyTracking = true;

                ProjectSerializer.Load(Store, filePath);

                TemplateVm.AfterProjectLoaded();
                AssignmentsVm.AfterProjectLoaded();
                TeachersVm.AfterProjectLoaded();
                SchedulerVm.AfterProjectLoaded();

                _currentProjectPath = filePath;
                SelectedTabIndex = 0;
                IsDirty = false;
                UserSessionService.RememberProject(_session, filePath);
                UserSessionService.Save(_session);
                RefreshRecentProjects();
                Status = startupLoad
                    ? "Yüklendi: " + Path.GetFileName(filePath)
                    : "Yüklendi: " + Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                Status = "Yükleme hatası: " + ex.Message;
            }
            finally
            {
                _suppressDirtyTracking = false;
                RebuildTrackingSubscriptions();
                RefreshProjectHealth();
                OnPropertyChanged(nameof(DashboardSummary));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(LastSavedLocation));
            }
        }

        private bool EnsureChangesHandled(string prompt)
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                prompt + "\n\nEvet: Kaydet\nHayır: Kaydetmeden devam et\nİptal: Vazgeç",
                "Kaydedilmemiş Değişiklikler",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
                return SaveProjectInternal(false, false);

            return true;
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (!IsDirty)
                return;

            try
            {
                UserSessionService.SaveAutoBackup(Store);
                if (_session != null)
                    UserSessionService.Save(_session);

                Status = "Otomatik yedek güncellendi.";
            }
            catch
            {
            }
        }

        private string ResolveInitialDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_currentProjectPath))
            {
                var currentDir = Path.GetDirectoryName(_currentProjectPath);
                if (!string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir))
                    return currentDir;
            }

            if (_session != null && !string.IsNullOrWhiteSpace(_session.LastFolderPath) && Directory.Exists(_session.LastFolderPath))
                return _session.LastFolderPath;

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void RefreshRecentProjects()
        {
            RecentProjects.Clear();
            if (_session == null || _session.RecentProjectPaths == null)
                return;

            foreach (var path in _session.RecentProjectPaths.Where(File.Exists))
                RecentProjects.Add(new RecentProjectItem { Path = path });
        }

        public ProjectHealthReport RefreshProjectHealth()
        {
            var report = ProjectHealthService.Analyze(Store);

            ProjectHealthItems.Clear();
            foreach (var item in report.Items.Take(6))
                ProjectHealthItems.Add(item);

            ReadinessLabel = report.ReadinessLabel;
            ReadinessSummary = report.Summary;
            ReadinessScore = report.Score;

            OnPropertyChanged(nameof(DashboardSummary));
            return report;
        }

        private void MarkDirty(string statusMessage)
        {
            if (_suppressDirtyTracking)
                return;

            IsDirty = true;
            if (!string.IsNullOrWhiteSpace(statusMessage))
                Status = statusMessage;

            RefreshProjectHealth();
            OnPropertyChanged(nameof(DashboardSummary));
        }

        private void AttachAllTracking()
        {
            Store.PropertyChanged += Store_PropertyChanged;
            RebuildTrackingSubscriptions();
        }

        private void RebuildTrackingSubscriptions()
        {
            foreach (var trackedObject in _trackedObjects.ToList())
                trackedObject.PropertyChanged -= TrackedObject_PropertyChanged;
            _trackedObjects.Clear();

            foreach (var tracked in _trackedCollections.ToList())
                tracked.Source.CollectionChanged -= tracked.Handler;
            _trackedCollections.Clear();

            AttachCollection(Store.Groups);
            AttachCollection(Store.Teachers);
            AttachCollection(Store.Courses);
            AttachCollection(Store.Rooms);
            AttachCollection(Store.Days);
            AttachCollection(Store.Assignments);
            AttachCollection(Store.GroupSlotRules);
            AttachCollection(Store.CourseKindSlotRules);
            AttachCollection(Store.FixedLessons);

            TrackObject(Store);
            TrackObjects(Store.Groups.Cast<object>());
            TrackObjects(Store.Teachers.Cast<object>());
            TrackObjects(Store.Courses.Cast<object>());
            TrackObjects(Store.Rooms.Cast<object>());
            TrackObjects(Store.Days.Cast<object>());
            TrackObjects(Store.Assignments.Cast<object>());
            TrackObjects(Store.GroupSlotRules.Cast<object>());
            TrackObjects(Store.CourseKindSlotRules.Cast<object>());
            TrackObjects(Store.FixedLessons.Cast<object>());

            foreach (var day in Store.Days)
                AttachChildCollection(day.Slots);

            foreach (var group in Store.Groups)
            {
                AttachChildCollection(group.Students);
                TrackObjects(group.Students.Cast<object>());
            }

            foreach (var teacher in Store.Teachers)
            {
                AttachChildCollection(teacher.CanTeachCourses);
                AttachChildCollection(teacher.PreferredCourseNames);
                AttachChildCollection(teacher.UnwantedCourseNames);
                AttachChildCollection(teacher.UnavailableDayNames);
                AttachChildCollection(teacher.UnavailableSlotKeys);
                AttachChildCollection(teacher.DutyDayNames);
            }
        }

        private void Store_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkDirty("Proje ayarları güncellendi.");
            OnPropertyChanged(nameof(WindowTitle));
        }

        private void AttachCollection(INotifyCollectionChanged collection)
        {
            if (collection == null)
                return;

            NotifyCollectionChangedEventHandler handler = (sender, e) =>
            {
                if (e.NewItems != null)
                    TrackObjects(e.NewItems.Cast<object>());

                MarkDirty("Proje verisi güncellendi.");
                RebuildTrackingSubscriptions();
            };

            collection.CollectionChanged += handler;
            _trackedCollections.Add(new TrackedCollection
            {
                Source = collection,
                Handler = handler
            });
        }

        private void AttachChildCollection(INotifyCollectionChanged collection)
        {
            if (collection == null)
                return;

            NotifyCollectionChangedEventHandler handler = (sender, e) =>
            {
                if (e.NewItems != null)
                    TrackObjects(e.NewItems.Cast<object>());

                MarkDirty("Alt listelerde değişiklik yapıldı.");
            };

            collection.CollectionChanged += handler;
            _trackedCollections.Add(new TrackedCollection
            {
                Source = collection,
                Handler = handler
            });
        }

        private void TrackObjects(IEnumerable objects)
        {
            if (objects == null) return;
            foreach (var item in objects)
                TrackObject(item);
        }

        private void TrackObject(object item)
        {
            var notify = item as INotifyPropertyChanged;
            if (notify == null || _trackedObjects.Contains(notify))
                return;

            notify.PropertyChanged += TrackedObject_PropertyChanged;
            _trackedObjects.Add(notify);
        }

        private void TrackedObject_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkDirty("Veriler güncellendi.");
        }
    }
}
