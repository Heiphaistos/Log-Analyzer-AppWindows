using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinLogAnalyzer.App.Infrastructure;

namespace WinLogAnalyzer.App.Views;

public partial class EventsView : UserControl
{
    public EventsView() => InitializeComponent();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir le lien :\n{ex.Message}",
                "WinLog Analyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }

    // ===== Outils de diagnostic (F4) =====
    private void OpenServices(object sender, RoutedEventArgs e) => ActionRunner.Open("services.msc");

    private void OpenReliability(object sender, RoutedEventArgs e)
        => ActionRunner.Open("perfmon", "/rel");

    private void OpenEventVwr(object sender, RoutedEventArgs e) => ActionRunner.Open("eventvwr.msc");

    private void OpenMemDiag(object sender, RoutedEventArgs e)
        => ActionRunner.RunWithConfirm(
            "Diagnostic memoire Windows",
            "Cet outil propose de redemarrer le PC pour tester la RAM. Enregistre ton travail avant.",
            "mdsched.exe");
}
