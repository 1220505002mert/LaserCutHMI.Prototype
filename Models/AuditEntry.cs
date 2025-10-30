using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Models
{
    
    public class AuditEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO"; // INFO, WARN, ERROR, DENY, OK
        public string Source { get; set; } = ""; // Params.Save, Code.Validate
        public string Message { get; set; } = "";

       
        public string User { get; set; } = "System";
        public UserRole Role { get; set; } = UserRole.Operator;
    }
}
