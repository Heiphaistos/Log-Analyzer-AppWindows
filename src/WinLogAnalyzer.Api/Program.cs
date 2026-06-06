using System.Diagnostics;
using WinLogAnalyzer.Core.Knowledge;
using WinLogAnalyzer.Core.Process;
using WinLogAnalyzer.Core.Reader;

var builder = WebApplication.CreateBuilder(args);

// Agent local : bind 127.0.0.1 uniquement. Jamais expose sur le reseau (logs sensibles).
const string Url = "http://127.0.0.1:5099";
builder.WebHost.UseUrls(Url);

builder.Services.AddControllers();

// Dictionnaire de solutions : singleton (charge une fois au demarrage).
var solutionsPath = Path.Combine(AppContext.BaseDirectory, "data", "solutions.json");
builder.Services.AddSingleton(new SolutionProvider(solutionsPath));

// Resolver + lecteur : scoped -> cache PID frais a chaque requete.
builder.Services.AddScoped<ProcessResolver>();
builder.Services.AddScoped<EventLogService>();

var app = builder.Build();

app.UseDefaultFiles();   // sert wwwroot/index.html
app.UseStaticFiles();    // assets frontend
app.MapControllers();

// Ouvre le navigateur au demarrage (UX agent local).
OpenBrowser(Url);

app.Run();

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
    catch (Exception ex)
    {
        // Echec non bloquant : l'utilisateur peut ouvrir l'URL manuellement.
        Console.Error.WriteLine($"[WARN] Ouverture navigateur echouee: {ex.Message}");
        Console.WriteLine($"Ouvrir manuellement : {url}");
    }
}
