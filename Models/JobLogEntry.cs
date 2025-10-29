using System;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Models
{
    public class JobLogEntry
    {
        public DateTime When { get; set; }
        public string NcName { get; set; } = "";
        public Material Material { get; set; }
        public Gas Gas { get; set; }
        public int ThicknessMm { get; set; }

        public double DurationSec { get; set; }
        public double CutLengthMm { get; set; }

        // Özet satırı için kısaca:
        public override string ToString()
            => $"{When:yyyy-MM-dd HH:mm} • {NcName} • {Material} {ThicknessMm}mm {Gas} • {CutLengthMm:F0} mm • {DurationSec:F1} sn";
    }
}
