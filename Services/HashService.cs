using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace LaserCutHMI.Prototype.Services
{
    public static class HashService
    {
        
        public static string CalculateSha256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        
        public static string CalculateSha256(string textData)
        {
            var bytes = Encoding.UTF8.GetBytes(textData);
            return CalculateSha256(bytes);
        }

        public static string ChainReportHash(string? previousHash, string metadataHash, string contentHash)
        {
            
            string combined = $"{previousHash ?? "START"}|{metadataHash}|{contentHash}";

            
            return CalculateSha256(combined);
        }
    }
}