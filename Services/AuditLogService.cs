using LaserCutHMI.Prototype.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LaserCutHMI.Prototype.Services
{
    
    public class AuditLogService : IAuditLog
    {
        // Logları uygulamanın çalıştığı dizinde 'audit.log' dosyasına yazacağız
        private readonly string _logFile = Path.Combine(AppContext.BaseDirectory, "audit.log");
        private readonly object _lock = new object(); // Aynı anda yazmayı önlemek için kilit

        public void Log(AuditEntry entry)
        {
            // Dosyaya yazarken kilitle
            lock (_lock)
            {
                string logText = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} " +
                                 $"[{entry.Level}] " +
                                 $"({entry.Source}) " +
                                 $"User: {entry.User} (Role: {entry.Role}) " +
                                 $"- {entry.Message}{Environment.NewLine}";

                File.AppendAllText(_logFile, logText);
            }
        }

        
        public void Log(string level, string source, string message, string user = "System", UserRole role = UserRole.Operator)
        {
            Log(new AuditEntry
            {
                Level = level,
                Source = source,
                Message = message,
                User = user,
                Role = role
                // Timestamp otomatik olarak set edilecek (AuditEntry modelinde)
            });
        }
    }
}
