using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Models
{
    public class ReportHistoryEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ReportType { get; set; } = "";
        public string ReportHash { get; set; } = "";
        public string ContentHash { get; set; } = "";
        public string MetadataHash { get; set; } = "";
        public string? PreviousHash { get; set; }

        // Açılır liste (ComboBox) için 'ToString' metodunu eziyoruz (override)
        public override string ToString()
        {
            return $"{Timestamp:dd.MM.yyyy HH:mm:ss} - {ReportType} Raporu";
        }
    }
}