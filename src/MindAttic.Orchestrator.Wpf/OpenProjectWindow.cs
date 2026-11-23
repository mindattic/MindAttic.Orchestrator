using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "+ Add" (M.ORCH-T1964): one pick among General Prompt, Launcher or a project leaf from the tree, "Add" in the
/// lower-right corner opens it on the login the button was pressed on. Replaces the Open Project cascading submenu
/// that used to live in ShowLoginMenu's own "⋯" menu (M.ORCH-T1585): a long project tree made that submenu unwieldy
/// to hover open level by level, so the tree moved here, alongside the General Prompt and Run Launcher picks that
/// used to be separate menu items of their own - one window instead of three places to look for "open a new CLI".
/// The tree itself is unchanged (dot-named projects nested on '.', M.ORCH-T1793) other than always showing fully
/// expanded, since picking a project closes the window anyway; a branch is a plain label, never a pick itself. A
/// leaf's own right-click-to-commit (M.ORCH-T1588) came along with it.
/// <para>Tree component and filter (M.ORCH-T2107): the hand-rolled branch/leaf rows are now <see cref="MaTree"/>, built
/// from the roster's flat <see cref="RosterProject"/> list rather than converting <see cref="ProjectNode"/> into
/// <see cref="MaTreeNode"/>; a top-right filter box narrows that list to a case-insensitive Contains match at 3+
/// characters (everything, under that). Every group still starts expanded, same as before.</para>
/// <para>Provider and model (M.ORCH-T2041): General Prompt or a project opens on <see cref="ProviderKey"/>, one of the
/// Launcher's own providers (<see cref="LauncherProviders"/>: Claude, Codex, Gemini, Kimi), rather than always Claude.
/// Both pickers live under the menu bar's Settings menu (M.ORCH-T2106), not here.</para>
/// <para>Run Command (M.ORCH-T2043): the Launcher's project "Run Command" (its ProjectActionMenu). Ticked, a picked project
/// runs the command in the box below the provider row in its folder as a hosted tab, rather than opening an agent CLI there:
/// the project's own Run Command from the Launcher's settings fills the box when it's picked, and any other command line can
/// be typed over it. General Prompt and Launcher aren't projects, so they can't be picked while it's ticked.</para>
/// </summary>
internal sealed class OpenProjectWindow : Window
{
    private readonly List<CheckBox> picks = [];
    private readonly Button add = new() { Content = "_Add", IsDefault = true, IsEnabled = false };
    private readonly CheckBox runMode = new() { Content = "_Run Command" };
    private readonly TextBox commandBox = new() { MinWidth = 300, IsEnabled = false };
    private readonly List<CheckBox> nonProjects = [];
    private readonly MaTree projectTree = new();
    private readonly TextBox projectFilter = new() { MinWidth = 140 };
    private readonly TextBlock noProjects = new() { IsEnabled = false };
    private IReadOnlyList<RosterProject> allProjects = [];
    private Action<RosterProject, string>? openProject;
    private Action<string>? picked;
    private RosterProject? pickedProject;
    /// <summary>What <see cref="commandBox"/> was last filled with from a project's own Run Command: replaced when another
    /// project is picked, unless the person has since typed a command of their own.</summary>
    private string prefilled = "";
    private bool settingAll;

    /// <summary>The Launcher provider key General Prompt and projects open on: the Settings menu's Provider pick, this run.</summary>
    internal static string ProviderKey { get; set; } = MindAttic.Orchestrator.Providers.Claude;

    internal OpenProjectWindow(IReadOnlyList<ProjectNode> tree, Action<string> generalPrompt, Action runLauncher, Action<RosterProject, string> openProject,
        Action<RosterProject, string> runCommand)
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

