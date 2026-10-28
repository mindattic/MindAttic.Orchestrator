using System.Windows;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow
{
    /// <summary>The top row's People button (M.ORCH-T936): opens the Team Wall, or brings it forward.</summary>
    private void TeamWallButton_Click(object sender, RoutedEventArgs e) => TeamWallWindow.Open();
}
