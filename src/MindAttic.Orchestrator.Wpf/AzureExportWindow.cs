using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Azure;
using MindAttic.Orchestrator.Backup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The toolbar's Azure group, "Export Azure SQL DB" (M.ORCH-T2056): lists every Azure SQL database the web apps use
/// (<see cref="Kickstart.List"/>'s rows through <see cref="AzureSqlExport.CollectTargets"/>), lets you tick the ones to export
/// and pick a target folder, then runs <see cref="AzureSqlExport"/> off the UI thread, each database written as a .sql script
/// (schema and data) at <c>{folder}\Databases\{server}\{database}.sql</c>, with its progress in the log. Read-only against Azure.
/// While an export runs only Cancel works (it stops before the next database, or mid-script) and the window won't close.
/// </summary>
internal sealed class AzureExportWindow : Window
{
    private readonly Kickstart kickstart;
    private readonly StackPanel list = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar bar = new() { Height = 4, Visibility = Visibility.Collapsed };
    private readonly TextBox folder = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox log = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        AcceptsReturn = true, MinHeight = 90, FontFamily = new System.Windows.Media.FontFamily("Consolas"),
    };
    private readonly Button browse = new() { Content = "_Browse…" };
    private readonly Button refresh = new() { Content = "_Refresh" };
    private readonly Button selectAll = new() { Content = "Select _all" };
    private readonly Button export = new() { Content = "_Export" };
    private readonly Button cancel = new() { Content = "Ca_ncel", Visibility = Visibility.Collapsed };
    private readonly Button close = new() { Content = "_Close", IsCancel = true };
    private readonly List<(CheckBox Box, AzureSqlTarget Target)> rows = [];
    private CancellationTokenSource? cts;
    private bool busy;

    /// <summary>Where an export goes unless another folder is picked: Documents\MindAttic Azure exports\{date}.</summary>
    internal static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MindAttic Azure exports", DateTime.Now.ToString("yyyy-MM-dd"));

    internal AzureExportWindow(string workspaceRoot) : this(new Kickstart(workspaceRoot)) { }

    internal AzureExportWindow(Kickstart kickstart)
    {
        this.kickstart = kickstart;
        Title = "Export Azure SQL databases";
        Width = 760;
        Height = 600;
        MinWidth = 480;
        MinHeight = 380;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var header = new TextBlock
        {
            Text = "Exports each ticked database to a .sql script (schema and data) under the folder's Databases\\{server}\\{database}.sql. "
                 + "Only reads from Azure; a sleeping serverless database is woken first.",
            TextWrapping = TextWrapping.Wrap,
        };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceBXS");
        status.SetResourceReference(MarginProperty, "SpaceBXS");
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        bar.SetResourceReference(MarginProperty, "SpaceBXS");

        folder.Text = DefaultFolder;
        folder.SetResourceReference(StyleProperty, "MaTextBox");
        AutomationProperties.SetName(folder, "Target folder");
        browse.SetResourceReference(StyleProperty, "MaButton");
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.ToolTip = "Picks the folder the .sql files go into";
        browse.Click += (_, _) => Browse();
        var folderLabel = new TextBlock { Text = "Target folder", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var folderRow = new DockPanel();
        folderRow.SetResourceReference(MarginProperty, "SpaceBXS");
        DockPanel.SetDock(folderLabel, Dock.Left);
        DockPanel.SetDock(browse, Dock.Right);
        folderRow.Children.Add(folderLabel);
        folderRow.Children.Add(browse);
        folderRow.Children.Add(folder);

        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        log.SetResourceReference(StyleProperty, "MaTextBox");
        log.SetResourceReference(MarginProperty, "SpaceTS");
        log.Height = 140;
        AutomationProperties.SetName(log, "Export progress");

        refresh.SetResourceReference(StyleProperty, "MaButton");
        refresh.SetResourceReference(MarginProperty, "SpaceRS");
        refresh.ToolTip = "Lists the web apps' databases again";
        refresh.Click += async (_, _) => await LoadAsync();
        selectAll.SetResourceReference(StyleProperty, "MaButton");
        selectAll.SetResourceReference(MarginProperty, "SpaceRS");
        selectAll.ToolTip = "Ticks every database, or unticks them all when they're all ticked";
        selectAll.Click += (_, _) =>
        {
            var on = rows.Any(r => r.Box.IsChecked != true);
            foreach (var r in rows) r.Box.IsChecked = on;
        };
        export.SetResourceReference(StyleProperty, "MaPrimaryButton");
        export.SetResourceReference(MarginProperty, "SpaceRS");
        export.ToolTip = "Exports each ticked database to its .sql file in the target folder";
        export.Click += async (_, _) => await ExportAsync();
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");
        cancel.ToolTip = "Stops the export; a database part-way through is not written";
        cancel.Click += (_, _) => { cts?.Cancel(); cancel.IsEnabled = false; status.Text = "Cancelling…"; };
        close.SetResourceReference(StyleProperty, "MaButton");
        close.Click += (_, _) => Close();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(refresh);
        footer.Children.Add(selectAll);
        footer.Children.Add(export);
        footer.Children.Add(cancel);
        footer.Children.Add(close);

        var top = new StackPanel();
        top.Children.Add(header);
        top.Children.Add(folderRow);
        top.Children.Add(bar);
        top.Children.Add(status);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(log, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(footer);
        panel.Children.Add(log);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>Lists the web apps (<see cref="Kickstart.List"/>) and shows one row per distinct database.</summary>
    internal async Task LoadAsync()
    {
        if (busy) return;
        SetBusy(true, "Listing the web apps' databases (az can take a minute)…", indeterminate: true);
        KickstartResult r;
        try { r = await Task.Run(kickstart.List); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Listing Azure databases failed; contained");
            r = KickstartResult.Failed(ex.Message);
        }
        var targets = AzureSqlExport.CollectTargets(r.Apps);
        ShowRows(targets);
        SetBusy(false, !r.Ok ? r.Error! + (r.NeedsLogin ? " Run az login, then Refresh." : "")
            : targets.Count == 0 ? "No web app has an Azure SQL database."
            : $"{targets.Count} database{(targets.Count == 1 ? "" : "s")}.", indeterminate: true);
    }

    private void ShowRows(IReadOnlyList<AzureSqlTarget> targets)
    {
        var ticked = rows.Where(r => r.Box.IsChecked == true).Select(r => r.Target).ToHashSet();
        rows.Clear();
        list.Children.Clear();
        foreach (var t in targets)
        {
            var box = new CheckBox
            {
                Content = $"{t.Database}  ·  {t.Fqdn}",
                IsChecked = ticked.Contains(t),
                Margin = new Thickness(0, 3, 0, 3),
                ToolTip = $"Writes {AzureSqlExport.ResolveExportFilePath("…", t.Server, t.Database)}",
            };
            AutomationProperties.SetName(box, $"Export {t.Database}");
            box.Checked += (_, _) => UpdateButtons();
            box.Unchecked += (_, _) => UpdateButtons();
            rows.Add((box, t));
            list.Children.Add(box);
        }
        UpdateButtons();
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Folder for the exported .sql files" };
        var current = folder.Text.Trim();
        if (Directory.Exists(current)) dialog.InitialDirectory = current;
        if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName;
    }

    /// <summary>Exports every ticked database into the target folder, one after another, logging each step.</summary>
    internal async Task ExportAsync()
    {
        if (busy) return;
        var targets = rows.Where(r => r.Box.IsChecked == true).Select(r => r.Target).ToList();
        var target = folder.Text.Trim();
        if (targets.Count == 0) { status.Text = "Tick at least one database."; return; }
        if (string.IsNullOrWhiteSpace(target)) { status.Text = "Pick a target folder."; return; }
        try { target = Path.GetFullPath(target); Directory.CreateDirectory(target); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { status.Text = $"Can't use that folder: {ex.Message}"; return; }

        cts = new CancellationTokenSource();
        var ct = cts.Token;
        log.Clear();
        bar.Maximum = targets.Count;
        bar.Value = 0;
        SetBusy(true, $"Exporting {targets.Count} database{(targets.Count == 1 ? "" : "s")} to {target}…", indeterminate: false);
        void Log(string line) => Dispatcher.BeginInvoke(() => { log.AppendText($"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}"); log.ScrollToEnd(); });
        var done = 0;
        IReadOnlyList<AzureSqlExportResult> results;
        try
        {
            results = await Task.Run(() => new AzureSqlExport().Export(targets, target, Log, r =>
            {
                Log(r.Ok
                    ? $"{r.Database}: done in {r.Elapsed:mm\\:ss}, {r.Tables} tables, {r.InsertStatements:n0} rows, {r.Bytes / 1024.0 / 1024.0:0.0} MB -> {r.ExportFile}"
                    : $"{r.Database}: FAILED - {r.Output}");
                var n = Interlocked.Increment(ref done);
                Dispatcher.BeginInvoke(() => bar.Value = n);
            }, ct), ct);
        }
        catch (OperationCanceledException) { results = []; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Azure SQL export failed; contained");
            Log($"Export failed: {ex.Message}");
            results = [];
        }
        finally
        {
            cts.Dispose();
            cts = null;
        }
        var ok = results.Count(r => r.Ok);
        var skipped = targets.Count - results.Count;
        SetBusy(false, $"Exported {ok} of {targets.Count} to {target}"
            + (results.Count - ok > 0 ? $"; {results.Count - ok} failed (see the log)" : "")
            + (skipped > 0 ? $"; {skipped} cancelled" : "") + ".", indeterminate: false);
        bar.Visibility = Visibility.Collapsed;
    }

    private void SetBusy(bool on, string message, bool indeterminate)
    {
        busy = on;
        status.Text = message;
        bar.IsIndeterminate = indeterminate;
        bar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        cancel.Visibility = on && !indeterminate ? Visibility.Visible : Visibility.Collapsed;
        cancel.IsEnabled = on;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        refresh.IsEnabled = !busy;
        browse.IsEnabled = !busy;
        folder.IsEnabled = !busy;
        selectAll.IsEnabled = !busy && rows.Count > 0;
        close.IsEnabled = !busy;
        export.IsEnabled = !busy && rows.Any(r => r.Box.IsChecked == true);
        foreach (var r in rows) r.Box.IsEnabled = !busy;
    }
}
