using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf.Views;

/// <summary>The Roadmap page (M.ORCH-T2384, M.ORCH-T2394), in the main area in place of the terminal; its data context is the
/// global login's tab. Drill-down columns: the repos, then one list per opened row, side by side.</summary>
public partial class RoadmapPage : Grid
{
    public RoadmapPage()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.Roadmap.Drilled -= ScrollToNewest;
            if (e.NewValue is MainViewModel now) now.Roadmap.Drilled += ScrollToNewest;
        };
    }

    /// <summary>After a click opens a column, brings the newest column into view once it's laid out; with none open, scrolls
    /// back to the repos.</summary>
    private void ScrollToNewest() => Dispatcher.BeginInvoke(() =>
    {
        if (OpenColumns.Items.Count == 0) { Body.ScrollToLeftEnd(); return; }
        if (OpenColumns.ItemContainerGenerator.ContainerFromIndex(OpenColumns.Items.Count - 1) is FrameworkElement column)
            column.BringIntoView();
    }, System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>A ticket row's right-click (M.ORCH-T2388): assign it to someone of the org (the one change the Roadmap makes),
    /// copy its id, or open it in Tickets Desktop.</summary>
    private void Row_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Button { DataContext: RoadmapRowViewModel { Node: { } node } row } button || DataContext is not MainViewModel model) return;
        e.Handled = true;
        var menu = new ContextMenu { PlacementTarget = button, Placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint };
        menu.SetResourceReference(StyleProperty, "MaDropdownMenu");
        AutomationProperties.SetName(menu, $"{node.Id} actions");
        var assign = Item("Assign to…");
        foreach (var person in model.People)
        {
            var who = Item(person.Name);
            who.IsChecked = string.Equals(person.Email, node.Assignee, StringComparison.OrdinalIgnoreCase);
            var email = person.Email;
            who.Click += (_, _) => model.Roadmap.Assign(row, email);
            assign.Items.Add(who);
        }
        menu.Items.Add(assign);
        var copy = Item("Copy id");
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(node.Id); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        };
        menu.Items.Add(copy);
        var open = Item("Open in Tickets");
        open.Command = model.OpenInTicketsCommand;
        open.CommandParameter = node.Id;
        menu.Items.Add(open);
        menu.IsOpen = true;
    }

    private static MenuItem Item(string header)
    {
        var item = new MenuItem { Header = header };
        item.SetResourceReference(StyleProperty, "MaDropdownMenuItem");
        AutomationProperties.SetName(item, header);
        return item;
    }
}
