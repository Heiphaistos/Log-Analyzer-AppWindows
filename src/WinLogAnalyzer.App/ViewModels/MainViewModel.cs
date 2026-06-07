using System.IO;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Settings;
using WinLogAnalyzer.Core.Tasks;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>ViewModel racine : detient les sous-VMs des onglets + ressources partagees.</summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly SolutionProvider _solutions;
    private readonly FileLogger _logger;

    public MainViewModel()
    {
        _logger = new FileLogger();
        var settings = AppSettings.Load();

        var solutionsPath = Path.Combine(AppContext.BaseDirectory, "data", "solutions.json");
        _solutions = new SolutionProvider(solutionsPath, hotReload: true);

        var errorDbPath = Path.Combine(AppContext.BaseDirectory, "data", "errordb.json");
        var errorDb = new ErrorDatabase(errorDbPath);

        Events = new EventsViewModel(_solutions, settings, _logger, errorDb);
        Tasks = new TasksViewModel(_logger, errorDb);
        Incidents = new IncidentsViewModel(_solutions, settings, _logger, errorDb);

        _logger.Info($"Application demarree. Base d'erreurs : {errorDb.Count} codes.");
    }

    public EventsViewModel Events { get; }
    public TasksViewModel Tasks { get; }
    public IncidentsViewModel Incidents { get; }

    public void Dispose()
    {
        Events.Dispose();
        _solutions.Dispose();
        _logger.Info("Application fermee.");
    }
}
