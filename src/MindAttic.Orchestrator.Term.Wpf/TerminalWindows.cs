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

    /// <summary>Whether <paramref name="root"/> is in a window and every native renderer window under it still exists. Calling
    /// into a renderer whose window is gone is a native access violation no catch can contain (M.ORCH-T1613).</summary>
    public static bool AreLive(FrameworkElement root)
    {
        try
        {
            if (!root.IsLoaded || PresentationSource.FromVisual(root) is null) return false;
            var hosts = Descendants<HwndHost>(root).ToList();
            return hosts.Count > 0 && hosts.All(h => h.Handle != IntPtr.Zero);
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Checking a terminal's windows failed; contained");
            return false;
        }
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
