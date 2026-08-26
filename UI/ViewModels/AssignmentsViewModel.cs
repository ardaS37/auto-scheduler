using AutoScheduler.Core.Models;
using AutoScheduler.Core.Mvvm;
using AutoScheduler.Core.Services;
using AutoScheduler.Core.Store;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Windows;
using System.Collections.Generic;

namespace AutoScheduler.UI.ViewModels
{
    public sealed class BulkImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ErrorCount => Errors.Count;
        public List<string> Errors { get; } = new List<string>();

        public string Summary
        {
            get
            {
                if (ErrorCount == 0)
                    return string.Format("Toplu giriş tamamlandı. {0} yeni, {1} güncellenen atama var.", AddedCount, UpdatedCount);

                return string.Format("Toplu giriş tamamlandı. {0} yeni, {1} güncellenen atama var. {2} satırda sorun oluştu.", AddedCount, UpdatedCount, ErrorCount);
            }
        }
    }

    public sealed class AssignmentsViewModel : BaseViewModel
    {
        public ProjectStore Store { get; }

        public ObservableCollection<Teacher> Teachers => Store.Teachers;
        public ObservableCollection<Course> Courses => Store.Courses;
        public ObservableCollection<ClassGroup> Groups => Store.Groups;
        public ObservableCollection<Room> Rooms => Store.Rooms;
        public ObservableCollection<CourseKind> CourseKinds { get; } =
            new ObservableCollection<CourseKind>((CourseKind[])Enum.GetValues(typeof(CourseKind)));

        // ✅ Remove butonları anında enable/disable olsun diye RelayCommand tutuyoruz
        public RelayCommand AddTeacherCommand { get; }
        public RelayCommand RemoveTeacherCommand { get; }

        public RelayCommand AddCourseCommand { get; }
        public RelayCommand RemoveCourseCommand { get; }

        public RelayCommand AddRoomCommand { get; }
        public RelayCommand RemoveRoomCommand { get; }

        public RelayCommand AddAssignmentCommand { get; }
        public RelayCommand RemoveAssignmentCommand { get; }

        private Teacher _selectedTeacher;
        public Teacher SelectedTeacher
        {
            get => _selectedTeacher;
            set
            {
                if (Set(ref _selectedTeacher, value))
                    RemoveTeacherCommand.RaiseCanExecuteChanged();
            }
        }

        private Course _selectedCourse;
        public Course SelectedCourse
        {
            get => _selectedCourse;
            set
            {
                if (Set(ref _selectedCourse, value))
                    RemoveCourseCommand.RaiseCanExecuteChanged();
            }
        }

        private Room _selectedRoom;
        public Room SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (Set(ref _selectedRoom, value))
                    RemoveRoomCommand.RaiseCanExecuteChanged();
            }
        }

        private ClassGroup _selectedGroup;
        public ClassGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                {
                    RefreshGroupAssignments();
                    AddAssignmentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private Assignment _selectedAssignment;
        public Assignment SelectedAssignment
        {
            get => _selectedAssignment;
            set
            {
                if (Set(ref _selectedAssignment, value))
                    RemoveAssignmentCommand.RaiseCanExecuteChanged();
            }
        }

        // Seçili sınıfın atamaları (tek koleksiyon)
        private readonly ObservableCollection<Assignment> _groupAssignments =
            new ObservableCollection<Assignment>();

        public ObservableCollection<Assignment> GroupAssignments => _groupAssignments;

        private void RefreshGroupAssignments()
        {
            _groupAssignments.Clear();
            if (SelectedGroup == null) return;

            foreach (var a in Store.Assignments.Where(x => x.Group == SelectedGroup))
                _groupAssignments.Add(a);
        }
        public void AfterProjectLoaded()
        {
            SelectedTeacher = Teachers.FirstOrDefault();
            SelectedCourse = Courses.FirstOrDefault();
            SelectedRoom = Rooms.FirstOrDefault();
            SelectedGroup = Groups.FirstOrDefault();
        }

        public bool IsRoomEditingEnabled => !Store.RandomizeRooms;

        private bool _quickAssignMode;
        public bool QuickAssignMode
        {
            get { return _quickAssignMode; }
            set { Set(ref _quickAssignMode, value); }
        }

        public AssignmentsViewModel(ProjectStore store) //constructor
        {
            Store = store;
            Store.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(IsRoomEditingEnabled));
            };

            AddTeacherCommand = new RelayCommand(AddTeacher, () => !Store.IsBusy);
            RemoveTeacherCommand = new RelayCommand(RemoveTeacher, () => !Store.IsBusy && SelectedTeacher != null);

            AddCourseCommand = new RelayCommand(AddCourse, () => !Store.IsBusy);
            RemoveCourseCommand = new RelayCommand(RemoveCourse, () => !Store.IsBusy && SelectedCourse != null);

            AddRoomCommand = new RelayCommand(AddRoom, () => !Store.IsBusy);
            RemoveRoomCommand = new RelayCommand(RemoveRoom, () => !Store.IsBusy && SelectedRoom != null);

            AddAssignmentCommand = new RelayCommand(AddAssignment, () => !Store.IsBusy && SelectedGroup != null);
            RemoveAssignmentCommand = new RelayCommand(RemoveAssignment, () => !Store.IsBusy && SelectedAssignment != null);
            /*
            // Demo veriler (tek sefer)
            if (Store.Rooms.Count == 0)
            {
                Store.Rooms.Add(new Room { Name = "E-401", Capacity = 30, Type = "Normal" });
                Store.Rooms.Add(new Room { Name = "Lab-Bilgisayar", Capacity = 20, Type = "Lab" });
                Store.Rooms.Add(new Room { Name = "Amfi-1", Capacity = 100, Type = "Amfi" });
            }

            if (Store.Courses.Count == 0)
            {
                Store.Courses.Add(new Course { Name = "Su Getirme" });
                Store.Courses.Add(new Course { Name = "Karayolları" });
                Store.Courses.Add(new Course { Name = "Ulaştırma" });
            }

            if (Store.Teachers.Count == 0)
            {
                Store.Teachers.Add(new Teacher { Name = "Kemal Saplıoğlu" });
                Store.Teachers.Add(new Teacher { Name = "Meltem Saplıoğlu" });
            }
            
            SelectedTeacher = Store.Teachers.FirstOrDefault();
            SelectedCourse = Store.Courses.FirstOrDefault();
            SelectedRoom = Store.Rooms.FirstOrDefault();
            SelectedGroup = Store.Groups.FirstOrDefault();
            */
            RefreshGroupAssignments();

            // İlk açılışta buton durumları doğru olsun
            RemoveTeacherCommand.RaiseCanExecuteChanged();
            RemoveCourseCommand.RaiseCanExecuteChanged();
            RemoveRoomCommand.RaiseCanExecuteChanged();
            AddAssignmentCommand.RaiseCanExecuteChanged();
            RemoveAssignmentCommand.RaiseCanExecuteChanged();
        }

        private void AddTeacher()
        {
            var t = new Teacher { Name = "Yeni Hoca" };
            Store.Teachers.Add(t);
            SelectedTeacher = t;
        }

        private void RemoveTeacher()
        {
            if (SelectedTeacher == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedTeacher.Name}\" silinsin mi? Bu hocaya ait tüm atamalar da silinecek.",
                "Öğretmeni Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveTeacher(Store, SelectedTeacher);
            SelectedTeacher = Store.Teachers.FirstOrDefault();

            RefreshGroupAssignments();
        }

        private void AddCourse()
        {
            var c = new Course { Name = "Yeni Ders" };
            Store.Courses.Add(c);
            SelectedCourse = c;
        }

        private void RemoveCourse()
        {
            if (SelectedCourse == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedCourse.Name}\" silinsin mi? Bu derse ait tüm atamalar da silinecek.",
                "Dersi Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveCourse(Store, SelectedCourse);
            SelectedCourse = Store.Courses.FirstOrDefault();

            RefreshGroupAssignments();
        }

        private void AddRoom()
        {
            var r = new Room { Name = "Yeni Salon", Capacity = 0, Type = "" };
            Store.Rooms.Add(r);
            SelectedRoom = r;
        }

        private void RemoveRoom()
        {
            if (SelectedRoom == null) return;

            var confirm = MessageBox.Show(
                $"\"{SelectedRoom.Name}\" silinsin mi? Bu salona ait atamalardaki salon bilgisi kaldırılacak.",
                "Salonu Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProjectCleanupService.RemoveRoom(Store, SelectedRoom);
            SelectedRoom = Store.Rooms.FirstOrDefault();

            RefreshGroupAssignments();
        }

        private void AddAssignment()
        {
            if (SelectedGroup == null) return;

            var a = CreateAssignment(SelectedGroup, Store.Courses.FirstOrDefault(), null, Store.RandomizeRooms ? null : Store.Rooms.FirstOrDefault(), 1, 1, true, 1);
            Store.Assignments.Add(a);
            SelectedAssignment = a;

            RefreshGroupAssignments();
            RemoveAssignmentCommand.RaiseCanExecuteChanged();
        }

        private void RemoveAssignment()
        {
            if (SelectedAssignment == null) return;

            var confirm = MessageBox.Show(
                "Seçili atama silinsin mi?",
                "Atamayı Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            Store.Assignments.Remove(SelectedAssignment);
            SelectedAssignment = null;

            RefreshGroupAssignments();
            RemoveAssignmentCommand.RaiseCanExecuteChanged();
        }

        public string ApplyBatchAssignment(IEnumerable<ClassGroup> groups, Course course, Teacher teacher, Room room, int weeklyHours, int blockSize, bool spreadAcrossDays, int maxPerDay)
        {
            var selectedGroups = (groups ?? Enumerable.Empty<ClassGroup>())
                .Where(g => g != null)
                .Distinct()
                .ToList();

            if (selectedGroups.Count == 0)
                return "Önce en az bir sınıf seçin.";

            if (course == null)
                return "Toplu atama için bir ders seçin.";

            if (teacher == null)
                return "Toplu atama için bir öğretmen seçin.";

            if (weeklyHours <= 0)
                return "Haftalık saat 0'dan büyük olmalı.";

            if (blockSize <= 0)
                return "Blok en az 1 olmalı.";

            var added = 0;
            var updated = 0;

            foreach (var group in selectedGroups)
            {
                var existing = Store.Assignments.FirstOrDefault(a =>
                    a.Group == group &&
                    a.Course == course);

                if (existing == null)
                {
                    Store.Assignments.Add(CreateAssignment(group, course, teacher, room, weeklyHours, blockSize, spreadAcrossDays, maxPerDay));
                    added++;
                    continue;
                }

                existing.TeacherPool = Store.Teachers;
                existing.Teacher = teacher;
                existing.Room = room;
                existing.WeeklyHours = weeklyHours;
                existing.BlockSize = blockSize;
                existing.SpreadAcrossDays = spreadAcrossDays;
                existing.MaxPerDay = maxPerDay;
                updated++;
            }

            RefreshGroupAssignments();
            return string.Format("{0} yeni atama eklendi, {1} mevcut atama güncellendi.", added, updated);
        }

        public BulkImportResult ImportBulkAssignments(string text, bool autoCreateGroups, bool autoCreateCourses, bool autoCreateTeachers, bool autoCreateRooms)
        {
            var result = new BulkImportResult();
            var lines = (text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count == 0)
            {
                result.Errors.Add("İçe aktarılacak satır bulunamadı.");
                return result;
            }

            foreach (var line in lines)
            {
                var parts = SplitBulkLine(line);
                if (parts.Length < 6)
                {
                    result.Errors.Add("Biçim hatası: " + line);
                    continue;
                }

                var groupName = parts[0].Trim();
                var courseName = parts[1].Trim();
                var teacherName = parts[2].Trim();
                var roomName = parts.Length >= 7 ? parts[6].Trim() : string.Empty;

                int weeklyHours;
                int blockSize;
                int maxPerDay;

                if (!int.TryParse(parts[3].Trim(), out weeklyHours) || weeklyHours <= 0)
                {
                    result.Errors.Add("Haftalık saat okunamadı: " + line);
                    continue;
                }

                if (!int.TryParse(parts[4].Trim(), out blockSize) || blockSize <= 0)
                {
                    result.Errors.Add("Blok okunamadı: " + line);
                    continue;
                }

                if (!int.TryParse(parts[5].Trim(), out maxPerDay) || maxPerDay < 0)
                {
                    result.Errors.Add("Günlük max okunamadı: " + line);
                    continue;
                }

                var group = Store.Groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.CurrentCultureIgnoreCase));
                var course = Store.Courses.FirstOrDefault(c => string.Equals(c.Name, courseName, StringComparison.CurrentCultureIgnoreCase));
                var teacher = Store.Teachers.FirstOrDefault(t => string.Equals(t.Name, teacherName, StringComparison.CurrentCultureIgnoreCase));
                var room = string.IsNullOrWhiteSpace(roomName)
                    ? null
                    : Store.Rooms.FirstOrDefault(r => string.Equals(r.Name, roomName, StringComparison.CurrentCultureIgnoreCase));

                if (group == null)
                {
                    if (!autoCreateGroups)
                    {
                        result.Errors.Add("Sınıf bulunamadı: " + groupName);
                        continue;
                    }

                    group = new ClassGroup { Name = groupName };
                    Store.Groups.Add(group);
                }

                if (course == null)
                {
                    if (!autoCreateCourses)
                    {
                        result.Errors.Add("Ders bulunamadı: " + courseName);
                        continue;
                    }

                    course = new Course { Name = courseName };
                    Store.Courses.Add(course);
                }

                if (teacher == null)
                {
                    if (!autoCreateTeachers)
                    {
                        result.Errors.Add("Öğretmen bulunamadı: " + teacherName);
                        continue;
                    }

                    teacher = new Teacher { Name = teacherName };
                    Store.Teachers.Add(teacher);
                }

                if (!teacher.CanTeachCourses.Contains(course))
                    teacher.CanTeachCourses.Add(course);

                if (!string.IsNullOrWhiteSpace(roomName) && room == null)
                {
                    if (!autoCreateRooms)
                    {
                        result.Errors.Add("Salon bulunamadı: " + roomName);
                        continue;
                    }

                    room = new Room { Name = roomName };
                    Store.Rooms.Add(room);
                }

                var existing = Store.Assignments.FirstOrDefault(a => a.Group == group && a.Course == course);
                if (existing == null)
                {
                    Store.Assignments.Add(CreateAssignment(group, course, teacher, room, weeklyHours, blockSize, true, maxPerDay));
                    result.AddedCount++;
                }
                else
                {
                    existing.TeacherPool = Store.Teachers;
                    existing.Teacher = teacher;
                    existing.Room = room;
                    existing.WeeklyHours = weeklyHours;
                    existing.BlockSize = blockSize;
                    existing.SpreadAcrossDays = true;
                    existing.MaxPerDay = maxPerDay;
                    result.UpdatedCount++;
                }
            }

            RefreshGroupAssignments();
            return result;
        }

        private Assignment CreateAssignment(ClassGroup group, Course course, Teacher teacher, Room room, int weeklyHours, int blockSize, bool spreadAcrossDays, int maxPerDay)
        {
            var assignment = new Assignment
            {
                Group = group,
                WeeklyHours = weeklyHours,
                BlockSize = blockSize,
                Room = room,
                TeacherPool = Store.Teachers,
                SpreadAcrossDays = spreadAcrossDays,
                MaxPerDay = maxPerDay
            };

            assignment.Course = course;
            if (teacher != null)
                assignment.Teacher = teacher;

            return assignment;
        }

        private static string[] SplitBulkLine(string line)
        {
            if (line.IndexOf(';') >= 0)
                return line.Split(';');

            if (line.IndexOf('\t') >= 0)
                return line.Split('\t');

            if (line.IndexOf(',') >= 0)
                return line.Split(',');

            return new[] { line };
        }
    }
}
