using Microsoft.Win32.TaskScheduler;
using WinLogAnalyzer.Core.Models;
using SchedTask = Microsoft.Win32.TaskScheduler.Task;

namespace WinLogAnalyzer.Core.Tasks;

/// <summary>
/// Enumere les taches planifiees Windows via l'API Task Scheduler 2.0.
/// Lecture seule : aucune modification des taches.
/// </summary>
public sealed class TaskSchedulerService
{
    private readonly ResultCodeProvider _codes;

    public TaskSchedulerService(ResultCodeProvider codes) => _codes = codes;

    /// <summary>
    /// Liste toutes les taches enregistrees. <paramref name="failuresOnly"/> ne garde
    /// que celles dont le dernier resultat est un echec.
    /// </summary>
    public IReadOnlyList<ScheduledTaskInfo> GetTasks(bool failuresOnly = false)
    {
        var results = new List<ScheduledTaskInfo>();

        using var ts = new TaskService();
        foreach (var task in ts.AllTasks)
        {
            ScheduledTaskInfo info;
            try
            {
                info = Map(task);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Skip task {task?.Path}: {ex.Message}");
                continue;
            }

            if (failuresOnly && !info.IsFailure) continue;
            results.Add(info);
        }

        // Echecs d'abord, puis par derniere execution decroissante.
        return results
            .OrderByDescending(t => t.IsFailure)
            .ThenByDescending(t => t.LastRun ?? DateTime.MinValue)
            .ToList();
    }

    private ScheduledTaskInfo Map(SchedTask task)
    {
        int code = task.LastTaskResult;
        bool isFailure = ResultCodeProvider.IsFailure(code);

        return new ScheduledTaskInfo
        {
            Name = task.Name,
            Path = task.Path,
            State = task.State.ToString(),
            Enabled = task.Enabled,
            LastRun = Normalize(task.LastRunTime),
            NextRun = Normalize(task.NextRunTime),
            LastResultCode = code,
            LastResultHex = ResultCodeProvider.ToHex(code),
            IsFailure = isFailure,
            Description = SafeDescription(task),
            ResultSolution = _codes.Describe(code)
        };
    }

    private static string? SafeDescription(SchedTask task)
    {
        try { return task.Definition.RegistrationInfo.Description; }
        catch { return null; }
    }

    // L'API renvoie DateTime.MinValue / une date sentinelle quand il n'y a pas d'execution.
    private static DateTime? Normalize(DateTime dt)
        => dt <= new DateTime(1900, 1, 1) || dt.Year > 9000 ? null : dt;
}
