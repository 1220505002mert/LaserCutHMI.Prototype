using LaserCutHMI.Prototype.ViewModels;
using LaserCutHMI.Prototype.Models;
using LaserCutHMI.Prototype.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Measure;

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

        private readonly IAuditLog _auditLog;
        private readonly IEmailService _emailService;
        private readonly ISessionService _sessionService;

        private readonly Debouncer _analizDebouncer = new Debouncer(TimeSpan.FromMilliseconds(500));

        private readonly ICache _cache = new MemoryCacheLite();

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

        // --- ComboBox'lar için ItemsSource listeleri  ---
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

        private double _headTemp = 246.0; // Lazer Başlık Sıcaklığı
        public double HeadTemp { get => _headTemp; set => Set(ref _headTemp, value); }

        private double _insideTemp = 39.0; // Makine İçi Sıcaklık
        public double InsideTemp { get => _insideTemp; set => Set(ref _insideTemp, value); }

        private double _outsideTemp = 21.0; // Dış Ortam Sıcaklığı
        public double OutsideTemp { get => _outsideTemp; set => Set(ref _outsideTemp, value); }

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

        private double _jobTargetMm = 2000;
        public double JobTargetMm { get => _jobTargetMm; set => Set(ref _jobTargetMm, value); }

        private int _currentLine;
        public int CurrentLine { get => _currentLine; set => Set(ref _currentLine, value); }

        private string? _ncPath;
        public string? NcPath { get => _ncPath; set => Set(ref _ncPath, value); }

        public ObservableCollection<string> NcLines { get; } = new();

        
        public DateTime OzetBaslangic { get; set; } = DateTime.Now.AddDays(-7);
        public DateTime OzetBitis { get; set; } = DateTime.Now;
        private double _ozetToplamKesim;
        public double OzetToplamKesim { get => _ozetToplamKesim; set => Set(ref _ozetToplamKesim, value); }
        private double _ozetOrtalamaSure;
        public double OzetOrtalamaSure { get => _ozetOrtalamaSure; set => Set(ref _ozetOrtalamaSure, value); }
        public ObservableCollection<JobLogEntry> OzetUretimKayitlari { get; } = new();


        private DateTime _analizBaslangic = DateTime.Now.AddDays(-30);
        public DateTime AnalizBaslangic
        {
            get => _analizBaslangic;
            set { if (Set(ref _analizBaslangic, value)) LoadAnalizData(true); } // Değiştiğinde sorguyu tetikle
        }

        private DateTime _analizBitis = DateTime.Now;
        public DateTime AnalizBitis
        {
            get => _analizBitis;
            set { if (Set(ref _analizBitis, value)) LoadAnalizData(true); } 
        }

        private DateTime CombineDateAndTime(DateTime date, string timeStr)
        {
            if (TimeSpan.TryParse(timeStr, out var ts))
            {
                return date.Date + ts;
            }
            return date.Date; // Hatalı format girilirse sadece tarihi (00:00) al
        }

        private string _ozetBaslangicSaat = "00:00";
        public string OzetBaslangicSaat { get => _ozetBaslangicSaat; set => Set(ref _ozetBaslangicSaat, value); }

        private string _ozetBitisSaat = "23:59";
        public string OzetBitisSaat { get => _ozetBitisSaat; set => Set(ref _ozetBitisSaat, value); }

        private string _analizBaslangicSaat = "00:00";
        public string AnalizBaslangicSaat { get => _analizBaslangicSaat; set => Set(ref _analizBaslangicSaat, value); }

        private string _analizBitisSaat = "23:59";
        public string AnalizBitisSaat { get => _analizBitisSaat; set => Set(ref _analizBitisSaat, value); }


        private double _analizToplamKesim;
        public double AnalizToplamKesim { get => _analizToplamKesim; set => Set(ref _analizToplamKesim, value); }
        private double _analizOrtalamaSure;
        public double AnalizOrtalamaSure { get => _analizOrtalamaSure; set => Set(ref _analizOrtalamaSure, value); }
        private int _analizToplamIs;
        public int AnalizToplamIs { get => _analizToplamIs; set => Set(ref _analizToplamIs, value); }

        public ObservableCollection<ISeries> AnalizZamanSerisi { get; set; } = new();
        public ObservableCollection<ISeries> AnalizMalzemeKirilim { get; set; } = new();
        public Axis[] AnalizXAxis { get; set; } = { new Axis { Labeler = value => new DateTime((long)value).ToString("dd MMM") } };

        private double _kpiMtbf;
        public double KpiMtbf { get => _kpiMtbf; set => Set(ref _kpiMtbf, value); } // Arızalar Arası Ortalama Süre (saat)
        private double _kpiMttr;
        public double KpiMttr { get => _kpiMttr; set => Set(ref _kpiMttr, value); } // Ortalama Onarım Süresi (saat)
        private double _kpiAvailability;
        public double KpiAvailability { get => _kpiAvailability; set => Set(ref _kpiAvailability, value); } // Kullanılabilirlik (%)

        
        public ObservableCollection<ReportHistoryEntry> VerifyReportList { get; } = new();

        
        private ReportHistoryEntry? _verifySelectedReport;
        public ReportHistoryEntry? VerifySelectedReport { get => _verifySelectedReport; set => Set(ref _verifySelectedReport, value); }

        
        private string _verifyResultText = "Lütfen doğrulamak için bir PDF rapor dosyası seçin.";
        public string VerifyResultText { get => _verifyResultText; set => Set(ref _verifyResultText, value); }

        private bool? _verifyHashUyumlu;
        public bool? VerifyHashUyumlu { get => _verifyHashUyumlu; set => Set(ref _verifyHashUyumlu, value); } 

        private bool? _verifyZincirTutarlı;
        public bool? VerifyZincirTutarlı { get => _verifyZincirTutarlı; set => Set(ref _verifyZincirTutarlı, value); }

        private bool _isFlyoutOpen;
        public bool IsFlyoutOpen { get => _isFlyoutOpen; set => Set(ref _isFlyoutOpen, value); }

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

        public ICommand LoadOzetDataCmd { get; }

        public ICommand ValidateCodeCmd { get; }
        public ICommand LogoutCmd { get; }

        private string _sessionStatusText = "Durum: Oturum kapalı. (Varsayılan: Operator)";
        public string SessionStatusText { get => _sessionStatusText; set => Set(ref _sessionStatusText, value); }

        public string AccessCode { get; set; } = string.Empty;

        public ICommand LoadAnalizDataCmd { get; }
        public ICommand ExportAnalizPdfCmd { get; }

        public ICommand LoadKpiDataCmd { get; }

        public ICommand LoadVerifyReportListCmd { get; }
        public ICommand VerifySelectedFileCmd { get; }

        public ICommand ToggleFlyoutCmd { get; }

        public ICommand RequestAdminCodeCmd { get; }
        public ICommand RequestServisCodeCmd { get; }

        public MainViewModel()
        {
            _paramStore = new SqliteParamStore();
            _impex = new ImportExportService(_paramStore);
            _checkService = new SystemCheckService();
            _prefs = new PreferencesService();

            _auditLog = new AuditLogService();
            _emailService = new EmailService(); 
            _sessionService = new SessionService(_emailService, _auditLog);

            SelectedMaterial = Material.StainlessSteel;
            SelectedGas = Gas.Nitrogen;
            SelectedThickness = 3;

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

            if (_prefs.GasLevels != null)
            {
                foreach (var tank in Checks.Tanks)
                {
                    string key = tank.Gas.ToString();
                    if (_prefs.GasLevels.ContainsKey(key))
                    {
                        tank.LevelPercent = _prefs.GasLevels[key];
                    }
                    else
                    {
                        
                        _prefs.GasLevels[key] = tank.LevelPercent;
                    }
                }
            }

            NavigateCmd = new RelayCommand(NavigateTo);
            OpenNcCmd = new RelayCommand(_ => OpenNc());

            SaveParamsCmd = new RelayCommand(_ => SaveParams(), _ => CanEditParameters());

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

            LoadOzetDataCmd = new RelayCommand(_ => LoadOzetData());

            ValidateCodeCmd = new RelayCommand(_ => ValidateCode());
            LogoutCmd = new RelayCommand(_ => DoLogout());

            LoadAnalizDataCmd = new RelayCommand(_ => LoadAnalizData());
            ExportAnalizPdfCmd = new RelayCommand(_ => ExportAnalizToPdf());

            LoadKpiDataCmd = new RelayCommand(_ => LoadKpiData());

            LoadVerifyReportListCmd = new RelayCommand(_ => LoadVerifyReportList());
            VerifySelectedFileCmd = new RelayCommand(_ => VerifySelectedFile());

            ToggleFlyoutCmd = new RelayCommand(_ => IsFlyoutOpen = !IsFlyoutOpen); 

            RequestAdminCodeCmd = new RelayCommand(_ => RequestCode(UserRole.Admin));
            RequestServisCodeCmd = new RelayCommand(_ => RequestCode(UserRole.Servis));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TickSeconds) };
            _timer.Tick += Timer_Tick;

            UpdateDerived();
            AddEvent("Checks", "INFO", "Uygulama başlatıldı.");

            _auditLog.Log("INFO", "App.Start", "Uygulama başlatıldı.");

            LoadAnalizData();
        }

        private bool CanEditParameters()
        {
            return _sessionService.IsValid &&
                   (_sessionService.CurrentRole == UserRole.Admin ||
                    _sessionService.CurrentRole == UserRole.Servis);
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
            if (!CanEditParameters())
            {
                _auditLog.Log(
                    level: "DENY",
                    source: "Params.Save",
                    message: "Yetkisiz parametre kaydetme denemesi.",
                    user: _sessionService.CurrentUser,
                    role: _sessionService.CurrentRole
                );

                AddEvent("Params", "ERROR", "Parametre kaydetme yetkiniz yok. Lütfen 'Yetki' sayfasından giriş yapın.");
                return;
            }

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

            _auditLog.Log(
                level: "OK",
                source: "Params.Save",
                message: $"Parametre kaydedildi: {SelectedMaterial}-{SelectedThickness}mm-{SelectedGas}",
                user: _sessionService.CurrentUser,
                role: _sessionService.CurrentRole
            );
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

                        if (_prefs.GasLevels != null)
                        {
                            _prefs.GasLevels[tank.Gas.ToString()] = 100.0;
                            _prefs.Save();
                        }

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

            XPos = 50; YPos = 50; ZPos = 0;
            _visited.Clear();
            _vx = (_rng.NextDouble() * 2 - 1);
            _vy = (_rng.NextDouble() * 2 - 1);

            Status = simOnly ? "Simulating" : "Running";
            IsSimPressed = simOnly;
            AddEvent("Runtime", "INFO", $"Job started: {(NcPath != null ? Path.GetFileName(NcPath) : "(no NC)")} (sim={simOnly})");

            _auditLog.Log("INFO", "Job.Start", $"İş başlatıldı (Sim={simOnly}). NC: {NcPath ?? "N/A"}", _sessionService.CurrentUser, _sessionService.CurrentRole);

            _timer.Start();
        }

        private void Stop()
        {
            if (!_isRunning) return;

            _timer.Stop();
            _isRunning = false;
            _isPaused = false;
            IsSimPressed = false;
            Status = "Stopped";
            _prefs.Save();


            if (!_simOnly && JobElapsedSec > 0 && JobCutMm > 0)
            {
                try
                {
                    var jobLog = new JobLogEntry
                    {
                        When = DateTime.Now,
                        NcName = Path.GetFileName(NcPath) ?? "N/A",
                        Material = SelectedMaterial,
                        Gas = SelectedGas,
                        ThicknessMm = SelectedThickness,
                        DurationSec = JobElapsedSec,
                        CutLengthMm = JobCutMm
                    };

                    _paramStore.LogProductionJob(jobLog);
                    AddEvent("Database", "INFO", "Üretim özeti veritabanına kaydedildi. (Stop ile)");
                }
                catch (Exception ex)
                {
                    AddEvent("Database", "ERROR", $"Özet kaydı hatası (Stop): {ex.Message}");
                }
            }

            AddEvent("Stop", "WARN", "Stopped by operator.");
            _auditLog.Log("WARN", "Job.Stop", "İş operatör tarafından durduruldu.", _sessionService.CurrentUser, _sessionService.CurrentRole);

           
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

            _auditLog.Log("WARN", "Job.Reset", "Sistem sıfırlandı.", _sessionService.CurrentUser, _sessionService.CurrentRole);


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
            _auditLog.Log("CRITICAL", "Job.EStop", "ACİL DURDURMA BUTONUNA BASILDI!", _sessionService.CurrentUser, _sessionService.CurrentRole);
            UpdateDerived();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            PressureBar = _simOnly ? 0 : CurrentParams.PressureBar;
            LaserPowerLive = _simOnly ? 0 : Math.Min(4000, CurrentParams.PowerW);

            double t = Math.Clamp((SelectedThickness - 1) / 49.0, 0.0, 1.0);
            double baseSpeed = 44 - (34 * t);
            double gasMult = SelectedGas switch { Gas.Oxygen => 1.3, Gas.Nitrogen => 1.56, _ => 1.0 };
            double jitter = 1.0 + ((_rng.NextDouble() * 0.2) - 0.1);
            CurrentSpeedMmSec = baseSpeed * gasMult * jitter;

            if (!_simOnly)
            {
                JobCutMm += CurrentSpeedMmSec * TickSeconds;
                JobElapsedSec += TickSeconds;
            }

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

            if (!_simOnly && Checks?.Tanks != null)
            {
                double lps = SelectedGas switch
                {
                    Gas.Oxygen => 3.0 / (20 * 60.0),
                    Gas.Nitrogen => 3.0 / (18 * 60.0),
                    _ => 3.0 / (60 * 60.0),
                };

                double percPerSec = (lps / 50.0) * 100.0 * 10.0;
                var tank = Checks.Tanks.First(tnk => tnk.Gas == SelectedGas);
                if (tank.Connected)
                {
                    tank.LevelPercent = Math.Max(0, tank.LevelPercent - percPerSec * TickSeconds);

                    if (_prefs.GasLevels != null)
                    {
                        _prefs.GasLevels[tank.Gas.ToString()] = tank.LevelPercent;
                    }

                    if (tank.LevelPercent <= 5)
                    {
                        AddEvent("Gas", "WARN", "Gaz seviyesi %5 altına indi. Süreç durduruldu.");
                        Stop();
                        GasOverallOk = false;
                    }
                }
            }

            if (!_simOnly && NcLines.Count > 0)
                CurrentLine = Math.Min(NcLines.Count, CurrentLine + 1);

            
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

        private void LoadOzetData()
        {

            DateTime start = CombineDateAndTime(OzetBaslangic, OzetBaslangicSaat);
            DateTime end = CombineDateAndTime(OzetBitis, OzetBitisSaat);
            // Cache için benzersiz bir anahtar oluştur
            string cacheKey = $"Ozet_{start:yyyyMMddHHmm}_{end:yyyyMMddHHmm}";

            // 1. Önce Cache'i kontrol et
            if (_cache.TryGet<List<JobLogEntry>>(cacheKey, out var cachedData))
            {
                OzetUretimKayitlari.Clear();
                if (cachedData == null || cachedData.Count == 0)
                {
                    OzetToplamKesim = 0;
                    OzetOrtalamaSure = 0;
                }
                else
                {
                    foreach (var item in cachedData)
                    {
                        OzetUretimKayitlari.Add(item);
                    }
                    OzetToplamKesim = cachedData.Sum(job => job.CutLengthMm);
                    OzetOrtalamaSure = cachedData.Average(job => job.DurationSec);
                }
                AddEvent("Ozet", "INFO", $"{cachedData?.Count ?? 0} adet üretim kaydı (Cache'den) yüklendi.");
                return; // Cache'de bulundu, metodu bitir.
            }

            // 2. Cache'de yoksa Veritabanına git 
            try
            {
                var data = _paramStore.GetProductionHistory(start, end);

                OzetUretimKayitlari.Clear();
                if (data == null || data.Count == 0)
                {
                    OzetToplamKesim = 0;
                    OzetOrtalamaSure = 0;
                    AddEvent("Ozet", "INFO", "Seçili tarih aralığında üretim kaydı bulunamadı.");
                }
                else
                {
                    foreach (var item in data)
                    {
                        OzetUretimKayitlari.Add(item);
                    }

                    OzetToplamKesim = data.Sum(job => job.CutLengthMm);
                    OzetOrtalamaSure = data.Average(job => job.DurationSec);
                    AddEvent("Ozet", "INFO", $"{data.Count} adet üretim kaydı (DB'den) yüklendi.");

                    // 3. Sonucu Cache'e kaydet (30 saniyeliğine)
                    _cache.Set(cacheKey, data, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                AddEvent("Ozet", "ERROR", $"Özet yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void ValidateCode()
        {
            if (string.IsNullOrWhiteSpace(AccessCode))
            {
                SessionStatusText = "Hata: Kod girmediniz.";
                return;
            }

            bool success = _sessionService.ValidateSession(AccessCode);

            if (success)
            {
                SessionStatusText = $"Giriş başarılı. Mevcut Rol: {_sessionService.CurrentRole}";

                _auditLog.Log(
                    level: "OK",
                    source: "Code.Validate",
                    message: $"Başarılı giriş. Rol: {_sessionService.CurrentRole}",
                    user: _sessionService.CurrentUser,
                    role: _sessionService.CurrentRole
                );

                (SaveParamsCmd as RelayCommand)?.RaiseCanExecuteChanged();
            }
            else
            {
                SessionStatusText = "Hata: Geçersiz veya süresi dolmuş kod! (Deneme hakkınız azalmış olabilir)";

                _auditLog.Log(
                    level: "DENY",
                    source: "Code.Validate",
                    message: "Başarısız kod denemesi.",
                    user: "System",
                    role: UserRole.Operator
                );
            }
        }

        private void DoLogout()
        {
            _sessionService.Logout();
            SessionStatusText = "Durum: Oturum kapatıldı. (Varsayılan: Operator)";

            _auditLog.Log(
                level: "INFO",
                source: "Session.Logout",
                message: "Oturum kapatıldı.",
                user: "System",
                role: UserRole.Operator
            );

            (SaveParamsCmd as RelayCommand)?.RaiseCanExecuteChanged();
        }

        
        private void LoadAnalizData(bool fromFilter = false)
        {
            // 'fromFilter' true ise (yani tarih seçiciden geldiyse), Debouncer'ı kullan.
            // Butondan geldiyse (fromFilter = false) anında çalıştır.
            if (fromFilter)
            {
                _analizDebouncer.Debounce(() =>
                {
                    
                    Application.Current.Dispatcher.Invoke(PerformLoadAnalizData);
                });
            }
            else
            {
                PerformLoadAnalizData(); // Butona basılırsa anında çalıştır
            }
        }


        private void PerformLoadAnalizData()
        {

            DateTime start = CombineDateAndTime(AnalizBaslangic, AnalizBaslangicSaat);
            DateTime end = CombineDateAndTime(AnalizBitis, AnalizBitisSaat);
            // Cache için benzersiz bir anahtar oluştur
            string cacheKey = $"Analiz_{start:yyyyMMddHHmm}_{end:yyyyMMddHHmm}";

            // 1. Önce Cache'i kontrol et
            if (_cache.TryGet<List<JobLogEntry>>(cacheKey, out var cachedData))
            {
                if (cachedData == null || cachedData.Count == 0)
                {
                    AnalizToplamIs = 0;
                    AnalizToplamKesim = 0;
                    AnalizOrtalamaSure = 0;
                    AnalizZamanSerisi.Clear();
                    AnalizMalzemeKirilim.Clear();
                }
                else
                {
                    // Cache'deki veriyi kullanarak grafikleri ve kartları güncelle
                    UpdateAnalizUI(cachedData);
                }
                AddEvent("Analiz", "INFO", $"{cachedData?.Count ?? 0} adet kayıt (Cache'den) analiz edildi.");
                return; // Cache'de bulundu, metodu bitir.
            }

            // 2. Cache'de yoksa Veritabanına git 
            try
            {
                var data = _paramStore.GetProductionHistory(start, end);

                if (data == null || data.Count == 0)
                {
                    AnalizToplamIs = 0;
                    AnalizToplamKesim = 0;
                    AnalizOrtalamaSure = 0;
                    AnalizZamanSerisi.Clear();
                    AnalizMalzemeKirilim.Clear();
                    AddEvent("Analiz", "INFO", "Seçili tarih aralığında analiz verisi bulunamadı.");
                    return;
                }

                // Grafikleri ve kartları güncelle
                UpdateAnalizUI(data);

                // 3. Sonucu Cache'e kaydet (30 saniyeliğine)
                _cache.Set(cacheKey, data, TimeSpan.FromSeconds(30));

                AddEvent("Analiz", "INFO", $"{data.Count} adet kayıt (DB'den) analiz edildi.");
            }
            catch (Exception ex)
            {
                AddEvent("Analiz", "ERROR", $"Analiz verisi yüklenirken hata: {ex.Message}");
            }
        }

        private void UpdateAnalizUI(List<JobLogEntry> data)
        {
            // Özet Kartlar
            AnalizToplamIs = data.Count;
            AnalizToplamKesim = data.Sum(job => job.CutLengthMm);
            AnalizOrtalamaSure = data.Average(job => job.DurationSec);

            //  Zaman Serisi
            AnalizZamanSerisi.Clear();
            AnalizZamanSerisi.Add(new LineSeries<JobLogEntry>
            {
                Values = data.OrderBy(d => d.When).ToList(),
                Mapping = (job, index) => new(job.When.Ticks, job.DurationSec),
                Name = "İş Süresi (sn)",
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 5 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                Fill = null
            });

            //  Malzeme Kırılımı
            AnalizMalzemeKirilim.Clear();
            var pieSeries = data
                .GroupBy(job => job.Material)
                .Select(group => new PieSeries<double>
                {
                    Values = new double[] { group.Sum(job => job.CutLengthMm) },
                    Name = group.Key.ToString(),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black) { FontFamily = "Arial" },
                    DataLabelsPosition = PolarLabelsPosition.Outer,
                    DataLabelsFormatter = p => $"{p.Model:F0} mm"
                });

            foreach (var series in pieSeries)
            {
                AnalizMalzemeKirilim.Add(series);
            }
        }

        private void ExportAnalizToPdf()
        {
            DateTime start = CombineDateAndTime(AnalizBaslangic, AnalizBaslangicSaat);
            DateTime end = CombineDateAndTime(AnalizBitis, AnalizBitisSaat);

            var data = _paramStore.GetProductionHistory(start, end);
            if (data == null || data.Count == 0)
            {
                AddEvent("Rapor", "WARN", "Raporlanacak veri bulunamadı.");
                return;
            }

            var totalCut = data.Sum(job => job.CutLengthMm);
            var avgDuration = data.Average(job => job.DurationSec);

            try
            {
                byte[] pdfBytes = _impex.GenerateAnalysisPdf(data, AnalizBaslangic, AnalizBitis, avgDuration, totalCut);

                string contentHash = HashService.CalculateSha256(pdfBytes);

                
                string metadata = $"From:{AnalizBaslangic:o}|To:{AnalizBitis:o}"; 
                string metadataHash = HashService.CalculateSha256(metadata);

                
                string? previousHash = _paramStore.GetLatestReportHash();
                string chainedHash = HashService.ChainReportHash(previousHash, metadataHash, contentHash);

                var dlg = new SaveFileDialog
                {
                    
                    FileName = $"AnalizRaporu_{AnalizBaslangic:yyyy-MM-dd}_{AnalizBitis:yyyy-MM-dd}_{DateTime.Now:HH-mm-ss}.pdf",
                    Filter = "PDF Dosyaları (*.pdf)|*.pdf"
                };

                if (dlg.ShowDialog() == true)
                {
                    
                    File.WriteAllBytes(dlg.FileName, pdfBytes);

                    
                    _paramStore.SaveReportHistory(
                        reportType: "Analiz",
                        reportHash: chainedHash,       // Zincirlenmiş ana hash
                        contentHash: contentHash,      // Sadece PDF içeriğinin hash'i
                        metadataHash: metadataHash,    // Sadece filtrelerin hash'i
                        previousHash: previousHash     // Bir önceki raporun hash'i
                    );

                    AddEvent("Rapor", "INFO", $"PDF Rapor kaydedildi ve veritabanına imzalandı.");
                    _auditLog.Log("OK", "Report.ExportPDF", $"Analiz raporu PDF olarak dışa aktarıldı. Hash: {chainedHash}", _sessionService.CurrentUser, _sessionService.CurrentRole);
                }
            }
            catch (Exception ex)
            {
                AddEvent("Rapor", "ERROR", $"PDF oluşturma/hash'leme hatası: {ex.Message}");
                _auditLog.Log("ERROR", "Report.ExportPDF", $"Hata: {ex.Message}", _sessionService.CurrentUser, _sessionService.CurrentRole);
            }
        }

        private void LoadKpiData()
        {
            
            try
            {
                
                var data = _paramStore.GetProductionHistory(DateTime.Now.AddDays(-30), DateTime.Now);
                double totalUptimeHours = data.Sum(job => job.DurationSec) / 3600.0; // Saniyeyi saate çevir

                // Arıza sayısını Event log'dan say 
               
                int failureCount = Events.Count(e => e.Level == "ERROR" || e.Level == "WARN");
                if (failureCount == 0) failureCount = 1; // 0'a bölme hatasını önle

               
                // MTBF = Toplam Çalışma Süresi / Arıza Sayısı
                KpiMtbf = totalUptimeHours / failureCount;

                // MTTR = Ortalama 1 saatte onarıldığını varsayalım (simülasyon)
                KpiMttr = 1.0;

                // Availability = (Çalışma Süresi) / (Çalışma Süresi + Arıza Süresi)
                double totalDowntimeHours = failureCount * KpiMttr;
                KpiAvailability = (totalUptimeHours / (totalUptimeHours + totalDowntimeHours)) * 100.0;
                if (double.IsNaN(KpiAvailability)) KpiAvailability = 100.0;

                AddEvent("KPI", "INFO", "KPI verileri simüle edildi ve hesaplandı.");
            }
            catch (Exception ex)
            {
                AddEvent("KPI", "ERROR", $"KPI hesaplama hatası: {ex.Message}");
                KpiMtbf = 0;
                KpiMttr = 0;
                KpiAvailability = 0;
            }
        }

        
        private void LoadVerifyReportList()
        {
            try
            {
                VerifyReportList.Clear();
                var list = _paramStore.GetReportHistoryList(); 
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        VerifyReportList.Add(item);
                    }
                }
                // ComboBox'taki ilk öğeyi (en son raporu) otomatik seç
                VerifySelectedReport = VerifyReportList.FirstOrDefault();

                VerifyResultText = "Veritabanındaki imzalı raporlar yüklendi. Lütfen birini seçin ve 'Doğrula'ya basın.";
                VerifyHashUyumlu = null;
                VerifyZincirTutarlı = null;
            }
            catch (Exception ex)
            {
                VerifyResultText = $"Hata: Rapor listesi yüklenemedi. {ex.Message}";
            }
        }

        
        private void VerifySelectedFile()
        {
            

            
            if (VerifySelectedReport == null)
            {
                VerifyResultText = "Hata: Lütfen önce 'Rapor Seç' listesinden bir kayıt seçin.";
                return;
            }

            
            var dlg = new OpenFileDialog
            {
                Title = "Doğrulanacak PDF Rapor Dosyasını Seçin",
                Filter = "PDF Dosyaları (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
            {
                VerifyResultText = "Doğrulama iptal edildi.";
                return;
            }

            try
            {
                //  Veritabanından seçilen raporun TÜM detaylarını al
                var dbEntry = _paramStore.GetReportHistoryEntry(VerifySelectedReport.Id); 
                if (dbEntry == null)
                {
                    VerifyResultText = "Kritik Hata: Rapor imzası veritabanında bulunamadı!";
                    return;
                }

                //  Kullanıcının seçtiği PDF dosyasının içeriğini oku ve hash'ini al
                byte[] fileBytes = File.ReadAllBytes(dlg.FileName);
                string fileContentHash = HashService.CalculateSha256(fileBytes); // 

                
                VerifyHashUyumlu = (fileContentHash == dbEntry.ContentHash);

                
                // Bir önceki raporun ana hash'ini al
                var allReports = _paramStore.GetReportHistoryList();
                var previousDbEntry = allReports.FirstOrDefault(r => r.Timestamp < dbEntry.Timestamp);
                string? expectedPreviousHash = previousDbEntry?.ReportHash;

                VerifyZincirTutarlı = (dbEntry.PreviousHash == expectedPreviousHash);

               
                if (VerifyHashUyumlu == true && VerifyZincirTutarlı == true)
                {
                    VerifyResultText = $"DOĞRULAMA BAŞARILI: Dosya içeriği (Hash: {fileContentHash.Substring(0, 10)}...) ve Rapor Zinciri tutarlı.";
                }
                else
                {
                    VerifyResultText = "DOĞRULAMA BAŞARISIZ! Rapor değiştirilmiş veya zincirde kopukluk var.";
                    if (VerifyHashUyumlu == false) VerifyResultText += " (Dosya içeriği uyuşmuyor)";
                    if (VerifyZincirTutarlı == false) VerifyResultText += " (Rapor zinciri kırık)";
                }
            }
            catch (Exception ex)
            {
                VerifyResultText = $"Doğrulama sırasında hata: {ex.Message}";
                VerifyHashUyumlu = false;
                VerifyZincirTutarlı = false;
            }
        }

        private void RequestCode(UserRole role)
        {
            SessionStatusText = $"{role} kodu isteniyor, e-posta gönderiliyor...";
            try
            {
                
                Task.Run(async () =>
                {
                    try
                    {
                        await _sessionService.RequestAccessCode(role);

                      
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SessionStatusText = $"{role} kodu yönetici e-postasına gönderildi. Lütfen kodu girin.";
                        });
                    }
                    catch (Exception ex)
                    {
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SessionStatusText = $"Hata: E-posta gönderilemedi. {ex.Message}";
                        });
                        _auditLog.Log("ERROR", "Session.CodeRequest.Task", $"{role} kodu gönderme hatası: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Task.Run() başlatılırken hata olursa 
                SessionStatusText = $"Hata: E-posta işlemi başlatılamadı. {ex.Message}";
                _auditLog.Log("CRITICAL", "Session.CodeRequest", $"{role} kodu isteme hatası: {ex.Message}");
            }
        }

    }
}