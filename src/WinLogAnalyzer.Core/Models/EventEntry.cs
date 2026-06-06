namespace WinLogAnalyzer.Core.Models;

/// <summary>Entree de log normalisee.</summary>
public sealed record EventEntry
{
    public required int EventId { get; init; }
    public required string Level { get; init; }          // Critical | Error | Warning | Information
    public required string LogName { get; init; }         // System, Application...
    public required string Source { get; init; }          // Provider
    public required DateTime TimeCreated { get; init; }
    public required string Message { get; init; }
    public int? ProcessId { get; init; }
    public required string ProcessName { get; init; }     // nom live ou "[termine]"

    /// <summary>Nombre d'occurrences (rempli lors de la deduplication, sinon 1).</summary>
    public int Count { get; init; } = 1;

    /// <summary>Enrichi via le dictionnaire. Null si Event ID inconnu.</summary>
    public Solution? Solution { get; init; }
}