        this.openProject = openProject;
        var list = new StackPanel();
        nonProjects.Add(Pick("General Prompt", "A CLI at the workspace root, every project in reach", generalPrompt));
        nonProjects.Add(Pick("Launcher", "A Launcher instance for this login, as a tab of its own in this dashboard", _ => runLauncher()));
        foreach (var b in nonProjects) list.Children.Add(b);
        var header = new TextBlock { Text = "Projects" };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        DockPanel.SetDock(header, Dock.Left);
        projectFilter.SetResourceReference(StyleProperty, "MaTextBox");
        MaTextBox.SetPlaceholder(projectFilter, "Filter…");
        projectFilter.ToolTip = "3 or more characters narrows the list to repos whose name contains it (case-insensitive)";
        projectFilter.HorizontalAlignment = HorizontalAlignment.Right;
        projectFilter.TextChanged += (_, _) => FillProjects();
        var headerRow = new DockPanel();
        headerRow.SetResourceReference(MarginProperty, "SpaceTS");
        headerRow.Children.Add(header);
        headerRow.Children.Add(projectFilter);
        list.Children.Add(headerRow);

        noProjects.SetResourceReference(StyleProperty, "MaMutedText");
        list.Children.Add(noProjects);
        projectTree.LeafSelected += OnLeafSelected;
        projectTree.PreviewMouseRightButtonUp += OnTreeRightClick;
        list.Children.Add(projectTree);

        if (tree.Count > 0) { allProjects = Leaves(tree).ToList(); FillProjects(); }
        else
        {
            // Opened before the roster's first read finished (M.ORCH-T2105): every repo still belongs here, so wait for it.
            noProjects.Text = "Reading the Launcher's roster…";
            projectTree.Visibility = Visibility.Collapsed;
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
        add.ToolTip = "Opens the pick above on this login";
        add.Click += (_, _) =>
        {
            if (picked is not { } go) return;
            if (runMode.IsChecked == true)
            {
                if (pickedProject is not { } project || commandBox.Text.Trim() is not { Length: > 0 } line) return;
                Close();
                runCommand(project, line);
                return;
            }
            Close();
            go(ProviderKey);
        };
        var cancel = new Button { Content = "_Cancel", IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");

        var prompt = new TextBlock { Text = "Pick what to open for this login, then Add.", TextWrapping = TextWrapping.Wrap };
        prompt.SetResourceReference(StyleProperty, "MaMutedText");
        prompt.SetResourceReference(MarginProperty, "SpaceBXS");

        var run = RunRow();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(add);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(prompt, Dock.Top);
        DockPanel.SetDock(run, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(prompt);
        panel.Children.Add(run);
        panel.Children.Add(footer);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => picks.FirstOrDefault()?.Focus();
    }

    /// <summary>The Run Command toggle and its command line (M.ORCH-T2043). Ticking it unpicks and greys General Prompt and
    /// Launcher (not projects), and fills the box from the project picked, if any.</summary>
    private FrameworkElement RunRow()
    {
        runMode.SetResourceReference(StyleProperty, "MaCheck");
        runMode.ToolTip = "Run a command in the picked project's folder as a tab of its own, instead of opening an agent CLI there";
        runMode.Margin = new Thickness(0, 2, 12, 2);
        runMode.VerticalAlignment = VerticalAlignment.Center;
        commandBox.SetResourceReference(StyleProperty, "MaTextBox");
        commandBox.ToolTip = "The command line, run through cmd in the project's folder: the project's own Run Command from the Launcher's settings, or any other";
        commandBox.TextChanged += (_, _) => UpdateAdd();
        void Toggle()
        {
            var on = runMode.IsChecked == true;
            commandBox.IsEnabled = on;
            foreach (var b in nonProjects)
            {
                if (on) b.IsChecked = false;
                b.IsEnabled = !on;
            }
            if (on) Prefill();
            UpdateAdd();
            if (on) commandBox.Focus();
        }
        runMode.Checked += (_, _) => Toggle();
        runMode.Unchecked += (_, _) => Toggle();

        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(runMode, Dock.Left);
        row.Children.Add(runMode);
        row.Children.Add(commandBox);
        return row;
    }

    /// <summary>Fills the command box from the picked project's own Run Command, unless the person has typed one of their own.</summary>
    private void Prefill()
    {
        if (commandBox.Text != prefilled && commandBox.Text.Trim().Length > 0) return;
        prefilled = pickedProject?.RunCommand ?? "";
        commandBox.Text = prefilled;
        commandBox.CaretIndex = commandBox.Text.Length;
    }

    /// <summary>Add is live for a pick, and in Run Command mode only for a project with a command to run.</summary>
    private void UpdateAdd() =>
        add.IsEnabled = picked is not null && (runMode.IsChecked != true || (pickedProject is not null && commandBox.Text.Trim().Length > 0));

    /// <summary>A single-select row: a check box that behaves like a radio button, ticking it clears every other one
    /// (both each other and whatever's picked in <see cref="projectTree"/>), since the library
    /// (MindAttic.Components.Wpf.Theme.xaml) has no radio button style of its own and this is the only place one's
    /// needed so far (General Prompt, Launcher; a project is picked from the tree instead, M.ORCH-T2107).</summary>
    private CheckBox Pick(string label, string? tip, Action<string> go)
    {
        var box = new CheckBox { Content = label, Margin = new Thickness(0, 2, 0, 2) };
        box.SetResourceReference(StyleProperty, "MaCheck");
        if (tip is not null) box.ToolTip = tip;
        box.Checked += (_, _) =>
        {
            if (settingAll) return;
            settingAll = true;
            try { foreach (var b in picks) if (!ReferenceEquals(b, box)) b.IsChecked = false; }
            finally { settingAll = false; }
            picked = go;
            pickedProject = null;
            if (runMode.IsChecked == true) Prefill();
            UpdateAdd();
        };
        box.Unchecked += (_, _) => { if (!settingAll && ReferenceEquals(picked, go)) { picked = null; pickedProject = null; UpdateAdd(); } };
        picks.Add(box);
        return box;
    }

    /// <summary>(Re)builds <see cref="projectTree"/>'s roots from <see cref="allProjects"/>, narrowed by
    /// <see cref="projectFilter"/> (3+ characters: a case-insensitive Contains on the name; fewer, everything),
    /// fully expanded so every match stays visible - mirrors the old tree's "always showing fully expanded".</summary>
    private void FillProjects()
    {
        var filter = projectFilter.Text.Trim();
        var filtered = filter.Length >= 3
            ? allProjects.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList()
            : allProjects;
        noProjects.Text = allProjects.Count == 0 ? "No projects on the Launcher's roster" : "No repo matches that filter";
        noProjects.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        projectTree.Visibility = filtered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var roots = MaTreeNode.GroupByDelimiter(filtered, p => p.Name);
        ExpandAll(roots);
        projectTree.ItemsSource = roots;
    }

    private static void ExpandAll(IEnumerable<MaTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsLeaf) node.IsExpanded = true;
            ExpandAll(node.Children);
        }
    }

