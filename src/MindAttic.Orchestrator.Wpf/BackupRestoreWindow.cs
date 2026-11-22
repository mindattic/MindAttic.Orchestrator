using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Azure;
using MindAttic.Orchestrator.Backup;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The toolbar's Backup &amp; Restore window (M.ORCH-T2050, part of M.ORCH-T1983). <b>Backup now</b> runs
/// <see cref="BackupService.Run"/> end to end into a fresh dated folder under the backup base: every SQLite database the roster
/// names (<see cref="RosterProject.SqliteDatabases"/>), optionally every Azure SQL database the web apps use
/// (<see cref="Kickstart.List"/> through <see cref="AzureSqlExport.CollectTargets"/>), and the <c>%APPDATA%\MindAttic</c> zip;
/// then, when ticked, uploads the folder's <c>Databases</c> off the machine (<see cref="AzureBlobUpload.UploadAndPrune(string, AzureUploadOptions, Action{string})"/>
/// with <see cref="AzureUploadOptions.Default"/>). <b>Restore from folder</b> runs <see cref="BackupRestore.Run"/> on the folder
/// picked (the newest one by default, <see cref="BackupFolders.FindNewest"/>), after asking. Every step runs off the UI thread with
/// its progress lines in the log; while one runs only Cancel works and the window won't close.
/// </summary>
internal sealed class BackupRestoreWindow : Window
{
    private readonly string workspaceRoot;
    private readonly BackupService backup;
    private readonly BackupRestore restore;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar bar = new() { IsIndeterminate = true, Height = 4, Visibility = Visibility.Collapsed };
    private readonly CheckBox includeAzureSql = new() { Content = "Include the web apps' _Azure SQL databases", IsChecked = true };
    private readonly CheckBox upload = new() { Content = "_Upload the databases to Azure storage afterwards", IsChecked = true };
    private readonly TextBox restoreFolder = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox log = new()
    {
        IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12,
        TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
    };
    private readonly Button backupButton = new() { Content = "_Backup now" };
    private readonly Button browse = new() { Content = "B_rowse…" };
    private readonly Button restoreButton = new() { Content = "Re_store from folder" };
    private readonly Button cancel = new() { Content = "Ca_ncel", Visibility = Visibility.Collapsed };
    private readonly Button close = new() { Content = "_Close", IsCancel = true };
    private readonly MaModal question = new() { DialogWidth = 480 };
    private TaskCompletionSource<bool>? answer;
    private CancellationTokenSource? cts;
    private bool busy;

    internal BackupRestoreWindow(string workspaceRoot) : this(workspaceRoot, new BackupService(), new BackupRestore()) { }

