using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace LaserCutHMI.Prototype.Services
{
    public class EmailService : IEmailService
    {
        
        

        
        private const string SMTP_HOST = "smtp.gmail.com";
        private const int SMTP_PORT = 587; // TLS için

        // E-postayı GÖNDEREN hesap (Yönetici veya sistem hesabı)
        private const string SENDER_EMAIL = "mertkavaksoc@gmail.com";

      
        private const string SENDER_PASSWORD = "txxj xtul griv nbgp"; 

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            using (var client = new SmtpClient(SMTP_HOST, SMTP_PORT))
            {
                client.EnableSsl = true; // TLS/SSL'i etkinleştir 
                client.Credentials = new NetworkCredential(SENDER_EMAIL, SENDER_PASSWORD); // Kimlik doğrulama 

                var message = new MailMessage(SENDER_EMAIL, to, subject, body)
                {
                    IsBodyHtml = false // Düz metin
                };

                // E-postayı asenkron olarak gönder
                await client.SendMailAsync(message);
            }
        }
    }
}
