using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Backup;
using MindAttic.Orchestrator.Bootstrap;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The Bootstrap window (M.ORCH-T2052, part of M.ORCH-T1984): the Launcher's <c>bootstrap</c> command as steps you click, in its
/// order. First the prerequisites (<see cref="PrerequisiteService.CheckAll"/>): the required tools, the optional dev tools, then the
/// AI agent CLIs, each with its state and an Install button when it's missing (winget, npm or the vendor's script). Then the
/// workspace: the clone step <see cref="BuildWorkspaceWindow"/> runs (every repo the manifest lists that isn't there yet). Last,
/// Restore latest backup: <see cref="BackupRestore.Run"/> on the newest dated folder <see cref="BackupFolders.FindNewest"/> finds,
/// which unzips AppData and replays each SQLite dump into the roster's live file (after the clone, since those files live in the
/// repos) and only lists the Azure SQL exports. Every step runs off the UI thread; while one is going every button is off and the
/// window won't close.
/// </summary>
internal sealed class BootstrapWindow : Window
{
    private readonly string workspaceRoot;
    private readonly PrerequisiteService prerequisites;
    private readonly BackupRestore restore;
    private readonly string backupBase;
    private readonly StackPanel prereqList = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock backupText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar bar = new() { IsIndeterminate = true, Height = 4, Visibility = Visibility.Collapsed };
    private readonly TextBox output = new()
    {
        IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12, Height = 150,
    };
    private readonly Button refresh = new() { Content = "_Refresh" };
    private readonly Button clone = new() { Content = "_Clone missing repos" };
    private readonly Button restoreButton = new() { Content = "Restore _latest backup" };
    private readonly MaModal question = new() { DialogWidth = 480 };
    private readonly List<Button> rowButtons = [];
    private readonly List<PrerequisiteStatus> shown = [];
    private TaskCompletionSource<bool>? answer;
    private string? latestBackup;
    private bool busy;

    internal BootstrapWindow(string workspaceRoot)
        : this(workspaceRoot, new PrerequisiteService(), new BackupRestore(), BackupFolders.DefaultBackupBase) { }

