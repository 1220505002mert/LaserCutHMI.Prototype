using System.Collections.Generic;

namespace LaserCutHMI.Prototype.Models
{
    public class NcProgram
    {
        public string Path { get; set; } = "";
        public List<string> Lines { get; set; } = new();
    }
}
