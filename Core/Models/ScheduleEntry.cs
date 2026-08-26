namespace AutoScheduler.Core.Models
{
    public sealed class ScheduleEntry
    {
        public ClassGroup Group { get; set; }
        public Day Day { get; set; }
        public int SlotIndex { get; set; }

        public Course Course { get; set; }
        public Teacher Teacher { get; set; }
        public Room Room { get; set; }

        public int BlockSize { get; set; }
        public int BlockPos { get; set; } // 1..BlockSize (görsel için)
    }
}
