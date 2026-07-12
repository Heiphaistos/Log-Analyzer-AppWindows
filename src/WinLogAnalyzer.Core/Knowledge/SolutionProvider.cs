using System.Text.Json;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Knowledge;

/// <summary>
/// Charge solutions.json en memoire (cle -> Solution). La cle peut etre un Event ID brut
/// ("41") ou composite ("Microsoft-Windows-Kernel-Power:41") pour lever les collisions.
/// Supporte le hot-reload : toute modif du fichier recharge la map et leve <see cref="Changed"/>.
/// </summary>
public sealed class SolutionProvider : IDisposable
{
    private readonly string _path;
    private volatile Dictionary<string, Solution> _map;
    private FileSystemWatcher? _watcher;
    private readonly object _reloadGate = new();
    private DateTime _lastReload = DateTime.MinValue;

    /// <summary>Leve apres un rechargement a chaud du fichier.</summary>
    public event Action? Changed;

    public int Count => _map.Count;

    public SolutionProvider(string jsonPath, bool hotReload = false)
    {
        _path = jsonPath;
        _map = Load(jsonPath);
        if (hotReload) EnableHotReload();
    }

    /// <summary>
    /// Recherche : tente d'abord la cle composite "source:id", puis l'Event ID seul.
    /// </summary>
    public Solution? Lookup(int eventId, string? source = null)
    {
        var map = _map;
        if (!string.IsNullOrEmpty(source) &&
            map.TryGetValue($"{source}:{eventId}", out var composite))
            return composite;

        return map.TryGetValue(eventId.ToString(), out var s) ? s : null;
    }

    private static Dictionary<string, Solution> Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[WARN] {path} absent. Dictionnaire vide.");
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, Solution>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new();
            return new Dictionary<string, Solution>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Parsing {path} echoue: {ex.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void EnableHotReload()
    {
        var dir = Path.GetDirectoryName(_path);
        var file = Path.GetFileName(_path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce : les editeurs declenchent plusieurs evenements rapproches.
        // Lock : le FileSystemWatcher peut lever depuis plusieurs threads (C5 audit).
        lock (_reloadGate)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastReload).TotalMilliseconds < 400) return;
            _lastReload = now;
        }

        try
        {
            Thread.Sleep(150); // laisser l'ecriture se terminer
            _map = Load(_path);
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Hot-reload echoue: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
