using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Azure;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The toolbar's Azure group, "Kickstart web apps" (M.ORCH-T2055): lists every Azure web app with its state and its database's
/// status (<see cref="Kickstart.List"/>), and wakes one (its row's Wake) or every one (Wake all) with <see cref="Kickstart.Wake"/>,
/// showing what each app and its database answered. The /kickstart command's engine, run off the UI thread; while a run is going
/// every button is off and the window won't close. When az isn't signed in (<see cref="KickstartResult.NeedsLogin"/>) a
/// "Sign in (az login)" button opens az login in a console of its own.
/// </summary>
internal sealed class KickstartWindow : Window
{
    private readonly Kickstart kickstart;
    private readonly StackPanel list = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar bar = new() { IsIndeterminate = true, Height = 4, Visibility = Visibility.Collapsed };
    private readonly Button refresh = new() { Content = "_Refresh" };
    private readonly Button wakeAll = new() { Content = "Wake _all" };
    private readonly Button login = new() { Content = "Sign in (az _login)", Visibility = Visibility.Collapsed };
    private readonly List<Button> rowButtons = [];
    private bool busy;

    internal KickstartWindow(string workspaceRoot) : this(new Kickstart(workspaceRoot)) { }

    internal KickstartWindow(Kickstart kickstart)
    {
        this.kickstart = kickstart;
        Title = "Kickstart Azure web apps";
        Width = 760;
        Height = 520;
        MinWidth = 480;
        MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var header = new TextBlock
        {
            Text = "Wakes a sleeping web app and its serverless SQL database so the next visit is fast. Changes nothing in Azure.",
            TextWrapping = TextWrapping.Wrap,
        };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceBXS");
        status.SetResourceReference(MarginProperty, "SpaceBXS");
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        bar.SetResourceReference(MarginProperty, "SpaceBXS");

        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        refresh.SetResourceReference(StyleProperty, "MaButton");
        refresh.SetResourceReference(MarginProperty, "SpaceRS");
        refresh.ToolTip = "Lists every web app again with its state and its database's status";
        refresh.Click += async (_, _) => await LoadAsync();
        wakeAll.SetResourceReference(StyleProperty, "MaPrimaryButton");
        wakeAll.SetResourceReference(MarginProperty, "SpaceRS");
        wakeAll.ToolTip = "Wakes every web app and its database, waiting until each answers";
        wakeAll.Click += async (_, _) => await WakeAsync("all", "every app");
        login.SetResourceReference(StyleProperty, "MaButton");
        login.SetResourceReference(MarginProperty, "SpaceRS");
        login.ToolTip = "Opens az login in a console of its own; Refresh once it's done";
        login.Click += (_, _) => StartAzLogin();
        var close = new Button { Content = "_Close", IsCancel = true };
        close.SetResourceReference(StyleProperty, "MaButton");
        close.Click += (_, _) => Close();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(login);
        footer.Children.Add(refresh);
        footer.Children.Add(wakeAll);
        footer.Children.Add(close);

        var top = new StackPanel();
        top.Children.Add(header);
        top.Children.Add(bar);
        top.Children.Add(status);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(footer);
        panel.Children.Add(scroll);
        Content = panel;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) { e.Handled = true; Close(); } };
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>Lists every app (<see cref="Kickstart.List"/>) into the rows.</summary>
    internal async Task LoadAsync()
    {
        if (busy) return;
        SetBusy(true, "Listing web apps (az can take a minute)…");
        var r = await RunAsync(kickstart.List);
        SetBusy(false, r.Ok ? $"{r.Apps.Count} web app{(r.Apps.Count == 1 ? "" : "s")}." : r.Error!);
        ShowRows(r.Apps);
        login.Visibility = r.NeedsLogin ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Wakes <paramref name="name"/> (an app's name, or <c>all</c>) and shows what each app and its database answered.</summary>
    internal async Task WakeAsync(string name, string label)
    {
        if (busy) return;
        SetBusy(true, $"Waking {label}: waiting until it answers (up to a few minutes)…");
        var r = await RunAsync(() => kickstart.Wake(name));
        var up = r.Apps.Count(a => a.AppUp);
        SetBusy(false, r.Ok
            ? $"Woke {label}: {up} of {r.Apps.Count} app{(r.Apps.Count == 1 ? "" : "s")} up."
            : r.Error!);
        if (r.Apps.Count > 0) MergeRows(r.Apps);
        login.Visibility = r.NeedsLogin ? Visibility.Visible : Visibility.Collapsed;
    }

    private static async Task<KickstartResult> RunAsync(Func<KickstartResult> run)
    {
        try { return await Task.Run(run); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Kickstart failed; contained");
            return KickstartResult.Failed(ex.Message);
        }
    }

    private readonly List<KickstartApp> shown = [];

    // A wake of one app returns only that app's row: keep every other row as it was listed, and replace the woken ones.
    private void MergeRows(IReadOnlyList<KickstartApp> woken)
    {
        var merged = shown.Select(a => woken.FirstOrDefault(w => string.Equals(w.Name, a.Name, StringComparison.OrdinalIgnoreCase)) ?? a).ToList();
        merged.AddRange(woken.Where(w => !shown.Any(a => string.Equals(a.Name, w.Name, StringComparison.OrdinalIgnoreCase))));
        ShowRows(merged);
    }

    private void ShowRows(IReadOnlyList<KickstartApp> apps)
    {
        shown.Clear();
        shown.AddRange(apps);
        list.Children.Clear();
        rowButtons.Clear();
        foreach (var app in apps) list.Children.Add(Row(app));
        UpdateButtons();
    }

    private Grid Row(KickstartApp app)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        var name = new TextBlock { Text = app.Name, FontWeight = FontWeights.SemiBold, ToolTip = app.Url, TextTrimming = TextTrimming.CharacterEllipsis };
        var db = app.Database is { } d ? $"database {d.Name}: {Blank(d.Status)}" : "no database";
        var detail = new TextBlock { Text = $"{Blank(app.State)} · {db}", TextTrimming = TextTrimming.CharacterEllipsis };
        detail.SetResourceReference(StyleProperty, "MaMutedText");
        text.Children.Add(name);
        text.Children.Add(detail);
        if (app.AppResult is not null || app.DbResult is not null)
        {
            var result = new TextBlock
            {
                Text = $"app: {app.AppResult ?? "-"}" + (app.DbResult is null ? "" : $" · db: {app.DbResult}"),
                TextWrapping = TextWrapping.Wrap,
            };
            result.SetResourceReference(ForegroundProperty, app.AppUp && (app.DbResult is null || app.DbOnline) ? "MaGood" : "MaWarn");
            text.Children.Add(result);
        }
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var wake = new Button { Content = "Wake", VerticalAlignment = VerticalAlignment.Center, ToolTip = $"Wakes {app.Name} and its database" };
        wake.SetResourceReference(StyleProperty, "MaButton");
        wake.Margin = new Thickness(8, 0, 0, 0);
        AutomationProperties.SetName(wake, $"Wake {app.Name}");
        wake.Click += async (_, _) => await WakeAsync(app.Name, app.Name);
        rowButtons.Add(wake);
        Grid.SetColumn(wake, 1);
        row.Children.Add(wake);
        return row;
    }

    private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? "unknown" : s;

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
        login.IsEnabled = !busy;
        wakeAll.IsEnabled = !busy && shown.Count > 0;
        foreach (var b in rowButtons) b.IsEnabled = !busy;
    }

    private static void StartAzLogin()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/k az login") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Starting az login failed; contained"); }
    }
}
