using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Enveloppe une EventEntry pour l'affichage (etat depli + champs formates).</summary>
public sealed class EventItemViewModel : ObservableObject
{
    private bool _isExpanded;

    public EventItemViewModel(EventEntry entry) => Entry = entry;

    public EventEntry Entry { get; }

    public int EventId => Entry.EventId;
    public string Level => Entry.Level;
    public string Source => Entry.Source;
    public string Message => Entry.Message;
    public Solution? Solution => Entry.Solution;

    public string TimeText => Entry.TimeCreated.ToString("dd/MM/yyyy HH:mm:ss");

    public string ProcessText => Entry.ProcessId is int pid
        ? $"PID {pid} · {Entry.ProcessName}"
        : "PID inconnu";

    /// <summary>Titre principal : titre de la solution si connue, sinon la source.</summary>
    public string Title => Entry.Solution?.Title ?? Entry.Source;

    public bool HasSolution => Entry.Solution is not null;

    public int Count => Entry.Count;
    public bool HasMultiple => Entry.Count > 1;
    public string CountText => $"×{Entry.Count}";

    public string MetaLine => $"{Source} · {TimeText} · {ProcessText}";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public void ToggleExpand() => IsExpanded = !IsExpanded;

    /// <summary>Predicat de filtre texte (id, source, message, titre solution).</summary>
    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        query = query.Trim();
        return EventId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
            || Source.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (Solution?.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
