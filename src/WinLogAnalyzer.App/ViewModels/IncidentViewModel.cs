using System.Collections.ObjectModel;
using WinLogAnalyzer.App.Infrastructure;
using WinLogAnalyzer.Core.Diagnostics;

namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Enveloppe un Incident (grappe d'events correles) pour l'affichage.</summary>
public sealed class IncidentViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public IncidentViewModel(Incident incident)
    {
        Incident = incident;
        Events = new ObservableCollection<EventItemViewModel>(
            incident.Events.Select(e => new EventItemViewModel(e)));
    }

    public Incident Incident { get; }
    public ObservableCollection<EventItemViewModel> Events { get; }

    public string Summary => Incident.Summary;
    public int CriticalCount => Incident.CriticalCount;
    public string DisplayLevel => Incident.CriticalCount > 0 ? "Critical" : "Error";

    /// <summary>Liste des Event IDs distincts impliques (cause racine probable en tete).</summary>
    public string Signature => string.Join(" + ",
        Incident.Events.Select(e => e.EventId).Distinct().Take(6));

    public bool IsExpanded { get => _isExpanded; set => SetField(ref _isExpanded, value); }
}
