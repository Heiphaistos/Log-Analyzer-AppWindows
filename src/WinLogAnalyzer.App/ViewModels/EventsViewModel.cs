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

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>ViewModel de l'onglet Evenements : lecture, filtres, dedup, export, surveillance.</summary>
public sealed class EventsViewModel : ObservableObject, IDisposable
{
    private readonly SolutionProvider _solutions;
    private readonly AppSettings _settings;
    private readonly FileLogger _logger;

    private List<EventEntry> _raw = new();
    private EventLogWatcher? _watcher;

    private string _searchText = "";
    private string _statusText = "Pret.";
    private bool _isLoading;
    private int _newCount;

    public EventsViewModel(SolutionProvider solutions, AppSettings settings, FileLogger logger)
    {
        _solutions = solutions;
        _settings = settings;
        _logger = logger;

        Events = new ObservableCollection<EventItemViewModel>();
        EventsView = CollectionViewSource.GetDefaultView(Events);
        EventsView.Filter = o => o is EventItemViewModel vm && vm.Matches(_searchText);

        Timeline = new ObservableCollection<TimelineBar>();

        AnalyzeCommand = new RelayCommand(async () => await AnalyzeAsync(), () => !_isLoading);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => _raw.Count > 0);
        ExportHtmlCommand = new RelayCommand(ExportHtml, () => _raw.Count > 0);
        ClearNewCommand = new RelayCommand(() => NewCount = 0);

