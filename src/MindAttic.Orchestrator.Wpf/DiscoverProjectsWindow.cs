using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "Discover projects…" (M.ORCH-T1987), a sibling of a login's "Refresh projects": lists every git repo under the workspace
/// root that isn't on the Launcher's roster yet (<see cref="ProjectDiscovery.FindUnregistered"/>), lets the user pick which
/// ones to add (every row starts checked, since the common case is "add them all"), and hands the picks to
/// <paramref name="addPicked"/> — <see cref="ProjectDiscovery.Append"/> followed by the same refresh "Refresh projects"
/// already does. Mirrors <see cref="OpenProjectWindow"/>/<see cref="CommitProjectsWindow"/>'s own check-box-list shape; unlike
/// the Launcher's DiscoverProjectsMenu, there's no per-project colour or Windows Terminal scheme to pick here.
/// </summary>
internal sealed class DiscoverProjectsWindow : Window
{
    private readonly List<(CheckBox Box, DiscoveredRepo Repo)> rows = [];
    private readonly Button add = new() { Content = "_Add", IsDefault = true, IsEnabled = false };
    private bool busy;

    internal DiscoverProjectsWindow(IReadOnlyList<DiscoveredRepo> repos, Func<IReadOnlyList<DiscoveredRepo>, Task> addPicked)
    {
        Title = "Discover Projects";
        Width = 520;
        Height = 480;
        MinWidth = 360;
        MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var list = new StackPanel();
        foreach (var repo in repos)
        {
            var box = new CheckBox { Content = repo.Name, ToolTip = repo.Path, IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
            box.SetResourceReference(StyleProperty, "MaCheck");
            box.Checked += (_, _) => UpdateAdd();
            box.Unchecked += (_, _) => UpdateAdd();
            rows.Add((box, repo));
            list.Children.Add(box);
        }
        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        add.SetResourceReference(StyleProperty, "MaPrimaryButton");
        add.ToolTip = "Appends the picked repos to the Launcher's roster (settings.json), then refreshes the project list";
        add.Click += async (_, _) => await AddAsync(addPicked);
        var cancel = new Button { Content = "_Cancel", IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");

        var header = new TextBlock
        {
            Text = $"{repos.Count} git repo{(repos.Count == 1 ? "" : "s")} under the workspace root aren't on the roster yet. Pick which to add.",
            TextWrapping = TextWrapping.Wrap,
        };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceBXS");

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(add);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(header);
        panel.Children.Add(footer);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) { e.Cancel = true; } };
        UpdateAdd();
    }

    private void UpdateAdd()
    {
        var count = rows.Count(r => r.Box.IsChecked == true);
        add.Content = count == 0 ? "_Add" : $"_Add {count} project{(count == 1 ? "" : "s")}";
        add.IsEnabled = !busy && count > 0;
    }

    private async Task AddAsync(Func<IReadOnlyList<DiscoveredRepo>, Task> addPicked)
    {
        var picked = rows.Where(r => r.Box.IsChecked == true).Select(r => r.Repo).ToList();
        if (picked.Count == 0 || busy) return;
        busy = true;
        UpdateAdd();
        try { await addPicked(picked); Close(); }
        finally { busy = false; UpdateAdd(); }
    }
}
