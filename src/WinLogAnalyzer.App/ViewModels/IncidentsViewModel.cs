using System.Collections.ObjectModel;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Diagnostics;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Models;
using WinLogAnalyzer.Core.Process;
using WinLogAnalyzer.Core.Reader;
using WinLogAnalyzer.Core.Settings;
using WinLogAnalyzer.Core.Tasks;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Onglet Incidents : regroupe les events proches dans le temps (cause racine).</summary>
public sealed class IncidentsViewModel : ObservableObject
{
    private readonly SolutionProvider _solutions;
    private readonly AppSettings _settings;
    private readonly FileLogger _logger;
    private readonly ErrorDatabase? _errorDb;

    private string _statusText = "Pret.";
    private bool _isLoading;
    private int _windowSeconds = 120;
    private List<EventEntry> _cachedEvents = new();

    public IncidentsViewModel(SolutionProvider solutions, AppSettings settings, FileLogger logger, ErrorDatabase? errorDb = null)
    {
        _solutions = solutions;
        _settings = settings;
        _logger = logger;
        _errorDb = errorDb;
        Incidents = new ObservableCollection<IncidentViewModel>();
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
    }

    public ObservableCollection<IncidentViewModel> Incidents { get; }
    public RelayCommand RefreshCommand { get; }

    public int[] WindowOptions { get; } = { 30, 60, 120, 300 };

    public int WindowSeconds
    {
        get => _windowSeconds;
        // Changer la fenetre ne re-lit pas les journaux (P2 audit) : on re-correle le cache.
        set { if (SetField(ref _windowSeconds, value)) _ = LoadAsync(reload: _cachedEvents.Count == 0); }
    }

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (SetField(ref _isLoading, value)) { OnPropertyChanged(nameof(IsNotLoading)); RefreshCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsNotLoading => !_isLoading;

    public int IncidentCount => Incidents.Count;

    public async Task LoadAsync(bool reload = true)
    {
        IsLoading = true;
        StatusText = "Correlation des evenements…";
        try
        {
            var logs = _settings.SelectedLogs();
            var levels = _settings.SelectedLevels();
            int window = _windowSeconds;

            var warnings = new List<string>();
            var incidents = await Task.Run(() =>
            {
                if (reload)
                {
                    var service = new EventLogService(new ProcessResolver(), _solutions, _errorDb);
                    var merged = new List<EventEntry>();
                    foreach (var log in logs)
                    {
                        try { merged.AddRange(service.GetRecent(log, 500, levels)); }
                        catch (InvalidOperationException ex) { warnings.Add(ex.Message); }
                    }
                    _cachedEvents = merged;
                }
                return Correlator.Correlate(_cachedEvents, window);
            });

            Incidents.Clear();
            foreach (var inc in incidents)
                Incidents.Add(new IncidentViewModel(inc));

            OnPropertyChanged(nameof(IncidentCount));
            StatusText = IncidentCount == 0
                ? "Aucun incident correle (events isoles)."
                : $"{IncidentCount} incidents detectes (fenetre {window}s).";
            if (warnings.Count > 0)
            {
                StatusText += $" ⚠ {string.Join(" · ", warnings)}";
                foreach (var w in warnings) _logger.Warn($"Incidents: {w}");
            }
            _logger.Info($"Incidents: {IncidentCount} (fenetre {window}s).");
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur : {ex.Message}";
            _logger.Error($"Incidents: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
