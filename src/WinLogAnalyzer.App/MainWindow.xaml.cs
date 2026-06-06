using System.Windows;
using WinLogAnalyzer.App.ViewModels;

namespace WinLogAnalyzer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Analyses initiales au demarrage.
        Loaded += (_, _) =>
        {
            _vm.Events.AnalyzeCommand.Execute(null);
            _ = _vm.Tasks.LoadAsync();
        };

        Closed += (_, _) => _vm.Dispose();
    }
}
