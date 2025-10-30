using LaserCutHMI.Prototype.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Services
{
    public class SessionService : ISessionService
    {
        
        private const string ADMIN_EMAIL = "mertkavaksoc@gmail.com"; 
        private const string SERVIS_EMAIL = "mertkavaksoc@gmail.com"; 

        
        private readonly IEmailService _emailService;
        private readonly IAuditLog _auditLog;
        private readonly Random _random = new Random();

        // Üretilen kodları ve son geçerlilik tarihlerini burada saklayacağız
        // (Kod, (Hangi Rol, Son Geçerlilik Tarihi))
        private readonly Dictionary<string, (UserRole Role, DateTime ExpiresAt)> _activeCodes = new();

        public SessionService(IEmailService emailService, IAuditLog auditLog)
        {
            _emailService = emailService;
            _auditLog = auditLog;
        }

        // Arayüz özellikleri (değişmedi)
        public bool IsValid { get; private set; } = false;
        public UserRole CurrentRole { get; private set; } = UserRole.Operator;
        public string CurrentUser { get; private set; } = "Operator";

        
        public async Task RequestAccessCode(UserRole role)
        {
            // 6 haneli rastgele bir kod üret
            string code = _random.Next(100000, 999999).ToString();

            // Raporunuzdaki 300 saniye (5 dakika) geçerlilik
            var expiresAt = DateTime.UtcNow.AddSeconds(300);

            // Kodu (rolü ve süresiyle) hafızaya kaydet
            _activeCodes[code] = (role, expiresAt);

            string targetEmail = (role == UserRole.Admin) ? ADMIN_EMAIL : SERVIS_EMAIL;
            string subject = $"HMI Erişim Kodu: {code}";
            string body = $"HMI ({role}) rolü için erişim kodunuz: {code}\nBu kod 5 dakika (300 saniye) geçerlidir.";

            try
            {
             
                await _emailService.SendEmailAsync(targetEmail, subject, body);

                _auditLog.Log("INFO", "Session.CodeRequest", $"{role} rolü için kod istendi ve {targetEmail} adresine gönderildi.", "System", UserRole.Operator);
            }
            catch (Exception ex)
            {
                _auditLog.Log("ERROR", "Session.CodeRequest", $"E-posta gönderme hatası: {ex.Message}", "System", UserRole.Operator);
                // Gerçek uygulamada kullanıcıya hata gösterilmelidir
            }
        }

        
        public bool ValidateSession(string accessCode)
        {
           

            if (string.IsNullOrEmpty(accessCode))
                return false;

            // 1. Kod listede var mı?
            if (_activeCodes.TryGetValue(accessCode, out var entry))
            {
                // 2. Kodun süresi dolmuş mu?
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    // Başarılı!
                    IsValid = true;
                    CurrentRole = entry.Role; // Koddaki rolü ata
                    CurrentUser = $"{entry.Role}User";

                    // Anti-Replay (YazStajı2.docx, 27.08.2025):
                    // Kod kullanıldıktan sonra listeden silinir.
                    _activeCodes.Remove(accessCode);

                    return true;
                }
                else
                {
                    // Kodun süresi dolmuşsa listeden sil
                    _activeCodes.Remove(accessCode);
                }
            }

            // Kod listede yoksa veya süresi dolmuşsa
            IsValid = false;
            return false;
        }

        public void Logout()
        {
            IsValid = false;
            CurrentRole = UserRole.Operator;
            CurrentUser = "Operator";
        }
    }
}