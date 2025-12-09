using System;
using System.Collections.Generic;
using System.IO;
using LaserCutHMI.Prototype.Models;
using Microsoft.Data.Sqlite;

namespace LaserCutHMI.Prototype.Services
{
    public class SqliteParamStore : IParamStore
    {
        private readonly string _dbPath;
        private readonly string _connStr;

        public SqliteParamStore()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(root, "LaserCutHMI");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _dbPath = Path.Combine(dir, "params.db");
            _connStr = $"Data Source={_dbPath}";

            // Hata burada oluşuyordu, şimdi düzelttik
            EnsureCreated();
        }

        public void EnsureCreated()
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS CutParam (
                  Material           INTEGER NOT NULL,
                  Gas                INTEGER NOT NULL,
                  ThicknessMm        INTEGER NOT NULL,
                  PowerW             INTEGER NOT NULL,
                  Frequency          INTEGER NOT NULL,
                  Duty               INTEGER NOT NULL,
                  PressureBar        REAL    NOT NULL,
                  CuttingHeightMm    REAL    NOT NULL,
                  PRIMARY KEY(Material, Gas, ThicknessMm)
                );";
            cmd.ExecuteNonQuery();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ReportHistory (
                  Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                  Timestamp        TEXT NOT NULL,
                  ReportType       TEXT NOT NULL,    -- 'Analiz', 'KPI' vb.
                  ReportHash       TEXT NOT NULL,    -- Zincirlenmiş (Chain) Hash
                  ContentHash      TEXT NOT NULL,    -- Sadece PDF içeriğinin hash'i
                  MetadataHash     TEXT NOT NULL,    -- Filtrelerin vb. hash'i
                  PreviousHash     TEXT              -- Bir önceki raporun ReportHash'i
                );";
            cmd.ExecuteNonQuery();

