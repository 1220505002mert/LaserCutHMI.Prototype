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
            return new CutParams(); // yoksa default
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
    }
}
