using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>Releases a terminal's native renderer windows (M.ORCH-T926): a removed tab or a closed viewer must free its HwndHost.</summary>
public static class TerminalWindows
{
    /// <summary>Disposes every native window (HwndHost) under <paramref name="root"/>, once it is out of the window. Never throws.</summary>
    public static void DisposeAll(DependencyObject root)
    {
        try
        {
            foreach (var host in Descendants<HwndHost>(root).ToList())
                try { host.Dispose(); }
                catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Releasing a terminal window failed; contained"); }
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Releasing a terminal's windows failed; contained"); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = parent is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(parent) : 0;
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) { yield return found; continue; }
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
