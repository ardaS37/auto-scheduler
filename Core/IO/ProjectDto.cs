using System;
using System.Collections.Generic;

namespace AutoScheduler.Core.IO
{
    public sealed class ProjectDto
    {
        public string ProjectName { get; set; }

        // Settings
        public int EducationMode { get; set; } // AutoScheduler.Core.Store.EducationMode as int
        public string LunchBreakStart { get; set; } // "12:00:00"
        public string LunchBreakEnd { get; set; }   // "13:00:00"
        public bool RandomizeRooms { get; set; }
        public int? DefaultRoomCapacity { get; set; }
        public int? DefaultSeatingRows { get; set; }
        public int? DefaultSeatingColumns { get; set; }
        public int? DefaultStudentsPerDesk { get; set; }
        public bool? ExamPreventOwnClassRoom { get; set; }
        public bool? ExamPreventSameGradeNeighbors { get; set; }
        public int? ExamNeighborRuleMode { get; set; }
        public bool PreferMorning { get; set; }
        public bool? RespectTeacherUnavailableDays { get; set; }
        public bool? RespectGroupSlotRules { get; set; }
        public bool? RespectLunchBreak { get; set; }
        public bool? RespectTeacherHalfDay { get; set; }
        public bool? UseDutyDayPriority { get; set; }
        public bool? UseCoursePriorityLevel { get; set; }
        public bool? UseTeacherCoursePreferences { get; set; }
        public bool? UseSpreadAcrossDays { get; set; }
        public bool? UseMaxPerDay { get; set; }
        public bool? UseDetailedTeacherAvailability { get; set; }
        public bool? UseIntensiveRepairSearch { get; set; }
        public bool? UseClassByClassPlacement { get; set; }
        public bool? UseProgressiveImprovement { get; set; }
        public bool? UseParallelSearch { get; set; }
        public bool? PreferMinimumVerbalPerDay { get; set; }
        public int? MinimumVerbalPerDay { get; set; }
        public bool? PreferMinimumNumericPerDay { get; set; }
        public int? MinimumNumericPerDay { get; set; }
        public bool? KeepBlocksStrict { get; set; }
        public bool? DeepSearchEnabled { get; set; }
        public int? MaxGenerationAttempts { get; set; }
        public string RelaxationOrder { get; set; }
        public bool? UseRelaxationOrder { get; set; }
        public string SearchStrategy { get; set; }

        public List<DayDto> Days { get; set; } = new List<DayDto>();
        // Legacy (v1): simple string lists
        [Newtonsoft.Json.JsonProperty("Groups")]
        public List<string> Groups { get; set; } = new List<string>();

        [Newtonsoft.Json.JsonProperty("Courses")]
        public List<string> Courses { get; set; } = new List<string>();

        // v2: richer objects (priority flags)
        [Newtonsoft.Json.JsonProperty("Groups2")]
        public List<GroupDto> Groups2 { get; set; } = new List<GroupDto>();

        [Newtonsoft.Json.JsonProperty("Courses2")]
        public List<CourseDto> Courses2 { get; set; } = new List<CourseDto>();

        public List<RoomDto> Rooms { get; set; } = new List<RoomDto>();
        public List<TeacherDto> Teachers { get; set; } = new List<TeacherDto>();

        public List<AssignmentDto> Assignments { get; set; } = new List<AssignmentDto>();
        public List<CourseConflictPairDto> CourseConflictPairs { get; set; } = new List<CourseConflictPairDto>();
        public List<GroupSlotRuleDto> GroupSlotRules { get; set; } = new List<GroupSlotRuleDto>();
        public List<CourseKindSlotRuleDto> CourseKindSlotRules { get; set; } = new List<CourseKindSlotRuleDto>();
        public List<FixedLessonDto> FixedLessons { get; set; } = new List<FixedLessonDto>();
        public List<ScheduleEntryDto> Schedule { get; set; } = new List<ScheduleEntryDto>();
    }

