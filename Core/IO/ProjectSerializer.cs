using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace AutoScheduler.Core.IO
{
    public static class ProjectSerializer
    {
        public static void Save(ProjectStore store, string filePath)
        {
            var dto = ToDto(store);

            var json = JsonConvert.SerializeObject(dto, Newtonsoft.Json.Formatting.Indented);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, json);
        }

        public static void Load(ProjectStore store, string filePath)
        {
            var json = File.ReadAllText(filePath);
            var dto = JsonConvert.DeserializeObject<ProjectDto>(json);
            if (dto == null) throw new InvalidOperationException("Dosya boş ya da format geçersiz.");

            ApplyDto(store, dto);
        }

        private static ProjectDto ToDto(ProjectStore store)
        {
            var dto = new ProjectDto
            {
                ProjectName = store.ProjectName,
                EducationMode = (int)store.EducationMode,
                LunchBreakStart = store.LunchBreakStart.ToString(),
                LunchBreakEnd = store.LunchBreakEnd.ToString(),
                RandomizeRooms = store.RandomizeRooms,
                DefaultRoomCapacity = store.DefaultRoomCapacity,
                DefaultSeatingRows = store.DefaultSeatingRows,
                DefaultSeatingColumns = store.DefaultSeatingColumns,
                DefaultStudentsPerDesk = store.DefaultStudentsPerDesk,
                ExamPreventOwnClassRoom = store.ExamPreventOwnClassRoom,
                ExamPreventSameGradeNeighbors = store.ExamPreventSameGradeNeighbors,
                ExamNeighborRuleMode = (int)store.ExamNeighborRuleMode,
                PreferMorning = store.PreferMorning,
                RespectTeacherUnavailableDays = store.RespectTeacherUnavailableDays,
                RespectGroupSlotRules = store.RespectGroupSlotRules,
                RespectLunchBreak = store.RespectLunchBreak,
                RespectTeacherHalfDay = store.RespectTeacherHalfDay,
                UseDutyDayPriority = store.UseDutyDayPriority,
                UseCoursePriorityLevel = store.UseCoursePriorityLevel,
                UseTeacherCoursePreferences = store.UseTeacherCoursePreferences,
                UseSpreadAcrossDays = store.UseSpreadAcrossDays,
                UseMaxPerDay = store.UseMaxPerDay,
                UseDetailedTeacherAvailability = store.UseDetailedTeacherAvailability,
                UseIntensiveRepairSearch = store.UseIntensiveRepairSearch,
                UseClassByClassPlacement = store.UseClassByClassPlacement,
                UseProgressiveImprovement = store.UseProgressiveImprovement,
                UseParallelSearch = store.UseParallelSearch,
                PreferMinimumVerbalPerDay = store.PreferMinimumVerbalPerDay,
                MinimumVerbalPerDay = store.MinimumVerbalPerDay,
                PreferMinimumNumericPerDay = store.PreferMinimumNumericPerDay,
                MinimumNumericPerDay = store.MinimumNumericPerDay,
                KeepBlocksStrict = store.KeepBlocksStrict,
                DeepSearchEnabled = store.DeepSearchEnabled,
                MaxGenerationAttempts = store.MaxGenerationAttempts,
                RelaxationOrder = store.RelaxationOrder,
                UseRelaxationOrder = store.UseRelaxationOrder,
                SearchStrategy = store.SearchStrategy.ToString()
            };

            // Days + Slots
            foreach (var d in store.Days)
            {
                var dayDto = new DayDto { Name = d.Name };
                foreach (var s in d.Slots.OrderBy(x => x.Index))
                {
                    dayDto.Slots.Add(new TimeSlotDto
                    {
                        Index = s.Index,
                        Label = s.Label,
                        Start = s.Start.ToString(), // "hh:mm:ss"
                        End = s.End.ToString()
                    });
                }
                dto.Days.Add(dayDto);
            }

            // v2 lists
            dto.Groups2 = store.Groups.Select(g => new GroupDto
            {
                Name = g.Name,
                IsPriority = g.IsPriority,
                Track = (int)g.Track,
                GradeLevel = g.GradeLevel,
                BranchCode = g.BranchCode,
                RoomName = g.RoomName,
                UseCustomSeatingLayout = g.UseCustomSeatingLayout,
                SeatingRows = g.SeatingRows,
                SeatingColumns = g.SeatingColumns,
                StudentsPerDesk = g.StudentsPerDesk,
                IncludeInExamShuffle = g.IncludeInExamShuffle,
                Students = g.Students.Select(student => new StudentDto
                {
                    FullName = student.FullName,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    StudentNumber = student.StudentNumber
                }).ToList()
            }).ToList();

            dto.Courses2 = store.Courses.Select(c => new CourseDto
            {
                Name = c.Name,
                Code = c.Code,
                IsPriority = c.IsPriority,
                PriorityLevel = c.PriorityLevel,
                Kind = (int)c.Kind
            }).ToList();

            // legacy lists (for older consumers)
            dto.Groups = store.Groups.Select(g => g.Name).ToList();
            dto.Courses = store.Courses.Select(c => c.Name).ToList();

            dto.Rooms = store.Rooms.Select(r => new RoomDto
            {
                Name = r.Name,
                Capacity = r.Capacity,
                Type = r.Type
            }).ToList();

            dto.Teachers = store.Teachers.Select(t => new TeacherDto
            {
                Name = t.Name,
                Phone = t.Phone,
                PhotoPath = t.PhotoPath,
                Title = (int)t.Title,
                HalfDayAvailability = (int)t.HalfDayAvailability,
                CanTeachCourses = t.CanTeachCourses.Select(c => c.Name).ToList(),
                PreferredCourses = t.PreferredCourseNames.Distinct().ToList(),
                UnwantedCourses = t.UnwantedCourseNames.Distinct().ToList(),
                UnavailableDays = t.UnavailableDayNames.Distinct().ToList(),
                UnavailableSlots = t.UnavailableSlotKeys.Distinct().ToList(),
                DutyDays = t.DutyDayNames.Distinct().ToList()
            }).ToList();

            dto.Assignments = store.Assignments.Select(a => new AssignmentDto
            {
                GroupName = a.Group != null ? a.Group.Name : null,
                CourseName = a.Course != null ? a.Course.Name : null,
                TeacherName = a.Teacher != null ? a.Teacher.Name : null,
                RoomName = a.Room != null ? a.Room.Name : null,
                WeeklyHours = a.WeeklyHours,
                BlockSize = a.BlockSize,
                SpreadAcrossDays = a.SpreadAcrossDays,
                MaxPerDay = a.MaxPerDay
            }).ToList();

            dto.CourseConflictPairs = store.CourseConflictPairs
                .Where(pair => pair?.FirstCourse != null && pair.SecondCourse != null && pair.FirstCourse != pair.SecondCourse)
                .Select(pair => new CourseConflictPairDto
                {
                    FirstCourseName = pair.FirstCourse.Name,
                    SecondCourseName = pair.SecondCourse.Name
                })
                .ToList();

            dto.FixedLessons = store.FixedLessons.Select(f => new FixedLessonDto
            {
                GroupName = f.Group != null ? f.Group.Name : null,
                DayName = f.Day != null ? f.Day.Name : null,
                SlotIndex = f.SlotIndex,
                CourseName = f.Course != null ? f.Course.Name : null,
                TeacherName = f.Teacher != null ? f.Teacher.Name : null,
                RoomName = f.Room != null ? f.Room.Name : null,
                BlockSize = f.BlockSize
            }).ToList();

            dto.Schedule = store.Schedule.Select(s => new ScheduleEntryDto
            {
                GroupName = s.Group != null ? s.Group.Name : null,
                DayName = s.Day != null ? s.Day.Name : null,
                SlotIndex = s.SlotIndex,
                CourseName = s.Course != null ? s.Course.Name : null,
                TeacherName = s.Teacher != null ? s.Teacher.Name : null,
                RoomName = s.Room != null ? s.Room.Name : null,
                BlockSize = s.BlockSize,
                BlockPos = s.BlockPos
            }).ToList();

            dto.GroupSlotRules = store.GroupSlotRules.Select(r => new GroupSlotRuleDto
            {
                GroupName = r.Group?.Name,
                DayName = r.Day?.Name,
                SlotIndex = r.SlotIndex,
                IsAllowed = r.IsAllowed
            }).ToList();

            dto.CourseKindSlotRules = store.CourseKindSlotRules.Select(r => new CourseKindSlotRuleDto
            {
                GroupName = r.Group != null ? r.Group.Name : null,
                DayName = r.Day != null ? r.Day.Name : null,
                SlotIndex = r.SlotIndex,
                Kind = (int)r.Kind
            }).ToList();

            return dto;
        }

        private static void ApplyDto(ProjectStore store, ProjectDto dto)
        {
            // önce temizle
            store.Days.Clear();
            store.Groups.Clear();
            store.Courses.Clear();
            store.Rooms.Clear();
            store.Teachers.Clear();
            store.Assignments.Clear();
            store.CourseConflictPairs.Clear();
            store.GroupSlotRules.Clear();
            store.CourseKindSlotRules.Clear();
            store.FixedLessons.Clear();
            store.Schedule.Clear();

            store.ProjectName = dto.ProjectName ?? "Yüklenen Proje";

            // Settings (backward compatible: missing fields => defaults)
            store.EducationMode = (AutoScheduler.Core.Store.EducationMode)(dto.EducationMode);

            if (!string.IsNullOrWhiteSpace(dto.LunchBreakStart) && TimeSpan.TryParse(dto.LunchBreakStart, CultureInfo.InvariantCulture, out var lbStart))
                store.LunchBreakStart = lbStart;

            if (!string.IsNullOrWhiteSpace(dto.LunchBreakEnd) && TimeSpan.TryParse(dto.LunchBreakEnd, CultureInfo.InvariantCulture, out var lbEnd))
                store.LunchBreakEnd = lbEnd;

            store.RandomizeRooms = dto.RandomizeRooms;
            store.DefaultRoomCapacity = dto.DefaultRoomCapacity ?? 30;
            store.DefaultSeatingRows = dto.DefaultSeatingRows ?? 5;
            store.DefaultSeatingColumns = dto.DefaultSeatingColumns ?? 3;
            store.DefaultStudentsPerDesk = dto.DefaultStudentsPerDesk ?? 1;
            store.ExamPreventOwnClassRoom = dto.ExamPreventOwnClassRoom ?? true;
            store.ExamPreventSameGradeNeighbors = dto.ExamPreventSameGradeNeighbors ?? true;
            store.ExamNeighborRuleMode = (ExamNeighborRuleMode)(dto.ExamNeighborRuleMode ?? (int)ExamNeighborRuleMode.YanOnArka);
            store.PreferMorning = dto.PreferMorning;
            store.RespectTeacherUnavailableDays = dto.RespectTeacherUnavailableDays ?? true;
            store.RespectGroupSlotRules = dto.RespectGroupSlotRules ?? true;
            store.RespectLunchBreak = dto.RespectLunchBreak ?? true;
            store.RespectTeacherHalfDay = dto.RespectTeacherHalfDay ?? true;
            store.UseDutyDayPriority = dto.UseDutyDayPriority ?? true;
            store.UseCoursePriorityLevel = dto.UseCoursePriorityLevel ?? true;
            store.UseTeacherCoursePreferences = dto.UseTeacherCoursePreferences ?? true;
            store.UseSpreadAcrossDays = dto.UseSpreadAcrossDays ?? true;
            store.UseMaxPerDay = dto.UseMaxPerDay ?? true;
            store.UseDetailedTeacherAvailability = dto.UseDetailedTeacherAvailability ?? true;
            store.UseIntensiveRepairSearch = dto.UseIntensiveRepairSearch ?? true;
            store.UseClassByClassPlacement = dto.UseClassByClassPlacement ?? true;
            store.UseProgressiveImprovement = dto.UseProgressiveImprovement ?? true;
            store.UseParallelSearch = dto.UseParallelSearch ?? true;
            store.PreferMinimumVerbalPerDay = dto.PreferMinimumVerbalPerDay ?? false;
            store.MinimumVerbalPerDay = dto.MinimumVerbalPerDay ?? 1;
            store.PreferMinimumNumericPerDay = dto.PreferMinimumNumericPerDay ?? false;
            store.MinimumNumericPerDay = dto.MinimumNumericPerDay ?? 1;
            store.KeepBlocksStrict = dto.KeepBlocksStrict ?? true;
            store.DeepSearchEnabled = dto.DeepSearchEnabled ?? true;
            store.MaxGenerationAttempts = dto.MaxGenerationAttempts ?? 5000;
            if (!string.IsNullOrWhiteSpace(dto.RelaxationOrder))
                store.RelaxationOrder = dto.RelaxationOrder;
            store.UseRelaxationOrder = dto.UseRelaxationOrder ?? true;

            GenerationSearchStrategy parsedStrategy;
            store.SearchStrategy = !string.IsNullOrWhiteSpace(dto.SearchStrategy) &&
                Enum.TryParse(dto.SearchStrategy, out parsedStrategy)
                ? parsedStrategy
                : GenerationSearchStrategy.Standart;

            // Days
            foreach (var d in dto.Days)
            {
                var day = new Day { Name = d.Name };
                foreach (var s in d.Slots.OrderBy(x => x.Index))
                {
                    day.Slots.Add(new TimeSlot
                    {
                        Index = s.Index,
                        Label = s.Label,
                        Start = TimeSpan.Parse(s.Start, CultureInfo.InvariantCulture),
                        End = TimeSpan.Parse(s.End, CultureInfo.InvariantCulture)
                    });
                }
                store.Days.Add(day);
            }

            // Groups: prefer v2 (Groups2), fallback to legacy string list (Groups)
            if (dto.Groups2 != null && dto.Groups2.Count > 0)
            {
                foreach (var g in dto.Groups2.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
                {
                    if (store.Groups.Any(gg => gg.Name == g.Name)) continue;
                    var group = new ClassGroup
                    {
                        Name = g.Name,
                        IsPriority = g.IsPriority,
                        Track = (ClassTrack)g.Track,
                        GradeLevel = g.GradeLevel,
                        BranchCode = g.BranchCode,
                        RoomName = g.RoomName,
                        UseCustomSeatingLayout = g.UseCustomSeatingLayout ?? false,
                        SeatingRows = g.SeatingRows ?? 5,
                        SeatingColumns = g.SeatingColumns ?? 3,
                        StudentsPerDesk = g.StudentsPerDesk ?? 1,
                        IncludeInExamShuffle = g.IncludeInExamShuffle ?? true
                    };

                    foreach (var student in (g.Students ?? new List<StudentDto>())
                        .Where(student => student != null && (!string.IsNullOrWhiteSpace(student.FullName) || !string.IsNullOrWhiteSpace(student.FirstName))))
                    {
                        var loadedStudent = new Student
                        {
                            FirstName = student.FirstName,
                            LastName = student.LastName,
                            StudentNumber = student.StudentNumber
                        };
                        if (string.IsNullOrWhiteSpace(loadedStudent.FirstName))
                            loadedStudent.FullName = student.FullName;

                        group.Students.Add(loadedStudent);
                    }

                    store.Groups.Add(group);
                }
            }
            else if (dto.Groups != null)
            {
                foreach (var g in dto.Groups.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                {
                    if (store.Groups.Any(gg => gg.Name == g)) continue;
                    store.Groups.Add(new ClassGroup { Name = g, IsPriority = false });
                }
            }

            // Courses: prefer v2 (Courses2), fallback to legacy string list (Courses)
            if (dto.Courses2 != null && dto.Courses2.Count > 0)
            {
                foreach (var c in dto.Courses2.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
                {
                    if (store.Courses.Any(cc => cc.Name == c.Name)) continue;
                    store.Courses.Add(new Course { Name = c.Name, Code = c.Code, IsPriority = c.IsPriority, PriorityLevel = c.PriorityLevel <= 0 ? 3 : c.PriorityLevel, Kind = (CourseKind)c.Kind });
                }
            }
            else if (dto.Courses != null)
            {
                foreach (var c in dto.Courses.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                {
                    if (store.Courses.Any(cc => cc.Name == c)) continue;
                    store.Courses.Add(new Course { Name = c, IsPriority = false });
                }
            }

            // Rooms
            foreach (var r in dto.Rooms)
                store.Rooms.Add(new Room { Name = r.Name, Capacity = r.Capacity, Type = r.Type });

            // Teachers
            foreach (var t in dto.Teachers)
            {
                var teacher = new Teacher
                {
                    Name = t.Name,
                    Phone = t.Phone,
                    PhotoPath = t.PhotoPath,
                    Title = (AcademicTitle)t.Title,
                    HalfDayAvailability = (AutoScheduler.Core.Models.HalfDayAvailability)t.HalfDayAvailability
                };
                store.Teachers.Add(teacher);
            }

            // hızlı lookup’lar
            var groupByName = store.Groups.ToDictionary(x => x.Name, x => x);
            var courseByName = store.Courses.ToDictionary(x => x.Name, x => x);
            var roomByName = store.Rooms.ToDictionary(x => x.Name, x => x);
            var dayByName = store.Days.ToDictionary(x => x.Name, x => x);
            var teacherByName = store.Teachers.ToDictionary(x => x.Name, x => x);

            // Ders eşleştirmeleri, dersler yüklendikten sonra ad üzerinden yeniden bağlanır.
            if (dto.CourseConflictPairs != null)
            {
                foreach (var pair in dto.CourseConflictPairs)
                {
                    if (pair == null || string.IsNullOrWhiteSpace(pair.FirstCourseName) || string.IsNullOrWhiteSpace(pair.SecondCourseName))
                        continue;
                    if (!courseByName.TryGetValue(pair.FirstCourseName, out var firstCourse) ||
                        !courseByName.TryGetValue(pair.SecondCourseName, out var secondCourse) ||
                        firstCourse == secondCourse)
                        continue;
                    if (store.CourseConflictPairs.Any(existing =>
                        (existing.FirstCourse == firstCourse && existing.SecondCourse == secondCourse) ||
                        (existing.FirstCourse == secondCourse && existing.SecondCourse == firstCourse)))
                        continue;

                    store.CourseConflictPairs.Add(new CourseConflictPair
                    {
                        FirstCourse = firstCourse,
                        SecondCourse = secondCourse
                    });
                }
            }

            // Teacher.CanTeachCourses + UnavailableDays
            foreach (var t in dto.Teachers)
            {
                if (!teacherByName.TryGetValue(t.Name, out var teacher)) continue;

                teacher.CanTeachCourses.Clear();
                foreach (var courseName in t.CanTeachCourses.Distinct())
                {
                    if (courseByName.TryGetValue(courseName, out var course))
                        teacher.CanTeachCourses.Add(course);
                }

                teacher.PreferredCourseNames.Clear();
                if (t.PreferredCourses != null)
                {
                    foreach (var courseName in t.PreferredCourses.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        teacher.PreferredCourseNames.Add(courseName);
                }

                teacher.UnwantedCourseNames.Clear();
                if (t.UnwantedCourses != null)
                {
                    foreach (var courseName in t.UnwantedCourses.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        teacher.UnwantedCourseNames.Add(courseName);
                }

                teacher.UnavailableDayNames.Clear();
                if (t.UnavailableDays != null)
                {
                    foreach (var dayName in t.UnavailableDays.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        teacher.UnavailableDayNames.Add(dayName);
                }

                teacher.UnavailableSlotKeys.Clear();
                if (t.UnavailableSlots != null)
                {
                    foreach (var slotKey in t.UnavailableSlots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        teacher.UnavailableSlotKeys.Add(slotKey);
                }

                teacher.DutyDayNames.Clear();
                if (t.DutyDays != null)
                {
                    foreach (var dayName in t.DutyDays.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        teacher.DutyDayNames.Add(dayName);
                }
            }

            // Assignments
            foreach (var a in dto.Assignments)
            {
                if (a.GroupName == null || a.CourseName == null || a.TeacherName == null)
                    continue;

                if (!groupByName.TryGetValue(a.GroupName, out var g)) continue;
                if (!courseByName.TryGetValue(a.CourseName, out var c)) continue;
                if (!teacherByName.TryGetValue(a.TeacherName, out var t)) continue;

                roomByName.TryGetValue(a.RoomName ?? "", out var r);

                var ass = new Assignment
                {
                    Group = g,
                    Course = c,
                    Teacher = t,
                    Room = r,
                    WeeklyHours = a.WeeklyHours,
                    BlockSize = Math.Max(1, a.BlockSize),
                    SpreadAcrossDays = a.SpreadAcrossDays,
                    MaxPerDay = a.MaxPerDay,
                    TeacherPool = store.Teachers
                };
                store.Assignments.Add(ass);
            }

            // GroupSlotRules
            foreach (var r in dto.GroupSlotRules)
            {
                if (r.GroupName == null || r.DayName == null) continue;
                if (!groupByName.TryGetValue(r.GroupName, out var g)) continue;
                if (!dayByName.TryGetValue(r.DayName, out var d)) continue;

                store.GroupSlotRules.Add(new GroupSlotRule
                {
                    Group = g,
                    Day = d,
                    SlotIndex = r.SlotIndex,
                    IsAllowed = r.IsAllowed
                });
            }

            if (dto.CourseKindSlotRules != null)
            {
                foreach (var r in dto.CourseKindSlotRules)
                {
                    if (r.GroupName == null || r.DayName == null) continue;
                    if (!groupByName.TryGetValue(r.GroupName, out var g)) continue;
                    if (!dayByName.TryGetValue(r.DayName, out var d)) continue;

                    store.CourseKindSlotRules.Add(new CourseKindSlotRule
                    {
                        Group = g,
                        Day = d,
                        SlotIndex = r.SlotIndex,
                        Kind = (CourseKind)r.Kind
                    });
                }
            }

            // FixedLessons
            if (dto.FixedLessons != null)
            {
                foreach (var f in dto.FixedLessons)
                {
                    if (f == null) continue;
                    if (string.IsNullOrWhiteSpace(f.GroupName) || string.IsNullOrWhiteSpace(f.DayName)) continue;
                    if (string.IsNullOrWhiteSpace(f.CourseName) || string.IsNullOrWhiteSpace(f.TeacherName)) continue;

                    if (!groupByName.TryGetValue(f.GroupName, out var g)) continue;
                    if (!dayByName.TryGetValue(f.DayName, out var d)) continue;
                    if (!courseByName.TryGetValue(f.CourseName, out var c)) continue;
                    if (!teacherByName.TryGetValue(f.TeacherName, out var t)) continue;

                    Room room = null;
                    if (!string.IsNullOrWhiteSpace(f.RoomName))
                        roomByName.TryGetValue(f.RoomName, out room);

                    store.FixedLessons.Add(new FixedLesson
                    {
                        Group = g,
                        Day = d,
                        SlotIndex = f.SlotIndex,
                        Course = c,
                        Teacher = t,
                        Room = room,
                        BlockSize = f.BlockSize <= 0 ? 1 : f.BlockSize
                    });
                }
            }

            // Schedule (kaydedilmiş / düzenlenmiş ders programı)
            if (dto.Schedule != null)
            {
                foreach (var s in dto.Schedule)
                {
                    if (s == null) continue;
                    if (string.IsNullOrWhiteSpace(s.GroupName) || string.IsNullOrWhiteSpace(s.DayName)) continue;
                    if (string.IsNullOrWhiteSpace(s.CourseName) || string.IsNullOrWhiteSpace(s.TeacherName)) continue;

                    if (!groupByName.TryGetValue(s.GroupName, out var g)) continue;
                    if (!dayByName.TryGetValue(s.DayName, out var d)) continue;
                    if (!courseByName.TryGetValue(s.CourseName, out var c)) continue;
                    if (!teacherByName.TryGetValue(s.TeacherName, out var t)) continue;

                    Room room = null;
                    if (!string.IsNullOrWhiteSpace(s.RoomName))
                        roomByName.TryGetValue(s.RoomName, out room);

                    store.Schedule.Add(new ScheduleEntry
                    {
                        Group = g,
                        Day = d,
                        SlotIndex = s.SlotIndex,
                        Course = c,
                        Teacher = t,
                        Room = room,
                        BlockSize = s.BlockSize <= 0 ? 1 : s.BlockSize,
                        BlockPos = s.BlockPos <= 0 ? 1 : s.BlockPos
                    });
                }
            }
        }
    }
}
