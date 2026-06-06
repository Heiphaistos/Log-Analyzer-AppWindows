using System.Diagnostics.Eventing.Reader;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Models;
using WinLogAnalyzer.Core.Process;

namespace WinLogAnalyzer.Core.Reader;

/// <summary>
/// Lit les events Critical (Level=1) et Error (Level=2) en streaming.
/// Aucune charge complete en memoire : lecture event par event + dispose immediat.
/// </summary>
public sealed class EventLogService
{
    private readonly ProcessResolver _resolver;
    private readonly SolutionProvider _solutions;

    // XPath : Level 1=Critical, 2=Error.
    private const string CriticalErrorQuery = "*[System[(Level=1 or Level=2)]]";

    public EventLogService(ProcessResolver resolver, SolutionProvider solutions)
    {
        _resolver = resolver;
        _solutions = solutions;
    }

    /// <summary>
    /// Extrait les <paramref name="max"/> dernieres erreurs critiques d'un log.
    /// </summary>
    /// <param name="logName">System, Application, Security...</param>
    /// <param name="max">Nombre max d'entrees (defaut 100).</param>
    public IReadOnlyList<EventEntry> GetRecentCritical(string logName = "System", int max = 100)
    {
        var results = new List<EventEntry>(max);

        var query = new EventLogQuery(logName, PathType.LogName, CriticalErrorQuery)
        {
            ReverseDirection = true // plus recent -> plus ancien
        };

        EventLogReader reader;
        try
        {
            reader = new EventLogReader(query);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Acces refuse au log '{logName}'. Lancer en administrateur.", ex);
        }
        catch (EventLogNotFoundException ex)
        {
            throw new InvalidOperationException($"Log '{logName}' introuvable.", ex);
        }

        using (reader)
        {
            EventRecord? record;
            while (results.Count < max && (record = reader.ReadEvent()) is not null)
            {
                using (record)
                {
                    try
                    {
                        results.Add(MapRecord(record, logName));
                    }
                    catch (Exception ex)
                    {
                        // Event corrompu / provider absent : on log et on continue.
                        Console.Error.WriteLine($"[WARN] Skip event: {ex.Message}");
                    }
                }
            }
        }

        return results;
    }

    private EventEntry MapRecord(EventRecord record, string logName)
    {
        int? pid = record.ProcessId;

        string message;
        try
        {
            message = record.FormatDescription() ?? "(aucune description)";
        }
        catch
        {
            message = "(description indisponible — provider manquant)";
        }

        int eventId = record.Id;

        return new EventEntry
        {
            EventId = eventId,
            Level = record.Level == 1 ? "Critical" : "Error",
            LogName = logName,
            Source = record.ProviderName ?? "(inconnu)",
            TimeCreated = record.TimeCreated ?? DateTime.MinValue,
            Message = message.Trim(),
            ProcessId = pid,
            ProcessName = _resolver.Resolve(pid),
            Solution = _solutions.Lookup(eventId)
        };
    }
}
