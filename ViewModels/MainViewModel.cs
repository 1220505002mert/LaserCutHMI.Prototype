using LaserCutHMI.Prototype.Commands;
using LaserCutHMI.Prototype.Models;
using LaserCutHMI.Prototype.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LaserCutHMI.Prototype.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private const double TickSeconds = 0.2; // 200ms
        private readonly Random _rng = new Random();
        private readonly DispatcherTimer _timer;

        private bool _isRunning;
        private bool _isPaused;
        private bool _simOnly;

        private bool _isSimPressed;
        public bool IsSimPressed
        {
            get => _isSimPressed;
            set => Set(ref _isSimPressed, value);
        }

        private double _vx = 1, _vy = 0;
        private readonly Dictionary<(int, int), int> _visited = new();
        private double _plotX = 0;

        private readonly IParamStore _paramStore;
        private readonly ImportExportService _impex;
        private readonly SystemCheckService _checkService;
        private readonly PreferencesService _prefs;

        private AppPage _selectedPage = AppPage.Giris;
        public AppPage SelectedPage
        {
            get => _selectedPage;
            set => Set(ref _selectedPage, value);
        }

        public SystemChecks Checks { get; } = new SystemChecks();
        public ObservableCollection<EventItem> Events { get; } = new();

        // --- SEÇİMLER ---
        private Material _selectedMaterial = Material.StainlessSteel;
        public Material SelectedMaterial
        {
            get => _selectedMaterial;
            set { if (Set(ref _selectedMaterial, value)) { LoadOrInitParams(); UpdateDerived(); } }
        }

        private Gas _selectedGas = Gas.Nitrogen;
        public Gas SelectedGas
        {
            get => _selectedGas;
            set { if (Set(ref _selectedGas, value)) { LoadOrInitParams(); UpdateDerived(); } }
        }

        private int _selectedThickness = 3;
        public int SelectedThickness
        {
            get => _selectedThickness;
            set { if (Set(ref _selectedThickness, value)) { LoadOrInitParams(); UpdateDerived(); } }
        }

        // --- ComboBox'lar için ItemsSource listeleri (YENİ) ---
        public IReadOnlyList<Material> Materials { get; } =
            Enum.GetValues(typeof(Material)).Cast<Material>().ToList();

        public IReadOnlyList<Gas> Gases { get; } =
            Enum.GetValues(typeof(Gas)).Cast<Gas>().ToList();

        public IReadOnlyList<int> Thicknesses { get; } =
            new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 18, 20, 25, 30, 35, 40, 45, 50 };

        // --- Parametreler ---
        public CutParams CurrentParams { get; } = new CutParams
        {
            PowerW = 3900,
            Frequency = 1000,
            Duty = 100,
            PressureBar = 10,
            CuttingHeightMm = 1
        };

        private double _pressureBar;
        public double PressureBar { get => _pressureBar; set => Set(ref _pressureBar, value); }

        private double _laserPowerLive;
        public double LaserPowerLive { get => _laserPowerLive; set => Set(ref _laserPowerLive, value); }

        private double _xPos;
        public double XPos { get => _xPos; set => Set(ref _xPos, value); }

        private double _yPos;
        public double YPos { get => _yPos; set => Set(ref _yPos, value); }

        private double _zPos = 300;
        public double ZPos { get => _zPos; set => Set(ref _zPos, value); }

        public PointCollection FeedPoints { get; } = new PointCollection();
        public PointCollection PowerPoints { get; } = new PointCollection();

        private double _currentSpeedMmSec;
        public double CurrentSpeedMmSec { get => _currentSpeedMmSec; set => Set(ref _currentSpeedMmSec, value); }

        private double _jobCutMm;
        public double JobCutMm { get => _jobCutMm; set => Set(ref _jobCutMm, value); }

        private double _jobElapsedSec;
        public double JobElapsedSec { get => _jobElapsedSec; set => Set(ref _jobElapsedSec, value); }

        // XAML’de Maximum="{Binding JobTargetMm}" kullanılıyor → ekledik (YENİ)
        private double _jobTargetMm = 2000;
        public double JobTargetMm { get => _jobTargetMm; set => Set(ref _jobTargetMm, value); }

        private int _currentLine;
        public int CurrentLine { get => _currentLine; set => Set(ref _currentLine, value); }

        private string? _ncPath;
        public string? NcPath { get => _ncPath; set => Set(ref _ncPath, value); }

        public ObservableCollection<string> NcLines { get; } = new();

        private bool _doorsOk, _ventOk, _tableOk, _laserOk, _resonatorOnOk, _gasOverallOk, _ruleOk, _ncLoaded;
        public bool DoorsOk { get => _doorsOk; set => Set(ref _doorsOk, value); }
        public bool VentOk { get => _ventOk; set => Set(ref _ventOk, value); }
        public bool TableOk { get => _tableOk; set => Set(ref _tableOk, value); }
        public bool LaserOk { get => _laserOk; set => Set(ref _laserOk, value); }
        public bool ResonatorOnOk { get => _resonatorOnOk; set => Set(ref _resonatorOnOk, value); }
        public bool GasOverallOk { get => _gasOverallOk; set => Set(ref _gasOverallOk, value); }
        public bool RuleOk { get => _ruleOk; set => Set(ref _ruleOk, value); }
        public bool NcLoaded { get => _ncLoaded; set => Set(ref _ncLoaded, value); }

        private string _status = "Idle";
        public string Status { get => _status; set => Set(ref _status, value); }

        public ICommand NavigateCmd { get; }
        public ICommand OpenNcCmd { get; }
        public ICommand SaveParamsCmd { get; }
        public ICommand LoadParamsCmd { get; }
        public ICommand ExportParamsCmd { get; }
        public ICommand CheckAllCmd { get; }
        public ICommand ReplaceGasCmd { get; }
        public ICommand StartCmd { get; }
        public ICommand StartSimCmd { get; }
        public ICommand StopCmd { get; }
        public ICommand ResumeCmd { get; }
        public ICommand ResetCmd { get; }
        public ICommand EStopCmd { get; }

        public MainViewModel()
        {
            _paramStore = new SqliteParamStore();
            _impex = new ImportExportService(_paramStore);
            _checkService = new SystemCheckService();
            _prefs = new PreferencesService();

            SelectedMaterial = Material.StainlessSteel;
            SelectedGas = Gas.Nitrogen;
            SelectedThickness = 3;

            // %90 OK başlangıç
            DoorsOk = _rng.NextDouble() < 0.90;
            VentOk = _rng.NextDouble() < 0.90;
            TableOk = _rng.NextDouble() < 0.90;
            LaserOk = _rng.NextDouble() < 0.90;
            ResonatorOnOk = _rng.NextDouble() < 0.90;

            GasOverallOk = true;
            RuleOk = true;

            Checks.Tanks.Clear();
            Checks.Tanks.Add(new GasTank { Gas = Gas.Air, Connected = true, LevelPercent = 100 });
            Checks.Tanks.Add(new GasTank { Gas = Gas.Oxygen, Connected = true, LevelPercent = 100 });
            Checks.Tanks.Add(new GasTank { Gas = Gas.Nitrogen, Connected = true, LevelPercent = 100 });

            NavigateCmd = new RelayCommand(NavigateTo);
            OpenNcCmd = new RelayCommand(_ => OpenNc());
            SaveParamsCmd = new RelayCommand(_ => SaveParams());
            LoadParamsCmd = new RelayCommand(_ => LoadParamsFromFile());
            ExportParamsCmd = new RelayCommand(_ => ExportParamsToFile());
            CheckAllCmd = new RelayCommand(_ => CheckAll());
            ReplaceGasCmd = new RelayCommand(_ => ReplaceGas());
            StartCmd = new RelayCommand(_ => Start(simOnly: false));
            StartSimCmd = new RelayCommand(_ => Start(simOnly: true));
            StopCmd = new RelayCommand(_ => Stop());
            ResumeCmd = new RelayCommand(_ => Resume());
            ResetCmd = new RelayCommand(_ => ResetAll());
            EStopCmd = new RelayCommand(_ => EmergencyStop());

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TickSeconds) };
            _timer.Tick += Timer_Tick;

            UpdateDerived();
            AddEvent("Checks", "INFO", "Uygulama başlatıldı.");
        }

        private void NavigateTo(object? parameter)
        {
            if (parameter is string s && Enum.TryParse<AppPage>(s, out var page))
                SelectedPage = page;
        }

        private void OpenNc()
        {
            var dlg = new OpenFileDialog
            {
                Title = "NC dosyası seçin",
                Filter = "NC files (*.nc)|*.nc|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) LoadNcFromPath(dlg.FileName);
        }

        public void LoadNcFromPath(string path)
        {
            try
            {
                NcLines.Clear();
                foreach (var line in File.ReadAllLines(path))
                    NcLines.Add(line);
                NcPath = path;
                NcLoaded = NcLines.Count > 0;
                Status = "NC loaded";
                _prefs.LastNcPath = path;
                _prefs.Save();
                AddEvent("NC", "INFO", $"Loaded {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                AddEvent("NC", "ERROR", $"Load failed: {ex.Message}");
            }
        }

        private void SaveParams()
        {
            var p = new CutParams
            {
                PowerW = CurrentParams.PowerW,
                Frequency = CurrentParams.Frequency,
                Duty = CurrentParams.Duty,
                PressureBar = CurrentParams.PressureBar,
                CuttingHeightMm = CurrentParams.CuttingHeightMm
            };
            _paramStore.Save(SelectedMaterial, SelectedGas, SelectedThickness, p);
            AddEvent("Params", "INFO", "Parametreler kaydedildi.");
        }

        private void LoadParamsFromFile()
        {
            try
            {
                _impex.LoadFromFileInteractive();
                AddEvent("Params", "INFO", "Dosyadan parametreler yüklendi.");
                LoadOrInitParams();
            }
            catch (Exception ex)
            {
                AddEvent("Params", "ERROR", $"İçe aktarma hatası: {ex.Message}");
            }
        }

        private void ExportParamsToFile()
        {
            try
            {
                _impex.ExportToFileInteractive();
                AddEvent("Params", "INFO", "Dışa aktarma tamamlandı.");
            }
            catch (Exception ex)
            {
                AddEvent("Params", "ERROR", $"Dışa aktarma hatası: {ex.Message}");
            }
        }

        private void LoadOrInitParams()
        {
            var rec = _paramStore.Get(SelectedMaterial, SelectedGas, SelectedThickness);
            if (rec != null)
            {
                CurrentParams.PowerW = rec.PowerW;
                CurrentParams.Frequency = rec.Frequency;
                CurrentParams.Duty = rec.Duty;
                CurrentParams.PressureBar = rec.PressureBar;
                CurrentParams.CuttingHeightMm = rec.CuttingHeightMm;
            }
            else
            {
                CurrentParams.PowerW = 3900;
                CurrentParams.Frequency = 1000;
                CurrentParams.Duty = 100;
                CurrentParams.PressureBar = 10;
                CurrentParams.CuttingHeightMm = 1;
            }
            OnPropertyChanged(nameof(CurrentParams));
            UpdateDerived();
        }

        public void UpdateDerived()
        {
            // Kural ve gaz uygunluğu
            Gas expected = SelectedThickness <= 5 ? Gas.Air
                            : (SelectedThickness <= 15 ? Gas.Oxygen : Gas.Nitrogen);
            RuleOk = SelectedGas == expected;

            var tank = Checks.Tanks.FirstOrDefault(t => t.Gas == SelectedGas);
            GasOverallOk = tank != null && tank.Connected && tank.LevelPercent > 0;

            if (!_isRunning || _isPaused)
            {
                PressureBar = 0;
                LaserPowerLive = 0;
            }
        }

        private void CheckAll()
        {
            // %95 ihtimalle düzelme (gaz seviyelerini artırmıyoruz)
            DoorsOk = _rng.NextDouble() < 0.95;
            VentOk = _rng.NextDouble() < 0.95;
            TableOk = _rng.NextDouble() < 0.95;
            LaserOk = _rng.NextDouble() < 0.95;
            ResonatorOnOk = _rng.NextDouble() < 0.95;

            AddEvent("Checks", "INFO", "Hataları kontrol et çalıştırıldı (gaz seviyeleri değişmedi).");
            UpdateDerived();
        }

        private void ReplaceGas()
        {
            try
            {
                var dlg = new Views.SelectGasWindow();
                if (dlg.ShowDialog() == true)
                {
                    var gas = dlg.SelectedGas;
                    var tank = Checks.Tanks.FirstOrDefault(t => t.Gas == gas);
                    if (tank != null)
                    {
                        tank.LevelPercent = 100;
                        tank.Connected = true;
                        AddEvent("Gas", "INFO", $"{gas} tüpü %100 olarak değiştirildi.");
                        UpdateDerived();
                    }
                    else
                    {
                        AddEvent("Gas", "WARN", $"{gas} için tüp bulunamadı.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddEvent("Gas", "ERROR", $"Tüp değiştirme hatası: {ex.Message}");
            }
        }

        private void Start(bool simOnly)
        {
            // Simülasyon: sadece LaserOk yeterli. Run: tüm şartlar gerekli.
            if (simOnly)
            {
                if (!LaserOk)
                {
                    AddEvent("Runtime", "WARN", "Simülasyon için Lazer Başlık OK olmalı.");
                    return;
                }
            }
            else
            {
                if (!DoorsOk || !VentOk || !TableOk || !LaserOk || !ResonatorOnOk || !GasOverallOk || !RuleOk || !NcLoaded)
                {
                    AddEvent("Runtime", "WARN", "Ön koşullar sağlanmadı, başlatılamadı.");
                    return;
                }
            }

            _simOnly = simOnly;
            _isPaused = false;
            _isRunning = true;

            // başlangıç konumları
            XPos = 50; YPos = 50; ZPos = 0;
            _visited.Clear();
            _vx = (_rng.NextDouble() * 2 - 1);
            _vy = (_rng.NextDouble() * 2 - 1);

            Status = simOnly ? "Simulating" : "Running";
            IsSimPressed = simOnly;
            AddEvent("Runtime", "INFO", $"Job started: {(NcPath != null ? Path.GetFileName(NcPath) : "(no NC)")} (sim={simOnly})");

            _timer.Start();
        }

        private void Stop()
        {
            if (!_isRunning) return;
            _timer.Stop();
            _isPaused = true;
            Status = "Stopped";
            IsSimPressed = false;
            AddEvent("Stop", "WARN", "Stopped by operator.");
        }

        private void Resume()
        {
            if (_isRunning && _isPaused)
            {
                _isPaused = false;
                Status = _simOnly ? "Simulating" : "Running";
                _timer.Start();
                AddEvent("Runtime", "INFO", "Resumed.");
            }
        }

        private void ResetAll()
        {
            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
            _simOnly = false;
            IsSimPressed = false;

            CurrentLine = 0;
            JobCutMm = 0;
            JobElapsedSec = 0;
            XPos = 0; YPos = 0; ZPos = 300;
            _visited.Clear();
            _plotX = 0;
            FeedPoints.Clear();
            PowerPoints.Clear();

            Status = "Reset";
            AddEvent("Runtime", "INFO", "Reset.");
            UpdateDerived();
        }

        private void EmergencyStop()
        {
            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
            _simOnly = false;
            IsSimPressed = false;

            Status = "E-STOP";
            AddEvent("E-STOP", "ERROR", "ACİL DURDURMA!");
            UpdateDerived();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            // Simülasyonda lazer/başınç 0; hız hesaplanır ve başlık hareket eder
            PressureBar = _simOnly ? 0 : CurrentParams.PressureBar;
            LaserPowerLive = _simOnly ? 0 : Math.Min(4000, CurrentParams.PowerW);

            // Hız (mm/sn) – kalınlığa göre 10..44, gaz katsayıları
            double t = Math.Clamp((SelectedThickness - 1) / 49.0, 0.0, 1.0);
            double baseSpeed = 44 - (34 * t);
            double gasMult = SelectedGas switch { Gas.Oxygen => 1.3, Gas.Nitrogen => 1.56, _ => 1.0 };
            double jitter = 1.0 + ((_rng.NextDouble() * 0.2) - 0.1);
            CurrentSpeedMmSec = baseSpeed * gasMult * jitter;

            // Kesim/süre sadece gerçek üretimde artar
            if (!_simOnly)
            {
                JobCutMm += CurrentSpeedMmSec * TickSeconds;
                JobElapsedSec += TickSeconds;
            }

            // Başlık hareketi
            double step = CurrentSpeedMmSec * TickSeconds;
            _vx += (_rng.NextDouble() - 0.5) * 0.3;
            _vy += (_rng.NextDouble() - 0.5) * 0.3;
            var mag = Math.Sqrt(_vx * _vx + _vy * _vy);
            if (mag < 1e-6) { _vx = 1; _vy = 0; mag = 1; }
            _vx /= mag; _vy /= mag;

            double nx = XPos + _vx * step * 10;
            double ny = YPos + _vy * step * 10;
            if (nx < 0 || nx > 1500) _vx = -_vx;
            if (ny < 0 || ny > 5000) _vy = -_vy;
            nx = Math.Clamp(nx, 0, 1500);
            ny = Math.Clamp(ny, 0, 5000);

            int cx = (int)(nx / 50.0);
            int cy = (int)(ny / 50.0);
            var key = (cx, cy);
            if (_visited.TryGetValue(key, out int count))
            {
                if (count >= 2) { _vx = -_vy; _vy = _vx; }
                else { _visited[key] = count + 1; }
            }
            else
            {
                _visited[key] = 1;
            }
            XPos = nx; YPos = ny;

            // Basit çizimler
            _plotX += 4;
            if (_plotX > 600)
            {
                _plotX = 0;
                FeedPoints.Clear();
                PowerPoints.Clear();
            }
            FeedPoints.Add(new System.Windows.Point(_plotX, 120 - (CurrentSpeedMmSec * 2)));
            PowerPoints.Add(new System.Windows.Point(_plotX, 120 - (LaserPowerLive / 40.0)));
            OnPropertyChanged(nameof(FeedPoints));
            OnPropertyChanged(nameof(PowerPoints));

            // Gaz tüketimi sadece gerçek üretimde
            if (!_simOnly && Checks?.Tanks != null)
            {
                double lps = SelectedGas switch
                {
                    Gas.Oxygen => 3.0 / (20 * 60.0),
                    Gas.Nitrogen => 3.0 / (18 * 60.0),
                    _ => 3.0 / (60 * 60.0),
                };
                // 10x hızlandırılmış tüketim modeli
                double percPerSec = (lps / 50.0) * 100.0 * 10.0;
                var tank = Checks.Tanks.First(tnk => tnk.Gas == SelectedGas);
                if (tank.Connected)
                {
                    tank.LevelPercent = Math.Max(0, tank.LevelPercent - percPerSec * TickSeconds);
                    if (tank.LevelPercent <= 5)
                    {
                        AddEvent("Gas", "WARN", "Gaz seviyesi %5 altına indi. Süreç durduruldu.");
                        Stop();
                        GasOverallOk = false;
                    }
                }
            }

            // NC satır ilerlemesi yalnız üretimde
            if (!_simOnly && NcLines.Count > 0)
                CurrentLine = Math.Min(NcLines.Count, CurrentLine + 1);

            // Ortalama ~50 saniyede bitir
            if (!_simOnly && JobElapsedSec >= 50)
            {
                _timer.Stop();
                _isRunning = false;
                _isPaused = false;
                Status = "Completed";
                AddEvent("Runtime", "INFO", "Job completed.");
            }
        }

        private void AddEvent(string source, string level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Events.Add(new EventItem
                {
                    Timestamp = DateTime.Now,
                    Source = source,
                    Level = level,
                    Message = message
                });
            });
        }
    }
}
