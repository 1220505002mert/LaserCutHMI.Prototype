using System;

namespace LaserCutHMI.Prototype.Models
{
    public class EventItem
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Source { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
