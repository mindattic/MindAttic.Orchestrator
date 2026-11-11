using System.Windows.Input;
using MindAttic.Orchestrator.Term.Wpf;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>The relay viewer's paste chord and payload, matching the shipped viewers (M.ORCH-T1291). It used to link the app's
/// src/MindAttic.Orchestrator.Wpf/TerminalPaste.cs, which now depends on TerminalPaneViewModel members and OrchestratorLog this
/// prototype doesn't have; the payload logic lives in the shared RelayInput (linked from MindAttic.Orchestrator.Term.Wpf).</summary>
internal static class TerminalPaste
{
    public static bool IsPasteChord(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        return (key == Key.V && mods == ModifierKeys.Control) || (key == Key.Insert && mods == ModifierKeys.Shift);
    }

    /// <summary>What to send the CLI for pasted <paramref name="text"/>: CRLF and LF as CR, bracketed when asked.</summary>
    internal static string Payload(string text, bool bracketed) => RelayInput.PastePayload(text, bracketed);
}
