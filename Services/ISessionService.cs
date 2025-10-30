using LaserCutHMI.Prototype.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Services
{
    public interface ISessionService
    {
        bool IsValid { get; }
        UserRole CurrentRole { get; }
        string CurrentUser { get; }

        
        Task RequestAccessCode(UserRole role);

        bool ValidateSession(string accessCode);

        void Logout();
    }
}
