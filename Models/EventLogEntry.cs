using System;

namespace LaserCutHMI.Prototype.Models
{
    public class EventLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO";   // INFO / WARN / ERROR
        public string Source { get; set; } = "";      // Checks, Runtime, Stop, E-Stop, NC, etc.
        public string Message { get; set; } = "";
    }
}
