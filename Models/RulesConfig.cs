using System.Collections.Generic;

namespace LaserCutHMI.Prototype.Models
{
   
    public class ThicknessRange
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public override string ToString() => $"{Min}-{Max} mm";
    }

    public class CuttingRules
    {
        
        public Dictionary<string, Dictionary<string, List<ThicknessRange>>> Materials { get; set; }
            = new();
    }
}
