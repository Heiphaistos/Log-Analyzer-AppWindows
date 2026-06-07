using System.Runtime.InteropServices;
using System.Text;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Tasks;

/// <summary>
/// Decode n'importe quel code d'erreur Windows (HRESULT, Win32, NTSTATUS, code applicatif)
/// en une <see cref="Solution"/> synthetique : message systeme reel + remediation heuristique.
/// Couvre la longue traine non presente dans taskcodes.json.
/// </summary>
public static class Win32ErrorDecoder
{
    private const uint FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;
    private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
    private const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int FormatMessage(uint flags, IntPtr source, uint messageId,
        uint langId, StringBuilder buffer, int size, IntPtr args);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string name, IntPtr reserved, uint flags);

    // Charge ntdll depuis System32 uniquement (anti DLL-planting), une seule fois.
    private static readonly IntPtr Ntdll =
        LoadLibraryEx("ntdll.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);

    /// <summary>Construit une explication + remediation pour tout code (jamais null).</summary>
    public static Solution Describe(int code) => Build(code, SystemMessage((uint)code));

    /// <summary>
    /// Construit la Solution a partir du code et d'un message deja connu (ex: issu de
    /// la base offline errordb.json). Si <paramref name="sysMsg"/> est vide, fallback live.
    /// </summary>
    public static Solution Build(int code, string? sysMsg)
    {
        uint u = (uint)code;
        string hex = "0x" + u.ToString("X8");
        if (string.IsNullOrWhiteSpace(sysMsg)) sysMsg = SystemMessage(u);
        var (kind, facility) = Classify(u);

        string explanation = string.IsNullOrWhiteSpace(sysMsg)
            ? $"Code {kind} {hex}. Aucune description systeme disponible — code probablement specifique a l'application."
            : $"{kind} ({facility}) — {sysMsg}";

        return new Solution
        {
            Title = string.IsNullOrWhiteSpace(sysMsg) ? $"Code {hex} ({kind})" : $"{Trim(sysMsg)} ({hex})",
            Explanation = explanation,
            Remediation = RemediationEngine.Remediate(u),
            Severity = Severity(u)
        };
    }

    /// <summary>
    /// Cherche un code d'erreur (HRESULT/NTSTATUS, 0x8/0xC/0xD/0x4 + 7 hex) dans un texte
    /// libre (message d'event) et le decode. Null si aucun code plausible.
    /// </summary>
    public static Solution? TryDecodeFromText(string? text, ErrorDatabase? db = null)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var m = System.Text.RegularExpressions.Regex.Match(
            text, "0x[8CDcd4][0-9A-Fa-f]{7}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        if (!uint.TryParse(m.Value.AsSpan(2),
                System.Globalization.NumberStyles.HexNumber, null, out uint code))
            return null;
        if (!IsFailure(code)) return null;

        var sol = db?.Lookup((int)code) ?? Describe((int)code);
        return sol with { Title = $"Code detecte dans le message — {sol.Title}" };
    }

    /// <summary>Un code represente-t-il un echec ? (bit de severite + cas benins connus).</summary>
    public static bool IsFailure(uint u)
    {
        if (u == 0) return false;                       // succes
        if (u >= 0x00041300 && u <= 0x0004131F) return false; // SCHED_S_* (succes/etat)
        if (u == 0x800710E0) return false;              // conditions non remplies (benin)
        if ((u & 0x80000000) == 0) return false;        // bit de severite a 0 = succes/info
        return true;
    }

    private static (string kind, string facility) Classify(uint u)
    {
        // NTSTATUS : severite haute + facilites specifiques (0xC=erreur, 0x4=warning...).
        byte sevTop = (byte)(u >> 30);
        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000)
            return ("NTSTATUS", "noyau/driver");

        // HRESULT
        if ((u & 0x80000000) != 0 || (u & 0x40000000) != 0)
        {
            uint fac = (u >> 16) & 0x1FFF;
            string facName = fac switch
            {
                0 => "general",
                7 => "Win32",
                1 => "RPC",
                3 => "Stockage",
                4 => "Interface COM",
                10 => "Securite/SSPI",
                0x10 => "Configuration",
                0x100 => "MSI/Installation",
                _ => $"facilite {fac}"
            };
            return ("HRESULT", facName);
        }

        if (u <= 0xFFFF) return ("Win32", "systeme");
        return ("applicatif", "specifique a l'app");
    }

    private static string SystemMessage(uint u)
    {
        // 1) Win32 pur (code court) ou HRESULT_FROM_WIN32 (0x8007xxxx) -> message systeme.
        uint win32 = (u & 0xFFFF0000) == 0x80070000 ? (u & 0xFFFF) : u;
        if (win32 <= 0xFFFF)
        {
            var m = FromSource(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, win32);
            if (!string.IsNullOrWhiteSpace(m)) return m;
        }

        // 2) HRESULT complet via le systeme.
        var direct = FromSource(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, u);
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        // 3) NTSTATUS via ntdll (handle cache, charge depuis System32).
        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000 or 0x40000000 && Ntdll != IntPtr.Zero)
        {
            var m = FromSource(FORMAT_MESSAGE_FROM_HMODULE, Ntdll, u);
            if (!string.IsNullOrWhiteSpace(m)) return m;
        }
        return "";
    }

    private static string FromSource(uint baseFlags, IntPtr module, uint id)
    {
        uint flags = baseFlags | FORMAT_MESSAGE_IGNORE_INSERTS;
        var sb = new StringBuilder(8192);
        int len = FormatMessage(flags, module, id, 0, sb, sb.Capacity, IntPtr.Zero);
        if (len == 0 && Marshal.GetLastWin32Error() == 122) // ERROR_INSUFFICIENT_BUFFER
        {
            sb = new StringBuilder(65535);
            len = FormatMessage(flags, module, id, 0, sb, sb.Capacity, IntPtr.Zero);
        }
        return len > 0 ? sb.ToString().Trim() : "";
    }

    private static string Severity(uint u)
    {
        if (!IsFailure(u)) return "info";
        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000) return "critical"; // NTSTATUS fatal
        return "error";
    }

    private static string Trim(string s) => s.Length > 80 ? s[..80].TrimEnd() + "…" : s;
}
