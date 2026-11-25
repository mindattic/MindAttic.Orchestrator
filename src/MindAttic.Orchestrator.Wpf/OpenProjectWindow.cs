using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "+ Add" (M.ORCH-T1964): tick any of General Prompt, Launcher and project leaves in the tree, and "Add" in the
/// lower-right corner opens every ticked one on the login the button was pressed on (M.ORCH-T2204). Replaces the Open
/// Project cascading submenu that used to live in ShowLoginMenu's own "⋯" menu (M.ORCH-T1585). A branch is a plain
/// label, never a pick itself; a leaf's own right-click-to-commit (M.ORCH-T1588) came along with it.
/// <para>Tree component and filter (M.ORCH-T2107): the rows are <see cref="MaTree"/>, built from the roster's flat
/// <see cref="RosterProject"/> list; a top-right filter box narrows the projects to a case-insensitive Contains match at
/// 3+ characters (everything, under that). Every group starts expanded.</para>
/// <para>Roots (M.ORCH-T2204): General Prompt and Launcher are the tree's first two roots, checkable like a project, then
/// a <see cref="MaTreeNode.Divider"/> (no selection, check box, focus or hover), then the projects. The filter only
/// narrows the projects; those three roots always stay, and a project's tick survives the filter rebuilding the tree.</para>
/// <para>Provider and model (M.ORCH-T2041): General Prompt or a project opens on <see cref="ProviderKey"/>, one of the
/// Launcher's own providers (<see cref="LauncherProviders"/>: Claude, Codex, Gemini, Kimi), rather than always Claude.
/// Both pickers live under the menu bar's Settings menu (M.ORCH-T2106), not here.</para>
/// </summary>
internal sealed class OpenProjectWindow : Window
{
    private readonly Button add = new() { Content = "_Add", IsDefault = true, IsEnabled = false };
    private readonly MaTree projectTree = new() { ShowCheckBoxes = true };
    private readonly TextBox projectFilter = new() { MinWidth = 140 };
    private readonly TextBlock noProjects = new() { IsEnabled = false };
    private readonly MaTreeNode generalPromptNode = new("General Prompt", "General Prompt", isLeaf: true);
    private readonly MaTreeNode launcherNode = new("Launcher", "Launcher", isLeaf: true);
    private readonly MaTreeNode divider = MaTreeNode.Divider();
    private readonly HashSet<RosterProject> checkedProjects = [];
    private IReadOnlyList<RosterProject> allProjects = [];

    /// <summary>The Launcher provider key General Prompt and projects open on: the Settings menu's Provider pick, this run.</summary>
    internal static string ProviderKey { get; set; } = MindAttic.Orchestrator.Providers.Claude;

    internal OpenProjectWindow(IReadOnlyList<ProjectNode> tree, Action<string> generalPrompt, Action runLauncher, Action<RosterProject, string> openProject)
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

        generalPromptNode.PropertyChanged += OnNodeChanged;
        launcherNode.PropertyChanged += OnNodeChanged;

        var list = new StackPanel();
        projectFilter.SetResourceReference(StyleProperty, "MaTextBox");
        MaTextBox.SetPlaceholder(projectFilter, "Filter…");
        projectFilter.ToolTip = "3 or more characters narrows the list to repos whose name contains it (case-insensitive)";
        projectFilter.HorizontalAlignment = HorizontalAlignment.Right;
        projectFilter.SetResourceReference(MarginProperty, "SpaceBXS");
        projectFilter.TextChanged += (_, _) => FillProjects();
        list.Children.Add(projectFilter);

        projectTree.PreviewMouseRightButtonUp += OnTreeRightClick;
        list.Children.Add(projectTree);
        noProjects.SetResourceReference(StyleProperty, "MaMutedText");
        list.Children.Add(noProjects);

