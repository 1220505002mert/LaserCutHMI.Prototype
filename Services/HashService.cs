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
        // Bir dosyanın (PDF) içeriğini hash'lemek için
        public static string CalculateSha256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                // Hash'i 64 karakterlik bir string'e dönüştür
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // Metin verisini (filtreler) hash'lemek için
        public static string CalculateSha256(string textData)
        {
            var bytes = Encoding.UTF8.GetBytes(textData);
            return CalculateSha256(bytes);
        }

        public static string ChainReportHash(string? previousHash, string metadataHash, string contentHash)
        {
            // Zinciri oluştur: Önceki Hash + Filtre Hash'i + İçerik Hash'i
            string combined = $"{previousHash ?? "START"}|{metadataHash}|{contentHash}";

            // Birleşik verinin hash'ini al
            return CalculateSha256(combined);
        }
    }
}