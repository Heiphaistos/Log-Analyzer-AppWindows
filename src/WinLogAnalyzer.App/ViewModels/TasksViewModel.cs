using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Tasks;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>ViewModel de l'onglet Planificateur de taches.</summary>
public sealed class TasksViewModel : ObservableObject
{
    private readonly TaskSchedulerService _service;
    private readonly FileLogger _logger;

    private string _searchText = "";
    private string _statusText = "Pret.";
    private bool _isLoading;
    private bool _failuresOnly;

    public TasksViewModel(FileLogger logger, ErrorDatabase? errorDb = null)
    {
        _logger = logger;
        var codesPath = Path.Combine(AppContext.BaseDirectory, "data", "taskcodes.json");
        _service = new TaskSchedulerService(new ResultCodeProvider(codesPath, errorDb));

        Tasks = new ObservableCollection<TaskItemViewModel>();
        TasksView = CollectionViewSource.GetDefaultView(Tasks);
        TasksView.Filter = o => o is TaskItemViewModel vm && vm.Matches(_searchText);

        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
    }

    public ObservableCollection<TaskItemViewModel> Tasks { get; }
    public ICollectionView TasksView { get; }
    public RelayCommand RefreshCommand { get; }

    public bool FailuresOnly
    {
        get => _failuresOnly;
        set { if (SetField(ref _failuresOnly, value)) _ = LoadAsync(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) ScheduleSearchRefresh(); }
    }

    private System.Windows.Threading.DispatcherTimer? _searchTimer;

    // Debounce : la machine peut avoir des centaines de taches ; une seule passe
    // de filtrage 250 ms apres la derniere frappe.
    private void ScheduleSearchRefresh()
    {
        if (_searchTimer is null)
        {
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchTimer.Tick += (_, _) => { _searchTimer!.Stop(); TasksView.Refresh(); };
        }
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (SetField(ref _isLoading, value)) { OnPropertyChanged(nameof(IsNotLoading)); RefreshCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsNotLoading => !_isLoading;

    public int TotalCount => Tasks.Count;
    public int FailureCount => Tasks.Count(t => t.IsFailure);

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Lecture des taches planifiees…";
        try
        {
            bool failuresOnly = _failuresOnly;
            var tasks = await Task.Run(() => _service.GetTasks(failuresOnly));

            Tasks.Clear();
            foreach (var t in tasks)
                Tasks.Add(new TaskItemViewModel(t));

            TasksView.Refresh();
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(FailureCount));
            StatusText = $"{TotalCount} taches · {FailureCount} en echec.";
            _logger.Info($"Taches: {TotalCount} ({FailureCount} echecs).");
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur : {ex.Message}";
            _logger.Error($"Taches: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
