using System.IO;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Settings;

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

        Events = new EventsViewModel(_solutions, settings, _logger);
        Tasks = new TasksViewModel(_logger);

        _logger.Info("Application demarree.");
    }

    public EventsViewModel Events { get; }
    public TasksViewModel Tasks { get; }

    public void Dispose()
    {
        Events.Dispose();
        _solutions.Dispose();
        _logger.Info("Application fermee.");
    }
}