        if (tree.Count > 0) { allProjects = Leaves(tree).ToList(); FillProjects(); }
        else
        {
            // Opened before the roster's first read finished (M.ORCH-T2105): every repo still belongs here, so wait for it.
            FillProjects();
            noProjects.Text = "Reading the Launcher's roster…";
            Loaded += async (_, _) =>
            {
                try { await MainViewModel.RefreshProjectsAsync(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Warning("Open Project: reading the roster failed: {Error}", ex.Message); }
                allProjects = Leaves(MainViewModel.ProjectTree ?? []).ToList();
                FillProjects();
            };
        }
        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        add.SetResourceReference(StyleProperty, "MaPrimaryButton");
        add.ToolTip = "Opens every ticked item above on this login";
        add.Click += (_, _) =>
        {
            var projects = allProjects.Where(checkedProjects.Contains).ToList();
            var prompt = generalPromptNode.IsChecked;
            var launcher = launcherNode.IsChecked;
            if (!prompt && !launcher && projects.Count == 0) return;
            Close();
            if (prompt) generalPrompt(ProviderKey);
            if (launcher) runLauncher();
            foreach (var project in projects) openProject(project, ProviderKey);
        };
        var cancel = new Button { Content = "_Cancel", IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");
        cancel.Click += (_, _) => Close();

        var hint = new TextBlock { Text = "Tick what to open for this login, then Add.", TextWrapping = TextWrapping.Wrap };
        hint.SetResourceReference(StyleProperty, "MaMutedText");
        hint.SetResourceReference(MarginProperty, "SpaceBXS");

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(add);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(hint, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(hint);
        panel.Children.Add(footer);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => projectTree.Focus();
    }

    /// <summary>(Re)builds <see cref="projectTree"/>'s roots: General Prompt, Launcher, the divider, then
    /// <see cref="allProjects"/> narrowed by <see cref="projectFilter"/> (3+ characters: a case-insensitive Contains on
    /// the name; fewer, everything), fully expanded, each project ticked again if it was before the rebuild.</summary>
    private void FillProjects()
    {
        var filter = projectFilter.Text.Trim();
        var filtered = filter.Length >= 3
            ? allProjects.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList()
            : allProjects;
        noProjects.Text = allProjects.Count == 0 ? "No projects on the Launcher's roster" : "No repo matches that filter";
        noProjects.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var projectRoots = MaTreeNode.GroupByDelimiter(filtered, p => p.Name);
        Prepare(projectRoots);
        ObservableCollection<MaTreeNode> roots = [generalPromptNode, launcherNode, divider, .. projectRoots];
        projectTree.ItemsSource = roots;
    }

    /// <summary>Expands every group and restores and tracks each project leaf's tick.</summary>
    private void Prepare(IEnumerable<MaTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsLeaf) node.IsExpanded = true;
            else if (node.Tag is RosterProject project)
            {
                node.IsChecked = checkedProjects.Contains(project);
                node.PropertyChanged += OnNodeChanged;
            }
            Prepare(node.Children);
        }
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MaTreeNode.IsChecked) || sender is not MaTreeNode node) return;
        if (node.Tag is RosterProject project)
        {
            if (node.IsChecked) checkedProjects.Add(project);
            else checkedProjects.Remove(project);
        }
        add.IsEnabled = generalPromptNode.IsChecked || launcherNode.IsChecked || checkedProjects.Count > 0;
    }

    /// <summary>Flattens a project tree back down to its leaves, dropping the branch labels (mirrors
    /// <c>MainViewModel.Tabs.Leaves</c>, which is private to that file).</summary>
    private static IEnumerable<RosterProject> Leaves(IEnumerable<ProjectNode> nodes) =>
        nodes.SelectMany<ProjectNode, RosterProject>(n => n switch { ProjectLeaf l => [l.Project], ProjectBranch b => Leaves(b.Children), _ => [] });

    /// <summary>Right-click a project leaf (M.ORCH-T1588, carried over from the old tree): a commit-message modal
    /// (Save/Cancel) that commits and pushes it, without ticking it.</summary>
    private void OnTreeRightClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not MaTreeItem) source = VisualTreeHelper.GetParent(source);
        if (source is not MaTreeItem { DataContext: MaTreeNode { IsLeaf: true, Tag: RosterProject project } }) return;
        e.Handled = true;
        CommitProjectWindow.Show(this, project);
    }
}
