using AutoScheduler.Core.Models;
using AutoScheduler.Core.Store;
using System.Linq;

namespace AutoScheduler.Core.Services
{
    public static class ProjectCleanupService
    {
        public static void RemoveDay(ProjectStore store, Day day)
        {
            if (store == null || day == null) return;

            foreach (var rule in store.GroupSlotRules.Where(r => r.Day == day).ToList())
                store.GroupSlotRules.Remove(rule);

            foreach (var rule in store.CourseKindSlotRules.Where(r => r.Day == day).ToList())
                store.CourseKindSlotRules.Remove(rule);

            foreach (var lesson in store.FixedLessons.Where(f => f.Day == day).ToList())
                store.FixedLessons.Remove(lesson);

            foreach (var entry in store.Schedule.Where(s => s.Day == day).ToList())
                store.Schedule.Remove(entry);

            foreach (var teacher in store.Teachers)
            {
                while (teacher.UnavailableDayNames.Contains(day.Name))
                    teacher.UnavailableDayNames.Remove(day.Name);

                while (teacher.DutyDayNames.Contains(day.Name))
                    teacher.DutyDayNames.Remove(day.Name);

                foreach (var slotKey in teacher.UnavailableSlotKeys.Where(k => k != null && k.StartsWith(day.Name + "|")).ToList())
                    teacher.UnavailableSlotKeys.Remove(slotKey);
            }

            store.Days.Remove(day);
        }

        public static void RemoveSlot(ProjectStore store, Day day, TimeSlot slot)
        {
            if (store == null || day == null || slot == null) return;

            foreach (var rule in store.GroupSlotRules.Where(r => r.Day == day && r.SlotIndex == slot.Index).ToList())
                store.GroupSlotRules.Remove(rule);

            foreach (var rule in store.CourseKindSlotRules.Where(r => r.Day == day && r.SlotIndex == slot.Index).ToList())
                store.CourseKindSlotRules.Remove(rule);

            foreach (var lesson in store.FixedLessons.Where(f => f.Day == day && f.SlotIndex == slot.Index).ToList())
                store.FixedLessons.Remove(lesson);

            foreach (var entry in store.Schedule.Where(s => s.Day == day && s.SlotIndex == slot.Index).ToList())
                store.Schedule.Remove(entry);

            day.Slots.Remove(slot);

            foreach (var higherSlot in day.Slots.Where(s => s.Index > slot.Index).OrderBy(s => s.Index))
                higherSlot.Index--;

            foreach (var rule in store.GroupSlotRules.Where(r => r.Day == day && r.SlotIndex > slot.Index))
                rule.SlotIndex--;

            foreach (var rule in store.CourseKindSlotRules.Where(r => r.Day == day && r.SlotIndex > slot.Index))
                rule.SlotIndex--;

            foreach (var lesson in store.FixedLessons.Where(f => f.Day == day && f.SlotIndex > slot.Index))
                lesson.SlotIndex--;

            foreach (var entry in store.Schedule.Where(s => s.Day == day && s.SlotIndex > slot.Index))
                entry.SlotIndex--;

            foreach (var teacher in store.Teachers)
            {
                var removedKey = day.Name + "|" + slot.Index;
                while (teacher.UnavailableSlotKeys.Contains(removedKey))
                    teacher.UnavailableSlotKeys.Remove(removedKey);

                foreach (var slotKey in teacher.UnavailableSlotKeys.ToList())
                {
                    var parts = slotKey.Split('|');
                    if (parts.Length != 2 || parts[0] != day.Name) continue;
                    int index;
                    if (!int.TryParse(parts[1], out index)) continue;
                    if (index <= slot.Index) continue;

                    teacher.UnavailableSlotKeys.Remove(slotKey);
                    teacher.UnavailableSlotKeys.Add(day.Name + "|" + (index - 1));
                }
            }
        }

        public static void RemoveGroup(ProjectStore store, ClassGroup group)
        {
            if (store == null || group == null) return;

            foreach (var assignment in store.Assignments.Where(a => a.Group == group).ToList())
                store.Assignments.Remove(assignment);

            foreach (var rule in store.GroupSlotRules.Where(r => r.Group == group).ToList())
                store.GroupSlotRules.Remove(rule);

            foreach (var rule in store.CourseKindSlotRules.Where(r => r.Group == group).ToList())
                store.CourseKindSlotRules.Remove(rule);

            foreach (var lesson in store.FixedLessons.Where(f => f.Group == group).ToList())
                store.FixedLessons.Remove(lesson);

            foreach (var entry in store.Schedule.Where(s => s.Group == group).ToList())
                store.Schedule.Remove(entry);

            store.Groups.Remove(group);
        }

        public static void RemoveTeacher(ProjectStore store, Teacher teacher)
        {
            if (store == null || teacher == null) return;

            foreach (var assignment in store.Assignments.Where(a => a.Teacher == teacher).ToList())
                store.Assignments.Remove(assignment);

            foreach (var lesson in store.FixedLessons.Where(f => f.Teacher == teacher).ToList())
                store.FixedLessons.Remove(lesson);

            foreach (var entry in store.Schedule.Where(s => s.Teacher == teacher).ToList())
                store.Schedule.Remove(entry);

            store.Teachers.Remove(teacher);
        }

        public static void RemoveCourse(ProjectStore store, Course course)
        {
            if (store == null || course == null) return;

            foreach (var assignment in store.Assignments.Where(a => a.Course == course).ToList())
                store.Assignments.Remove(assignment);

            foreach (var lesson in store.FixedLessons.Where(f => f.Course == course).ToList())
                store.FixedLessons.Remove(lesson);

            foreach (var entry in store.Schedule.Where(s => s.Course == course).ToList())
                store.Schedule.Remove(entry);

            foreach (var pair in store.CourseConflictPairs.Where(pair => pair.FirstCourse == course || pair.SecondCourse == course).ToList())
                store.CourseConflictPairs.Remove(pair);

            foreach (var teacher in store.Teachers)
            {
                while (teacher.CanTeachCourses.Contains(course))
                    teacher.CanTeachCourses.Remove(course);

                while (teacher.PreferredCourseNames.Contains(course.Name))
                    teacher.PreferredCourseNames.Remove(course.Name);

                while (teacher.UnwantedCourseNames.Contains(course.Name))
                    teacher.UnwantedCourseNames.Remove(course.Name);
            }

            store.Courses.Remove(course);
        }

        public static void RemoveRoom(ProjectStore store, Room room)
        {
            if (store == null || room == null) return;

            foreach (var assignment in store.Assignments.Where(a => a.Room == room).ToList())
                assignment.Room = null;

            foreach (var lesson in store.FixedLessons.Where(f => f.Room == room).ToList())
                lesson.Room = null;

            foreach (var entry in store.Schedule.Where(s => s.Room == room).ToList())
                entry.Room = null;

            store.Rooms.Remove(room);
        }
    }
}
