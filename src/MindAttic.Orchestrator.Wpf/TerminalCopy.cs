using System.Windows;
using System.Windows.Input;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Ctrl+C in the embedded terminal that has keyboard focus copies its selected text to the clipboard (M.ORCH-T867). The native
/// terminal doesn't copy on its own: a key clears its selection and goes to the CLI. So the chord is taken before the terminal
/// sees it, only when there is a selection: <c>TerminalControl.GetSelectedText</c> reads (and clears) it. With no selection, or
/// one that can't be read, the key is left alone and reaches the CLI as its interrupt (^C), as it always did.
/// <para>Ctrl+X cuts the selection (M.ORCH-T866). A terminal's screen text can't be removed, so a cut copies the selection and
/// clears it (reading it clears it); with no selection Ctrl+X goes to the CLI unchanged. Only plain Ctrl+C and Ctrl+X;
/// Ctrl+Shift+C and the like still go to the CLI.</para>
/// </summary>
internal static class TerminalCopy
{
    /// <summary>What a chord asks of the terminal's selection.</summary>
    internal enum Chord { None, Copy, Cut }

    internal static Chord ChordOf(Key key, ModifierKeys modifiers) =>
        modifiers != ModifierKeys.Control ? Chord.None : key switch { Key.C => Chord.Copy, Key.X => Chord.Cut, _ => Chord.None };

    /// <summary>Takes the selection with <paramref name="readSelection"/> and puts it on the clipboard with
    /// <paramref name="setClipboard"/>, for a copy or a cut alike (reading it clears it). True when there was a selection (the key is then handled, even if the clipboard was
    /// busy: the selection is spent and the person asked to copy, not to interrupt); false when there's none or it couldn't be
    /// read, so the key goes on to the CLI.</summary>
    internal static bool TryTake(Func<string?> readSelection, Action<string> setClipboard)
    {
        string? text;
        try { text = readSelection(); }
        catch (Exception) { return false; }
        if (string.IsNullOrEmpty(text)) return false;
        try { setClipboard(text); }
        catch (Exception) { }
        return true;
    }

    public static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.Handled) return;
            // The tab the key went to, not the one Keyboard.FocusedElement names (null while the renderer has Win32 focus, M.ORCH-T1109).
            var pane = TerminalPaste.Focused(e);
            var chord = ChordOf(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
            if (chord == Chord.None) { if (pane is not null) pane.SwallowCopyRepeats = false; return; }
            if (pane is not { } p) return;
            if (e.IsRepeat && p.SwallowCopyRepeats) { e.Handled = true; return; }
            p.SwallowCopyRepeats = false;
            // The selection is local to the renderer, hosted tab (M.ORCH-T926) or in-process alike.
            if (!TryTake(p.ReadSelection, SetClipboard)) return;
            p.SwallowCopyRepeats = true;
            e.Handled = true;
        }
        catch (Exception)
        {
            // Never let a copy fault take the dashboard down; an unhandled key still reaches the CLI.
        }
    }

    private static void SetClipboard(string text) => Clipboard.SetDataObject(text, copy: true);
}