    /// <param name="backup">The backup engine; its <see cref="BackupService.BackupBase"/> is where the dated folders go.</param>
    /// <param name="restore">The restore engine.</param>
    internal BackupRestoreWindow(string workspaceRoot, BackupService backup, BackupRestore restore)
    {
        this.workspaceRoot = workspaceRoot;
        this.backup = backup;
        this.restore = restore;
        Title = "Backup & Restore";
        Width = 800;
        Height = 640;
        MinWidth = 520;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var header = Muted($"Backups go into a dated folder under {backup.BackupBase}: each SQLite database as a .sql dump, each Azure SQL "
            + "database as a .sql export and %APPDATA%\\MindAttic as AppData.zip. Nothing runs until you click it.");
        status.SetResourceReference(MarginProperty, "SpaceBXS");
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        bar.SetResourceReference(MarginProperty, "SpaceBXS");
        log.SetResourceReference(StyleProperty, "MaTextArea");
        log.SetResourceReference(MarginProperty, "SpaceTS");
        AutomationProperties.SetName(log, "Backup and restore progress");

        includeAzureSql.Margin = new Thickness(0, 2, 0, 2);
        includeAzureSql.ToolTip = "Lists the web apps through az and exports each one's database (schema and data); needs az login";
        upload.Margin = new Thickness(0, 2, 0, 6);
        upload.ToolTip = $"Copies the backup's Databases folder to the {AzureBlobUpload.DefaultStorageAccount}/{AzureBlobUpload.DefaultContainer} "
            + $"container, then deletes uploads older than the newest {AzureBlobUpload.DefaultKeepNights}; needs az login";
        backupButton.SetResourceReference(StyleProperty, "MaPrimaryButton");
        backupButton.HorizontalAlignment = HorizontalAlignment.Left;
        backupButton.ToolTip = "Runs a whole backup into a new dated folder";
        backupButton.Click += async (_, _) => await BackupNowAsync();

        restoreFolder.SetResourceReference(StyleProperty, "MaTextBox");
        AutomationProperties.SetName(restoreFolder, "Backup folder to restore");
        restoreFolder.TextChanged += (_, _) => UpdateButtons();
        browse.SetResourceReference(StyleProperty, "MaButton");
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.ToolTip = "Picks the dated backup folder to restore";
        browse.Click += (_, _) => Browse();
        var folderRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(browse, Dock.Right);
        folderRow.Children.Add(browse);
        folderRow.Children.Add(restoreFolder);
        restoreButton.SetResourceReference(StyleProperty, "MaButton");
        restoreButton.HorizontalAlignment = HorizontalAlignment.Left;
        restoreButton.ToolTip = "Unzips the folder's AppData over %APPDATA%\\MindAttic and replays its SQLite dumps into the live databases";
        restoreButton.Click += async (_, _) => await RestoreAsync();

        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");
        cancel.ToolTip = "Stops the backup before its next database";
        cancel.Click += (_, _) => { cts?.Cancel(); cancel.IsEnabled = false; status.Text = "Cancelling…"; };
        close.SetResourceReference(StyleProperty, "MaButton");
        close.Click += (_, _) => Close();

        var steps = new StackPanel();
        steps.Children.Add(Heading("Backup"));
        steps.Children.Add(includeAzureSql);
        steps.Children.Add(upload);
        steps.Children.Add(backupButton);
        steps.Children.Add(Heading("Restore"));
        steps.Children.Add(Muted("Restores a dated folder onto this running workspace. Azure SQL exports in it are only listed, never applied."));
        steps.Children.Add(folderRow);
        steps.Children.Add(restoreButton);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(close);

        var top = new StackPanel();
        top.Children.Add(header);
        top.Children.Add(bar);
        top.Children.Add(status);
        top.Children.Add(steps);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(footer);
        panel.Children.Add(log);

        question.Closed += (_, _) => answer?.TrySetResult(false);
        Closed += (_, _) => answer?.TrySetResult(false);
        var root = new Grid();
        root.Children.Add(panel);
        root.Children.Add(question);
        Content = root;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy && !question.IsOpen) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Loaded += async (_, _) => await FindNewestAsync();
        UpdateButtons();
    }

    /// <summary>Fills the restore folder with the newest dated backup, when the box is still empty.</summary>
    internal async Task FindNewestAsync()
    {
        string? newest;
        try { newest = await Task.Run(() => BackupFolders.FindNewest(backup.BackupBase)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { newest = null; OrchestratorLog.For("Dashboard").Warning(ex, "Finding the newest backup failed"); }
        if (string.IsNullOrWhiteSpace(restoreFolder.Text) && newest is not null) restoreFolder.Text = newest;
        status.Text = newest is null ? $"No dated backup folder under {backup.BackupBase} yet." : $"Newest backup: {newest}";
        UpdateButtons();
    }

    /// <summary>Runs a whole backup end to end (SQLite, Azure SQL if ticked, AppData), then the off-machine upload if ticked.</summary>
    internal async Task BackupNowAsync()
    {
        if (busy) return;
        var withAzureSql = includeAzureSql.IsChecked == true;
        var withUpload = upload.IsChecked == true;
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        SetBusy(true, "Backing up…", cancellable: true);
        Say($"Backup started into {backup.BackupBase}.");
        BackupRunResult? run = null;
        AzureUploadResult? sent = null;
        try
        {
            (run, sent) = await Task.Run(() =>
            {
                var databases = SqliteBackup.CollectTargets(
                    ProjectRoster.Load().Select(p => (p.Name, p.Path, (IEnumerable<string>?)p.SqliteDatabases)));
                Post($"  {databases.Count} SQLite database{(databases.Count == 1 ? "" : "s")} on the roster.");
                IReadOnlyList<AzureSqlTarget> azure = [];
                if (withAzureSql && !ct.IsCancellationRequested)
                {
                    Post("  Listing the web apps' Azure SQL databases (az can take a minute)…");
                    var listed = new Kickstart(workspaceRoot).List();
                    if (listed.Ok)
                    {
                        azure = AzureSqlExport.CollectTargets(listed.Apps);
                        Post($"  {azure.Count} Azure SQL database{(azure.Count == 1 ? "" : "s")}.");
                    }
                    else Post($"  Azure SQL skipped: {listed.Error}{(listed.NeedsLogin ? " (run az login)" : "")}");
                }
                var result = backup.Run(databases,
                    d => Post(d.Ok ? $"  SQLite {d.Project}: {d.BackupFile}" : $"  SQLite {d.Project}: FAILED - {d.Output}"),
                    ct, azure,
                    a => Post(a.Ok
                        ? $"  Azure SQL {a.Server}/{a.Database}: {a.Tables} tables, {a.InsertStatements:n0} rows, {AzureBlobUpload.FormatBytes(a.Bytes)} in {a.Elapsed:mm\\:ss}"
                        : $"  Azure SQL {a.Server}/{a.Database}: FAILED - {a.Output}"),
                    line => Post("    " + line));
                Post(result.AppData.Ok
                    ? $"  AppData: {result.AppData.ZipPath} ({AzureBlobUpload.FormatBytes(result.AppData.Bytes)})"
                    : $"  AppData: FAILED - {result.AppData.Output}");
                AzureUploadResult? uploaded = null;
                if (withUpload && !ct.IsCancellationRequested)
                {
                    Post("  Uploading the databases to Azure storage…");
                    uploaded = new AzureBlobUpload().UploadAndPrune(result.Folder, AzureUploadOptions.Default, line => Post("    " + line));
                    Post(uploaded.Ok ? $"  Upload: {uploaded.Message}" : $"  Upload FAILED: {uploaded.Message}");
                }
                return (result, uploaded);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Backup from the toolbar failed; contained");
            Say($"  Backup failed: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
            cts = null;
        }
        // The posted lines land on this thread; let them through before the summary.
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        var ok = run is { Ok: true } && (sent?.Ok ?? true);
        if (run is not null)
        {
            restoreFolder.Text = run.Folder;
            OrchestratorLog.For("Dashboard").Information("Backup into {Folder:l}: {Ok}, {Sqlite} SQLite, {Azure} Azure SQL, upload {Upload}",
                run.Folder, run.Ok, run.Sqlite.Count, run.AzureSql.Count, sent?.Ok);
        }
        SetBusy(false, run is null ? "The backup didn't run; the log says why."
            : ok ? $"Backed up to {run.Folder}."
            : $"The backup to {run.Folder} didn't fully succeed; the log says what.", cancellable: false);
    }

    /// <summary>Asks, then restores the folder in the box through <see cref="BackupRestore.Run"/>.</summary>
    internal async Task RestoreAsync()
    {
        if (busy) return;
        var folder = restoreFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { status.Text = "Pick a backup folder that exists."; return; }
        if (!await AskAsync("Restore this backup?",
                $"Restores {folder}: its AppData.zip is unzipped over {restore.AppDataTarget}, and each SQLite dump replaces the "
                + "live database the roster names for its project (the old file is kept aside and put back if a replay fails). "
                + "Azure SQL exports are only listed.")) return;

        SetBusy(true, $"Restoring {Path.GetFileName(folder)}…", cancellable: false);
        Say($"Restoring {folder}…");
        BackupRestoreResult r;
        try
        {
            var databases = SqliteBackup.CollectTargets(
                ProjectRoster.Load().Select(p => (p.Name, p.Path, (IEnumerable<string>?)p.SqliteDatabases)));
            r = await Task.Run(() => restore.Run(folder, databases,
                d => Post(d.Ok ? $"  SQLite {d.Project}: {d.DatabaseFile} restored." : $"  SQLite {d.Project}: {d.DatabaseFile} failed: {d.Output}")));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Restore from the toolbar failed; contained");
            r = new BackupRestoreResult(folder, null, [], [], [], ex.Message);
        }
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
        OrchestratorLog.For("Dashboard").Information("Restore of {Folder:l}: {Ok}, {Count} SQLite", folder, r.Ok, r.Sqlite.Count);
        SetBusy(false, r.Ok
            ? $"Restored {Path.GetFileName(folder)}: {r.Sqlite.Count} SQLite database{(r.Sqlite.Count == 1 ? "" : "s")}."
            : $"The restore of {Path.GetFileName(folder)} didn't fully succeed; the log says what.", cancellable: false);
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Dated backup folder to restore" };
        var current = restoreFolder.Text.Trim();
        if (Directory.Exists(current)) dialog.InitialDirectory = current;
        else if (Directory.Exists(backup.BackupBase)) dialog.InitialDirectory = backup.BackupBase;
        if (dialog.ShowDialog(this) == true) restoreFolder.Text = dialog.FolderName;
    }

    private static TextBlock Heading(string text) =>
        new() { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 10, 0, 4) };

    private static TextBlock Muted(string text)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        t.SetResourceReference(StyleProperty, "MaMutedText");
        return t;
    }

    /// <summary>Appends a timestamped line to the log from any thread.</summary>
    private void Post(string line) => Dispatcher.BeginInvoke(() => Say(line));

    private void Say(string line)
    {
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}");
        log.ScrollToEnd();
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

    private void SetBusy(bool on, string message, bool cancellable)
    {
        busy = on;
        status.Text = message;
        bar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        cancel.Visibility = on && cancellable ? Visibility.Visible : Visibility.Collapsed;
        cancel.IsEnabled = on && cancellable;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        backupButton.IsEnabled = !busy;
        includeAzureSql.IsEnabled = !busy;
        upload.IsEnabled = !busy;
        restoreFolder.IsEnabled = !busy;
        browse.IsEnabled = !busy;
        restoreButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(restoreFolder.Text);
        close.IsEnabled = !busy;
    }
}
