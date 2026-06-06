using System.Diagnostics;
using System.Windows;

namespace WinLogAnalyzer.App.Infrastructure;

/// <summary>
/// Lance des outils Windows de diagnostic. Les actions a effet de bord (reboot, scan disque)
/// demandent une confirmation explicite. Aucune action destructive automatique.
/// </summary>
public static class ActionRunner
{
    /// <summary>Lance un outil sans confirmation (ouverture d'une console d'admin).</summary>
    public static void Open(string fileName, string? args = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args ?? "",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de lancer '{fileName}' :\n{ex.Message}",
                "WinLog Analyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Lance une action a effet de bord apres confirmation utilisateur.</summary>
    public static void RunWithConfirm(string title, string warning, string fileName, string? args = null)
    {
        var r = MessageBox.Show(
            warning + "\n\nContinuer ?",
            title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r == MessageBoxResult.Yes)
            Open(fileName, args);
    }
}
