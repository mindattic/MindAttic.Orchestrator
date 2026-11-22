using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "+ Add" (M.ORCH-T1964): one pick among General Prompt, Run Launcher or a project leaf from the tree, "Add" in the
/// lower-right corner opens it on the login the button was pressed on. Replaces the Open Project cascading submenu
/// that used to live in ShowLoginMenu's own "⋯" menu (M.ORCH-T1585): a long project tree made that submenu unwieldy
/// to hover open level by level, so the tree moved here, alongside the General Prompt and Run Launcher picks that
/// used to be separate menu items of their own - one window instead of three places to look for "open a new CLI".
/// The tree itself is unchanged (dot-named projects nested on '.', M.ORCH-T1793) other than always showing fully
/// expanded, since picking a project closes the window anyway; a branch is a plain label, never a pick itself. A
/// leaf's own right-click-to-commit (M.ORCH-T1588) came along with it.
/// </summary>
internal sealed class OpenProjectWindow : Window
{
    private readonly List<CheckBox> picks = [];
    private readonly Button add = new() { Content = "_Add", IsDefault = true, IsEnabled = false };
    private Action? picked;
    private bool settingAll;

    internal OpenProjectWindow(IReadOnlyList<ProjectNode> tree, Action generalPrompt, Action runLauncher, Action<RosterProject> openProject)
    {
        Title = "Open Project";
        Width = 460;
        Height = 620;
        MinWidth = 360;
        MinHeight = 360;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var list = new StackPanel();
        list.Children.Add(Pick("General Prompt", "A CLI at the workspace root, every project in reach", generalPrompt));
        list.Children.Add(Pick("Run Launcher", "Opens this login's standalone Launcher: its own process, outside this dashboard", runLauncher));
        var header = new TextBlock { Text = "Projects" };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceTS");
        list.Children.Add(header);
        if (tree.Count == 0)
        {
            var none = new TextBlock { Text = "No projects on the Launcher's roster", IsEnabled = false };
            none.SetResourceReference(StyleProperty, "MaMutedText");
            list.Children.Add(none);
        }
        else foreach (var node in tree) list.Children.Add(Node(node, openProject, 0));
        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        add.SetResourceReference(StyleProperty, "MaPrimaryButton");
        add.ToolTip = "Opens the pick above on this login";
        add.Click += (_, _) => { if (picked is { } go) { Close(); go(); } };
        var cancel = new Button { Content = "_Cancel", IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");

        var prompt = new TextBlock { Text = "Pick what to open for this login, then Add.", TextWrapping = TextWrapping.Wrap };
        prompt.SetResourceReference(StyleProperty, "MaMutedText");
        prompt.SetResourceReference(MarginProperty, "SpaceBXS");

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(add);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(prompt, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(prompt);
        panel.Children.Add(footer);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => picks.FirstOrDefault()?.Focus();
    }

    /// <summary>A single-select row: a check box that behaves like a radio button, ticking it clears every other one,
    /// since the library (MindAttic.Components.Wpf.Theme.xaml) has no radio button style of its own and this is the
    /// only place one's needed so far.</summary>
    private CheckBox Pick(string label, string? tip, Action go, int depth = 0)
    {
        var box = new CheckBox { Content = label, Margin = new Thickness(depth * 20, 2, 0, 2) };
        box.SetResourceReference(StyleProperty, "MaCheck");
        if (tip is not null) box.ToolTip = tip;
        box.Checked += (_, _) =>
        {
            if (settingAll) return;
            settingAll = true;
            try { foreach (var b in picks) if (!ReferenceEquals(b, box)) b.IsChecked = false; }
            finally { settingAll = false; }
            picked = go;
            add.IsEnabled = true;
        };
        box.Unchecked += (_, _) => { if (!settingAll && ReferenceEquals(picked, go)) { picked = null; add.IsEnabled = false; } };
        picks.Add(box);
        return box;
    }

    /// <summary>A row of the tree: a branch is a plain label with its children indented under it, a leaf a pick.</summary>
    private FrameworkElement Node(ProjectNode node, Action<RosterProject> openProject, int depth)
    {
        switch (node)
        {
            case ProjectBranch branch:
                var panel = new StackPanel();
                var label = new TextBlock { Text = node.Label, Margin = new Thickness(depth * 20, 6, 0, 2) };
                label.SetResourceReference(StyleProperty, "MaMutedText");
                panel.Children.Add(label);
                foreach (var child in branch.Children) panel.Children.Add(Node(child, openProject, depth + 1));
                return panel;
            case ProjectLeaf leaf:
                var box = Pick(node.Label, $"{leaf.Project.Path}\nRight-click: commit and push it under a message you write", () => openProject(leaf.Project), depth);
                // Right-click a project (M.ORCH-T1588, carried over from the old tree menu): a commit-message modal
                // (Save/Cancel) that commits and pushes it, without picking it to open a CLI.
                box.PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; CommitProjectWindow.Show(this, leaf.Project); };
                return box;
            default:
                return new TextBlock();
        }
    }
}
