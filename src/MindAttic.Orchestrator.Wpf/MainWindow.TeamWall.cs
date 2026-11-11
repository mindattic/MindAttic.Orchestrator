using System.Windows;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow
{
    /// <summary>The top row's People button (M.ORCH-T936): opens the Team Wall, or brings it forward. Its tiles' activity strips
    /// (M.ORCH-T965) read the agent row each CLI tab's status read already matched.</summary>
    private void TeamWallButton_Click(object sender, RoutedEventArgs e) => TeamWallWindow.Open(activityOf: ActivityOf);

    /// <summary>The agent row of this machine's hosted session <paramref name="termId"/>, as its tab last matched it; null for none.</summary>
    private AgentBoardRow? ActivityOf(string termId)
    {
        foreach (var pane in model.AllTerminals.Panes)
            if (string.Equals(pane.TermId, termId, StringComparison.Ordinal) || string.Equals(pane.Session?.Id, termId, StringComparison.Ordinal))
                return pane.StatusRow;
        return null;
    }
}
