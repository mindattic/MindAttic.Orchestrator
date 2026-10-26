using System.Windows;
using System.Windows.Controls;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The element a Terminals tab puts in the panel's host grid (M.ORCH-T926): it holds the tab's terminal for the tab's whole
/// life, a <see cref="RelayTerminal"/> for a hosted CLI or, as the fallback, an in-process EasyTerminalControl, and can swap
/// one for the other (a host that fails to start) without the window's grid noticing. Focus on it goes on to the terminal.
/// </summary>
internal sealed class TerminalView : Border
{
    public TerminalView()
    {
        Focusable = true;
        GotFocus += (_, e) => { if (ReferenceEquals(e.OriginalSource, this) && Child is UIElement inner) inner.Focus(); };
    }

    /// <summary>Disposes every native window (HwndHost) under <paramref name="root"/>, once it is out of the window. Never throws.</summary>
    internal static void DisposeWindows(DependencyObject root) => TerminalWindows.DisposeAll(root);
}
