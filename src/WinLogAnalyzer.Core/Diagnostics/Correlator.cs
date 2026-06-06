using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Diagnostics;

/// <summary>Un incident = grappe d'events survenus dans une meme fenetre temporelle.</summary>
public sealed record Incident
{
    public required DateTime Start { get; init; }
    public required DateTime End { get; init; }
    public required IReadOnlyList<EventEntry> Events { get; init; }
    public int CriticalCount => Events.Count(e => e.Level == "Critical");
    public string Summary => $"{Events.Count} events · {Start:dd/MM HH:mm:ss} → {End:HH:mm:ss}";
}

/// <summary>
/// Regroupe les events proches dans le temps pour reveler une cause racine commune
/// (ex: Kernel-Power 41 + 6008 + BugCheck 1001 = un seul crash).
/// </summary>
public static class Correlator
{
    /// <summary>
    /// Clusterise les events dont l'ecart au precedent est <= <paramref name="windowSeconds"/>.
    /// Ne retourne que les grappes de 2 events ou plus.
    /// </summary>
    public static IReadOnlyList<Incident> Correlate(IEnumerable<EventEntry> events, int windowSeconds = 60)
    {
        var ordered = events.OrderBy(e => e.TimeCreated).ToList();
        var incidents = new List<Incident>();
        if (ordered.Count == 0) return incidents;

        var window = TimeSpan.FromSeconds(windowSeconds);
        var current = new List<EventEntry> { ordered[0] };

        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].TimeCreated - current[^1].TimeCreated <= window)
            {
                current.Add(ordered[i]);
            }
            else
            {
                Flush(incidents, current);
                current = new List<EventEntry> { ordered[i] };
            }
        }
        Flush(incidents, current);

        return incidents.OrderByDescending(i => i.Start).ToList();
    }

    private static void Flush(List<Incident> incidents, List<EventEntry> group)
    {
        if (group.Count < 2) return;
        incidents.Add(new Incident
        {
            Start = group[0].TimeCreated,
            End = group[^1].TimeCreated,
            Events = group.ToList()
        });
    }
}
