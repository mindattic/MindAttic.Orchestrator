using System.IO;
using System.Windows;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The sidebar remembers its layout across restarts (M.ORCH-T2179): the order of its draggable panels
/// (<see cref="OrchestratorSettings.SidebarPanelOrder"/>) and which panels, nested ticket ones included, are collapsed
/// (<see cref="OrchestratorSettings.SidebarCollapsedPanels"/>), each by its MaPanelGroup.PanelKey. Applied once at start; a
/// change made elsewhere applies at the next start.
/// </summary>
public partial class MainWindow
{
    private void AttachSidebarPanels() => AttachSidebarPanels(TopRow, model.Store);

    /// <summary>Applies the saved order and collapsed panels to <paramref name="group"/>, then saves each change the user
    /// makes, one write per action however many panels it touched.</summary>
    internal static void AttachSidebarPanels(MaPanelGroup group, JsonStore store)
    {
        var panels = KeyedPanels(group).ToList();
        var (order, collapsed) = SavedSidebarLayout(store);
        foreach (var panel in panels)
            if (panel.IsCollapsible && collapsed.Contains(MaPanelGroup.GetPanelKey(panel)!))
                panel.SetCurrentValue(MaPanel.IsExpandedProperty, false);
        if (order.Count > 0) group.ApplyOrder(order);

        var queued = false;
        void Queue()
        {
            if (queued) return;
            queued = true;
            group.Dispatcher.BeginInvoke(() =>
            {
                queued = false;
                SaveSidebarLayout(store, group.Order,
                    panels.Where(p => !p.IsExpanded).Select(p => MaPanelGroup.GetPanelKey(p)!).ToList());
            }, DispatcherPriority.Background);
        }
        group.OrderChanged += (_, _) => Queue();
        foreach (var panel in panels)
        {
            panel.Expanded += (_, _) => Queue();
            panel.Collapsed += (_, _) => Queue();
        }
    }

    /// <summary>Every MaPanel under <paramref name="root"/> (declared, so in the logical tree) that has a PanelKey.</summary>
    private static IEnumerable<MaPanel> KeyedPanels(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is MaPanel panel && !string.IsNullOrEmpty(MaPanelGroup.GetPanelKey(panel))) yield return panel;
            foreach (var nested in KeyedPanels(child)) yield return nested;
        }
    }

    /// <summary>The saved order and collapsed panels; none of either when what's saved can't be read.</summary>
    internal static (List<string> Order, HashSet<string> Collapsed) SavedSidebarLayout(JsonStore store)
    {
        try
        {
            var settings = store.LoadSettings();
            return ([.. settings.SidebarPanelOrder], [.. settings.SidebarCollapsedPanels]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return ([], []); }
    }

    /// <summary>Saves the sidebar's <paramref name="order"/> (kept as saved when null: no panel has moved) and its
    /// <paramref name="collapsed"/> panels; false (nothing changed) when the settings can't be written, the layout itself
    /// staying as it is for this run.</summary>
    internal static bool SaveSidebarLayout(JsonStore store, IEnumerable<string>? order, IReadOnlyCollection<string> collapsed)
    {
        try
        {
            // LoadSettingsToChange: a settings row that can't be read is never saved over with defaults for the sake of a layout.
            var settings = store.LoadSettingsToChange();
            var newOrder = order?.ToList() ?? settings.SidebarPanelOrder;
            if (newOrder.SequenceEqual(settings.SidebarPanelOrder) && collapsed.SequenceEqual(settings.SidebarCollapsedPanels)) return true;
            store.SaveSettings(settings with { SidebarPanelOrder = newOrder, SidebarCollapsedPanels = [.. collapsed] });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            OrchestratorLog.For("Dashboard").Warning(ex, "Couldn't save the sidebar's panel layout");
            return false;
        }
    }
}
