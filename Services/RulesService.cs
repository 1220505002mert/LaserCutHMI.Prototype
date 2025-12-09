using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Services
{
    public interface IRulesService
    {
        CuttingRules Rules { get; }
        bool IsAllowed(Material material, Gas gas, int thicknessMm);
        Gas Recommend(Material material, int thicknessMm);
    }

    public class RulesService : IRulesService
    {
        private readonly string _path;
        public CuttingRules Rules { get; private set; } = new();

        public RulesService(string? baseDir = null)
        {
            _path = Path.Combine(baseDir ?? AppContext.BaseDirectory, "rules.json");
            LoadOrCreate();
        }

        private void LoadOrCreate()
        {
            if (File.Exists(_path))
            {
                try
                {
                    var json = File.ReadAllText(_path);
                    var cfg = JsonSerializer.Deserialize<CuttingRules>(json);
                    if (cfg != null && cfg.Materials.Count > 0)
                    {
                        Rules = cfg;
                        return;
                    }
                }
                catch {  }
            }

            Rules = BuildDefault();
            Save();
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(Rules, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_path, json);
        }

        
        private static CuttingRules BuildDefault()
        {
            Dictionary<string, List<ThicknessRange>> MakeGasMap() => new()
            {
                ["Air"] = new() { new ThicknessRange { Min = 1, Max = 5 } },
                ["Oxygen"] = new() { new ThicknessRange { Min = 6, Max = 15 } },
                ["Nitrogen"] = new() { new ThicknessRange { Min = 16, Max = 50 } },
            };

            return new CuttingRules
            {
                Materials = new Dictionary<string, Dictionary<string, List<ThicknessRange>>>
                {
                    ["AlloySteel"] = MakeGasMap(),
                    ["StainlessSteel"] = MakeGasMap(),
                    ["Aluminum"] = MakeGasMap(),
                }
            };
        }

        public bool IsAllowed(Material material, Gas gas, int thicknessMm)
        {
            var m = material.ToString();
            var g = gas.ToString();

            if (!Rules.Materials.TryGetValue(m, out var gasMap))
                return true; 

            if (!gasMap.TryGetValue(g, out var ranges))
                return false;

            return ranges.Any(r => thicknessMm >= r.Min && thicknessMm <= r.Max);
        }

        public Gas Recommend(Material material, int thicknessMm)
        {
            var m = material.ToString();
            if (!Rules.Materials.TryGetValue(m, out var gasMap) || gasMap.Count == 0)
                return Gas.Air;

            foreach (var kv in gasMap)
            {
                var gasName = kv.Key;
                var ranges = kv.Value;
                if (ranges.Any(r => thicknessMm >= r.Min && thicknessMm <= r.Max))
                {
                    if (Enum.TryParse<Gas>(gasName, out var g))
                        return g;
                }
            }
            return Gas.Air;
        }
    }
}
