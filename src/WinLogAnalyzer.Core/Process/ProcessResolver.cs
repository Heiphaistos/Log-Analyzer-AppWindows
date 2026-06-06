using System.Diagnostics;

namespace WinLogAnalyzer.Core.Process;

/// <summary>
/// Traduit un PID en nom de process. Cache par requete pour eviter appels repetes.
/// Un PID dont le process est mort renvoie "[termine]" (cas nominal, pas une erreur).
/// </summary>
public sealed class ProcessResolver
{
    private readonly Dictionary<int, string> _cache = new();

    public string Resolve(int? processId)
    {
        if (processId is null or 0)
            return "[inconnu]";

        int pid = processId.Value;
        if (_cache.TryGetValue(pid, out var cached))
            return cached;

        string name;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process mort depuis l'ecriture du log.
            name = "[termine]";
        }
        catch (InvalidOperationException)
        {
            name = "[termine]";
        }

        _cache[pid] = name;
        return name;
    }
}