        _solutions.Changed += OnSolutionsChanged;
        StatusText = $"Pret. {_solutions.Count} solutions chargees.";
    }

    public ObservableCollection<EventItemViewModel> Events { get; }
    public ICollectionView EventsView { get; }
    public ObservableCollection<TimelineBar> Timeline { get; }

    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportHtmlCommand { get; }
    public RelayCommand ClearNewCommand { get; }

    public string[] AvailableLogs { get; } = { "System", "Application", "Security" };

    // --- Liees aux settings (persistees) ---
    public string SelectedLog
    {
        get => _settings.SelectedLog;
        set { if (_settings.SelectedLog != value) { _settings.SelectedLog = value; OnPropertyChanged(); Persist(); } }
    }

    public int MaxCount
    {
        get => _settings.MaxCount;
        set { var v = Math.Clamp(value, 1, 1000); if (_settings.MaxCount != v) { _settings.MaxCount = v; OnPropertyChanged(); Persist(); } }
    }

    public bool LevelCritical { get => _settings.LevelCritical; set { _settings.LevelCritical = value; OnPropertyChanged(); Persist(); } }
    public bool LevelError { get => _settings.LevelError; set { _settings.LevelError = value; OnPropertyChanged(); Persist(); } }
    public bool LevelWarning { get => _settings.LevelWarning; set { _settings.LevelWarning = value; OnPropertyChanged(); Persist(); } }
    public bool LevelInformation { get => _settings.LevelInformation; set { _settings.LevelInformation = value; OnPropertyChanged(); Persist(); } }

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
        set { if (SetField(ref _searchText, value)) EventsView.Refresh(); }
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

    private async Task AnalyzeAsync()
    {
        IsLoading = true;
        StatusText = $"Analyse du journal {SelectedLog}…";

        try
        {
            string log = SelectedLog;
            int max = MaxCount;
            var levels = _settings.SelectedLevels();

            var entries = await Task.Run(() =>
            {
                var service = new EventLogService(new ProcessResolver(), _solutions);
                return service.GetRecent(log, max, levels);
            });

            _raw = entries.ToList();
            Rebuild();
            _logger.Info($"Analyse {log}: {_raw.Count} events.");
        }
        catch (InvalidOperationException ex)
        {
            StatusText = $"Erreur : {ex.Message}";
            _logger.Error($"Analyse {SelectedLog}: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur inattendue : {ex.Message}";
            _logger.Error($"Analyse inattendue: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Reconstruit la liste affichee depuis _raw (dedup + timeline + stats).</summary>
    private void Rebuild()
    {
        var display = GroupDuplicates ? EventGrouper.Deduplicate(_raw) : (IReadOnlyList<EventEntry>)_raw;

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
        if (_raw.Count == 0) return;

        var byDay = _raw.GroupBy(e => e.TimeCreated.Date)
                        .OrderBy(g => g.Key)
                        .ToList();
        int maxCount = byDay.Max(g => g.Count());
        const double maxH = 60;

        foreach (var g in byDay)
        {
            Timeline.Add(new TimelineBar
            {
                Label = g.Key.ToString("dd/MM"),
                Count = g.Count(),
                Height = Math.Max(4, maxH * g.Count() / maxCount),
                HasCritical = g.Any(e => e.Level == "Critical")
            });
        }
    }

    // ===== Surveillance temps reel =====
    private void StartMonitor()
    {
        try
        {
            var service = new EventLogService(new ProcessResolver(), _solutions);
            _watcher = service.CreateWatcher(SelectedLog, _settings.SelectedLevels());
            _watcher.EventRecordWritten += OnEventWritten;
            _watcher.Enabled = true;
            StatusText = $"Surveillance active sur {SelectedLog}.";
            _logger.Info($"Surveillance ON ({SelectedLog}).");
        }
        catch (Exception ex)
        {
            StatusText = $"Surveillance impossible : {ex.Message}";
            _logger.Error($"Surveillance: {ex.Message}");
            _settings.MonitorEnabled = false;
            OnPropertyChanged(nameof(MonitorEnabled));
        }
    }

    private void StopMonitor()
    {
        if (_watcher is null) return;
        _watcher.EventRecordWritten -= OnEventWritten;
        _watcher.Dispose();
        _watcher = null;
        StatusText = "Surveillance arretee.";
        _logger.Info("Surveillance OFF.");
    }

    private void OnEventWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord is null) return;
        EventEntry entry;
        try
        {
            var service = new EventLogService(new ProcessResolver(), _solutions);
            entry = service.Map(e.EventRecord, SelectedLog);
        }
        catch { return; }
        finally { e.EventRecord.Dispose(); }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            _raw.Insert(0, entry);
            Events.Insert(0, new EventItemViewModel(entry));
            NewCount++;
            RaiseStats();
            BuildTimeline();
            RefreshExportState();
        });
    }

    // ===== Export =====
    private void ExportCsv()
    {
        var path = AskSavePath("CSV (*.csv)|*.csv", "csv");
        if (path is null) return;
        try { ReportExporter.ToCsv(GroupDuplicates ? EventGrouper.Deduplicate(_raw) : _raw, path);
              StatusText = $"Export CSV : {path}"; _logger.Info($"Export CSV {path}"); }
        catch (Exception ex) { StatusText = $"Export echoue : {ex.Message}"; }
    }

    private void ExportHtml()
    {
        var path = AskSavePath("HTML (*.html)|*.html", "html");
        if (path is null) return;
        try { ReportExporter.ToHtml(GroupDuplicates ? EventGrouper.Deduplicate(_raw) : _raw, path, SelectedLog);
              StatusText = $"Export HTML : {path}"; _logger.Info($"Export HTML {path}"); }
        catch (Exception ex) { StatusText = $"Export echoue : {ex.Message}"; }
    }

    private string? AskSavePath(string filter, string ext)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = $"winlog_{SelectedLog}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void OnSolutionsChanged()
        => Application.Current?.Dispatcher.Invoke(async () =>
        {
            if (!IsLoading) await AnalyzeAsync();
        });

    private void RefreshExportState()
    {
        ExportCsvCommand.RaiseCanExecuteChanged();
        ExportHtmlCommand.RaiseCanExecuteChanged();
    }

    private void RaiseStats()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(SolvedCount));
    }

    private void Persist() => _settings.Save();

    public void Dispose()
    {
        StopMonitor();
        _solutions.Changed -= OnSolutionsChanged;
    }
}
