namespace WinLogAnalyzer.Core.Models;

/// <summary>Resume d'une tache planifiee Windows pour l'affichage et le diagnostic.</summary>
public sealed record ScheduledTaskInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string State { get; init; }        // Running | Ready | Disabled | Queued | Unknown
    public required bool Enabled { get; init; }
    public DateTime? LastRun { get; init; }
    public DateTime? NextRun { get; init; }

    public required int LastResultCode { get; init; }
    public required string LastResultHex { get; init; }   // ex: 0x80070002
    public required bool IsFailure { get; init; }
    public string? Description { get; init; }

    /// <summary>Explication + remediation du code de resultat (null si code inconnu).</summary>
    public Solution? ResultSolution { get; init; }
}
