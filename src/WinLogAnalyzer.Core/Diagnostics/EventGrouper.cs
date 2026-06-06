using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Diagnostics;

/// <summary>Deduplication des events : regroupe les identiques (EventId+Source) avec compteur.</summary>
public static class EventGrouper
{
    /// <summary>
    /// Fusionne les events de meme EventId+Source. Conserve l'occurrence la plus recente,
    /// renseigne <see cref="EventEntry.Count"/> et garde l'ordre par date decroissante.
    /// </summary>
    public static IReadOnlyList<EventEntry> Deduplicate(IEnumerable<EventEntry> events)
    {
        return events
            .GroupBy(e => (e.EventId, e.Source))
            .Select(g =>
            {
                var latest = g.OrderByDescending(e => e.TimeCreated).First();
                return latest with { Count = g.Count() };
            })
            .OrderByDescending(e => e.TimeCreated)
            .ToList();
    }
}