    /// <summary>Flattens a project tree back down to its leaves, dropping the branch labels (mirrors
    /// <c>MainViewModel.Tabs.Leaves</c>, which is private to that file).</summary>
    private static IEnumerable<RosterProject> Leaves(IEnumerable<ProjectNode> nodes) =>
        nodes.SelectMany<ProjectNode, RosterProject>(n => n switch { ProjectLeaf l => [l.Project], ProjectBranch b => Leaves(b.Children), _ => [] });

    /// <summary>A leaf pick in the tree behaves like the single-select <see cref="Pick"/> rows above it: it clears
    /// General Prompt/Launcher and becomes the thing "Add" opens.</summary>
    private void OnLeafSelected(object sender, RoutedPropertyChangedEventArgs<MaTreeNode?> e)
    {
        if (e.NewValue is not { IsLeaf: true, Tag: RosterProject project } || openProject is not { } open) return;
        settingAll = true;
        try { foreach (var b in nonProjects) b.IsChecked = false; }
        finally { settingAll = false; }
        picked = kind => open(project, kind);
        pickedProject = project;
        if (runMode.IsChecked == true) Prefill();
        UpdateAdd();
    }

    /// <summary>Right-click a project leaf (M.ORCH-T1588, carried over from the old tree): a commit-message modal
    /// (Save/Cancel) that commits and pushes it, without picking it to open a CLI.</summary>
    private void OnTreeRightClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not MaTreeItem) source = VisualTreeHelper.GetParent(source);
        if (source is not MaTreeItem { DataContext: MaTreeNode { IsLeaf: true, Tag: RosterProject project } }) return;
        e.Handled = true;
        CommitProjectWindow.Show(this, project);
    }
}