    public sealed class GroupDto
    {
        public string Name { get; set; }
        public bool IsPriority { get; set; }
        public int Track { get; set; }
        public string GradeLevel { get; set; }
        public string BranchCode { get; set; }
        public string RoomName { get; set; }
        public List<StudentDto> Students { get; set; } = new List<StudentDto>();
        public bool? UseCustomSeatingLayout { get; set; }
        public int? SeatingRows { get; set; }
        public int? SeatingColumns { get; set; }
        public int? StudentsPerDesk { get; set; }
        public bool? IncludeInExamShuffle { get; set; }
    }

    public sealed class StudentDto
    {
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string StudentNumber { get; set; }
    }

    public sealed class CourseDto
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public bool IsPriority { get; set; }
        public int PriorityLevel { get; set; }
        public int Kind { get; set; }
    }

    public sealed class DayDto
    {
        public string Name { get; set; }
        public List<TimeSlotDto> Slots { get; set; } = new List<TimeSlotDto>();
    }

    public sealed class TimeSlotDto
    {
        public int Index { get; set; }
        public string Label { get; set; }

        // TimeSpan JSON’da string olarak saklayalım
        public string Start { get; set; } // "09:00:00"
        public string End { get; set; }   // "10:00:00"
    }

    public sealed class RoomDto
    {
        public string Name { get; set; }
        public int Capacity { get; set; }
        public string Type { get; set; }
    }

    public sealed class TeacherDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string PhotoPath { get; set; }
        public int Title { get; set; } // enum int olarak

        // K12: half-day availability
        public int HalfDayAvailability { get; set; } // AutoScheduler.Core.Models.HalfDayAvailability as int

        public List<string> CanTeachCourses { get; set; } = new List<string>();
        public List<string> PreferredCourses { get; set; } = new List<string>();
        public List<string> UnwantedCourses { get; set; } = new List<string>();

        // Day.Name listesi (hocanın müsait olmadığı günler)
        public List<string> UnavailableDays { get; set; } = new List<string>();
        public List<string> UnavailableSlots { get; set; } = new List<string>();

        // K12: Day.Name listesi (hocanın nöbetçi olduğu günler)
        public List<string> DutyDays { get; set; } = new List<string>();
    }

    public sealed class AssignmentDto
    {
        public string GroupName { get; set; }
        public string CourseName { get; set; }
        public string TeacherName { get; set; }
        public string RoomName { get; set; } // null olabilir

        public int WeeklyHours { get; set; }
        public int BlockSize { get; set; }

        // K12 distribution
        public bool SpreadAcrossDays { get; set; }
        public int MaxPerDay { get; set; }
    }

    public sealed class CourseConflictPairDto
    {
        public string FirstCourseName { get; set; }
        public string SecondCourseName { get; set; }
    }

    public sealed class FixedLessonDto
    {
        public string GroupName { get; set; }
        public string DayName { get; set; }
        public int SlotIndex { get; set; }

        public string CourseName { get; set; }
        public string TeacherName { get; set; }
        public string RoomName { get; set; }

        public int BlockSize { get; set; }
    }

    public sealed class ScheduleEntryDto
    {
        public string GroupName { get; set; }
        public string DayName { get; set; }
        public int SlotIndex { get; set; }

        public string CourseName { get; set; }
        public string TeacherName { get; set; }
        public string RoomName { get; set; }

        public int BlockSize { get; set; }
        public int BlockPos { get; set; }
    }

    public sealed class GroupSlotRuleDto
    {
        public string GroupName { get; set; }
        public string DayName { get; set; }
        public int SlotIndex { get; set; }
        public bool IsAllowed { get; set; }
    }

    public sealed class CourseKindSlotRuleDto
    {
        public string GroupName { get; set; }
        public string DayName { get; set; }
        public int SlotIndex { get; set; }
        public int Kind { get; set; }
    }
}
