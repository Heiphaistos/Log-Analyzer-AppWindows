namespace WinLogAnalyzer.App.ViewModels;

/// <summary>Une barre d'histogramme (events par jour) pour la timeline.</summary>
public sealed record TimelineBar
{
    public required string Label { get; init; }
    public required int Count { get; init; }
    public required double Height { get; init; }   // pixels (proportionnel au max)
    public required bool HasCritical { get; init; }
    public string Tooltip => $"{Label} — {Count} event(s)";
}
