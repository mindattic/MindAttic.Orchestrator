using EasyWindowsTerminalControl;

namespace MindAttic.Orchestrator.Wpf.ViewModels
{
    /// <summary>Stands in for the app's pane view model, the one type the linked TerminalPaste.cs names (in its Track method,
    /// which the relay viewer never calls).</summary>
    internal sealed class TerminalPaneViewModel
    {
        public EasyTerminalControl Control { get; } = null!;
    }
}
