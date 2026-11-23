using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        var list = new StackPanel();
        nonProjects.Add(Pick("General Prompt", "A CLI at the workspace root, every project in reach", generalPrompt));
        nonProjects.Add(Pick("Launcher", "A Launcher instance for this login, as a tab of its own in this dashboard", _ => runLauncher()));
        foreach (var b in nonProjects) list.Children.Add(b);
        var header = new TextBlock { Text = "Projects" };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceTS");
        list.Children.Add(header);
        var projects = new StackPanel();
        list.Children.Add(projects);
        if (tree.Count > 0) FillProjects(projects, tree, openProject);
        else
        {
            // Opened before the roster's first read finished (M.ORCH-T2105): every repo still belongs here, so wait for it.
            projects.Children.Add(Muted("Reading the Launcher's roster…"));
            Loaded += async (_, _) =>
            {
                try { await MainViewModel.RefreshProjectsAsync(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Warning("Open Project: reading the roster failed: {Error}", ex.Message); }
                FillProjects(projects, MainViewModel.ProjectTree ?? [], openProject);
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

    /// <summary>A single-select row: a check box that behaves like a radio button, ticking it clears every other one,
    /// since the library (MindAttic.Components.Wpf.Theme.xaml) has no radio button style of its own and this is the
    /// only place one's needed so far.</summary>
    private CheckBox Pick(string label, string? tip, Action<string> go, int depth = 0, RosterProject? project = null)
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
            pickedProject = project;
            if (runMode.IsChecked == true) Prefill();
            UpdateAdd();
        };
        box.Unchecked += (_, _) => { if (!settingAll && ReferenceEquals(picked, go)) { picked = null; pickedProject = null; UpdateAdd(); } };
        picks.Add(box);
        return box;
    }

    /// <summary>Puts <paramref name="tree"/>'s rows in <paramref name="projects"/>, or says the roster has none.</summary>
    private void FillProjects(StackPanel projects, IReadOnlyList<ProjectNode> tree, Action<RosterProject, string> openProject)
    {
        projects.Children.Clear();
        if (tree.Count == 0) projects.Children.Add(Muted("No projects on the Launcher's roster"));
        else foreach (var node in tree) projects.Children.Add(Node(node, openProject, 0));
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, IsEnabled = false };
        block.SetResourceReference(StyleProperty, "MaMutedText");
        return block;
    }

    /// <summary>A row of the tree: a branch is a plain label with its children indented under it, a leaf a pick.</summary>
    private FrameworkElement Node(ProjectNode node, Action<RosterProject, string> openProject, int depth)
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
                var box = Pick(node.Label, $"{leaf.Project.Path}\nRight-click: commit and push it under a message you write", kind => openProject(leaf.Project, kind), depth, leaf.Project);
                // Right-click a project (M.ORCH-T1588, carried over from the old tree menu): a commit-message modal
                // (Save/Cancel) that commits and pushes it, without picking it to open a CLI.
                box.PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; CommitProjectWindow.Show(this, leaf.Project); };
                return box;
            default:
                return new TextBlock();
        }
    }
}
