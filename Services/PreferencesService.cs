using System;
using System.IO;
using System.Text.Json;

namespace LaserCutHMI.Prototype.Services
{
    public interface IPreferencesService
    {
        string? LastNcPath { get; set; }
        void Save();
    }

    public class PreferencesService : IPreferencesService
    {
        private class Prefs { public string? LastNcPath { get; set; } }
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
                }
            }
            catch { _prefs = new Prefs(); }
        }

        public string? LastNcPath
        {
            get => _prefs.LastNcPath;
            set { _prefs.LastNcPath = value; Save(); }
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
