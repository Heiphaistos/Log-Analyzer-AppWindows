using System.Text.Json;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Knowledge;

/// <summary>
/// Charge solutions.json en memoire (dictionnaire Event ID -> Solution).
/// Edition du JSON sans recompilation. Map vide si fichier absent (degradation propre).
/// </summary>
public sealed class SolutionProvider
{
    private readonly Dictionary<int, Solution> _map;

    public int Count => _map.Count;

    public SolutionProvider(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            _map = new();
            Console.Error.WriteLine($"[WARN] {jsonPath} absent. Dictionnaire vide.");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, Solution>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new();

            _map = raw.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
        }
        catch (Exception ex)
        {
            // JSON corrompu -> on log et on demarre avec dictionnaire vide.
            Console.Error.WriteLine($"[ERROR] Parsing {jsonPath} echoue: {ex.Message}");
            _map = new();
        }
    }

    public Solution? Lookup(int eventId)
        => _map.TryGetValue(eventId, out var s) ? s : null;
}
