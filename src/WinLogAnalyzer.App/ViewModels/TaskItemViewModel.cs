using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Enveloppe une ScheduledTaskInfo pour l'affichage (etat depli + champs formates).</summary>
public sealed class TaskItemViewModel : ObservableObject
{
    private bool _isExpanded;

    public TaskItemViewModel(ScheduledTaskInfo info) => Info = info;

    public ScheduledTaskInfo Info { get; }

    public string Name => Info.Name;
    public string Path => Info.Path;
    public string State => Info.State;
    public bool IsFailure => Info.IsFailure;
    public string LastResultHex => Info.LastResultHex;
    public Solution? Solution => Info.ResultSolution;
    public bool HasSolution => Info.ResultSolution is not null;
    public string? Description => Info.Description;

    /// <summary>Niveau d'affichage : echec -> Error, sinon Information (pilote la couleur).</summary>
    public string DisplayLevel => Info.IsFailure ? "Error" : "Information";

    public string LastRunText => Info.LastRun?.ToString("dd/MM/yyyy HH:mm") ?? "jamais";
    public string NextRunText => Info.NextRun?.ToString("dd/MM/yyyy HH:mm") ?? "—";

    public string MetaLine
        => $"{State} · dernier code {LastResultHex} · derniere exec {LastRunText} · prochaine {NextRunText}";

    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        query = query.Trim();
        return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Path.Contains(query, StringComparison.OrdinalIgnoreCase)
            || LastResultHex.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
