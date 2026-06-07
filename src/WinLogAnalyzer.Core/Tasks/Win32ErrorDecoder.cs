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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int FormatMessage(uint flags, IntPtr source, uint messageId,
        uint langId, StringBuilder buffer, int size, IntPtr args);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string name);

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
            Remediation = Remediation(u, kind),
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

        // 3) NTSTATUS via ntdll.
        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000 or 0x40000000)
        {
            var nt = LoadLibrary("ntdll.dll");
            if (nt != IntPtr.Zero)
            {
                var m = FromSource(FORMAT_MESSAGE_FROM_HMODULE, nt, u);
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }
        }
        return "";
    }

    private static string FromSource(uint baseFlags, IntPtr module, uint id)
    {
        var sb = new StringBuilder(2048);
        int len = FormatMessage(baseFlags | FORMAT_MESSAGE_IGNORE_INSERTS,
            module, id, 0, sb, sb.Capacity, IntPtr.Zero);
        return len > 0 ? sb.ToString().Trim() : "";
    }

    private static string Remediation(uint u, string kind)
    {
        uint w = (u & 0xFFFF0000) == 0x80070000 ? (u & 0xFFFF) : u;

        return w switch
        {
            2 or 3 => "1. Verifier le chemin du programme/script (onglet Actions). 2. Guillemets si espaces. 3. Verifier que le fichier existe.",
            5 => "1. 'Executer avec les autorisations maximales' (General). 2. Verifier le compte d'execution. 3. Verifier les ACL du fichier cible.",
            0x20 => "1. Fichier verrouille par un autre processus (Resource Monitor). 2. Planifier quand le fichier est libre.",
            0x57 => "1. Verifier les arguments passes a l'action (parametre invalide).",
            0x102 or 0x5B4 => "1. Action trop longue/bloquee. 2. Augmenter le delai (Parametres). 3. Optimiser le script.",
            0x4C7 => "1. Operation annulee — relancer si involontaire.",
            _ when kind == "NTSTATUS" => "1. Plantage bas-niveau du programme. 2. Mettre a jour l'app et les drivers. 3. Tester RAM (mdsched) / disque (chkdsk).",
            _ when u == 0xE0434352 => "1. Exception .NET non geree : voir le journal Application (Event 1026). 2. Mettre a jour l'app et le .NET Runtime.",
            _ when ((u >> 16) & 0x1FFF) == 4 => "1. Composant COM en cause : reinstaller/reparer le logiciel. 2. Re-enregistrer la DLL (regsvr32). 3. Verifier 32/64 bits.",
            _ => "1. Executer l'action manuellement pour observer l'erreur. 2. Consulter les logs de l'application. 3. Verifier chemins, droits et dependances."
        };
    }

    private static string Severity(uint u)
    {
        if (!IsFailure(u)) return "info";
        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000) return "critical"; // NTSTATUS fatal
        return "error";
    }

    private static string Trim(string s) => s.Length > 80 ? s[..80].TrimEnd() + "…" : s;
}
