using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EasyWindowsTerminalControl;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Ctrl+V and Shift+Insert paste the clipboard into the CLI of the embedded terminal that has keyboard focus (M.ORCH-T868). The
/// terminal control doesn't paste on its own, so the text is written straight to the CLI's input: newlines as CR, wrapped in
/// bracketed-paste markers when the CLI turned that mode on (Claude Code does), so a multi-line paste lands in the prompt
/// instead of submitting at its first line.
/// <para>A hosted tab (M.ORCH-T926) writes through its relay connection, and knows the bracketed mode from its TermHost's
/// Modes frames, so a reattached tab knows it at once; an in-process tab writes to its ConPTY and reads the mode from its
/// output (<see cref="Track"/>).</para>
/// </summary>
internal static class TerminalPaste
{
    private const string BracketOn = "\u001b[?2004h", BracketOff = "\u001b[?2004l";

    /// <summary>Follows an in-process tab's CLI turning bracketed paste on and off, once its console has started.</summary>
    public static void Track(TerminalPaneViewModel pane, EasyTerminalControl control)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            if (control.ConPTYTerm is not { } term) return;
            timer.Stop();
            var previous = term.InterceptOutputToUITerminal;
            term.InterceptOutputToUITerminal = (ref Span<char> text) =>
            {
                previous?.Invoke(ref text);
                if (Scan(text) is { } on) pane.SetLocalBracketed(on);
            };
        };
        timer.Start();
    }

    /// <summary>Whether <paramref name="text"/> last turned bracketed paste on (true) or off (false); null when it says neither.</summary>
    internal static bool? Scan(ReadOnlySpan<char> text)
    {
        var on = text.LastIndexOf(BracketOn.AsSpan());
        var off = text.LastIndexOf(BracketOff.AsSpan());
        return on >= 0 || off >= 0 ? on > off : null;
    }

    /// <summary>The terminal tab under keyboard focus, or null.</summary>
    internal static TerminalPaneViewModel? Focused(DependencyObject root) =>
        Keyboard.FocusedElement is DependencyObject focused ? TerminalPaneViewModel.Of(focused) : null;

    public static bool IsPasteChord(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        return (key == Key.V && mods == ModifierKeys.Control) || (key == Key.Insert && mods == ModifierKeys.Shift);
    }

    /// <summary>Pastes into the focused terminal when <paramref name="e"/> is a paste chord; marks it handled.</summary>
    public static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.Handled || !IsPasteChord(e) || sender is not DependencyObject root || Focused(root) is not { } pane) return;
            e.Handled = true;
            string text;
            try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
            catch (System.Runtime.InteropServices.ExternalException) { return; }
            if (Payload(text, pane.Bracketed) is { Length: > 0 } payload) pane.WriteInput(payload);
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Pasting into a terminal failed; contained"); }
    }

    /// <summary>What to send the CLI for pasted <paramref name="text"/>: CRLF and LF as CR, bracketed when asked.</summary>
    internal static string Payload(string text, bool bracketed) => RelayInput.PastePayload(text, bracketed);
}
