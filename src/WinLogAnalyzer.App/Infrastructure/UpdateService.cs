using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using WinLogAnalyzer.Core.Logging;
using WinLogAnalyzer.Core.Settings;
using WinLogAnalyzer.Core.Update;

namespace WinLogAnalyzer.App.Infrastructure;

/// <summary>Planifie les verifications (demarrage + toutes les 6 h) et affiche le dialogue.</summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly AppUpdater _updater;
    private readonly Version _current;
    private readonly AppSettings _settings;
    private readonly FileLogger _logger;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(6) };
    private string? _declined; // version refusee : pas de rappel automatique avant la prochaine
    private bool _busy;

    public UpdateService(AppSettings settings, FileLogger logger)
    {
        _settings = settings;
        _logger = logger;
        _current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WinLogAnalyzer-Updater");
        _updater = new AppUpdater(_http, _current);
        _timer.Tick += (_, _) => { if (_settings.AutoUpdateCheck) _ = CheckAsync(false); };
    }

    public void Start()
    {
        _timer.Start();
        if (_settings.AutoUpdateCheck) _ = CheckAsync(false);
    }

    /// <summary>manual = bouton : informe aussi quand il n'y a rien ou en cas d'erreur.</summary>
    public async Task CheckAsync(bool manual)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var info = await _updater.CheckAsync();
            if (info is null)
            {
                if (manual) Info("Vous utilisez déjà la dernière version.");
                return;
            }
            if (!manual && _declined == info.Version.ToString()) return;
            _logger.Info($"Mise a jour disponible : {info.Version}");
            var win = new UpdateWindow(_updater, info, _current) { Owner = Application.Current.MainWindow };
            win.ShowDialog();
            _declined = info.Version.ToString();
        }
        catch (Exception ex) when (ex is UpdateException or OperationCanceledException)
        {
            _logger.Warn($"Verification de mise a jour : {ex.Message}");
            if (manual) Info($"Impossible de vérifier les mises à jour.\n\n{ex.Message}");
        }
        finally { _busy = false; }
    }

    private static void Info(string text)
        => MessageBox.Show(Application.Current.MainWindow, text, "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }
}
