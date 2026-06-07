using System.Text.Json;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Tasks;

/// <summary>
/// Base offline exhaustive des codes d'erreur Windows (errordb.json, ~11 000 entrees :
/// Win32, HRESULT, NTSTATUS, Windows Update). Generee par tools/ErrorDbGen.
/// Fournit le message systeme meme pour des codes absents de la machine courante.
/// </summary>
public sealed class ErrorDatabase
{
    private readonly Dictionary<string, string> _map;

    public int Count => _map.Count;

    public ErrorDatabase(string jsonPath)
    {
        _map = Load(jsonPath);
    }

    /// <summary>Solution construite depuis la base (null si code absent).</summary>
    public Solution? Lookup(int code)
    {
        var msg = Message(code);
        return msg is null ? null : Win32ErrorDecoder.Build(code, msg);
    }

    /// <summary>Message brut pour un code (gere le mapping HRESULT_FROM_WIN32 0x8007xxxx).</summary>
    public string? Message(int code)
    {
        uint u = (uint)code;
        if (_map.TryGetValue("0x" + u.ToString("X8"), out var m)) return m;

        // HRESULT enveloppant un code Win32 -> chercher le code court.
        if ((u & 0xFFFF0000) == 0x80070000 &&
            _map.TryGetValue("0x" + (u & 0xFFFF).ToString("X8"), out var w)) return w;

        return null;
    }

    private static Dictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[WARN] {path} absent. Base d'erreurs vide.");
            return new(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            return new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Parsing {path}: {ex.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
