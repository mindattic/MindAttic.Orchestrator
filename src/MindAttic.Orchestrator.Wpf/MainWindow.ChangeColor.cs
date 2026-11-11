using System.Windows;
using System.Windows.Controls;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>Change Color (M.ORCH-T1594) on the right-click menu of each person's tab and each CLI tab: a submenu of swatches,
/// the one in force checked, and a way back to the default. A person's pick is their login's colour, which their open CLI tabs
/// follow; a CLI's pick is that one tab's own. Either one colours the Task List rows of that person or CLI.</summary>
public partial class MainWindow
{
    /// <summary>"Change Color" for a person's tab: their login's colour, every open CLI of theirs following it.</summary>
    private MenuItem LoginColorMenu(MainViewModel tab)
    {
        var menu = ColorMenu(tab.TabColorHex, isDefault: false,
            "Their own colour",
            "Back to the colour picked for this login from its email",
            color => _ = tab.ChangeTabColorAsync(color));
        menu.ToolTip = "This login's colour: its tab, the tabs of its CLIs (unless one has its own) and its Task List rows";
        menu.IsEnabled = tab.IsTabSignedIn;
        ToolTipService.SetShowOnDisabled(menu, true);
        return menu;
    }

    /// <summary>"Change Color" for a CLI tab: that one tab's colour and its Task List rows; "Follow login" drops it again.</summary>
    private MenuItem PaneColorMenu(TerminalPaneViewModel pane)
    {
        var menu = ColorMenu(pane.Request.EffectiveColor, isDefault: !pane.HasCustomColor,
            "Follow its login's colour",
            "Drop this tab's own colour: it takes its login's again",
            color =>
            {
                if (!model.AllTerminals.Recolor(pane, color))
                    OrchestratorLog.For("Dashboard").Information("{Title:l} already has that colour", pane.Title);
            });
        menu.ToolTip = "This CLI's own colour: its tab and its Task List rows";
        return menu;
    }

    /// <summary>The submenu: each of <see cref="LoginLook.Swatches"/> with a dot of its colour, <paramref name="current"/>
    /// checked (unless <paramref name="isDefault"/>), then the default item, checked while it's in force.</summary>
    private static MenuItem ColorMenu(string? current, bool isDefault, string defaultHeader, string defaultTip, Action<string?> apply)
    {
        var root = DropdownItem("Change Color");
        var now = LoginLook.CleanColor(current);
        foreach (var (name, hex) in LoginLook.Swatches)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = "●", Foreground = MainViewModel.BrushOf(hex), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            var item = DropdownItem(header);
            item.IsCheckable = true;
            item.IsChecked = !isDefault && string.Equals(now, hex, StringComparison.OrdinalIgnoreCase);
            item.ToolTip = hex;
            System.Windows.Automation.AutomationProperties.SetName(item, $"{name} ({hex})");
            item.Click += (_, e) => { e.Handled = true; apply(hex); };
            root.Items.Add(item);
        }
        root.Items.Add(new Separator());
        var reset = DropdownItem(defaultHeader);
        reset.IsCheckable = true;
        reset.IsChecked = isDefault;
        reset.ToolTip = defaultTip;
        reset.Click += (_, e) => { e.Handled = true; apply(null); };
        root.Items.Add(reset);
        return root;
    }
}
