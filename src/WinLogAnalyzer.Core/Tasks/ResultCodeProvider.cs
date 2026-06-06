using System.Text.Json;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Tasks;

/// <summary>
/// Traduit les codes de resultat des taches planifiees (HRESULT) en explication + remediation.
/// Charge depuis data/taskcodes.json (cle = code hex en minuscules, ex "0x80070002").
/// </summary>
public sealed class ResultCodeProvider
{
    private readonly Dictionary<string, Solution> _map;

    public int Count => _map.Count;

    public ResultCodeProvider(string jsonPath)
    {
        _map = Load(jsonPath);
    }

    public Solution? Lookup(int code)
    {
        string hex = "0x" + ((uint)code).ToString("X8");
        return _map.TryGetValue(hex, out var s) ? s : null;
    }

    public static string ToHex(int code) => "0x" + ((uint)code).ToString("X8");

    /// <summary>Un code != 0 est considere comme un echec (sauf 0x41301 = en cours).</summary>
    public static bool IsFailure(int code) => code != 0 && (uint)code != 0x00041301;

    private static Dictionary<string, Solution> Load(string path)
    {
        if (!File.Exists(path))
            return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, Solution>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return new Dictionary<string, Solution>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Parsing {path} echoue: {ex.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
