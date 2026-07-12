using System.Text;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Export;

/// <summary>Exporte une liste d'events en CSV (donnees brutes) ou HTML (rapport stylé).</summary>
public static class ReportExporter
{
    public static void ToCsv(IEnumerable<EventEntry> events, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("EventId;Level;Source;TimeCreated;ProcessId;ProcessName;Count;HasSolution;Message");
        foreach (var e in events)
        {
            sb.Append(e.EventId).Append(';')
              .Append(Csv(e.Level)).Append(';')
              .Append(Csv(e.Source)).Append(';')
              .Append(e.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")).Append(';')
              .Append(e.ProcessId?.ToString() ?? "").Append(';')
              .Append(Csv(e.ProcessName)).Append(';')
              .Append(e.Count).Append(';')
              .Append(e.Solution is not null ? "oui" : "non").Append(';')
              .Append(Csv(e.Message))
              .AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void ToHtml(IEnumerable<EventEntry> events, string path, string logName)
    {
        var list = events.ToList();
        int critical = list.Count(e => e.Level == "Critical");
        int solved = list.Count(e => e.Solution is not null);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<title>Rapport WinLog Analyzer</title><style>");
        sb.AppendLine("body{font-family:Segoe UI,sans-serif;background:#0d1117;color:#e6edf3;padding:30px;}");
        sb.AppendLine("h1{font-size:22px;}.meta{color:#8b949e;font-size:13px;margin-bottom:20px;}");
        sb.AppendLine(".card{background:#1c2230;border:1px solid #2a3140;border-radius:10px;padding:16px;margin:0 0 12px;}");
        sb.AppendLine(".id{font-weight:bold;font-size:15px;}.lvl{font-size:11px;padding:3px 9px;border-radius:99px;margin-left:8px;}");
        sb.AppendLine(".critical{color:#ff5c5c;border:1px solid #ff5c5c;}.error{color:#ff9f40;border:1px solid #ff9f40;}");
        sb.AppendLine(".sol{background:#1a2433;border:1px solid #2d4763;border-radius:8px;padding:12px;margin-top:10px;}");
        sb.AppendLine(".dim{color:#8b949e;font-size:12px;}pre{white-space:pre-wrap;font-size:12px;color:#8b949e;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>⚡ Rapport WinLog Analyzer</h1>");
        sb.AppendLine($"<div class=\"meta\">Journal : {Html(logName)} · Genere le {DateTime.Now:dd/MM/yyyy HH:mm:ss} · " +
                      $"{list.Count} events · {critical} critiques · {solved} avec solution</div>");

        foreach (var e in list)
        {
            string lvlClass = e.Level == "Critical" ? "critical" : "error";
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine($"<span class=\"id\">{e.EventId}</span>" +
                          $"<span class=\"lvl {lvlClass}\">{Html(e.Level)}</span>" +
                          (e.Count > 1 ? $"<span class=\"dim\"> ×{e.Count}</span>" : ""));
            sb.AppendLine($"<div class=\"dim\">{Html(e.Source)} · {e.TimeCreated:dd/MM/yyyy HH:mm:ss} · " +
                          $"{(e.ProcessId is int p ? $"PID {p} · {Html(e.ProcessName)}" : "PID inconnu")}</div>");
            sb.AppendLine($"<pre>{Html(e.Message)}</pre>");
            if (e.Solution is { } s)
            {
                sb.AppendLine("<div class=\"sol\">");
                sb.AppendLine($"<b>💡 {Html(s.Title)}</b>");
                sb.AppendLine($"<p>{Html(s.Explanation)}</p>");
                sb.AppendLine($"<p><b>Remediation :</b><br>{Html(s.Remediation).Replace("\n", "<br>")}</p>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string? v)
    {
        v ??= "";
        // Anti-injection de formule (OWASP) : un message d'event peut contenir du texte
        // controle par un tiers ; = + - @ TAB en tete seraient evalues par Excel.
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t')
            v = "'" + v;
        if (v.Contains(';') || v.Contains('"') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        return v;
    }

    private static string Html(string? v) => (v ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
