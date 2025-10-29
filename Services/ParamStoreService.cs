using System.Collections.Generic;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Services
{
    public interface IParamStore
    {
        void EnsureCreated();
        CutParams Get(Material mat, Gas gas, int thicknessMm);
        void Save(Material mat, Gas gas, int thicknessMm, CutParams p);

        // Tümü
        IEnumerable<(Material Mat, Gas Gas, int ThicknessMm, CutParams Params)> GetAll();

        // Toplu ekleme/güncelleme
        void BulkUpsert(IEnumerable<ParamRow> rows);

        // Geriye dönük uyumluluk (ImportExportService SaveMany çağırıyor)
        void SaveMany(IEnumerable<ParamRow> rows);
    }

    // In-memory yedek (esas kalıcı olan SqliteParamStore)
    public class ParamStoreService : IParamStore
    {
        private readonly Dictionary<string, CutParams> _map = new();
        private static string Key(Material m, Gas g, int t) => $"{m}|{g}|{t}";

        public void EnsureCreated() { /* no-op */ }

        public CutParams Get(Material mat, Gas gas, int thicknessMm)
        {
            var key = Key(mat, gas, thicknessMm);
            if (!_map.TryGetValue(key, out var p))
            {
                p = new CutParams(); // default
                _map[key] = p;
            }
            return p;
        }

        public void Save(Material mat, Gas gas, int thicknessMm, CutParams p)
        {
            var key = Key(mat, gas, thicknessMm);
            _map[key] = new CutParams(p.PowerW, p.Frequency, p.Duty, p.PressureBar, p.CuttingHeightMm);
        }

        public IEnumerable<(Material Mat, Gas Gas, int ThicknessMm, CutParams Params)> GetAll()
        {
            foreach (var kv in _map)
            {
                var parts = kv.Key.Split('|');
                if (parts.Length != 3) continue;
                if (!System.Enum.TryParse(parts[0], out Material m)) continue;
                if (!System.Enum.TryParse(parts[1], out Gas g)) continue;
                if (!int.TryParse(parts[2], out int t)) continue;
                yield return (m, g, t, kv.Value);
            }
        }

        public void BulkUpsert(IEnumerable<ParamRow> rows)
        {
            foreach (var r in rows)
                Save(r.Material, r.Gas, r.ThicknessMm,
                    new CutParams(r.PowerW, r.Frequency, r.Duty, r.PressureBar, r.CuttingHeightMm));
        }

        public void SaveMany(IEnumerable<ParamRow> rows) => BulkUpsert(rows);
    }
}
