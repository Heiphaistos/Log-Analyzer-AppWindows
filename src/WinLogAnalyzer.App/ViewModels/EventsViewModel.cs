using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Diagnostics;
using WinLogAnalyzer.Core.Export;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Models;
using WinLogAnalyzer.Core.Process;
using WinLogAnalyzer.Core.Reader;
using WinLogAnalyzer.Core.Settings;
using WinLogAnalyzer.Core.Tasks;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Option de plage temporelle (label + nombre d'heures, 0 = tout).</summary>
public sealed record RangeOption(string Label, int Hours);

/// <summary>ViewModel de l'onglet Evenements : multi-journaux, filtres, dedup, export, surveillance.</summary>
public sealed class EventsViewModel : ObservableObject, IDisposable
{
    private readonly SolutionProvider _solutions;
    private readonly AppSettings _settings;
    private readonly FileLogger _logger;
    private readonly ErrorDatabase? _errorDb;

    private List<EventEntry> _raw = new();
    private readonly List<EventLogWatcher> _watchers = new();

    private string _searchText = "";
    private string _statusText = "Pret.";
    private bool _isLoading;
    private int _newCount;

    public EventsViewModel(SolutionProvider solutions, AppSettings settings, FileLogger logger, ErrorDatabase? errorDb = null)
    {
        _solutions = solutions;
        _settings = settings;
        _logger = logger;
        _errorDb = errorDb;

        Events = new ObservableCollection<EventItemViewModel>();
        EventsView = CollectionViewSource.GetDefaultView(Events);
        EventsView.Filter = o => o is EventItemViewModel vm && vm.Matches(_searchText);
        Timeline = new ObservableCollection<TimelineBar>();

        RangeOptions = new[]
        {
            new RangeOption("Tout", 0),
            new RangeOption("24 heures", 24),
            new RangeOption("7 jours", 168),
            new RangeOption("30 jours", 720)
        };

        AnalyzeCommand = new RelayCommand(async () => await AnalyzeAsync(), () => !_isLoading);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => _raw.Count > 0);
        ExportHtmlCommand = new RelayCommand(ExportHtml, () => _raw.Count > 0);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => _raw.Count > 0);
        ClearNewCommand = new RelayCommand(() => NewCount = 0);

        _solutions.Changed += OnSolutionsChanged;
        StatusText = $"Pret. {_solutions.Count} solutions chargees.";
    }

    public ObservableCollection<EventItemViewModel> Events { get; }
    public ICollectionView EventsView { get; }
    public ObservableCollection<TimelineBar> Timeline { get; }
    public RangeOption[] RangeOptions { get; }

    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportHtmlCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ClearNewCommand { get; }

    // --- Journaux (multi) ---
    public bool LogSystem { get => _settings.LogSystem; set { _settings.LogSystem = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }
    public bool LogApplication { get => _settings.LogApplication; set { _settings.LogApplication = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }
    public bool LogSecurity { get => _settings.LogSecurity; set { _settings.LogSecurity = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }

    public int MaxCount
    {
        get => _settings.MaxCount;
        set { var v = Math.Clamp(value, 1, 1000); if (_settings.MaxCount != v) { _settings.MaxCount = v; OnPropertyChanged(); Persist(); } }
    }

    public int SelectedRangeHours
    {
        get => _settings.TimeRangeHours;
        set
        {
            if (_settings.TimeRangeHours == value) return;
            _settings.TimeRangeHours = value;
            OnPropertyChanged();
            Persist();
            // La periode fait partie de la requete : re-analyser pour obtenir les N events
            // de la periode (pas juste re-filtrer les N derniers deja charges).
            if (!IsLoading && _raw.Count > 0) _ = AnalyzeAsync();
            else Rebuild();
        }
    }

    public bool LevelCritical { get => _settings.LevelCritical; set { _settings.LevelCritical = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }
    public bool LevelError { get => _settings.LevelError; set { _settings.LevelError = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }
    public bool LevelWarning { get => _settings.LevelWarning; set { _settings.LevelWarning = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }
    public bool LevelInformation { get => _settings.LevelInformation; set { _settings.LevelInformation = value; OnPropertyChanged(); Persist(); RestartMonitorIfActive(); } }

    public bool GroupDuplicates
    {
        get => _settings.GroupDuplicates;
        set { if (_settings.GroupDuplicates != value) { _settings.GroupDuplicates = value; OnPropertyChanged(); Persist(); Rebuild(); } }
    }

    public bool MonitorEnabled
    {
        get => _settings.MonitorEnabled;
        set
        {
            if (_settings.MonitorEnabled == value) return;
            _settings.MonitorEnabled = value;
            OnPropertyChanged();
            Persist();
            if (value) StartMonitor(); else StopMonitor();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) ScheduleSearchRefresh(); }
    }

    private System.Windows.Threading.DispatcherTimer? _searchTimer;

    // Debounce : Refresh() re-filtre toute la collection ; a chaque frappe sur une grosse
    // liste, l'UI accroche. Une seule passe 250 ms apres la derniere touche.
    private void ScheduleSearchRefresh()
    {
        if (_searchTimer is null)
        {
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (_, _) => { _searchTimer!.Stop(); EventsView.Refresh(); };
        }
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (SetField(ref _isLoading, value)) { OnPropertyChanged(nameof(IsNotLoading)); AnalyzeCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsNotLoading => !_isLoading;

    public int NewCount { get => _newCount; set { if (SetField(ref _newCount, value)) OnPropertyChanged(nameof(HasNew)); } }
    public bool HasNew => _newCount > 0;

    public int TotalCount => Events.Count;
    public int CriticalCount => Events.Count(e => e.Level == "Critical");
    public int ErrorCount => Events.Count(e => e.Level == "Error");
    public int SolvedCount => Events.Count(e => e.HasSolution);

    private string Scope => string.Join(", ", _settings.SelectedLogs());

    private async Task AnalyzeAsync()
    {
        IsLoading = true;
        StatusText = $"Analyse de {Scope}…";

        try
        {
            var logs = _settings.SelectedLogs();
            int max = MaxCount;
            var levels = _settings.SelectedLevels();
            int? maxAge = SelectedRangeHours > 0 ? SelectedRangeHours : null;

            var warnings = new List<string>();
            var entries = await Task.Run(() =>
            {
                var resolver = new ProcessResolver();
                var service = new EventLogService(resolver, _solutions, _errorDb);
                var merged = new List<EventEntry>();
                foreach (var log in logs)
                {
                    try { merged.AddRange(service.GetRecent(log, max, levels, maxAge)); }
                    catch (InvalidOperationException ex) { warnings.Add(ex.Message); }
                }
                return merged
                    .OrderByDescending(e => e.TimeCreated)
                    .Take(max * logs.Count)
                    .ToList();
            });

            _raw = entries;
            Rebuild();
            if (warnings.Count > 0)
            {
                StatusText += $" ⚠ {string.Join(" · ", warnings)}";
                foreach (var w in warnings) _logger.Warn($"Analyse [{Scope}]: {w}");
            }
            _logger.Info($"Analyse [{Scope}]: {_raw.Count} events.");
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur : {ex.Message}";
            _logger.Error($"Analyse [{Scope}]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IReadOnlyList<EventEntry> Filtered()
    {
        if (SelectedRangeHours <= 0) return _raw;
        var cutoff = DateTime.Now.AddHours(-SelectedRangeHours);
        return _raw.Where(e => e.TimeCreated >= cutoff).ToList();
    }

    private IReadOnlyList<EventEntry> DisplaySet()
    {
        var f = Filtered();
        return GroupDuplicates ? EventGrouper.Deduplicate(f) : f;
    }

    private void Rebuild()
    {
        var display = DisplaySet();
        Events.Clear();
        foreach (var e in display)
            Events.Add(new EventItemViewModel(e));

        EventsView.Refresh();
        BuildTimeline();
        RaiseStats();
        RefreshExportState();

        StatusText = TotalCount == 0
            ? "Aucun evenement trouve."
            : $"{TotalCount} evenements · {CriticalCount} critiques · {SolvedCount} avec solution.";
    }

    private void BuildTimeline()
    {
        Timeline.Clear();
        var src = Filtered();
        if (src.Count == 0) return;

        var byDay = src.GroupBy(e => e.TimeCreated.Date).OrderBy(g => g.Key).ToList();
        int maxCount = byDay.Max(g => g.Count());
        const double maxH = 60;

        foreach (var g in byDay)
            Timeline.Add(new TimelineBar
            {
                Label = g.Key.ToString("dd/MM"),
                Count = g.Count(),
                Height = Math.Max(4, maxH * g.Count() / maxCount),
                HasCritical = g.Any(e => e.Level == "Critical")
            });
    }

    // ===== Surveillance temps reel (un watcher par journal) =====
    private void StartMonitor()
    {
        try
        {
            var service = new EventLogService(new ProcessResolver(), _solutions, _errorDb);
            foreach (var log in _settings.SelectedLogs())
            {
                string logName = log;
                var w = service.CreateWatcher(logName, _settings.SelectedLevels());
                w.EventRecordWritten += (s, e) => OnEventWritten(logName, e);
                w.Enabled = true;
                _watchers.Add(w);
            }
            StatusText = $"Surveillance active sur {Scope}.";
            _logger.Info($"Surveillance ON [{Scope}].");
        }
        catch (Exception ex)
        {
            StatusText = $"Surveillance impossible : {ex.Message}";
            _logger.Error($"Surveillance: {ex.Message}");
            _settings.MonitorEnabled = false;
            OnPropertyChanged(nameof(MonitorEnabled));
        }
    }

    /// <summary>Resynchronise les watchers quand journaux/niveaux changent en cours de surveillance.</summary>
    private void RestartMonitorIfActive()
    {
        if (!_settings.MonitorEnabled || _watchers.Count == 0) return;
        StopMonitor();
        StartMonitor();
    }

    private void StopMonitor()
    {
        foreach (var w in _watchers)
        {
            try { w.Enabled = false; w.Dispose(); } catch { }
        }
        _watchers.Clear();
        StatusText = "Surveillance arretee.";
        _logger.Info("Surveillance OFF.");
    }

    private void OnEventWritten(string logName, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord is null) return;
        EventEntry entry;
        try
        {
            var service = new EventLogService(new ProcessResolver(), _solutions, _errorDb);
            entry = service.Map(e.EventRecord, logName);
        }
        catch { return; }
        finally { e.EventRecord.Dispose(); }

        // Async (BeginInvoke) pour ne pas bloquer le thread du watcher ; rebuild coalesce
        // pour absorber les rafales (un seul rebuild par fenetre de 300 ms).
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _raw.Insert(0, entry);
            NewCount++;
            ScheduleRebuild();
        });
    }

    private System.Windows.Threading.DispatcherTimer? _rebuildTimer;

    private void ScheduleRebuild()
    {
        if (_rebuildTimer is null)
        {
            _rebuildTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _rebuildTimer.Tick += (_, _) => { _rebuildTimer!.Stop(); Rebuild(); };
        }
        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    // ===== Export =====
    private void ExportCsv()
    {
        var path = AskSavePath("CSV (*.csv)|*.csv", "csv");
        if (path is null) return;
        Try(() => ReportExporter.ToCsv(DisplaySet(), path), $"Export CSV : {path}", "CSV");
    }

    private void ExportHtml()
    {
        var path = AskSavePath("HTML (*.html)|*.html", "html");
        if (path is null) return;
        Try(() => ReportExporter.ToHtml(DisplaySet(), path, Scope), $"Export HTML : {path}", "HTML");
    }

    private void ExportPdf()
    {
        var path = AskSavePath("PDF (*.pdf)|*.pdf", "pdf");
        if (path is null) return;
        Try(() => PdfExporter.ToPdf(DisplaySet(), path, Scope), $"Export PDF : {path}", "PDF");
    }

    private void Try(Action export, string okMsg, string kind)
    {
        try { export(); StatusText = okMsg; _logger.Info(okMsg); }
        catch (Exception ex) { StatusText = $"Export {kind} echoue : {ex.Message}"; _logger.Error($"Export {kind}: {ex.Message}"); }
    }

    private string? AskSavePath(string filter, string ext)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = $"winlog_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void OnSolutionsChanged()
        => Application.Current?.Dispatcher.Invoke(async () => { if (!IsLoading) await AnalyzeAsync(); });

    private void RefreshExportState()
    {
        ExportCsvCommand.RaiseCanExecuteChanged();
        ExportHtmlCommand.RaiseCanExecuteChanged();
        ExportPdfCommand.RaiseCanExecuteChanged();
    }

    private void RaiseStats()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(SolvedCount));
    }

    private void Persist() => _settings.SaveDebounced();

    public void Dispose()
    {
        _searchTimer?.Stop();
        _rebuildTimer?.Stop();
        StopMonitor();
        _solutions.Changed -= OnSolutionsChanged;
    }
}
