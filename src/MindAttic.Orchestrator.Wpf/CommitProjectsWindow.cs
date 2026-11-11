using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "Commit and push projects…" (M.ORCH-T1589), from a login tab's Open Project menu: the same dot-delimited project tree as
/// that menu, with a check box on every project and branch (a branch picks or clears everything under it), one commit message,
/// and Commit and push, which commits each picked project's working tree under that message and pushes it, one after another
/// (<see cref="ProjectCommit.CommitAndPush"/>), logging how each went. Each project's changed-file count fills in beside it
/// once git status has been read, and "Pick changed" picks every project that has changes.
/// </summary>
internal sealed class CommitProjectsWindow : Window
{
    private readonly List<(CheckBox Box, RosterProject Project)> leaves = [];
    private readonly TextBox message = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, MaxHeight = 120,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox output = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12,
        TextWrapping = TextWrapping.NoWrap, Height = 140 };
    private readonly Button commit = new() { Content = "_Commit and push", IsDefault = true };
    private readonly Button pickChanged = new() { Content = "Pick _changed", IsEnabled = false };
    private readonly Button clear = new() { Content = "C_lear" };
    private readonly Dictionary<RosterProject, int?> changes = [];
    private bool busy;

    internal CommitProjectsWindow(IReadOnlyList<ProjectNode> tree)
    {
        Title = "Commit and push projects";
        Width = 640;
        Height = 720;
        MinWidth = 420;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var list = new StackPanel();
        foreach (var node in tree) list.Children.Add(Node(node, null, 0));
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        message.SetResourceReference(StyleProperty, "MaTextArea");
        message.TextChanged += (_, _) => UpdateCommit();
        output.SetResourceReference(StyleProperty, "MaTextArea");
        foreach (var b in new[] { pickChanged, clear }) { b.SetResourceReference(StyleProperty, "MaButton"); b.SetResourceReference(MarginProperty, "SpaceRS"); }
        commit.SetResourceReference(StyleProperty, "MaPrimaryButton");
        pickChanged.ToolTip = "Pick every project whose working tree has changes";
        pickChanged.Click += (_, _) => { foreach (var (box, p) in leaves) box.IsChecked = changes.GetValueOrDefault(p) > 0; };
        clear.Click += (_, _) => { foreach (var (box, _) in leaves) box.IsChecked = false; };
        commit.ToolTip = "Commit each picked project's whole working tree under this message, pull --rebase and push it";
        commit.Click += async (_, _) => await CommitAsync();

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        var header = new TextBlock { Text = "Pick the projects to commit and push, one message for all of them.", TextWrapping = TextWrapping.Wrap };
        header.SetResourceReference(MarginProperty, "SpaceBXS");
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        var bottom = new StackPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        var label = new TextBlock { Text = "Commit message" };
        label.SetResourceReference(MarginProperty, "SpaceTS");
        bottom.Children.Add(label);
        bottom.Children.Add(message);
        var buttons = new DockPanel();
        buttons.SetResourceReference(MarginProperty, "SpaceTS");
        DockPanel.SetDock(pickChanged, Dock.Left);
        DockPanel.SetDock(clear, Dock.Left);
        commit.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.Children.Add(pickChanged);
        buttons.Children.Add(clear);
        buttons.Children.Add(commit);
        bottom.Children.Add(buttons);
        output.SetResourceReference(MarginProperty, "SpaceTS");
        bottom.Children.Add(output);
        panel.Children.Add(bottom);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) { e.Cancel = true; Say("Still pushing; it closes once the run is done."); } };
        UpdateCommit();
        Loaded += async (_, _) => { message.Focus(); await ReadChangesAsync(); };
    }

    /// <summary>A row of the tree: a check box, and for a branch its children indented under it.</summary>
    private FrameworkElement Node(ProjectNode node, CheckBox? parent, int depth)
    {
        var box = new CheckBox { Content = node.Label, IsChecked = false, Margin = new Thickness(depth * 20, 2, 0, 2) };
        box.SetResourceReference(StyleProperty, "MaCheck");
        switch (node)
        {
            case ProjectLeaf leaf:
                box.ToolTip = leaf.Project.Path;
                leaves.Add((box, leaf.Project));
                box.Checked += (_, _) => Changed(parent);
                box.Unchecked += (_, _) => Changed(parent);
                return box;
            case ProjectBranch branch:
                parents[box] = parent;
                var panel = new StackPanel();
                panel.Children.Add(box);
                var children = new List<CheckBox>();
                foreach (var child in branch.Children)
                {
                    var row = Node(child, box, depth + 1);
                    panel.Children.Add(row);
                    children.Add(row as CheckBox ?? (CheckBox)((StackPanel)row).Children[0]);
                }
                box.Tag = children;
                // A click on a branch never lands on indeterminate: it picks everything under it, or clears it.
                box.Click += (_, _) =>
                {
                    var pick = box.IsChecked != false;
                    box.IsChecked = pick;
                    SetAll(children, pick);
                    Changed(parent);
                };
                return panel;
        }
        return box;
    }

    private bool settingAll;

    private void SetAll(IEnumerable<CheckBox> boxes, bool pick)
    {
        settingAll = true;
        try
        {
            foreach (var b in boxes)
            {
                b.IsChecked = pick;
                if (b.Tag is List<CheckBox> kids) SetAll(kids, pick);
            }
        }
        finally { settingAll = false; }
    }

    /// <summary>A box under <paramref name="parent"/> changed: the branch shows all, none or some (indeterminate), and so on up.</summary>
    private void Changed(CheckBox? parent)
    {
        UpdateCommit();
        if (settingAll || parent?.Tag is not List<CheckBox> kids) return;
        var all = kids.All(k => k.IsChecked == true);
        var none = kids.All(k => k.IsChecked == false);
        parent.IsChecked = all ? true : none ? false : null;
        if (parents.GetValueOrDefault(parent) is { } up) Changed(up);
    }

    /// <summary>Each branch's box to the box of the branch it sits in (null at the top).</summary>
    private readonly Dictionary<CheckBox, CheckBox?> parents = [];

    private IReadOnlyList<RosterProject> Picked => leaves.Where(l => l.Box.IsChecked == true).Select(l => l.Project).ToList();

    private void UpdateCommit()
    {
        var count = leaves.Count(l => l.Box.IsChecked == true);
        commit.Content = count == 0 ? "_Commit and push" : $"_Commit and push {count} project{(count == 1 ? "" : "s")}";
        commit.IsEnabled = !busy && count > 0 && !string.IsNullOrWhiteSpace(message.Text);
    }

    /// <summary>Reads every project's git status off the UI thread and shows its changed-file count beside it.</summary>
    private async Task ReadChangesAsync()
    {
        var projects = leaves.Select(l => l.Project).ToList();
        var counts = await Task.Run(() =>
        {
            var result = new System.Collections.Concurrent.ConcurrentDictionary<RosterProject, int?>();
            Parallel.ForEach(projects, new ParallelOptions { MaxDegreeOfParallelism = 6 }, p => result[p] = ProjectCommit.ChangedFiles(p.Path));
            return result;
        });
        foreach (var (box, project) in leaves)
        {
            var n = counts.GetValueOrDefault(project);
            changes[project] = n;
            if (n > 0) box.Content = $"{box.Content}  ({n} changed)";
            else if (n is null) box.Content = $"{box.Content}  (not a git checkout)";
        }
        pickChanged.IsEnabled = changes.Values.Any(n => n > 0);
    }

    private async Task CommitAsync()
    {
        var picked = Picked;
        var text = message.Text.Trim();
        if (picked.Count == 0 || text.Length == 0 || busy) return;
        busy = true;
        UpdateCommit();
        Say($"Committing and pushing {picked.Count} project(s)…");
        int pushed = 0, nothing = 0, failed = 0;
        try
        {
            foreach (var project in picked)
            {
                ProjectCommitResult r;
                try { r = await Task.Run(() => ProjectCommit.CommitAndPush(project, text)); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    OrchestratorLog.For("Dashboard").Error(ex, "Commit and push {Project:l} failed; contained", project.Name);
                    r = new(project, ProjectCommitOutcome.Failed, ex.Message);
                }
                switch (r.Outcome)
                {
                    case ProjectCommitOutcome.Pushed: pushed++; break;
                    case ProjectCommitOutcome.NothingToDo: nothing++; break;
                    default: failed++; break;
                }
                Say($"  {project.Name}: {r.Detail}");
                OrchestratorLog.For("Dashboard").Information("Commit and push {Project:l}: {Outcome} {Detail:l}", project.Name, r.Outcome, r.Detail);
            }
            Say($"Done: {pushed} pushed, {nothing} with nothing to do, {failed} failed.");
        }
        finally
        {
            busy = false;
            UpdateCommit();
        }
        await ReadChangesAfterRunAsync();
    }

    /// <summary>Once a run is done the changed counts are stale: strip them and read them again.</summary>
    private async Task ReadChangesAfterRunAsync()
    {
        foreach (var (box, project) in leaves)
            if (box.Content is string s && s.IndexOf("  (", StringComparison.Ordinal) is var at and >= 0) box.Content = s[..at];
        changes.Clear();
        await ReadChangesAsync();
    }

    private void Say(string line)
    {
        output.AppendText(line + Environment.NewLine);
        output.ScrollToEnd();
    }
}
