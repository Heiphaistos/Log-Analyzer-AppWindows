namespace WinLogAnalyzer.Core.Models;

/// <summary>
/// Remediation associee a un Event ID, chargee depuis solutions.json.
/// </summary>
public sealed record Solution
{
    public required string Title { get; init; }
    public required string Explanation { get; init; }
    public required string Remediation { get; init; }

    /// <summary>info | warning | critical — pilote le code couleur du frontend.</summary>
    public required string Severity { get; init; }

    public string[]? Links { get; init; }
}
