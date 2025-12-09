using System;
using System.Collections.Generic; // Dictionary için gerekli
using System.IO;
using System.Text.Json;

namespace LaserCutHMI.Prototype.Services
{
    public interface IPreferencesService
    {
        string? LastNcPath { get; set; }
        
        Dictionary<string, double> GasLevels { get; set; }
        void Save();
    }

    public class PreferencesService : IPreferencesService
    {
        private class Prefs
        {
            public string? LastNcPath { get; set; }
            
            public Dictionary<string, double> GasLevels { get; set; } = new();
        }

        private readonly string _dir;
        private readonly string _file;
        private Prefs _prefs = new();

        public PreferencesService()
        {
            _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LaserCutHMI.Prototype");
            _file = Path.Combine(_dir, "userprefs.json");
            try
            {
                if (File.Exists(_file))
                {
                    var json = File.ReadAllText(_file);
                    _prefs = JsonSerializer.Deserialize<Prefs>(json) ?? new Prefs();
                    
                    if (_prefs.GasLevels == null) _prefs.GasLevels = new Dictionary<string, double>();
                }
            }
            catch { _prefs = new Prefs(); }
        }

        public string? LastNcPath
        {
            get => _prefs.LastNcPath;
            set { _prefs.LastNcPath = value; Save(); }
        }

        
        public Dictionary<string, double> GasLevels
        {
            get => _prefs.GasLevels;
            set { _prefs.GasLevels = value; Save(); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var json = JsonSerializer.Serialize(_prefs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_file, json);
            }
            catch { /* sessiz geç */ }
        }
    }
}