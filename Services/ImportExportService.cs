using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Services
{
    public class ImportExportService
    {
        private readonly IParamStore _store;
        public ImportExportService(IParamStore store) { _store = store; }

        // ---- Kullanıcıdan dosya seçtirerek içe aktar ----
        public void LoadFromFileInteractive()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Parametre dosyası yükle",
                Filter = "JSON (*.json)|*.json|CSV (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                int count = 0;
                var ext = Path.GetExtension(dlg.FileName)?.ToLowerInvariant();
                if (ext == ".json")
                {
                    var json = File.ReadAllText(dlg.FileName);
                    // Önce ParamRow şemasını deneyelim
                    try
                    {
                        var rows = JsonSerializer.Deserialize<List<ParamRow>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        _store.BulkUpsert(rows);
                        count = rows.Count;
                    }
                    catch
                    {
                        // Eski DTO'yu da destekle
                        var items = JsonSerializer.Deserialize<List<ParamDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        var rows = new List<ParamRow>();
                        foreach (var it in items)
                        {
                            rows.Add(new ParamRow
                            {
                                Material = it.Material,
                                Gas = it.Gas,
                                ThicknessMm = it.Thickness,
                                PowerW = it.PowerW,
                                Frequency = it.Frequency,
                                Duty = it.Duty,
                                PressureBar = it.PressureBar,
                                CuttingHeightMm = it.CuttingHeightMm
                            });
                        }
                        _store.BulkUpsert(rows);
                        count = rows.Count;
                    }
                }
                else // CSV
                {
                    var lines = File.ReadAllLines(dlg.FileName);
                    var rows = ParseCsv(lines);
                    _store.BulkUpsert(rows);
                    count = rows.Count;
                }

                MessageBox.Show($"{count} kayıt yüklendi.", "Yükleme tamam", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Dışa aktar ----
        public void ExportToFileInteractive()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Parametreleri indir",
                Filter = "JSON (*.json)|*.json|CSV (*.csv)|*.csv",
                FileName = "params.json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var all = _store.GetAll().ToList();
                var ext = Path.GetExtension(dlg.FileName)?.ToLowerInvariant();
                if (ext == ".csv")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Material,Gas,ThicknessMm,PowerW,Frequency,Duty,PressureBar,CuttingHeightMm");
                    foreach (var row in all)
                    {
                        sb.AppendLine(string.Join(",",
                            row.Mat, row.Gas, row.ThicknessMm,
                            row.Params.PowerW.ToString(CultureInfo.InvariantCulture),
                            row.Params.Frequency.ToString(CultureInfo.InvariantCulture),
                            row.Params.Duty.ToString(CultureInfo.InvariantCulture),
                            row.Params.PressureBar.ToString(CultureInfo.InvariantCulture),
                            row.Params.CuttingHeightMm.ToString(CultureInfo.InvariantCulture)
                        ));
                    }
                    File.WriteAllText(dlg.FileName, sb.ToString());
                }
                else
                {
                    // JSON
                    var rows = new List<ParamRow>();
                    foreach (var row in all)
                    {
                        rows.Add(new ParamRow
                        {
                            Material = row.Mat,
                            Gas = row.Gas,
                            ThicknessMm = row.ThicknessMm,
                            PowerW = row.Params.PowerW,
                            Frequency = row.Params.Frequency,
                            Duty = row.Params.Duty,
                            PressureBar = row.Params.PressureBar,
                            CuttingHeightMm = row.Params.CuttingHeightMm
                        });
                    }
                    var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                }

                MessageBox.Show("Dışa aktarma tamamlandı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<ParamRow> ParseCsv(IEnumerable<string> lines)
        {
            var rows = new List<ParamRow>();
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var line = raw.Trim();
                if (line.StartsWith("#")) continue;

                var parts = line.Split(new[] { ',', ';' }, StringSplitOptions.None);
                if (parts.Length < 8) continue;

                // Header satırı olursa atla
                if (parts[0].Equals("Material", StringComparison.OrdinalIgnoreCase)) continue;

                var material = Enum.Parse<Material>(parts[0].Trim(), ignoreCase: true);
                var gas = Enum.Parse<Gas>(parts[1].Trim(), ignoreCase: true);
                var thick = int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                var p = new ParamRow
                {
                    Material = material,
                    Gas = gas,
                    ThicknessMm = thick,
                    PowerW = int.Parse(parts[3].Trim(), CultureInfo.InvariantCulture),
                    Frequency = int.Parse(parts[4].Trim(), CultureInfo.InvariantCulture),
                    Duty = int.Parse(parts[5].Trim(), CultureInfo.InvariantCulture),
                    PressureBar = double.Parse(parts[6].Trim(), CultureInfo.InvariantCulture),
                    CuttingHeightMm = double.Parse(parts[7].Trim(), CultureInfo.InvariantCulture)
                };
                rows.Add(p);
            }
            return rows;
        }

        private class ParamDto
        {
            public Material Material { get; set; }
            public Gas Gas { get; set; }
            public int Thickness { get; set; }
            public int PowerW { get; set; }
            public int Frequency { get; set; }
            public int Duty { get; set; }
            public double PressureBar { get; set; }
            public double CuttingHeightMm { get; set; }
        }
    }
}
