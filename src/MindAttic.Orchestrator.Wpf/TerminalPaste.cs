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
/// </summary>
internal static class TerminalPaste
{
    private const string BracketOn = "\u001b[?2004h", BracketOff = "\u001b[?2004l";
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EasyTerminalControl, Mode> Modes = new();

    private sealed class Mode { public bool Bracketed; }

    /// <summary>Follows the pane's CLI turning bracketed paste on and off, once its console has started.</summary>
    public static void Track(TerminalPaneViewModel pane)
    {
        var control = pane.Control;
        var mode = Modes.GetOrCreateValue(control);
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            if (control.ConPTYTerm is not { } term) return;
            timer.Stop();
            var previous = term.InterceptOutputToUITerminal;
            term.InterceptOutputToUITerminal = (ref Span<char> text) =>
            {
                previous?.Invoke(ref text);
                Scan(text, mode);
            };
        };
        timer.Start();
    }

    private static void Scan(ReadOnlySpan<char> text, Mode mode)
    {
        var on = text.LastIndexOf(BracketOn.AsSpan());
        var off = text.LastIndexOf(BracketOff.AsSpan());
        if (on >= 0 || off >= 0) mode.Bracketed = on > off;
    }

    /// <summary>The terminal under keyboard focus, or null.</summary>
    internal static EasyTerminalControl? Focused(DependencyObject root)
    {
        for (var at = Keyboard.FocusedElement as DependencyObject; at is not null; at = System.Windows.Media.VisualTreeHelper.GetParent(at))
            if (at is EasyTerminalControl c) return c;
        return null;
    }

    public static bool IsPasteChord(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        return (key == Key.V && mods == ModifierKeys.Control) || (key == Key.Insert && mods == ModifierKeys.Shift);
    }

    /// <summary>Pastes into the focused terminal when <paramref name="e"/> is a paste chord; marks it handled.</summary>
    public static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !IsPasteChord(e) || sender is not DependencyObject root || Focused(root) is not { } control) return;
        e.Handled = true;
        string text;
        try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
        catch (System.Runtime.InteropServices.ExternalException) { return; }
        if (control.ConPTYTerm is not { } term || Payload(text, Modes.TryGetValue(control, out var m) && m.Bracketed) is not { Length: > 0 } payload) return;
        term.WriteToTerm(payload.AsSpan());
    }

    /// <summary>What to send the CLI for pasted <paramref name="text"/>: CRLF and LF as CR, bracketed when asked.</summary>
    internal static string Payload(string text, bool bracketed)
    {
        if (text.Length == 0) return "";
        text = text.Replace("\r\n", "\r").Replace('\n', '\r');
        return bracketed ? "\u001b[200~" + text + "\u001b[201~" : text;
    }
}
