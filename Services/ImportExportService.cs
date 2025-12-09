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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LaserCutHMI.Prototype.Services
{
    public class ImportExportService
    {
        private readonly IParamStore _store;
        public ImportExportService(IParamStore store) { _store = store; }

        
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
                    
                    try
                    {
                        var rows = JsonSerializer.Deserialize<List<ParamRow>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        _store.BulkUpsert(rows);
                        count = rows.Count;
                    }
                    catch
                    {
                        
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
                else 
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

       
        public byte[] GenerateAnalysisPdf(List<JobLogEntry> data, DateTime from, DateTime to, double avgDuration, double totalCut)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);

                    
                    page.Header().Text(text =>
                    {
                        text.Span($"Analiz Raporu: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}")
                            .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        
                        col.Item().PaddingBottom(10).Text(text =>
                        {
                            text.Span("Özet İstatistikler").SemiBold().FontSize(16);
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"Toplam Kayıt: {data.Count}");
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"Ortalama Süre: {avgDuration:F1} sn");
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"Toplam Kesim: {totalCut:F0} mm");
                        });

                        // Veri Tablosu
                        col.Item().PaddingTop(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Zaman
                                columns.RelativeColumn(3); // NC Dosyası
                                columns.RelativeColumn(2); // Malzeme
                                columns.RelativeColumn(1); // Süre
                                columns.RelativeColumn(2); // Kesim
                            });

                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten1).Padding(5).Text("Zaman");
                                header.Cell().Background(Colors.Grey.Lighten1).Padding(5).Text("NC Dosyası");
                                header.Cell().Background(Colors.Grey.Lighten1).Padding(5).Text("Reçete");
                                header.Cell().Background(Colors.Grey.Lighten1).Padding(5).Text("Süre (sn)");
                                header.Cell().Background(Colors.Grey.Lighten1).Padding(5).Text("Kesim (mm)");
                            });

                            
                            foreach (var item in data)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.When.ToString("dd.MM HH:mm"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.NcName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{item.Material} {item.ThicknessMm}mm {item.Gas}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.DurationSec.ToString("F1"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.CutLengthMm.ToString("F0"));
                            }
                        });
                    });

                    
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Sayfa ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();
        }
    }
}