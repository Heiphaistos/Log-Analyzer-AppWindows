using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WinLogAnalyzer.Core.Models;

namespace WinLogAnalyzer.Core.Export;

/// <summary>Exporte un rapport PDF (synthese + detail des events) via QuestPDF.</summary>
public static class PdfExporter
{
    static PdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public static void ToPdf(IReadOnlyList<EventEntry> events, string path, string scope)
    {
        int critical = events.Count(e => e.Level == "Critical");
        int solved = events.Count(e => e.Solution is not null);

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor("#1c2230"));

                page.Header().Column(h =>
                {
                    h.Item().Text("WinLog Analyzer — Rapport de diagnostic")
                        .FontSize(16).Bold().FontColor("#2d4763");
                    h.Item().Text($"Source : {scope}  ·  {DateTime.Now:dd/MM/yyyy HH:mm:ss}  ·  " +
                                  $"{events.Count} events · {critical} critiques · {solved} avec solution")
                        .FontSize(9).FontColor("#666");
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor("#cccccc");
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(8);
                    foreach (var e in events)
                        col.Item().Element(c => RenderEvent(c, e));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);
    }

    private static void RenderEvent(IContainer container, EventEntry e)
    {
        string color = e.Level == "Critical" ? "#ff5c5c" : e.Level == "Error" ? "#d97a1a" : "#4c8dff";

        container.Border(1).BorderColor("#dddddd").Padding(8).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.AutoItem().Text($"#{e.EventId}").Bold().FontSize(11);
                r.AutoItem().PaddingLeft(8).Text(e.Level).Bold().FontColor(color);
                if (e.Count > 1) r.AutoItem().PaddingLeft(6).Text($"×{e.Count}").FontColor("#888");
                r.RelativeItem().PaddingLeft(8).AlignRight()
                    .Text($"{e.Source} · {e.TimeCreated:dd/MM/yyyy HH:mm:ss}").FontColor("#666");
            });

            col.Item().PaddingTop(3).Text(e.Message.Length > 600 ? e.Message[..600] + "…" : e.Message)
                .FontSize(8).FontColor("#444");

            if (e.Solution is { } s)
            {
                col.Item().PaddingTop(5).Background("#eef4fb").Padding(6).Column(sc =>
                {
                    sc.Item().Text($"Solution — {s.Title}").Bold().FontColor("#2d4763");
                    sc.Item().PaddingTop(2).Text(s.Explanation);
                    sc.Item().PaddingTop(2).Text($"Remediation : {s.Remediation}");
                });
            }
        });
    }
}
