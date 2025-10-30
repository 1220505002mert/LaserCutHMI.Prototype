using LaserCutHMI.Prototype.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Services
{
    
    public interface IAuditLog
    {
        void Log(AuditEntry entry);

        void Log(string level, string source, string message, string user = "System", UserRole role = UserRole.Operator);
    }
}