    /// <param name="prerequisites">The prerequisite engine; tests give one over fakes, so nothing is installed.</param>
    /// <param name="restore">The restore engine.</param>
    /// <param name="backupBase">Where the dated backup folders are.</param>
    internal BootstrapWindow(string workspaceRoot, PrerequisiteService prerequisites, BackupRestore restore, string backupBase)
    {
        this.workspaceRoot = workspaceRoot;
        this.prerequisites = prerequisites;
        this.restore = restore;
        this.backupBase = backupBase;
        Title = "Bootstrap this machine";
        Width = 780;
        Height = 680;
        MinWidth = 520;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var header = new TextBlock
        {
            Text = "Sets up a machine the way the Launcher's bootstrap does: the prerequisites, the workspace's repos, then the latest "
                + "backup's AppData and SQLite databases. Nothing installs or restores until you click it.",
            TextWrapping = TextWrapping.Wrap,
        };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceBXS");
        status.SetResourceReference(MarginProperty, "SpaceBXS");
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        bar.SetResourceReference(MarginProperty, "SpaceBXS");
        output.SetResourceReference(StyleProperty, "MaTextArea");
        output.TextWrapping = TextWrapping.NoWrap;
        output.SetResourceReference(MarginProperty, "SpaceTS");

        refresh.SetResourceReference(StyleProperty, "MaButton");
        refresh.SetResourceReference(MarginProperty, "SpaceRS");
        refresh.ToolTip = "Checks every prerequisite again and looks for the newest backup";
        refresh.Click += async (_, _) => await LoadAsync();
        clone.SetResourceReference(StyleProperty, "MaButton");
        clone.HorizontalAlignment = HorizontalAlignment.Left;
        clone.ToolTip = $"Clones every repo the workspace manifest lists that isn't in {workspaceRoot} yet";
        clone.Click += (_, _) => CloneWorkspace();
        restoreButton.SetResourceReference(StyleProperty, "MaButton");
        restoreButton.HorizontalAlignment = HorizontalAlignment.Left;
        restoreButton.ToolTip = "Unzips the backup's AppData over %APPDATA%\\MindAttic and replays its SQLite dumps into the live databases";
        restoreButton.Click += async (_, _) => await RestoreLatestAsync();
        var close = new Button { Content = "_Close", IsCancel = true };
        close.SetResourceReference(StyleProperty, "MaButton");
        close.Click += (_, _) => Close();

        var steps = new StackPanel();
        steps.Children.Add(Heading("1. Prerequisites"));
        steps.Children.Add(prereqList);
        steps.Children.Add(Heading("2. Workspace"));
        steps.Children.Add(Muted($"Clones the workspace's repos into {workspaceRoot}; the ones already there are left as they are."));
        steps.Children.Add(clone);
        steps.Children.Add(Heading("3. Restore latest backup"));
        steps.Children.Add(backupText);
        steps.Children.Add(restoreButton);
        var scroll = new ScrollViewer
        {
            Content = steps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(refresh);
        footer.Children.Add(close);

        var top = new StackPanel();
        top.Children.Add(header);
        top.Children.Add(bar);
        top.Children.Add(status);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(output, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(footer);
        panel.Children.Add(output);
        panel.Children.Add(scroll);

        question.Closed += (_, _) => answer?.TrySetResult(false);
        Closed += (_, _) => answer?.TrySetResult(false);
        var root = new Grid();
        root.Children.Add(panel);
        root.Children.Add(question);
        Content = root;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy && !question.IsOpen) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>The newest backup folder found, or null.</summary>
    internal string? LatestBackup => latestBackup;

    /// <summary>The prerequisites as last checked, in the order shown.</summary>
    internal IReadOnlyList<PrerequisiteStatus> Shown => shown;

    /// <summary>Checks every prerequisite (<see cref="PrerequisiteService.CheckAll"/>) and finds the newest backup folder.</summary>
    internal async Task LoadAsync()
    {
        if (busy) return;
        SetBusy(true, "Checking the prerequisites (the optional tools ask winget, which takes a few seconds each)…");
        IReadOnlyList<PrerequisiteStatus> statuses;
        try
        {
            (statuses, latestBackup) = await Task.Run(() => (prerequisites.CheckAll(), BackupFolders.FindNewest(backupBase)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Bootstrap check failed; contained");
            SetBusy(false, $"Checking failed: {ex.Message}");
            return;
        }
        ShowRows(Ordered(statuses));
        ShowBackup();
        var missing = shown.Count(s => !s.Installed);
        SetBusy(false, missing == 0 ? "Every prerequisite is installed." : $"{missing} prerequisite{(missing == 1 ? " is" : "s are")} missing.");
    }

    /// <summary>The Launcher's bootstrap order: the required tools, the optional dev tools, then the AI CLIs.</summary>
    internal static IReadOnlyList<PrerequisiteStatus> Ordered(IReadOnlyList<PrerequisiteStatus> statuses) =>
        [.. statuses.OrderBy(s => s.Kind switch { PrerequisiteKind.Required => 0, PrerequisiteKind.Optional => 1, _ => 2 })];

    /// <summary>Installs one missing prerequisite through the engine and shows what it answered.</summary>
    internal async Task InstallAsync(PrerequisiteStatus item)
    {
        if (busy) return;
        SetBusy(true, $"Installing {item.Name} (winget may raise its own UAC prompt)…");
        Say($"Installing {item.Name}…");
        (bool Ok, string Detail) r;
        try { r = await Task.Run(() => Install(item)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Installing {Name:l} failed; contained", item.Name);
            r = (false, ex.Message);
        }
        if (r.Ok)
        {
            var i = shown.FindIndex(s => s.Kind == item.Kind && s.Name == item.Name);
            if (i >= 0) shown[i] = item with { Installed = true };
            ShowRows([.. shown]);
        }
        Say(r.Ok ? $"  Installed {item.Name}." : $"  {item.Name} not installed: {r.Detail}");
        OrchestratorLog.For("Dashboard").Information("Bootstrap install of {Name:l}: {Ok}", item.Name, r.Ok);
        SetBusy(false, r.Ok ? $"Installed {item.Name}." : $"{item.Name} wasn't installed; the log below says why.");
    }

    private (bool Ok, string Detail) Install(PrerequisiteStatus item)
    {
        switch (item.Kind)
        {
            case PrerequisiteKind.Required:
                var (command, ids) = PrerequisiteService.HardRequirements.First(h => h.Command == item.Name);
                var hard = prerequisites.EnsureHard(command, ids);
                return (hard.Present, hard.Error ?? "");
            case PrerequisiteKind.AiCli:
                var cli = prerequisites.EnsureAiCli(PrerequisiteService.AiClis.First(c => c.Name == item.Name));
                return (cli.Present, cli.Error ?? "");
            default:
                var tool = PrerequisiteService.OptionalTools.First(t => t.Name == item.Name);
                return prerequisites.InstallViaWinget(tool.WingetId);
        }
    }

    private void CloneWorkspace()
    {
        if (busy) return;
        try
        {
            Say($"Cloning the workspace into {workspaceRoot}…");
            var window = new BuildWorkspaceWindow(workspaceRoot, OrchestratorLog.For("Dashboard")) { Owner = this };
            window.ShowDialog();
            Say(window.Succeeded ? "  The workspace is there." : "  The clone didn't finish; its window said why.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Bootstrap clone failed; contained");
            Say($"  Cloning failed: {ex.Message}");
        }
    }

    /// <summary>Asks, then restores <see cref="LatestBackup"/> through <see cref="BackupRestore.Run"/>, reporting each database as it lands.</summary>
    internal async Task RestoreLatestAsync()
    {
        if (busy || latestBackup is not { } folder) return;
        if (!await AskAsync("Restore the latest backup?",
                $"Restores {folder}: its AppData.zip is unzipped over {restore.AppDataTarget}, and each SQLite dump replaces the "
                + "live database the roster names for its project (the old file is kept aside and put back if a replay fails). "
                + "Azure SQL exports are only listed.")) return;

        SetBusy(true, $"Restoring {Path.GetFileName(folder)}…");
        Say($"Restoring {folder}…");
        var progress = new Progress<SqliteRestoreResult>(d =>
            Say(d.Ok ? $"  SQLite {d.Project}: {d.DatabaseFile} restored." : $"  SQLite {d.Project}: {d.DatabaseFile} failed: {d.Output}"));
        BackupRestoreResult r;
        try
        {
            var databases = SqliteBackup.CollectTargets(
                ProjectRoster.Load().Select(p => (p.Name, p.Path, (IEnumerable<string>?)p.SqliteDatabases)));
            r = await Task.Run(() => restore.Run(folder, databases, d => ((IProgress<SqliteRestoreResult>)progress).Report(d)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Bootstrap restore failed; contained");
            r = new BackupRestoreResult(folder, null, [], [], [], ex.Message);
        }
        // The Progress callbacks post to this thread; let them land before the summary.
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        if (r.Error is { } error) Say($"  {error}");
        if (r.AppData is { } app) Say(app.Ok ? $"  AppData restored to {restore.AppDataTarget}." : $"  AppData failed: {app.Output}");
        else if (r.Error is null) Say("  No AppData.zip in this backup.");
        foreach (var skip in r.Skipped) Say($"  Skipped {skip.DumpFile}: no {skip.Project} database on the roster.");
        if (r.AzureSqlExports.Count > 0)
        {
            Say("  Azure SQL exports (never applied here; apply one into an empty database with sqlcmd -S <server> -d <empty db> -G -i <file>):");
            foreach (var export in r.AzureSqlExports) Say("    " + export);
        }
        OrchestratorLog.For("Dashboard").Information("Bootstrap restore of {Folder:l}: {Ok}, {Count} SQLite", folder, r.Ok, r.Sqlite.Count);
        SetBusy(false, r.Ok
            ? $"Restored {Path.GetFileName(folder)}: {r.Sqlite.Count} SQLite database{(r.Sqlite.Count == 1 ? "" : "s")}."
            : $"The restore of {Path.GetFileName(folder)} didn't fully succeed; the log below says what.");
    }

    private void ShowRows(IReadOnlyList<PrerequisiteStatus> statuses)
    {
        shown.Clear();
        shown.AddRange(statuses);
        prereqList.Children.Clear();
        rowButtons.Clear();
        PrerequisiteKind? group = null;
        foreach (var item in statuses)
        {
            if (item.Kind != group)
            {
                group = item.Kind;
                var label = Muted(item.Kind switch
                {
                    PrerequisiteKind.Required => "Required",
                    PrerequisiteKind.Optional => "Optional dev tools",
                    _ => "AI agent CLIs",
                });
                label.Margin = new Thickness(0, 6, 0, 2);
                prereqList.Children.Add(label);
            }
            prereqList.Children.Add(Row(item));
        }
        UpdateButtons();
    }

    private Grid Row(PrerequisiteStatus item)
    {
        var row = new Grid { Margin = new Thickness(12, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(name, 0);
        row.Children.Add(name);

        var state = new TextBlock
        {
            Text = item.Installed ? "installed" : "missing", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
        };
        state.SetResourceReference(ForegroundProperty, item.Installed ? "MaGood" : item.Kind == PrerequisiteKind.Required ? "MaBad" : "MaWarn");
        Grid.SetColumn(state, 1);
        row.Children.Add(state);

        if (!item.Installed)
        {
            var install = new Button { Content = "Install", VerticalAlignment = VerticalAlignment.Center, ToolTip = $"Installs {item.Name}" };
            install.SetResourceReference(StyleProperty, "MaButton");
            install.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetName(install, $"Install {item.Name}");
            install.Click += async (_, _) => await InstallAsync(item);
            rowButtons.Add(install);
            Grid.SetColumn(install, 2);
            row.Children.Add(install);
        }
        return row;
    }

    private void ShowBackup()
    {
        backupText.Text = latestBackup is { } folder
            ? $"Newest backup: {folder}"
            : $"No dated backup folder is reachable under {backupBase}.";
        UpdateButtons();
    }

    private static TextBlock Heading(string text)
    {
        var t = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 10, 0, 4) };
        return t;
    }

    private static TextBlock Muted(string text)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        t.SetResourceReference(StyleProperty, "MaMutedText");
        return t;
    }

    private void Say(string line)
    {
        output.AppendText(line + Environment.NewLine);
        output.ScrollToEnd();
    }

    /// <summary>Asks a yes/no question in an MaModal; Esc, its close button or the backdrop answer no.</summary>
    private Task<bool> AskAsync(string title, string detail)
    {
        answer = new TaskCompletionSource<bool>();
        var no = new Button { Content = "_No" };
        no.SetResourceReference(MarginProperty, "SpaceRS");
        var yes = new Button { Content = "_Yes", IsDefault = true };
        no.SetResourceReference(StyleProperty, "MaButton");
        yes.SetResourceReference(StyleProperty, "MaPrimaryButton");
        no.Click += (_, _) => Answer(false);
        yes.Click += (_, _) => Answer(true);
        var footer = new StackPanel { Orientation = Orientation.Horizontal };
        footer.Children.Add(no);
        footer.Children.Add(yes);
        question.Title = title;
        question.Content = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap };
        question.Footer = footer;
        question.IsOpen = true;
        return answer.Task;
    }

    private void Answer(bool yes)
    {
        answer?.TrySetResult(yes);
        question.IsOpen = false;
    }

    private void SetBusy(bool on, string message)
    {
        busy = on;
        status.Text = message;
        bar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        refresh.IsEnabled = !busy;
        clone.IsEnabled = !busy;
        restoreButton.IsEnabled = !busy && latestBackup is not null;
        foreach (var b in rowButtons) b.IsEnabled = !busy;
    }
}
