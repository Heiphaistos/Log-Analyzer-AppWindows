using System.Diagnostics.Eventing.Reader;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Models;
using WinLogAnalyzer.Core.Process;
using WinLogAnalyzer.Core.Tasks;

namespace WinLogAnalyzer.Core.Reader;

/// <summary>
/// Lit les events d'un journal en streaming (pas de charge complete en RAM) et les
/// normalise en <see cref="EventEntry"/> enrichies de leur solution.
/// </summary>
public sealed class EventLogService
{
    private readonly ProcessResolver _resolver;
    private readonly SolutionProvider _solutions;
    private readonly ErrorDatabase? _errorDb;

    // Niveaux Windows : 1=Critical, 2=Error, 3=Warning, 4=Information.
    public static readonly IReadOnlyList<int> CriticalAndError = new[] { 1, 2 };

    public EventLogService(ProcessResolver resolver, SolutionProvider solutions, ErrorDatabase? errorDb = null)
    {
        _resolver = resolver;
        _solutions = solutions;
        _errorDb = errorDb;
    }

    /// <summary>
    /// Extrait les <paramref name="max"/> derniers events des niveaux demandes.
    /// <paramref name="maxAgeHours"/> restreint la requete a la periode (0/null = tout),
    /// cote serveur : on obtient les N events de la periode, pas un sous-ensemble des N derniers.
    /// </summary>
    public IReadOnlyList<EventEntry> GetRecent(string logName, int max, IReadOnlyCollection<int>? levels = null, int? maxAgeHours = null)
    {
        var results = new List<EventEntry>(max);
        var query = new EventLogQuery(logName, PathType.LogName, BuildQuery(levels, maxAgeHours))
        {
            ReverseDirection = true
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
                    try { results.Add(Map(record, logName)); }
                    catch (Exception ex) { Console.Error.WriteLine($"[WARN] Skip event: {ex.Message}"); }
                }
            }
        }

        return results;
    }

    /// <summary>Cree un watcher push pour la surveillance temps reel (a Dispose par l'appelant).</summary>
    public EventLogWatcher CreateWatcher(string logName, IReadOnlyCollection<int>? levels = null)
    {
        var query = new EventLogQuery(logName, PathType.LogName, BuildQuery(levels));
        return new EventLogWatcher(query);
    }

    /// <summary>Normalise un EventRecord en EventEntry (reutilise par lecture et watcher).</summary>
    public EventEntry Map(EventRecord record, string logName)
    {
        int? pid = record.ProcessId;
        string source = record.ProviderName ?? "(inconnu)";
        int eventId = record.Id;

        string message;
        try { message = record.FormatDescription() ?? "(aucune description)"; }
        catch { message = "(description indisponible — provider manquant)"; }
        message = message.Trim();

        // Solution curee (Event ID), sinon code d'erreur decode depuis le message.
        var solution = _solutions.Lookup(eventId, source)
                       ?? Win32ErrorDecoder.TryDecodeFromText(message, _errorDb);

        return new EventEntry
        {
            EventId = eventId,
            Level = LevelName(record.Level),
            LogName = logName,
            Source = source,
            TimeCreated = record.TimeCreated ?? DateTime.MinValue,
            Message = message,
            ProcessId = pid,
            ProcessName = _resolver.Resolve(pid),
            Solution = solution
        };
    }

    /// <summary>Construit la requete XPath (niveaux + periode). Public pour tests.</summary>
    public static string BuildQuery(IReadOnlyCollection<int>? levels, int? maxAgeHours = null)
    {
        var list = (levels is null || levels.Count == 0) ? CriticalAndError : levels;
        var clause = string.Join(" or ", list.Select(l => $"Level={l}"));
        if (maxAgeHours is > 0)
        {
            long ms = (long)maxAgeHours.Value * 3_600_000;
            return $"*[System[({clause}) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";
        }
        return $"*[System[({clause})]]";
    }

    private static string LevelName(byte? level) => level switch
    {
        1 => "Critical",
        2 => "Error",
        3 => "Warning",
        4 => "Information",
        _ => "Information"
    };
}