            // YENİ EKLENDİ (ADIM 1)
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS UretimKayitlari (
                  Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                  [When]           TEXT NOT NULL,  -- DÜZELTME (1): 'When' köşeli parantez içine alındı
                  NcName           TEXT,
                  Material         INTEGER NOT NULL,
                  Gas              INTEGER NOT NULL,
                  ThicknessMm      INTEGER NOT NULL,
                  DurationSec      REAL NOT NULL,
                  CutLengthMm      REAL NOT NULL
                );";
            cmd.ExecuteNonQuery(); // Hata bu satırdaydı (line 58)
        }

        public CutParams Get(Material mat, Gas gas, int thicknessMm)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT PowerW, Frequency, Duty, PressureBar, CuttingHeightMm
                FROM CutParam
                WHERE Material = $m AND Gas = $g AND ThicknessMm = $t;";
            cmd.Parameters.AddWithValue("$m", (int)mat);
            cmd.Parameters.AddWithValue("$g", (int)gas);
            cmd.Parameters.AddWithValue("$t", thicknessMm);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new CutParams(
                    r.GetInt32(0),
                    r.GetInt32(1),
                    r.GetInt32(2),
                    r.GetDouble(3),
                    r.GetDouble(4));
            }
            return new CutParams();
        }

        public void Save(Material mat, Gas gas, int thicknessMm, CutParams p)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO CutParam(Material, Gas, ThicknessMm, PowerW, Frequency, Duty, PressureBar, CuttingHeightMm)
                VALUES($m, $g, $t, $pw, $fr, $du, $pr, $h)
                ON CONFLICT(Material, Gas, ThicknessMm) DO UPDATE SET
                    PowerW = excluded.PowerW,
                    Frequency = excluded.Frequency,
                    Duty = excluded.Duty,
                    PressureBar = excluded.PressureBar,
                    CuttingHeightMm = excluded.CuttingHeightMm;";
            cmd.Parameters.AddWithValue("$m", (int)mat);
            cmd.Parameters.AddWithValue("$g", (int)gas);
            cmd.Parameters.AddWithValue("$t", thicknessMm);
            cmd.Parameters.AddWithValue("$pw", p.PowerW);
            cmd.Parameters.AddWithValue("$fr", p.Frequency);
            cmd.Parameters.AddWithValue("$du", p.Duty);
            cmd.Parameters.AddWithValue("$pr", p.PressureBar);
            cmd.Parameters.AddWithValue("$h", p.CuttingHeightMm);
            cmd.ExecuteNonQuery();
        }

        public IEnumerable<(Material Mat, Gas Gas, int ThicknessMm, CutParams Params)> GetAll()
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT Material, Gas, ThicknessMm, PowerW, Frequency, Duty, PressureBar, CuttingHeightMm FROM CutParam;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var mat = (Material)r.GetInt32(0);
                var gas = (Gas)r.GetInt32(1);
                var th = r.GetInt32(2);
                var p = new CutParams(
                    r.GetInt32(3), r.GetInt32(4), r.GetInt32(5),
                    r.GetDouble(6), r.GetDouble(7));
                yield return (mat, gas, th, p);
            }
        }

        public void LogProductionJob(JobLogEntry job)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UretimKayitlari ([When], NcName, Material, Gas, ThicknessMm, DurationSec, CutLengthMm) -- DÜZELTME (2)
                VALUES ($when, $nc, $mat, $gas, $thick, $dur, $len);";
            cmd.Parameters.AddWithValue("$when", job.When.ToString("o"));
            cmd.Parameters.AddWithValue("$nc", job.NcName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$mat", (int)job.Material);
            cmd.Parameters.AddWithValue("$gas", (int)job.Gas);
            cmd.Parameters.AddWithValue("$thick", job.ThicknessMm);
            cmd.Parameters.AddWithValue("$dur", job.DurationSec);
            cmd.Parameters.AddWithValue("$len", job.CutLengthMm);
            cmd.ExecuteNonQuery();
        }

        public List<JobLogEntry> GetProductionHistory(DateTime from, DateTime to)
        {
            var list = new List<JobLogEntry>();
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT [When], NcName, Material, Gas, ThicknessMm, DurationSec, CutLengthMm -- DÜZELTME (3)
                FROM UretimKayitlari
                WHERE [When] >= $from AND [When] <= $to -- DÜZELTME (4)
                ORDER BY [When] DESC;";
            cmd.Parameters.AddWithValue("$from", from.ToString("o"));
            cmd.Parameters.AddWithValue("$to", to.ToString("o"));

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new JobLogEntry
                {
                    When = DateTime.Parse(r.GetString(0)),
                    NcName = r.IsDBNull(1) ? "" : r.GetString(1),
                    Material = (Material)r.GetInt32(2),
                    Gas = (Gas)r.GetInt32(3),
                    ThicknessMm = r.GetInt32(4),
                    DurationSec = r.GetDouble(5),
                    CutLengthMm = r.GetDouble(6)
                });
            }
            return list;
        }


        public void BulkUpsert(IEnumerable<ParamRow> rows)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var tx = conn.BeginTransaction();

            foreach (var r in rows)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO CutParam(Material, Gas, ThicknessMm, PowerW, Frequency, Duty, PressureBar, CuttingHeightMm)
                    VALUES($m, $g, $t, $pw, $fr, $du, $pr, $h)
                    ON CONFLICT(Material, Gas, ThicknessMm) DO UPDATE SET
                        PowerW = excluded.PowerW,
                        Frequency = excluded.Frequency,
                        Duty = excluded.Duty,
                        PressureBar = excluded.PressureBar,
                        CuttingHeightMm = excluded.CuttingHeightMm;";
                cmd.Parameters.AddWithValue("$m", (int)r.Material);
                cmd.Parameters.AddWithValue("$g", (int)r.Gas);
                cmd.Parameters.AddWithValue("$t", r.ThicknessMm);
                cmd.Parameters.AddWithValue("$pw", r.PowerW);
                cmd.Parameters.AddWithValue("$fr", r.Frequency);
                cmd.Parameters.AddWithValue("$du", r.Duty);
                cmd.Parameters.AddWithValue("$pr", r.PressureBar);
                cmd.Parameters.AddWithValue("$h", r.CuttingHeightMm);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public void SaveMany(IEnumerable<ParamRow> rows) => BulkUpsert(rows);

        public string? GetLatestReportHash()
        {
            using var conn = new SqliteConnection(_connStr);
        conn.Open();
            var cmd = conn.CreateCommand();
        cmd.CommandText = @"
                SELECT ReportHash FROM ReportHistory
                ORDER BY Timestamp DESC
                LIMIT 1;";
            
            // ExecuteScalar, tek bir değer döndürür (veya tablo boşsa null)
            return cmd.ExecuteScalar() as string;
        }

        // Yeni oluşturulan raporun hash'ini veritabanına kaydeder
        public void SaveReportHistory(string reportType, string reportHash, string contentHash, string metadataHash, string? previousHash)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ReportHistory (Timestamp, ReportType, ReportHash, ContentHash, MetadataHash, PreviousHash)
                VALUES ($time, $type, $reportHash, $contentHash, $metaHash, $prevHash);";

            cmd.Parameters.AddWithValue("$time", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$type", reportType);
            cmd.Parameters.AddWithValue("$reportHash", reportHash);
            cmd.Parameters.AddWithValue("$contentHash", contentHash);
            cmd.Parameters.AddWithValue("$metaHash", metadataHash);
            cmd.Parameters.AddWithValue("$prevHash", (object)previousHash ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public List<ReportHistoryEntry> GetReportHistoryList()
        {
            var list = new List<ReportHistoryEntry>();
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();

            // DÜZELTME: Sorguya 'ReportHash' eklendi.
            // Böylece doğrulama yaparken listedeki eski raporların hash'ini bilebiliriz.
            cmd.CommandText = @"
        SELECT Id, Timestamp, ReportType, ReportHash 
        FROM ReportHistory
        ORDER BY Timestamp DESC;";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ReportHistoryEntry
                {
                    Id = r.GetInt32(0),
                    Timestamp = DateTime.Parse(r.GetString(1)),
                    ReportType = r.GetString(2),
                    ReportHash = r.GetString(3) // DÜZELTME: Hash değeri okundu
                });
            }
            return list;
        }


        public ReportHistoryEntry? GetReportHistoryEntry(int id)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Timestamp, ReportType, ReportHash, ContentHash, MetadataHash, PreviousHash 
                FROM ReportHistory
                WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new ReportHistoryEntry
                {
                    Id = r.GetInt32(0),
                    Timestamp = DateTime.Parse(r.GetString(1)),
                    ReportType = r.GetString(2),
                    ReportHash = r.GetString(3),
                    ContentHash = r.GetString(4),
                    MetadataHash = r.GetString(5),
                    PreviousHash = r.IsDBNull(6) ? null : r.GetString(6)
                };
            }
            return null; // Bulunamadı
        }
    }
}